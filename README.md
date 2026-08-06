# AFMS Inventory

An offline Windows desktop app for one person to keep the inventory records of a
municipal office. It answers four questions and nothing more:

- What came in, and on what document?
- What went out, and to whom?
- What is on hand right now?
- What is still with someone and has not come back?

It handles ordinary supplies counted by quantity and accountable forms tracked by
serial number in the same list. For serial-tracked items it refuses to release a
serial that is still out with somebody, and names who has it.

## Screens

| Screen | What it is for |
|---|---|
| Dashboard | On hand, what is out, what is running low, latest activity |
| Stock In | Record a delivery, a transfer in, a return, a beginning balance, or a count correction |
| Release | Record items going out, to whom, and for what; prints a signed release slip |
| Who Has What | Everything released and not returned, with the serial ranges each person still holds |
| History | Every in and out line, searchable by item, person, date, or serial |
| Reports | Five printable reports as PDF or Excel |
| Items | The things being tracked, their unit, and their low-stock level |
| People | Everyone items are released to |
| Settings & Backup | Office details for printed forms, backups and restore, and the record of changes |

## How the records work

Nothing is ever deleted. A mistake is **voided** with a written reason, and both
the original entry and the correction stay on file.

Quantity on hand and accountability are never stored as a running total. They are
worked out from the recorded stock-in and release lines every time they are shown,
so a balance can never drift away from the documents behind it.

Accountability clears in one way only: a Stock In entry of the kind *Returned by a
recipient*, naming the person. That is what takes an item, and its serial range,
off their outstanding list.

Every action is written to an append-only record of changes. Database triggers
block edits and deletes on it, so the trail holds even outside the app.

## Reports

- Stock on hand
- Stock card for one item, with a running balance
- Releases to recipients, with serial numbers
- Outstanding accountability
- Stock in and out summary for a date range

Each one prints with the office header and signature lines from Settings, as PDF
or as a filterable Excel sheet.

## Where your data lives

Everything is under `%LOCALAPPDATA%\AFMS`:

```
inventory.db      your records
Backups\          dated .afmsbak copies
Reports\          exported PDF and Excel files
Logs\             rolling technical log, 31 days
```

Program files and records are kept apart on purpose, so reinstalling or upgrading
the app never touches the records.

Backups are checked as soon as they are written: the copy has to open, pass an
integrity check, and contain the expected tables. Each one is a single
self-contained file, so copying it to a USB drive is enough. One is taken
automatically each day and one when the app closes. Restoring asks for a reason,
takes a safety copy of the current data first, and applies on restart.

Nothing is sent anywhere. The app needs no network connection.

## First launch

You will be asked to create one account. There is no default password. The
password needs 12 characters with an uppercase letter, a lowercase letter, a
number, and a symbol. After five wrong attempts the account locks for 15 minutes,
and the app locks itself after a period of inactivity you set in Settings.

Starter records come from `AFMS.App/starter-data.json`, which is imported once
into a new database. Edit it before the first launch to match your office. It
never overwrites anything you later change in the app.

## Projects

- `AFMS.Core` — records, rules, database, reports, and backups
- `AFMS.App` — the WPF windows and screens
- `AFMS.Tests` — 78 tests, weighted towards serial tracking and accountability

SQLite is chosen in one place, `AFMS.Core.DependencyInjection`.

## Development

```powershell
dotnet restore AFMS.sln
dotnet build AFMS.sln
dotnet test AFMS.sln
dotnet run --project AFMS.App
```

If the build machine has a newer .NET runtime but not 8.0, set
`DOTNET_ROLL_FORWARD=LatestMajor` before running the tests. The test project
already sets `RollForward` for itself.

Changing anything under `AFMS.Core/Domain` or `Persistence` means a new migration:

```powershell
dotnet tool restore
dotnet ef migrations add YourChange --project AFMS.Core --output-dir Persistence\Migrations
```

Migrations are applied automatically at start up, after a safety backup.

## Windows installer

1. Install the .NET 8 SDK and Inno Setup 6.
2. Run `powershell -ExecutionPolicy Bypass -File .\build-installer.ps1`.
3. Test the result on a clean Windows 10 or 11 x64 machine.

The script runs the tests, publishes a self-contained x64 build, and compiles the
installer into `artifacts\installer`. For real deployment, code-sign both
`AFMS.App.exe` and the installer.

## Worth knowing

The database file and the backups are not encrypted. Anyone who can open the
folder can read the records. If the computer is shared or could be lost, turn on
BitLocker.
