# One image per company: the Admin host plus only that company's compiled services.
# Build context is the repository root so the service projects resolve the shared
# Abstractions project through their existing relative Directory.Build.props path.
# Rebuilding this image is what refreshes a company's services (EXECUTE.md, P1) —
# nothing else scans them, and no other company's sources enter the build.
ARG COMPANY

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG COMPANY
WORKDIR /src
COPY . .

# Host binary and its built-in plugins.
RUN dotnet publish Projects/Admin/Admin.csproj -c Release -o /app

# Publish only this company's services into the immutable plugin root. POSIX shell
# guards keep a company with no scaffolded services buildable.
RUN mkdir -p "/opt/companyhq/Companies/${COMPANY}/Services" \
    && for csproj in MockServer/Companies/${COMPANY}/Services/*/*.csproj; do \
         [ -e "$csproj" ] || continue; \
         service="$(basename "$(dirname "$csproj")")"; \
         destination="/opt/companyhq/Companies/${COMPANY}/Services/${service}"; \
         dotnet publish "$csproj" -c Release -o "${destination}/bin"; \
         if [ -f "MockServer/Companies/${COMPANY}/Services/${service}/Settings/service.json" ]; then \
           mkdir -p "${destination}/Settings"; \
           cp "MockServer/Companies/${COMPANY}/Services/${service}/Settings/service.json" \
             "${destination}/Settings/service.json"; \
         fi; \
       done

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG COMPANY
WORKDIR /app
COPY --from=build /app ./
# PluginRoot is immutable image content; ServerRoot is mutable company state and may
# be mounted without hiding rebuilt service assemblies.
COPY --from=build /opt/companyhq /opt/companyhq
ENV CompanyHq__Company=${COMPANY} \
    CompanyHq__PluginRoot=/opt/companyhq \
    CompanyHq__ServerRoot=/srv/companyhq \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Admin.dll"]
