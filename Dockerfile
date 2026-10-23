# FREE DISPOSABLE demo only; do not use with real patient data.
# Production SQL Server deployment lives in the non-demo branch.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore DareCare.slnx
RUN dotnet publish src/DareCare.Api/DareCare.Api.csproj -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# DISPOSABLE TEST DATA ONLY. This branch's image defaults to Staging/SQLite.
# .NET WebApplication gives DOTNET_ENVIRONMENT priority over ASPNETCORE_ENVIRONMENT.
# Runtime environment variables from Render can override these defaults.
# Never set JWT secrets in the Dockerfile or commit real patient data.
ENV DOTNET_ENVIRONMENT=Staging \
    ASPNETCORE_ENVIRONMENT=Staging \
    ASPNETCORE_HTTP_PORTS=8080 \
    Database__Provider=Sqlite \
    Demo__AllowStagingSqlite=true \
    Auth__RequireConfirmedEmail=false \
    ApiDocs__EnableInStaging=true \
    Storage__PrivateDirectory=/tmp/darecare-demo/prescriptions
RUN mkdir -p /tmp/darecare-demo/prescriptions && chown -R app:app /tmp/darecare-demo
COPY --chown=app:app --from=build /publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "DareCare.Api.dll"]
