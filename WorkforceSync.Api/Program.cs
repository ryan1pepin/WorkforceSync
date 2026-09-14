using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using WorkforceSync.Api.Auth;
using WorkforceSync.Api.Data;
using WorkforceSync.Api.Services;
using WorkforceSync.HcmSource;

var builder = WebApplication.CreateBuilder(args);

// --- Database (EF Core + SQLite) ---
var connectionString = builder.Configuration.GetConnectionString("WorkforceSync")
    ?? "Data Source=workforcesync.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// --- Authentication (JWT Bearer) ---
// In Development, sign with a fresh random key per process so any token from a
// previous run is invalid the moment the API restarts — every start forces a
// fresh login (clean demo). In Production, use the configured key so sessions
// persist across restarts.
var jwtKey = builder.Environment.IsDevelopment()
    ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
    : builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "WorkforceSync";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "WorkforceSync";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// --- Application services ---
builder.Services.AddSingleton(new TokenService(jwtKey, jwtIssuer, jwtAudience));
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<IntegrationStatus>();

// --- Ingestion pipeline (M3): poll mock HCM feed → Channel<T> → processor ---
var ingestion = builder.Configuration.GetSection("Ingestion").Get<IngestionOptions>()
    ?? new IngestionOptions(Enabled: true, FeedUrl: "http://127.0.0.1:5199/feed", PollIntervalSeconds: 5, QueueCapacity: 1000);
builder.Services.AddSingleton(ingestion);
builder.Services.AddSingleton(new EventQueue(ingestion.QueueCapacity));
builder.Services.AddHttpClient<HcmFeedClient>(client =>
    client.BaseAddress = new Uri(ingestion.FeedUrl));
builder.Services.AddScoped<EventProcessor>();
if (ingestion.Enabled)
{
    builder.Services.AddHostedService<IngestionService>();
}

// --- MVC + JSON ---
builder.Services.AddControllers(options =>
{
    // Client-abort guard: if the browser goes away mid-request (F5, navigation,
    // closed tab), the request's CancellationToken fires and EF Core throws
    // OperationCanceledException. Treat that as a normal "client closed request"
    // (499) instead of an unhandled exception — it's not an error worth logging
    // or breaking the debugger on.
    options.Filters.Add(new ClientAbortFilter());
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// --- OpenAPI / Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "WorkforceSync API",
        Version = "v1",
        Description = "HR data integration platform: workforce data, JWT auth, integration audit.",
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Paste your access token (from /auth/login).",
    });
});

// --- CORS (Angular dev server) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("angular", policy => policy
        .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// --- Mock HCM feed server (dev only) ---
HcmFeedServer? hcmServer = null;
if (ingestion.Enabled && app.Environment.IsDevelopment())
{
    var scenario = new HcmScenario(ingestion.ScenarioStepSeconds, ingestion.ScenarioCycleSeconds);
    var feedPort = new Uri(ingestion.FeedUrl).Port;
    try
    {
        hcmServer = new HcmFeedServer(feedPort, () => scenario.CurrentEvents);
        hcmServer.Start();
        app.Logger.LogInformation("Mock HCM feed serving at http://127.0.0.1:{Port}/feed", feedPort);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not start mock HCM feed on port {Port}; ingestion will fail to poll.", feedPort);
    }
}

// Ensure the database exists (dev convenience; use migrations in production).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Force a fresh login on every start (dev): revoke all refresh-token
    // families so a session from a previous run can't be silently refreshed.
    // (The per-process signing key already invalidates old access tokens; this
    // closes the refresh path.)
    if (app.Environment.IsDevelopment())
    {
        var now = DateTime.UtcNow;
        foreach (var t in db.RefreshTokens.ToList())
        {
            t.RevokedAtUtc = now;
        }
        db.SaveChanges();
    }

    // Seed a demo user so the UI is usable out of the box (dev only).
    if (app.Environment.IsDevelopment() && !db.Users.Any())
    {
        db.Users.Add(new User
        {
            Email = "demo@corp.example",
            FirstName = "Demo",
            LastName = "User",
            PasswordHash = PasswordHasher.Hash("Demo123!"),
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.SaveChanges();
        app.Logger.LogInformation("Seeded demo user: demo@corp.example / Demo123!");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WorkforceSync API v1");
    });
}

app.UseCors("angular");
// No UseHttpsRedirection: this is a local dev app proxied by Angular (ng serve →
// http://localhost:5140). A 307 to https://localhost:7178 would bounce the proxy
// cross-origin and drop the Authorization header, 401-ing every data call.
app.UseAuthentication();
app.UseAuthorization();

// Serve the built Angular app (copied into wwwroot at build time) so the API can
// run standalone — e.g. in Docker. In dev, `ng serve` proxies to this API instead.
var frontendRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(frontendRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendRoot),
        RequestPath = ""
    });
    app.MapFallbackToFile("index.html");
}

app.MapControllers();

app.Run();

/// <summary>
/// Converts OperationCanceledException (client went away mid-request) into a
/// 499 Client Closed Request so it never surfaces as an unhandled exception.
/// </summary>
public sealed class ClientAbortFilter : Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter
{
    public void OnException(Microsoft.AspNetCore.Mvc.Filters.ExceptionContext context)
    {
        if (context.Exception is OperationCanceledException)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.StatusCodeResult(499);
            context.ExceptionHandled = true;
        }
    }
}
