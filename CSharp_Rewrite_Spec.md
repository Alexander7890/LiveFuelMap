# LiveFuelMap C# Rewrite Brief

## Goal

Rewrite the current LiveFuelMap project as a standard split application:

- Frontend: keep the current HTML/CSS/JavaScript behavior at first, later replaceable by React.
- Backend: replace Node.js/Express/Socket.IO with ASP.NET Core.
- Database: move the existing MySQL schema into Entity Framework Core migrations, improving the schema where needed.
- Parsing: move fuel-price parsing from JavaScript into a C# hosted background service.

The new backend must satisfy the requirements from `План.docx`: EF Core, layered architecture, Repository/Unit of Work/Factory patterns, JWT auth, roles, API tokens, logging, metrics, Swagger, tests, and production/deployment preparation.

## Current Project Summary

Current source project:

`C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\VER_20_05\untitled1`

Current app is a small Node.js project:

- `server.js`: Express REST API, JWT auth, registration/login, subscriptions, station/fuel/compare endpoints, cron parser schedule.
- `socketServer.js`: Socket.IO auth, initial station/fuel push, live fuel data polling every 5 seconds, price-history chart data.
- `parseFuel.js`: parses Minfin fuel prices for Kharkiv and writes to MySQL.
- `db.js`: MySQL connection pool using `.env`.
- `frontend/public/index.html`: single-page frontend with Bootstrap, Google Maps, Chart.js, Socket.IO client, auth modal, subscriptions, station cards, map markers, price chart, station comparison.
- `testq6.sql`: MySQL schema/data dump for `livefuelmap`.

## Required Target Architecture

Use a layered ASP.NET Core solution:

- `LiveFuelMap.Api`: controllers, SignalR hubs, auth setup, Swagger, middleware.
- `LiveFuelMap.BLL`: business services, DTOs, validation, parser orchestration, email/subscription logic.
- `LiveFuelMap.DAL`: EF Core `DbContext`, entity configurations, repositories, Unit of Work, migrations.
- `LiveFuelMap.Infrastructure`: external integrations such as parser HTTP clients, email provider, token hashing, logging sinks if separated.
- `LiveFuelMap.Tests`: unit and integration tests.
- `frontend`: current static frontend, later React-ready.

Use ASP.NET Core Web API plus optional MVC/Razor pages for admin/UI if needed for the course requirements.

## Backend Features To Implement

1. Authentication and authorization
   - Register, login, verify current user.
   - Password hashing.
   - JWT access tokens.
   - Roles: `Guest`, `User`, `Admin`.
   - Protected endpoints for subscriptions and comparison.
   - Admin-only endpoints for user management and API token management.

2. API tokens for third-party systems
   - Add `ApiTokens` table.
   - Admin can create/revoke tokens.
   - Store only hashed token values.
   - Protect at least one endpoint with API-token auth, separate from normal user JWT.

3. Fuel data API
   - Current prices by station.
   - Fuel types.
   - Station list and station details.
   - Price history for charts.
   - Compare selected stations by all fuel types.
   - Search/filter by city, station name, fuel type, date range.
   - Pagination where result sets can grow.

4. CRUD requirements
   - Implement CRUD for at least one major entity, preferably `Stations`, `FuelPrices`, or `Sources`.
   - Admin should be able to create/edit/delete stations and manually correct fuel prices.

5. Parsing / ETL
   - Convert `parseFuel.js` into a C# `BackgroundService` or scheduled job.
   - Keep Minfin as one source.
   - Add a second source as required by the plan.
   - Parse HTML/JSON, normalize station/fuel names, detect duplicates, and save only meaningful price changes.
   - Log parser start/end, source URL, record counts, errors, and duration.

6. Real-time updates
   - Replace Socket.IO with SignalR.
   - Hub events should cover current `fuelDataUpdate` and price-history requests.
   - Consider allowing anonymous read-only live updates, because the plan says guests can view public information.

7. Subscriptions and email
   - Keep current subscription creation.
   - Add email confirmation, password reset, price-change alerts, daily/weekly digests.
   - Use SMTP/SendGrid/AWS SES via configuration.

8. Admin panel
   - Users, roles, stations, prices, parser sources, API tokens, logs/metrics.
   - Can be MVC/Razor initially, with API endpoints available for later React.

9. Logging, metrics, Swagger, tests
   - Console and file logging.
   - Request/response logging middleware.
   - Auth success/failure logs.
   - Parser and database exception logs.
   - Basic API usage metrics: call count per controller, average response time.
   - Swagger/OpenAPI with auth examples.
   - Unit tests: at least 2 test classes, 5 tests each.
   - Integration tests: at least 1 controller test class, 5 tests.

## Database Migration / Rework

Start from `testq6.sql`, but convert it into EF Core entities and migrations instead of keeping raw SQL as the main source of truth.

Existing tables:

- `users`: id, email, password_hash, created_at.
- `gasstations`: id, name, address, city, latitude, longitude, image_url.
- `fuels`: id, name.
- `fuelprices`: id, gas_station_id, fuel_id, price, popularity, date.
- `subscriptions`: id, user_id, fuel_id, city, frequency.
- `comments`: id, user_id, gas_station_id, fuel_id, content, created_at, updated_at.

Recommended changes:

- Rename consistently: `Stations` instead of `GasStations`, or keep one naming style everywhere.
- Add `Roles` or use ASP.NET Core Identity roles.
- Add `EmailConfirmed`, `PasswordResetTokens`, and confirmation token fields if not using Identity.
- Add `ApiTokens`: id, name, token_hash, scopes, expires_at, revoked_at, created_by_user_id.
- Add `DataSources`: id, name, url, type, enabled, last_success_at.
- Add parser audit table: source_id, started_at, finished_at, status, records_found, records_saved, error.
- Add unique indexes:
  - station name + city, or normalized station key.
  - fuel name.
  - fuel price unique key on station + fuel + date.
  - subscription unique key on user + fuel + city + frequency.
- Add indexes for price-history queries: `(fuel_id, date)`, `(gas_station_id, fuel_id, date)`.
- Do not import personal sample users/password hashes from the SQL dump into the new project.

Important current bug to fix during migration:

- Fuel IDs are inconsistent in frontend/backend display logic. SQL says `1 = А 95+`, `2 = А 95`, `4 = ДП`, but the station cards treat `fuel_id = 1` as A-95 and `fuel_id = 2` as diesel. Replace hard-coded IDs with fuel-code/name mapping from the API.

## Frontend Migration

Keep the current first screen behavior:

- Price cards carousel.
- Subscription form for logged-in users.
- Chart.js price-history chart.
- Station comparison table.
- Google Map markers with station fuel prices.
- Login/register modal.

Required frontend changes:

- Move API base URL into a single config variable.
- Replace Socket.IO client with SignalR client.
- Replace hard-coded `http://localhost:3000` with the ASP.NET Core API URL.
- Do not hard-code Google Maps API key in the HTML. Move it to configuration and restrict it in Google Cloud.
- Remove duplicate initialization calls for `autoLogin`, `initChart`, and station loading.
- Avoid injecting unsanitized server data into `innerHTML`.
- Later React migration can reuse the same backend API and SignalR hub contracts.

## Files To Send To GPT Chat

Send these files:

- `C:\Users\Aleksandr\Desktop\plan\План.docx`
- `C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\testq6.sql`
- `C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\VER_20_05\untitled1\package.json`
- `C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\VER_20_05\untitled1\db.js`
- `C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\VER_20_05\untitled1\server.js`
- `C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\VER_20_05\untitled1\socketServer.js`
- `C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\VER_20_05\untitled1\parseFuel.js`
- `C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\VER_20_05\untitled1\frontend\public\index.html`
- `C:\Users\Aleksandr\Desktop\plan\LiveFuelMap1\VER_20_05\untitled1\frontend\public\default-station.jpg`

Do not send:

- `node_modules`
- `.git`
- `.idea`
- `package-lock.json`, unless exact dependency versions are required
- `.env` with real secrets
- personal user data from SQL if the chat is not private/trusted
- `VER_20_05.rar`, videos, PDFs, PPTX files, unless the assistant needs presentation/demo context

Instead of `.env`, send this example:

```env
DB_HOST=localhost
DB_USER=root
DB_PASSWORD=change_me
DB_NAME=livefuelmap
JWT_SECRET=change_me
PORT=3000
```

## Suggested Prompt For GPT Chat

Use this prompt together with the files above:

```text
Review this existing LiveFuelMap Node.js/MySQL/frontend project and rewrite it as an ASP.NET Core solution.

Target architecture:
- ASP.NET Core Web API backend in C#
- EF Core with MySQL and migrations
- Layered architecture: API, BLL, DAL, Infrastructure
- Repository, Unit of Work, Factory, interfaces, DI
- JWT auth with roles Guest/User/Admin
- API-token auth for third-party endpoints
- SignalR instead of Socket.IO
- C# hosted service for fuel-price parsing
- Swagger, logging, metrics, unit tests, integration tests
- Keep the current HTML/CSS/JS frontend initially, but make it easy to migrate to React later

Use the Word plan as requirements. Use the SQL dump only as the initial schema/reference, but improve it where needed. Do not import real personal sample users into seed data. Fix current fuel-ID mismatches and move secrets/API keys to configuration.

Deliver:
1. Proposed solution structure
2. EF Core entities and DbContext
3. Migrations/schema design
4. REST controllers and SignalR hub contracts
5. Parser hosted service design
6. Updated frontend API/SignalR integration plan
7. Tests and deployment checklist
```

## Acceptance Checklist

- App builds as a .NET solution.
- EF Core migrations create the database.
- Current frontend can load stations, prices, chart data, map data, login/register, subscribe, and compare stations against the C# backend.
- Parser runs on schedule and saves new price history.
- Swagger documents all APIs.
- JWT-protected, role-protected, and API-token-protected endpoints are demonstrated.
- Logs and basic metrics are available.
- Tests cover services and controllers.
- Secrets are not committed.
