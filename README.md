# AFMS – Accountable Forms Management System

AFMS is an offline-first Windows desktop system for a Municipal Treasury Office. The current implementation provides a layered .NET 8/WPF/MVVM solution, EF Core SQLite persistence, secure authentication, permission-based authorization, master-data administration, system settings, audit logging, and a navigable application shell.

## Implemented modules

- Accountable form type management
- Barangay management
- Collector management
- Administrator-only user, role, reset-password, and account-activity management
- Permission-based navigation and service authorization
- Editable municipality, office, signatory, fiscal-year, reference, backup, and report settings
- Search, active/inactive filters, activation/deactivation, validation, notifications, and audit history
- Phase 3 stock-receiving drafts, submission, transactional finalization, and controlled reversal
- Automatic accountable-form booklet generation with serial-range and quantity validation
- Searchable stock inventory, individual receipt-number lookup, booklet detail, and movement history
- Atomic year/prefix reference-number sequences such as `RCV-2026-000001`
- Phase 4 issuance drafts, booklet reservations, supervisor approval/rejection, atomic finalization, and exceptional reversal
- Collector accountability ledgers derived from immutable stock movements, with outstanding and summary views
- Offline Accountable Forms Issuance Receipt print preview and PDF export using configurable office settings
- Phase 5 multi-range receipt usage with sequential-gap, duplicate, ownership, and overlap validation
- Partial unused-range transfers and returns with supervisor approval and atomic collector-ledger updates
- Spoiled, cancelled, lost, missing, damaged, and invalidated receipt exception processing
- Safe chained reversals that preserve later activity and restore prior range accountability only when valid
- Supporting transfer, return, and exception attachments stored under organized AFMS application-data folders
- Phase 6 Report Center with inventory, collector, transaction, exception, consolidated, and audit report catalogs
- Shared report-query calculations based on finalized/reversed movement history, with explicit draft and transaction-status filters
- Professional offline PDF generation through PDFsharp and Excel export through ClosedXML
- On-screen preview, configured output folders, PDF preview, registered-printer support, and export/print audit records
- Printable receiving, issuance, usage, transfer, return, exception, and reversal forms
- Phase 7 validated manual, scheduled, closing, and pre-restore backups with retention and backup history
- Restart-safe database restoration with corruption detection and an automatic safety backup
- Fiscal-year pre-close checks, year-end snapshots, carry-forward rows, dual authorization, and closed-period guards
- Append-only SHA-256 integrity-chained audit records with server-side filters and pagination
- Generic failed-login responses, lockout, configurable inactivity logout, and live permission refresh
- Serialized database writes/maintenance operations, UTC indexed history queries, DataGrid virtualization, and a live operational dashboard

## Projects

- `AFMS.App` — WPF views, view models, commands, navigation, startup, and exception handling.
- `AFMS.Domain` — entities, enums, business primitives, and serial-range rules.
- `AFMS.Application` — use-case services, validation, DTOs, and infrastructure-neutral contracts.
- `AFMS.Infrastructure` — EF Core, SQLite provider registration, repositories, BCrypt, migrations, logging support, and local file paths.
- `AFMS.Tests` — unit and integration tests.

## Local data

The application creates its database and rolling logs under `%LOCALAPPDATA%\AFMS`. It requires no network connection at runtime. The EF Core provider is selected only in `AFMS.Infrastructure.DependencyInjection`, so a future multi-user deployment can replace SQLite with PostgreSQL or SQL Server without changing the domain or WPF layers.

Municipality-specific starter records are stored in `AFMS.App/initial-data.json`. The initializer imports each configuration version only once, never overwrites records already edited in the application, and records the import in the audit log. Imported barangays, form types, collectors, and settings remain editable through the administration screens.

## Development

```powershell
dotnet restore AFMS.sln
dotnet build AFMS.sln
dotnet test AFMS.sln
dotnet run --project AFMS.App
```

On first launch, AFMS applies migrations and requires creation of an administrator account that meets the password policy. No permanent or default administrator password exists. Phase 3 is introduced by `Phase3StockReceivingInventory`; Phase 4 by `Phase4IssuanceCollectorAccountability`; and Phase 5 by `Phase5UsageTransfersReturnsExceptions` plus `Phase5RangeReversalLineage`.

Phase 7 is introduced by `Phase7SecurityBackupFiscalHardening`, with follow-up model-synchronization migrations for portable UTC history queries. The live database is migrated automatically during startup.

## Windows installer

1. Install the .NET 8 SDK and Inno Setup 6 on the build computer.
2. Open PowerShell in this repository.
3. Run `powershell -ExecutionPolicy Bypass -File .\build-installer.ps1`.
4. The script restores packages, runs the Release test suite, publishes a self-contained Windows x64 build, and compiles `artifacts\installer\AFMS-Setup-1.0.0-win-x64.exe`.
5. Test the installer on a clean Windows 10/11 x64 computer. On first launch, create the administrator; no default password is included.

The installer writes program files under Program Files. The live database, attachments, and logs remain under `%LOCALAPPDATA%\AFMS`, so an application upgrade or uninstall does not silently erase municipal records. For deployment, code-sign both `AFMS.App.exe` and the final installer with the municipality's trusted Authenticode certificate.
