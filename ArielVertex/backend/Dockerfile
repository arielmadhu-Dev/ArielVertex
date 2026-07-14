# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/ArielVertex.Api/ArielVertex.Api.csproj
RUN dotnet publish src/ArielVertex.Api/ArielVertex.Api.csproj -c Release -o /app /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# Private upload storage lives on a mounted volume.
RUN mkdir -p /data/uploads
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Storage__Root=/data/uploads
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=4s --start-period=20s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "ArielVertex.Api.dll"]
