FROM mcr.microsoft.com/dotnet/sdk:10.0 AS source
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

FROM source AS build
RUN dotnet publish src/ITMO.Buddy/Api/ITMO.Buddy.Api/ITMO.Buddy.Api.csproj \
    --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

FROM source AS bundle
COPY .config/dotnet-tools.json .config/
RUN dotnet tool restore
RUN ConnectionStrings__Postgres='Host=localhost;Database=design_time;Username=design_time' \
    dotnet ef migrations bundle --project src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence \
    --configuration Release --output /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle ./
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "ITMO.Buddy.Api.dll"]
