# Getting Started

```powershell
git clone https://github.com/BradleyHewitt55/CompanyHQ.git
cd CompanyHQ
dotnet build Projects/Admin/Admin.csproj
dotnet build Projects/Cli/Cli.csproj
```

Start PostgreSQL with the provisioning login `companyhq/companyhq`, then create a development company:

```powershell
docker compose up -d postgres
$env:ConnectionStrings__CompanyHqServer = "Host=localhost;Database=postgres;Username=companyhq;Password=companyhq"
dotnet run --project Projects/Cli/Cli.csproj -- --c "alpha" --root "$PWD/MockServer"
```

Choose local storage to create a dedicated database and role on that PostgreSQL server, or supply an external connection string. Either choice writes the company's runtime credentials to `MockServer/Companies/alpha/db.json`.

Scaffold a service and enable it before building its image:

```powershell
cd MockServer/Companies/alpha
dotnet run --project ../../../Projects/Cli/Cli.csproj -- service Billing
# Set Settings/service.json enabled to true when Billing is ready.
cd ../../..
docker compose build admin-alpha
docker compose up -d postgres admin-alpha admin-beta caddy
```

Open `http://alpha.localhost:8080/admin`. Rebuild and recreate only `admin-alpha` after changing Alpha's services:

```powershell
docker compose up -d --build admin-alpha
```

To run Admin without Compose, bind it explicitly to one company:

```powershell
$env:CompanyHq__Company = "alpha"
$env:CompanyHq__ServerRoot = "$PWD/MockServer"
dotnet run --project Projects/Admin/Admin.csproj
```

Run verification:

```powershell
dotnet test Projects/Tests/UnitTests/UnitTests.csproj
dotnet test Projects/Tests/IntegrationTests/IntegrationTests.csproj
```
