# Build context is the repository root, not Apps/Api: the API's project references reach across
# Services/.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

# Project files first, so an edit to a .cs file reuses the cached restore layer. Keep this list in
# step with the projects under Services/: a missing line fails restore, a stale one fails the COPY.
COPY Apps/Api/Api.csproj Apps/Api/
COPY Services/Appointment/Appointment.csproj Services/Appointment/
COPY Services/Appointment/Contracts/Appointment.Contracts.csproj Services/Appointment/Contracts/
COPY Services/Auth/Auth.csproj Services/Auth/
COPY Services/Auth/Contracts/Auth.Contracts.csproj Services/Auth/Contracts/
COPY Services/Blog/Blog.csproj Services/Blog/
COPY Services/Blog/Contracts/Blog.Contracts.csproj Services/Blog/Contracts/
COPY Services/Cache/Cache.csproj Services/Cache/
COPY Services/Cloudflare/Cloudflare.csproj Services/Cloudflare/
COPY Services/Company/Company.csproj Services/Company/
COPY Services/Company/Contracts/Company.Contracts.csproj Services/Company/Contracts/
COPY Services/Contact/Contact.csproj Services/Contact/
COPY Services/Contact/Contracts/Contact.Contracts.csproj Services/Contact/Contracts/
COPY Services/Email/Contracts/Email.Contracts.csproj Services/Email/Contracts/
COPY Services/Email/Email.csproj Services/Email/
COPY Services/Faq/Contracts/Faq.Contracts.csproj Services/Faq/Contracts/
COPY Services/Faq/Faq.csproj Services/Faq/
COPY Services/Firebase/Firebase.csproj Services/Firebase/
COPY Services/Geo/Contracts/Geo.Contracts.csproj Services/Geo/Contracts/
COPY Services/Geo/Geo.csproj Services/Geo/
COPY Services/Mcp/Contracts/Mcp.Contracts.csproj Services/Mcp/Contracts/
COPY Services/Mcp/Mcp.csproj Services/Mcp/
COPY Services/Media/Contracts/Media.Contracts.csproj Services/Media/Contracts/
COPY Services/Media/Media.csproj Services/Media/
COPY Services/Message/Contracts/Message.Contracts.csproj Services/Message/Contracts/
COPY Services/Message/Message.csproj Services/Message/
COPY Services/Moderation/Contracts/Moderation.Contracts.csproj Services/Moderation/Contracts/
COPY Services/Moderation/Moderation.csproj Services/Moderation/
COPY Services/Notification/Contracts/Notification.Contracts.csproj Services/Notification/Contracts/
COPY Services/Notification/Notification.csproj Services/Notification/
COPY Services/Postgres/Contracts/Postgres.Contracts.csproj Services/Postgres/Contracts/
COPY Services/Postgres/Postgres.csproj Services/Postgres/
COPY Services/Resend/Resend.csproj Services/Resend/
COPY Services/User/Contracts/User.Contracts.csproj Services/User/Contracts/
COPY Services/User/User.csproj Services/User/
RUN dotnet restore Apps/Api/Api.csproj

COPY Apps/Api/ Apps/Api/
COPY Services/ Services/
RUN dotnet publish Apps/Api/Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Kestrel serves plain HTTP in the container; TLS terminates at whatever fronts it. The published
# app has no .env beside it, so compose supplies the same variables through env_file.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
