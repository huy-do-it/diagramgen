# Build từ thư mục src/:  docker build -f Services/DiagramEngine/DiagramEngine.Grpc/Dockerfile src
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY Contracts/ Contracts/
COPY BuildingBlocks/ BuildingBlocks/
COPY Services/DiagramEngine/ Services/DiagramEngine/
RUN dotnet publish Services/DiagramEngine/DiagramEngine.Grpc/DiagramEngine.Grpc.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DiagramEngine.Grpc.dll"]
