# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# ---- Stage 0: build the Angular app (so the API can serve the UI) ----
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["WorkforceSync.Api/WorkforceSync.Api.csproj", "WorkforceSync.Api/"]
COPY ["WorkforceSync.Core/WorkforceSync.Core.csproj", "WorkforceSync.Core/"]
COPY ["WorkforceSync.HcmSource/WorkforceSync.HcmSource.csproj", "WorkforceSync.HcmSource/"]
RUN dotnet restore "./WorkforceSync.Api/WorkforceSync.Api.csproj"
COPY . .
# Bring the built frontend in so the CopyFrontend target picks it up.
COPY --from=frontend /src/frontend/dist/frontend ./frontend/dist/frontend
WORKDIR "/src/WorkforceSync.Api"
RUN dotnet build "./WorkforceSync.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./WorkforceSync.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkforceSync.Api.dll"]