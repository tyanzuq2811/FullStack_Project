# Multi-stage build for ASP.NET Core API
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY src/IPM.Domain/IPM.Domain.csproj src/IPM.Domain/
COPY src/IPM.Application/IPM.Application.csproj src/IPM.Application/
COPY src/IPM.Infrastructure/IPM.Infrastructure.csproj src/IPM.Infrastructure/
COPY src/IPM.API/IPM.API.csproj src/IPM.API/

RUN dotnet restore src/IPM.API/IPM.API.csproj --disable-parallel

# Copy all source code
COPY src/ src/

# Build and publish
WORKDIR /src/src/IPM.API
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install cultures and curl for Vietnamese support and health check
RUN apt-get update && apt-get install -y --no-install-recommends \
    locales \
    curl \
    && rm -rf /var/lib/apt/lists/* \
    && sed -i '/vi_VN.UTF-8/s/^# //g' /etc/locale.gen \
    && locale-gen

ENV LANG=vi_VN.UTF-8
ENV LANGUAGE=vi_VN:vi
ENV LC_ALL=vi_VN.UTF-8

# Copy published files
COPY --from=build /app/publish .

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Expose port
EXPOSE 8080

# Health check disabled - endpoints require authentication
# HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
#     CMD curl -f http://localhost:8080/api/projects || exit 1

ENTRYPOINT ["dotnet", "IPM.API.dll"]
