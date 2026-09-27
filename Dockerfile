FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props FordNexus.sln ./
COPY src/FordNexus.Domain/FordNexus.Domain.csproj src/FordNexus.Domain/
COPY src/FordNexus.Application/FordNexus.Application.csproj src/FordNexus.Application/
COPY src/FordNexus.Infrastructure/FordNexus.Infrastructure.csproj src/FordNexus.Infrastructure/
COPY src/FordNexus.Api/FordNexus.Api.csproj src/FordNexus.Api/
RUN dotnet restore src/FordNexus.Api/FordNexus.Api.csproj

COPY src/ src/
RUN dotnet publish src/FordNexus.Api/FordNexus.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FordNexus.Api.dll"]
