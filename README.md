# LiveFuelMap ASP.NET Core Rewrite

Layered ASP.NET Core replacement for the legacy Node.js/Express/Socket.IO project.

## Structure

- `src/LiveFuelMap.Api` - Web API, SignalR, Swagger, auth, middleware.
- `src/LiveFuelMap.BLL` - services, DTOs, validation, parser orchestration contracts.
- `src/LiveFuelMap.DAL` - EF Core entities, DbContext, repositories, Unit of Work, migrations.
- `src/LiveFuelMap.Infrastructure` - JWT, hashing, email, logging, parser HTTP clients, hosted worker.
- `tests/LiveFuelMap.Tests` - unit and integration tests.
- `frontend/src` - React/Vite frontend wired to ASP.NET Core + SignalR.
- `frontend/public` - static frontend assets copied by Vite.

## Local Commands

Fast local start:

```powershell
.\run-livefuelmap.cmd
```

This installs frontend packages when needed, builds React, applies EF Core migrations, starts the ASP.NET Core API, and serves the built frontend at `http://localhost:5000/`.

If you run PowerShell scripts directly instead of the `.cmd` wrappers, use ExecutionPolicy bypass for this session:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-livefuelmap.ps1
```

Use the local NuGet config and workspace app-data paths in this sandboxed environment:

```powershell
$env:DOTNET_CLI_HOME='C:\Users\Aleksandr\Desktop\plan\LiveFuelMap'
$env:APPDATA='C:\Users\Aleksandr\Desktop\plan\LiveFuelMap\.appdata'
$env:LOCALAPPDATA='C:\Users\Aleksandr\Desktop\plan\LiveFuelMap\.localappdata'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet restore src\LiveFuelMap.Api\LiveFuelMap.Api.csproj --configfile NuGet.Config --ignore-failed-sources -p:RestoreUseStaticGraphEvaluation=true --disable-parallel
dotnet msbuild src\LiveFuelMap.Api\LiveFuelMap.Api.csproj /restore:false /p:BuildInParallel=false
dotnet test tests\LiveFuelMap.Tests\LiveFuelMap.Tests.csproj --no-build
```

Apply migrations after configuring MySQL:

```powershell
Copy-Item .env.example .env
# Edit .env so DB_USER/DB_PASSWORD match a real MySQL account.
dotnet ef database update --project src\LiveFuelMap.DAL\LiveFuelMap.DAL.csproj --startup-project src\LiveFuelMap.Api\LiveFuelMap.Api.csproj
dotnet run --project src\LiveFuelMap.Api\LiveFuelMap.Api.csproj --urls http://localhost:5000
```

For local MySQL 8, the generated connection string disables SSL and enables public key retrieval. If MySQL still returns `Access denied for user 'root'@'localhost'`, the password in `.env` does not match the MySQL account; create a dedicated user or update `DB_USER`/`DB_PASSWORD`.

Use a clean database such as `LiveFuelMapAspNet` for the EF Core schema. The legacy `livefuelmap` dump uses older table/column names and should remain a reference source, not the migration target.

Swagger: `http://localhost:5000/swagger`

Frontend: `http://localhost:5000/`

SignalR hub: `http://localhost:5000/hubs/fuel`

## Local AI Chat

The chat bot is configured for a free local Ollama model by default. Install Ollama, then pull one model:

```powershell
ollama pull mistral
ollama serve
```

Relevant `.env` values:

```env
AI_PROVIDER=Ollama
AI_MODEL=mistral
AI_ENDPOINT=http://localhost:11434/api/generate
AI_TIMEOUT=60
CHAT_RATE_LIMIT=20
CHAT_MAX_MESSAGE_LENGTH=1000
```

All database access stays in the ASP.NET Core backend. The local model receives only the prepared site context and the user question.
