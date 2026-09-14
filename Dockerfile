FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/

RUN dotnet restore ./src/OrizonAgents.Web/OrizonAgents.Web.csproj
RUN dotnet publish ./src/OrizonAgents.Web/OrizonAgents.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --property:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

ENTRYPOINT ["sh", "-c", "dotnet OrizonAgents.Web.dll --urls http://0.0.0.0:${PORT:-8080}"]
