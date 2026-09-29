FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/server/BusinessWorkflowEngine.Api/BusinessWorkflowEngine.Api.csproj src/server/BusinessWorkflowEngine.Api/
COPY examples/ examples/
RUN dotnet restore src/server/BusinessWorkflowEngine.Api/BusinessWorkflowEngine.Api.csproj

COPY src/server/BusinessWorkflowEngine.Api/ src/server/BusinessWorkflowEngine.Api/
RUN dotnet publish src/server/BusinessWorkflowEngine.Api/BusinessWorkflowEngine.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "BusinessWorkflowEngine.Api.dll"]
