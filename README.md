# LiveFuelMap

LiveFuelMap is an ASP.NET Core Web API + React/Vite application for fuel price monitoring, comments, AI chat, email subscriptions, admin management, exports, JWT/refresh-token authentication, Google OAuth, SignalR updates and API-token access.

## Project Structure

- `src/LiveFuelMap.Api` - ASP.NET Core API, controllers, SignalR hub, auth, middleware, Swagger.
- `src/LiveFuelMap.BLL` - DTOs, service contracts and business services.
- `src/LiveFuelMap.DAL` - EF Core entities, DbContext, repositories and migrations.
- `src/LiveFuelMap.Infrastructure` - JWT, password hashing, Google OAuth, CAPTCHA, email, AI, parser clients and hosted workers.
- `tests/LiveFuelMap.Tests` - unit and integration tests.
- `frontend/src` - React, Vite, Tailwind CSS, i18next UI.
- `frontend/public` - static assets copied by Vite.
- `docs` - architecture, deployment and diploma notes.

## Environment

The project uses one environment template:

- `.env.example` - shared backend + frontend development template.

Create local config:

```powershell
Copy-Item .env.example .env
```

Do not commit `.env`, SMTP passwords, Google OAuth client secrets, JWT secrets or CAPTCHA secret keys.

Backend reads server-side values from root `.env`. Frontend also reads root `.env` through Vite `envDir`, but only `VITE_*` values are exposed to the browser. Keep secrets out of `VITE_*`.

Important groups in `.env`:

```env
# MySQL
DB_HOST=localhost
DB_PORT=3306
DB_USER=livefuelmap
DB_PASSWORD=change_me
DB_NAME=LiveFuelMapAspNet

# JWT
Jwt__Secret=change_me_change_me_change_me_change_me_32_chars

# Google OAuth
GoogleAuth__ClientId=
GoogleAuth__ClientSecret=
GoogleAuth__RedirectUri=http://localhost:5000/api/auth/google/callback
GoogleAuth__FrontendCallbackUrl=http://localhost:5173/auth/google/callback

# reCAPTCHA v2
Captcha__Enabled=false
Captcha__SiteKey=
Captcha__SecretKey=

# Frontend public config
VITE_API_BASE_URL=http://localhost:5000
VITE_SIGNALR_HUB_URL=http://localhost:5000/hubs/fuel
VITE_GOOGLE_MAPS_API_KEY=
VITE_CAPTCHA_ENABLED=false
VITE_CAPTCHA_SITE_KEY=
```

The browser Google Maps key is public by design. In Google Cloud Console restrict it to the site HTTP referrers and only the APIs used by the map, including Maps JavaScript API and Places API (New). Do not put server-side Google OAuth secrets or unrestricted Maps keys into `VITE_*` values.

For production, put real values into hosting secrets or a private env file outside git. `appsettings.Production.json` and real env files are ignored.

## Local Development

Run backend API on `http://localhost:5000`:

```powershell
.\run-backend.cmd
```

Run React/Vite frontend on `http://localhost:5173`:

```powershell
.\run-frontend.cmd
```

Swagger:

```text
http://localhost:5000/swagger
```

Google OAuth local redirect URI:

```text
http://localhost:5000/api/auth/google/callback
```

Google OAuth frontend callback:

```text
http://localhost:5173/auth/google/callback
```

## Combined Local Run

To build the frontend, apply migrations and run the API serving the built frontend from `http://localhost:5000`:

```powershell
.\run-livefuelmap.cmd
```

If PowerShell script execution is blocked, use the `.cmd` wrappers or run with a temporary execution policy bypass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-backend.ps1
```

## Database

Use MySQL 8. Configure `.env`, then apply migrations:

```powershell
dotnet ef database update --project src\LiveFuelMap.DAL\LiveFuelMap.DAL.csproj --startup-project src\LiveFuelMap.Api\LiveFuelMap.Api.csproj
```

Use a clean EF Core database such as `LiveFuelMapAspNet`.

## AI Chat

The AI chat uses the Groq API from the ASP.NET Core backend. The frontend never calls Groq directly, and the API key must be provided through private environment configuration.

Relevant variables:

```env
AI_PROVIDER=Groq
GROQ_API_KEY=
GROQ_MODEL=llama-3.1-8b-instant
GROQ_BASE_URL=https://api.groq.com/openai/v1/chat/completions
AI_TIMEOUT=30
AI_TEMPERATURE=0.4
AI_MAX_TOKENS=400
CHAT_RATE_LIMIT=20
CHAT_MAX_MESSAGE_LENGTH=1000
```

## Build And Test

Frontend:

```powershell
cd frontend
cmd /c npm install
cmd /c npm run build
```

Backend:

```powershell
dotnet restore LiveFuelMap.sln
dotnet build LiveFuelMap.sln --no-restore
```

Tests:

```powershell
dotnet test tests\LiveFuelMap.Tests\LiveFuelMap.Tests.csproj
```

## Notes

- reCAPTCHA is v2 checkbox. Google OAuth login does not require CAPTCHA.
- User uploads under `frontend/public/uploads/` are runtime data and are ignored by git.
- `frontend/public/config.js` is not used; root `.env` is the single local configuration source.
