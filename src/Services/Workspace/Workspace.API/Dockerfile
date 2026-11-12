FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Contracts/UMLForge.Contracts.Events/UMLForge.Contracts.Events.csproj", "Contracts/UMLForge.Contracts.Events/"]
COPY ["BuildingBlocks/UMLForge.BuildingBlocks.Auth/UMLForge.BuildingBlocks.Auth.csproj", "BuildingBlocks/UMLForge.BuildingBlocks.Auth/"]
COPY ["BuildingBlocks/UMLForge.BuildingBlocks.Web/UMLForge.BuildingBlocks.Web.csproj", "BuildingBlocks/UMLForge.BuildingBlocks.Web/"]
COPY ["BuildingBlocks/UMLForge.BuildingBlocks.Jobs/UMLForge.BuildingBlocks.Jobs.csproj", "BuildingBlocks/UMLForge.BuildingBlocks.Jobs/"]
COPY ["BuildingBlocks/UMLForge.BuildingBlocks.Outbox/UMLForge.BuildingBlocks.Outbox.csproj", "BuildingBlocks/UMLForge.BuildingBlocks.Outbox/"]
COPY ["BuildingBlocks/UMLForge.BuildingBlocks.EventBus/UMLForge.BuildingBlocks.EventBus.csproj", "BuildingBlocks/UMLForge.BuildingBlocks.EventBus/"]
COPY ["Services/Workspace/Workspace.Repositories/Workspace.Repositories.csproj", "Services/Workspace/Workspace.Repositories/"]
COPY ["Services/Workspace/Workspace.Service/Workspace.Service.csproj", "Services/Workspace/Workspace.Service/"]
COPY ["Services/Workspace/Workspace.API/Workspace.API.csproj", "Services/Workspace/Workspace.API/"]

RUN dotnet restore "Services/Workspace/Workspace.API/Workspace.API.csproj"

COPY Contracts/ Contracts/
COPY BuildingBlocks/ BuildingBlocks/
COPY Services/Workspace/ Services/Workspace/

WORKDIR "/src/Services/Workspace/Workspace.API"
RUN dotnet build "Workspace.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Workspace.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Workspace.API.dll"]
