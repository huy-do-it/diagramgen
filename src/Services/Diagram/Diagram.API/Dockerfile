FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Services/Diagram/Diagram.API/Diagram.API.csproj", "Services/Diagram/Diagram.API/"]
COPY ["Services/Diagram/Diagram.Service/Diagram.Service.csproj", "Services/Diagram/Diagram.Service/"]
COPY ["Services/Diagram/Diagram.Repositories/Diagram.Repositories.csproj", "Services/Diagram/Diagram.Repositories/"]

RUN dotnet restore "Services/Diagram/Diagram.API/Diagram.API.csproj"

COPY Services/Diagram/ Services/Diagram/

WORKDIR "/src/Services/Diagram/Diagram.API"
RUN dotnet build "Diagram.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Diagram.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Diagram.API.dll"]
