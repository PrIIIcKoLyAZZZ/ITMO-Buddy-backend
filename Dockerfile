FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ITMO-Buddy-backend.slnx Directory.Build.props Directory.Packages.props ./
COPY src/ITMO.Buddy/Api/ITMO.Buddy.Api/ITMO.Buddy.Api.csproj src/ITMO.Buddy/Api/ITMO.Buddy.Api/
COPY src/ITMO.Buddy/Application/ITMO.Buddy.Application/ITMO.Buddy.Application.csproj src/ITMO.Buddy/Application/ITMO.Buddy.Application/
COPY src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/ITMO.Buddy.Application.Abstractions.csproj src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/
COPY src/ITMO.Buddy/Application/ITMO.Buddy.Application.Contracts/ITMO.Buddy.Application.Contracts.csproj src/ITMO.Buddy/Application/ITMO.Buddy.Application.Contracts/
COPY src/ITMO.Buddy/Domain/ITMO.Buddy.Domain/ITMO.Buddy.Domain.csproj src/ITMO.Buddy/Domain/ITMO.Buddy.Domain/
COPY src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure/ITMO.Buddy.Infrastructure.csproj src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure/
COPY src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/ITMO.Buddy.Infrastructure.Persistence.csproj src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/
RUN dotnet restore ITMO-Buddy-backend.slnx -p:Configuration=Release

COPY src/ src/
RUN dotnet publish src/ITMO.Buddy/Api/ITMO.Buddy.Api/ITMO.Buddy.Api.csproj \
    --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "ITMO.Buddy.Api.dll"]
