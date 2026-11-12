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

COPY Directory.Build.props ./
COPY ["BuildingBlocks/UMLForge.BuildingBlocks.Auth/UMLForge.BuildingBlocks.Auth.csproj", "BuildingBlocks/UMLForge.BuildingBlocks.Auth/"]
COPY ["Gateway/UMLForge.Gateway/UMLForge.Gateway.csproj", "Gateway/UMLForge.Gateway/"]

RUN dotnet restore "Gateway/UMLForge.Gateway/UMLForge.Gateway.csproj"

COPY BuildingBlocks/UMLForge.BuildingBlocks.Auth/ BuildingBlocks/UMLForge.BuildingBlocks.Auth/
COPY Gateway/UMLForge.Gateway/ Gateway/UMLForge.Gateway/

WORKDIR "/src/Gateway/UMLForge.Gateway"
RUN dotnet build "UMLForge.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "UMLForge.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /warnaserror

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UMLForge.Gateway.dll"]
