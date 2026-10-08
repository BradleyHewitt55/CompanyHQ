FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src
COPY nuget.config Directory.Build.props ./
COPY Apps/Api Apps/Api
COPY Services Services
# The CompanyHQ packages need a GitHub token; as a build secret it never reaches an image layer.
# The same build writes every OpenAPI contract. Describing the API boots the host, so it gets
# throwaway settings and never touches a database.
RUN --mount=type=secret,id=packages_token,env=COMPANYHQ_PACKAGES_TOKEN \
    Postgres__Password=contracts-only Postgres__AutoMigrate=false Postgres__AutoSeed=false \
    Jwt__SigningKey="$(head -c 32 /dev/urandom | base64)" \
    dotnet publish Apps/Api/Company.Api.csproj -c Release -o /publish \
        -p:OpenApiGenerateDocumentsOnBuild=true -p:OpenApiDocumentsDirectory=/openapi

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4
WORKDIR /app/Apps/Api
COPY --from=build /publish ./
# The start scripts copy these back into each service's Contracts folder.
COPY --from=build /openapi /app/openapi
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Company.Api.dll"]
