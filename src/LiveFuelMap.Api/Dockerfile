FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY Directory.Build.props ./
COPY src ./src
RUN dotnet restore src/LiveFuelMap.Api/LiveFuelMap.Api.csproj --configfile NuGet.Config
RUN dotnet publish src/LiveFuelMap.Api/LiveFuelMap.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY frontend /frontend
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LiveFuelMap.Api.dll"]
