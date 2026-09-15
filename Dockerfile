# ---- Stage 1: build the Angular app ----
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---- Stage 2: build the .NET API (copies the frontend into wwwroot) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY WorkforceSync.slnx ./
COPY WorkforceSync.Api/WorkforceSync.Api.csproj WorkforceSync.Api/
COPY WorkforceSync.Core/WorkforceSync.Core.csproj WorkforceSync.Core/
COPY WorkforceSync.HcmSource/WorkforceSync.HcmSource.csproj WorkforceSync.HcmSource/
# Restore the API project (not the solution) — the solution also references
# WorkforceSync.Core.Tests, which isn't copied in and has no place in the image.
# The API transitively pulls in Core + HcmSource.
RUN dotnet restore WorkforceSync.Api/WorkforceSync.Api.csproj
COPY . .
# Bring the built frontend in so the CopyFrontend target picks it up.
COPY --from=frontend /src/frontend/dist/frontend ./frontend/dist/frontend
RUN dotnet publish WorkforceSync.Api -c Release -o /app

# ---- Stage 3: run ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /app .
# curl is needed by the compose healthcheck (the slim aspnet image lacks it).
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "WorkforceSync.Api.dll"]
