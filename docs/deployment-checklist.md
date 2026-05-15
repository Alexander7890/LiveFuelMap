# Deployment Checklist

- Set `ConnectionStrings__Default` via environment variables or a secret manager.
- Replace `Jwt__Secret` with a long random value.
- Set `Frontend__GoogleMapsApiKey` only in runtime configuration and restrict the key in Google Cloud.
- Configure SMTP, SendGrid, AWS SES, or another provider through `Email__*` settings.
- Run `dotnet ef database update` against MySQL before first production start.
- Keep `Parser__RunOnStartup=false` for production unless a one-time bootstrap parse is intended.
- Review `DataSources`; enable the second JSON source only after replacing the placeholder URL.
- Publish a release build: `dotnet publish src/LiveFuelMap.Api/LiveFuelMap.Api.csproj -c Release`.
- Smoke-test `/swagger`, `/api/fuels`, `/api/stations`, `/hubs/fuel`, JWT login, and API-token protected `/api/external/current-prices`.
- Route logs from `logs/livefuelmap.log` and console output into the hosting provider log system.
