# Staging container for synthetic test data. Requires external SQL Server and persistent storage.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore DareCare.slnx
RUN dotnet publish src/DareCare.Api/DareCare.Api.csproj -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
RUN mkdir -p /data/prescriptions && chown -R app:app /data
COPY --chown=app:app --from=build /publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "DareCare.Api.dll"]
