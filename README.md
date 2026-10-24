# EF Core Terminal UI (`efui`)

[![NuGet](https://img.shields.io/nuget/v/EFCoreTerminalUI.svg)](https://www.nuget.org/packages/EFCoreTerminalUI)
[![NuGet Downloads](https://img.shields.io/nuget/dt/EFCoreTerminalUI.svg)](https://www.nuget.org/packages/EFCoreTerminalUI)
[![CI](https://github.com/alican645/EntityFrameworkUI/actions/workflows/ci.yml/badge.svg)](https://github.com/alican645/EntityFrameworkUI/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

An interactive terminal UI for Entity Framework Core migrations. Pick your projects and migrations with the arrow keys to add, remove or apply migrations — no more typing long `dotnet ef` commands with `--project` and `--startup-project` flags.

```
── EF Core Terminal UI ──────────────────────────────────────────────
Migration   Infrastructure/Infrastructure.csproj
Startup     WebApi/WebApi.csproj
DbContext   Infrastructure.AppDbContext
Database    connected

╭────┬─────────────────────────────────────┬──────────────╮
│ #  │ Migration                           │ Status       │
├────┼─────────────────────────────────────┼──────────────┤
│ 1  │ 20261023055621_Init                 │ ✔ applied    │
│ 2  │ 20261023055653_AddPrice  ◀ latest   │ ● pending    │
╰────┴─────────────────────────────────────┴──────────────╯

What do you want to do? (↑/↓ and Enter)
> + Add Migration
  - Remove Last Migration
  ↑ Update Database (latest migration)
  → Update Database (pick migration / revert)
  # Generate SQL Script
  ...
```

## Features

- **Arrow-key menus** for every operation — nothing to memorize.
- **Built for layered solutions**: scans the folder for `.csproj` files and suggests the migration project (where your `DbContext` and `Migrations` folder live) and the startup project (Web / Exe).
- **Remembers your setup**: project and `DbContext` selections are saved to `.efui.json`, so the next run goes straight to the menu.
- **Migration status at a glance**: applied (✔) and pending (●) migrations, with the latest one highlighted. Falls back to an offline list when the database is unreachable.
- **Safe remove**: if the last migration is already applied, `efui` offers to revert it from the database before removing it.
- **Revert to any point**: update the database to any migration, or back to `0`.
- **Live, colorized output** from `dotnet ef`.
- **Multilingual UI**: English, Türkçe, Deutsch, Español and Français.

## Installation

```bash
dotnet tool install --global EFCoreTerminalUI
```

Update to the latest version:

```bash
dotnet tool update --global EFCoreTerminalUI
```

## Usage

Run it from your solution folder:

```bash
efui              # current folder
efui ../MyProject # another folder
```

1. On first run, pick the **migration project** (e.g. `Infrastructure`) and the **startup project** (e.g. `WebApi`). The most likely candidates are listed first.
2. If there is more than one `DbContext`, pick one from the list.
3. Your choices are saved to `.efui.json`. Use **Settings → Change project / DbContext** to change them later.

### Menu

| Action | Runs |
|---|---|
| Add Migration | `dotnet ef migrations add <name>`, optionally followed by `database update` |
| Remove Last Migration | `dotnet ef migrations remove` (with `--force` to revert it first if it is applied) |
| Update Database (latest) | `dotnet ef database update` |
| Update Database (pick / revert) | `dotnet ef database update <migration>` or `0` |
| Generate SQL Script | `dotnet ef migrations script <from> <to> [--idempotent] -o file.sql` |
| Drop Database | `dotnet ef database drop --force` (you must type `DROP` to confirm) |
| Settings | Change the UI language or the project / `DbContext` selection |

### Language

On first run `efui` asks for your language, with your system language suggested first. You can change it any time from **Settings → Language**.

| Language | Code |
|---|---|
| English | `en` |
| Türkçe | `tr` |
| Deutsch | `de` |
| Español | `es` |
| Français | `fr` |

The language is a per-user preference stored in `~/.efui/config.json`, so it applies to every project.

### Configuration files

| File | Scope | Contents |
|---|---|---|
| `.efui.json` (in your solution folder) | per project | migration project, startup project, `DbContext` |
| `~/.efui/config.json` | per user | UI language |

> **Tip:** `.efui.json` holds local paths for your machine — you may want to add it to your project's `.gitignore`.

## Requirements

- .NET 9 SDK or later
- [`dotnet-ef`](https://www.nuget.org/packages/dotnet-ef) — `efui` offers to install it if it is missing
- `Microsoft.EntityFrameworkCore.Design` referenced by the startup project

## Building from source

```bash
git clone https://github.com/alican645/EntityFrameworkUI.git
cd EntityFrameworkUI/EFCoreTerminalUI
dotnet pack -c Release
dotnet tool install --global --add-source ./nupkg EFCoreTerminalUI
```

## Contributing

Issues and pull requests are welcome — including new translations: add a `Strings` instance in `EFCoreTerminalUI/Localization/Translations.cs` and register it in `L.Languages`. Any text you leave out falls back to English.

Development happens on the `dev` branch; merging into `main` with a bumped `<Version>` publishes a new release to NuGet automatically.

## License

[MIT](LICENSE). This is a community tool and is not affiliated with or endorsed by Microsoft.
