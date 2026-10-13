# Build context is the repository root, not Apps/Api: the API's project references reach across
# Services/.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

# No per-project list to maintain: the build follows Api.csproj's own project references, so a
# service added to or removed from the API needs no change here. The NuGet cache mount keeps
# restored packages between builds, which is what a separate restore layer used to buy.
COPY Directory.Build.props ./
COPY Apps/Api/ Apps/Api/
COPY Services/ Services/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish Apps/Api/Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Kestrel serves plain HTTP in the container; TLS terminates at whatever fronts it. The published
# app has no .env beside it, so compose supplies the same variables through env_file.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
