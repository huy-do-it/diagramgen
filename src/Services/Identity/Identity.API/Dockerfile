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
COPY ["Services/Identity/Identity.Repositories/Identity.Repositories.csproj", "Services/Identity/Identity.Repositories/"]
COPY ["Services/Identity/Identity.Service/Identity.Service.csproj", "Services/Identity/Identity.Service/"]
COPY ["Services/Identity/Identity.API/Identity.API.csproj", "Services/Identity/Identity.API/"]

RUN dotnet restore "Services/Identity/Identity.API/Identity.API.csproj"

COPY Contracts/UMLForge.Contracts.Events/ Contracts/UMLForge.Contracts.Events/
COPY Services/Identity/ Services/Identity/

WORKDIR "/src/Services/Identity/Identity.API"
RUN dotnet build "Identity.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Identity.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Identity.API.dll"]
