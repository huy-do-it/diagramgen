FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY BuildingBlocks/ BuildingBlocks/
COPY Contracts/ Contracts/
COPY Services/Render/ Services/Render/
RUN dotnet publish Services/Render/Render.Worker/Render.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false /warnaserror

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "UMLForge.Render.Worker.dll"]
