# ==============================================================================
# Multi-stage Dockerfile for Hudoor.Api (.NET 10 Web API)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project files for caching package restore
COPY Hudoor.slnx ./
COPY src/backend/Hudoor.Api/Hudoor.Api.csproj src/backend/Hudoor.Api/
COPY tests/Hudoor.Api.Tests/Hudoor.Api.Tests.csproj tests/Hudoor.Api.Tests/

RUN dotnet restore src/backend/Hudoor.Api/Hudoor.Api.csproj

# Copy source code and publish
COPY src/backend/Hudoor.Api/ src/backend/Hudoor.Api/
WORKDIR /app/src/backend/Hudoor.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ==============================================================================
# Production Runtime Stage
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Security: Run as non-root user
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Hudoor.Api.dll"]
