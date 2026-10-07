FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY .companyhq/source/Apps/Api /src/.companyhq/source/Apps/Api
COPY .companyhq/source/Services /src/.companyhq/source/Services
COPY Apps/Api /src/Apps/Api
RUN dotnet publish Apps/Api/Company.Api.csproj -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app/Apps/Api
COPY --from=build /publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Company.Api.dll"]
