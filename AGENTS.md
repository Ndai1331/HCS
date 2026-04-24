# AGENTS.md

## Cursor Cloud specific instructions

### Overview

HC (AxisHCS) is a Hospital/Healthcare Management System built on **ABP Framework Commercial v10.0.1** targeting **.NET 10.0** with **Blazor Server** frontend, **PostgreSQL** database, and **Redis** for caching/locking.

### Architecture (3 runnable services + 1 migrator)


| Service      | Project               | Default URL               | Purpose                |
| ------------ | --------------------- | ------------------------- | ---------------------- |
| AuthServer   | `src/HC.AuthServer`   | `https://localhost:44301` | OpenIddict auth server |
| HttpApi.Host | `src/HC.HttpApi.Host` | `https://localhost:44379` | REST API backend       |
| Blazor       | `src/HC.Blazor`       | `https://localhost:44302` | Blazor Server frontend |
| DbMigrator   | `src/HC.DbMigrator`   | N/A (console)             | DB migrations & seed   |


### Prerequisites

- **.NET 10 SDK** — install via `dotnet-install.sh --channel 10.0`
- **Redis** — must be running on `127.0.0.1:6379` (all services depend on it)
- **PostgreSQL** — remote at `113.160.232.208:5400`, DB: `AxisHCS` (connection strings in `appsettings.json`)
- **Node.js v18+** — for `abp install-libs` client-side packages
- **ABP CLI** — `dotnet tool install -g Volo.Abp.Studio.Cli`

### ABP Commercial License (CRITICAL)

All three services require a valid ABP Commercial license at runtime. The license code exists in each project's `appsettings.secrets.json`. However, in **development mode**, ABP also requires the developer to be logged in:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
abp login <username> -p <password>
```

Without valid ABP login credentials, services will crash with `ABP-LIC-ERROR`. This is the primary blocker for running services locally in cloud environments.

### NuGet Configuration

The root `NuGet.Config` uses `packageSourceMapping`. The Volo.Forms module has its own `NuGet.Config` with source key `ABP Commercial NuGet Source`. Both source keys must be mapped in the root config for `dotnet restore` to succeed — this mapping has already been added.

### Build & Test Commands

- **Restore**: `dotnet restore HC.sln`
- **Build**: `dotnet build HC.sln`
- **Tests**: `dotnet test modules/Volo.Forms/test/Volo.Forms.Domain.Tests/` and `dotnet test modules/Volo.Forms/test/Volo.Forms.Application.Tests/`
- **Client libs**: `abp install-libs -wd src/HC.Blazor && abp install-libs -wd src/HC.AuthServer`

### Running Services

Start in this order: Redis → AuthServer → HttpApi.Host → Blazor

```bash
sudo redis-server --daemonize yes
cd src/HC.AuthServer && dotnet run --urls "https://localhost:44301"
cd src/HC.HttpApi.Host && dotnet run --urls "https://localhost:44379"
cd src/HC.Blazor && dotnet run --urls "https://localhost:44302"
```

### OpenIddict Dev Certificate

Generate before first run:

```bash
cd src/HC.AuthServer && dotnet dev-certs https -v -ep openiddict.pfx -p 9fafa8e6-4e2f-41ae-98c4-3dee157c40c6
```

### Gotchas

- The `packageSourceMapping` in root `NuGet.Config` must include both `nuget.abp.io` and `ABP Commercial NuGet Source` keys for Volo.* packages, otherwise restore fails for the Volo.Forms module.
- Redis must be running before any service starts — services will crash immediately without it.
- The database is remote (not local). Connection string points to `113.160.232.208:5400`.
- RabbitMQ and MinIO are optional — services start without them but real-time chat/file features won't work.

