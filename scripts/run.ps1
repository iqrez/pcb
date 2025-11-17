$ErrorActionPreference = "Stop"

dotnet restore

dotnet build -c Release -f net8.0-windows10.0.17763.0

dotnet run --project ./src/ReziRemapLite/ReziRemapLite.csproj -c Release -f net8.0-windows10.0.17763.0
