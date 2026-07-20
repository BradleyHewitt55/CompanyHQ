# Getting Started

Clone the repository:

```powershell
git clone https://github.com/BradleyHewitt55/CompanyHQ.git
cd CompanyHQ
```

Start the local PostgreSQL container with the admin login
`companyhq/companyhq` on `localhost:5432`, then build each project:

```powershell
dotnet build Projects/Abstractions/Abstractions.csproj
dotnet build Projects/Services/Companies/Companies.csproj
dotnet build Projects/Services/Postgress/Postgress.csproj
dotnet build Projects/Services/Auth/Auth.csproj
dotnet build Projects/Services/Plugins/Plugins.csproj
dotnet build Projects/Admin/Admin.csproj
dotnet build Projects/Cli/Cli.csproj
```

Create a development company under `MockServer/`:

```powershell
$env:ConnectionStrings__CompanyHqServer = "Host=localhost;Database=postgres;Username=companyhq;Password=companyhq"
dotnet run --project Projects/Cli/Cli.csproj -- --c "Acme" --root "$PWD/MockServer"
```

Scaffold a service from inside the company directory — the company and server
root are inferred from the working directory:

```powershell
cd MockServer/Companies/Acme
dotnet run --project ../../../Projects/Cli/Cli.csproj -- service Billing
cd ../../..
```

Run the Admin host and open `https://localhost:7081/admin`:

```powershell
dotnet run --project Projects/Admin/Admin.csproj
```

Run the tests:

```powershell
dotnet test Projects/Tests/UnitTests/UnitTests.csproj
dotnet test Projects/Tests/IntegrationTests/IntegrationTests.csproj
```
