# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + project files first for layer caching
COPY PlayForge.slnx .
COPY src/PlayForge.Domain/PlayForge.Domain.csproj                               src/PlayForge.Domain/
COPY src/PlayForge.Application/PlayForge.Application.csproj                     src/PlayForge.Application/
COPY src/PlayForge.Infrastructure/PlayForge.Infrastructure.csproj               src/PlayForge.Infrastructure/
COPY src/PlayForge.Web/PlayForge.Web.csproj                                     src/PlayForge.Web/
COPY tests/PlayForge.Domain.Tests/PlayForge.Domain.Tests.csproj                 tests/PlayForge.Domain.Tests/
COPY tests/PlayForge.Application.Tests/PlayForge.Application.Tests.csproj       tests/PlayForge.Application.Tests/
COPY tests/PlayForge.Integration.Tests/PlayForge.Integration.Tests.csproj       tests/PlayForge.Integration.Tests/

RUN dotnet restore PlayForge.slnx

COPY . .
RUN dotnet publish src/PlayForge.Web/PlayForge.Web.csproj \
    -c Release \
    -o /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PlayForge.Web.dll"]
