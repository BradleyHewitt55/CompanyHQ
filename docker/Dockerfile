FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:127d7d4d601ae26b8e04c54efb37e9ce8766931bded0ee59fcd799afd21d6850 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Web/Web.csproj && dotnet restore src/Builder/Builder.csproj && dotnet publish src/Web/Web.csproj -c Release -o /out --no-restore -p:SkipServiceBuild=true \
    && mkdir -p /out/modules \
    && for project in $(find src/Services -name '*.csproj' ! -path '*/Tests/*'); do name="$(basename "${project%.csproj}")"; output="/tmp/service-$name"; if dotnet publish "$project" -c Release -o "$output" --nologo; then cp -R "$output/." /out/; cp "$output/$name.dll" /out/modules/; find "$output" -maxdepth 1 -name '*.json' -exec cp {} /out/modules/ \;; else echo "Service $name failed to compile and was skipped." >&2; fi; done \
    && if [ -d lib ]; then cp lib/*.dll lib/*.json /out/modules/ 2>/dev/null || true; cp lib/*.so /out/ 2>/dev/null || true; fi

FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:127d7d4d601ae26b8e04c54efb37e9ce8766931bded0ee59fcd799afd21d6850 AS builder
WORKDIR /builder
COPY src/Builder/ src/Builder/
RUN dotnet restore src/Builder/Builder.csproj
ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081
ENTRYPOINT ["dotnet", "run", "--project", "src/Builder/Builder.csproj", "--no-launch-profile", "--no-restore"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:1fa23fc4872d95fd71c2833ebe65d7e84a43b2d51a31d119516852f13d9505a7 AS runtime
WORKDIR /app
COPY --from=build /out .
COPY docker/entrypoint.sh /entrypoint.sh
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && chmod +x /entrypoint.sh && mkdir -p /app/modules /app/overrides /data /data-protection && chown -R app:app /app /data /data-protection
USER app
ENV ASPNETCORE_URLS=http://+:8080 ModulesPath=/app/modules AdminOverridesPath=/app/overrides/Web.Admin.Overrides.dll Database__Provider=postgres DataProtection__Path=/data-protection
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=5 CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["/entrypoint.sh"]
