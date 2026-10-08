# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY backend/ backend/
RUN dotnet restore backend/ClaimsModule.sln
RUN dotnet publish backend/src/ClaimsModule.API/ClaimsModule.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM build AS migration-build
RUN dotnet tool install --global dotnet-ef --version 9.0.20
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj \
    --startup-project backend/src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --output /app/migrations/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ClaimsModule.API.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS migrations
WORKDIR /app
COPY --from=migration-build /app/migrations/efbundle ./efbundle
COPY backend/docker/run-migrations.sh ./run-migrations.sh
RUN chmod +x ./efbundle ./run-migrations.sh
USER $APP_UID
ENTRYPOINT ["./run-migrations.sh"]

