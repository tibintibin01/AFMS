# AFMS – Accountable Forms Management System

AFMS is an offline-first Windows desktop foundation for a Municipal Treasury Office. This phase provides a layered .NET 8/WPF/MVVM solution, domain model, EF Core SQLite persistence, secure initial administrator setup, authentication, audit logging, authorization groundwork, and a navigable application shell.

## Projects

- `AFMS.App` — WPF views, view models, commands, navigation, startup, and exception handling.
- `AFMS.Domain` — entities, enums, business primitives, and serial-range rules.
- `AFMS.Application` — use-case services, validation, DTOs, and infrastructure-neutral contracts.
- `AFMS.Infrastructure` — EF Core, SQLite provider registration, repositories, BCrypt, migrations, logging support, and local file paths.
- `AFMS.Tests` — unit and integration tests.

## Local data

The application creates its database and rolling logs under `%LOCALAPPDATA%\AFMS`. It requires no network connection at runtime. The EF Core provider is selected only in `AFMS.Infrastructure.DependencyInjection`, so a future multi-user deployment can replace SQLite with PostgreSQL or SQL Server without changing the domain or WPF layers.

## Development

```powershell
dotnet restore AFMS.sln
dotnet build AFMS.sln
dotnet test AFMS.sln
dotnet run --project AFMS.App
```

On first launch, AFMS applies migrations and requires creation of an administrator account that meets the password policy. No permanent or default administrator password exists.

Stock receiving, issuance, usage, transfers, returns, and reports are intentionally not implemented in this foundation phase.
