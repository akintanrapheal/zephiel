# ── Stage 1: Build ─────────────────────────────────────────────────────────────
FROM node:20-alpine AS css-builder
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY tailwind.config.js postcss.config.js ./
COPY src/Zephiel.Web/wwwroot/css/input.css ./src/Zephiel.Web/wwwroot/css/input.css
COPY src/Zephiel.Web/Views ./src/Zephiel.Web/Views
COPY src/Zephiel.Web/Areas ./src/Zephiel.Web/Areas
RUN npm run build:css

# ── Stage 2: .NET Build ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-builder
WORKDIR /src

# Restore NuGet packages (cached layer).
# Restore the web project specifically — restoring the .sln would also pull in the
# test project, which isn't copied into this build stage.
COPY src/Zephiel.Web/Zephiel.Web.csproj ./src/Zephiel.Web/
RUN dotnet restore src/Zephiel.Web/Zephiel.Web.csproj

# Copy rest and build
COPY . .
# Copy compiled Tailwind CSS from previous stage
COPY --from=css-builder /app/src/Zephiel.Web/wwwroot/css/app.css ./src/Zephiel.Web/wwwroot/css/app.css

RUN dotnet publish src/Zephiel.Web/Zephiel.Web.csproj \
    -c Release -o /app/publish --no-restore \
    /p:BuildTailwind=false

# ── Stage 3: Runtime ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Timezone: the store is Nigeria (West Africa Time, UTC+1, no DST). The base image
# runs in UTC, so DateTime.ToLocalTime() would render times 1 hour behind. Install the
# IANA zone database (tzdata) and set TZ so the process-wide local zone is WAT — this
# makes every ToLocalTime() display the correct local clock. (Storage stays UTC.)
RUN apt-get update && apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
ENV TZ=Africa/Lagos

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=dotnet-builder --chown=appuser /app/publish .

# EF Core migrations are run before deploy via:
#   dotnet ef database update --project src/Zephiel.Web
# or via the startup EnsureCreated (development only).

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Zephiel.Web.dll"]
