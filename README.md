# Aspire.Nexus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Aspire](https://img.shields.io/badge/Aspire-13.1-blueviolet)](https://learn.microsoft.com/en-us/dotnet/aspire/)

A **configuration-driven** .NET Aspire AppHost that orchestrates your entire dev environment — infrastructure, APIs, and frontend clients — from JSON config. No C# changes needed to add, remove, or toggle services.

---

## What is .NET Aspire?

[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) is Microsoft's framework for building observable, production-ready cloud-native apps. It gives you:

- A **dashboard** to monitor all your services (logs, traces, metrics) in one place
- **Service orchestration** — start your entire stack with one command
- **Service discovery** — services find each other automatically

If you're running microservices, databases, and frontends locally, Aspire replaces the pain of opening 10 terminals and running things manually.

## The Problem Aspire.Nexus Solves

In a standard Aspire AppHost, you **hardcode every service** in `Program.cs`:

```csharp
// Standard Aspire — every service is hardcoded in C#
var postgres = builder.AddPostgres("postgres").WithPgAdmin();
var redis = builder.AddRedis("redis");
var api = builder.AddProject<Projects.OrderApi>("order-api");
var web = builder.AddNpmApp("web", "../web-client");
// ... repeat for every service in your solution
```

This works for small projects. But when your solution grows to **10+ APIs, 5+ frontends, and multiple databases**, you hit real problems:

- Adding a new service = modify C# code, rebuild the AppHost
- Switching which services to debug = comment/uncomment code
- Infrastructure containers get **destroyed** when Aspire stops = **data loss**
- Every developer needs a different subset of services running

**Aspire.Nexus** fixes all of this by moving service definitions to **JSON configuration**:

```json
{
  "order-api": {
    "Type": "DotNet",
    "ProjectPath": "C:\\src\\OrderApi\\OrderApi.csproj",
    "Port": 5000,
    "Active": true
  }
}
```

Toggle `"Active": true/false` to control what runs. No code changes. No rebuild.

## Features

- **Zero-code service management** — add, remove, toggle services from JSON config
- **Five service types**: Container, DotNet, Client, NodeJs (Aspire-native), Python (Aspire-native)
- **Aspire-native Node.js** — npm, yarn, or pnpm with automatic install, health checks, and dashboard integration
- **Aspire-native Python** — virtual environment support, pip install, and full dashboard lifecycle
- **Startup validation** — misconfigure a service? Get a clear error message, not a crash
- **Infrastructure persistence** — docker-compose keeps your databases alive after Aspire stops
- **Reuse existing containers** — already running MongoDB from docker-compose? Aspire.Nexus reuses it
- **Auto pre-build** — builds your .NET solution before Aspire starts
- **Auto dependency install** — npm install, pip install, or custom commands per service
- **Rebuild on restart** — restart a service from the Aspire dashboard, it rebuilds automatically
- **HTTPS support** — PEM or PFX certificates per service, or HTTPS flag for client dev servers
- **User secrets** — passwords, paths, and certs stay out of source control
- **Cross-platform** — resolves `npm.cmd`/`npx.cmd` on Windows automatically via PATH lookup

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Node.js (for frontend clients)

---

## Quick Start

### 1. Clone and configure

```bash
git clone https://github.com/nkmnhan/Aspire.Nexus.git
cd Aspire.Nexus
```

### 2. Set up your services

Edit `setup-secrets.ps1` to point to your projects, then run:

```
setup-secrets.cmd
```

This writes your service definitions to [.NET user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — nothing is stored in source control.

### 3. Start everything

```
START-ASPIRE.cmd
```

This will:
1. **Validate** your configuration (catches errors before anything starts)
2. **Start infrastructure** via docker-compose (databases, cache, messaging)
3. **Build** your .NET projects
4. **Install** frontend dependencies
5. **Launch** the Aspire dashboard with all services

### 4. Open the dashboard

Navigate to **http://localhost:15178** to see all your services, logs, and traces.

---

## How It Works

```
                    setup-secrets.cmd
                          |
                    User Secrets (JSON)
                          |
                   +------+------+
                   | Aspire.Nexus |
                   +------+------+
                          |
     Phase 0: Validate config (fail fast with clear errors)
                          |
          +---------------+---------------+
          |               |               |
   Phase 1: Infra    Phase 2: Build   Phase 3: Install
   docker-compose    dotnet build     npm install
   up -d             (solutions)      (frontends)
          |               |               |
          +---------------+---------------+
                          |
                  Phase 4: Aspire Host
                  Dashboard + Service Orchestration
                  http://localhost:15178
```

### Service Types

| Type | What it runs | Example | Managed By |
|------|-------------|---------|------------|
| `Container` | Infrastructure services | PostgreSQL, MongoDB, Redis, RabbitMQ, Elasticsearch | docker-compose |
| `DotNet` | .NET backend APIs and workers | Your .csproj microservices | Aspire |
| `NodeJs` | Node.js apps via npm/yarn/pnpm | React, Next.js, Vue, Angular (Aspire-native lifecycle) | Aspire |
| `Python` | Python apps and APIs | FastAPI, Flask, Django (Aspire-native lifecycle) | Aspire |
| `Client` | Any other dev server | PHP, Go, Ruby, or custom commands (generic `AddExecutable`) | Aspire |

> **When to use `NodeJs` vs `Client`:** Use `NodeJs` for any npm/yarn/pnpm project — Aspire manages the full lifecycle (install, start, health checks, dashboard). Use `Client` only for non-JavaScript projects (PHP, Go, Ruby) or when you need a fully custom launch command.

### Why docker-compose for infrastructure?

Aspire normally creates its own containers — but they get **destroyed when Aspire stops**. Your database data is gone.

Aspire.Nexus delegates infrastructure to docker-compose instead:
- Containers **persist** after Aspire shutdown
- Containers appear **grouped** in Docker Desktop (easy to manage)
- If containers are **already running**, they're reused — no duplicates
- Volumes keep your data safe across restarts

---

## Configuration Guide

### The setup-secrets.ps1 file

This is where you define your environment. It's a PowerShell script that writes to .NET user secrets:

```powershell
$secrets = [ordered]@{
    # Infrastructure — point to your docker-compose file
    "AppHost:Infrastructure:DockerComposePath"    = "C:\YourProject\infrastructure\docker-compose.yml"
    "AppHost:Infrastructure:DockerComposeProject" = "nexus-infrastructure"
    "AppHost:Infrastructure:Network"              = "nexus-network"

    # A .NET API service
    "AppHost:Services:order-api:Type"        = "DotNet"
    "AppHost:Services:order-api:ProjectPath" = "C:\YourProject\src\OrderApi\OrderApi.csproj"
    "AppHost:Services:order-api:Port"        = "5000"
    "AppHost:Services:order-api:Active"      = "true"

    # An Angular frontend
    "AppHost:Services:shop-web:Type"             = "Client"
    "AppHost:Services:shop-web:WorkingDirectory" = "C:\YourProject\src\shop-web"
    "AppHost:Services:shop-web:Port"             = "4200"
    "AppHost:Services:shop-web:DevCommand"       = "npm run start"
    "AppHost:Services:shop-web:InstallCommand"   = "npm install --force"
    "AppHost:Services:shop-web:Active"           = "true"

    # A Next.js frontend (defaults: npm install + npm run dev)
    "AppHost:Services:store-portal:Type"             = "Client"
    "AppHost:Services:store-portal:WorkingDirectory" = "C:\YourProject\src\store-portal"
    "AppHost:Services:store-portal:Port"             = "3000"
    "AppHost:Services:store-portal:Active"           = "true"
}
```

### Full Configuration Schema

```json
{
  "AppHost": {
    "Environment": {
      "LogLevel": "Debug",
      "AspNetCoreEnvironment": "Development",
      "ExtraVariables": {}
    },
    "Infrastructure": {
      "DockerComposePath": "path/to/docker-compose.yml",
      "DockerComposeProject": "nexus-infrastructure",
      "Network": "nexus-network",
      "DockerComposeServices": ["mongo", "redis-cache", "postgres-sql"]
    },
    "Services": {
      "order-api": {
        "Type": "DotNet",
        "Group": "Backend/APIs",
        "ProjectPath": "C:\\YourProject\\src\\OrderApi\\OrderApi.csproj",
        "SolutionPath": "C:\\YourProject\\YourSolution.sln",
        "Port": 5000,
        "Active": true,
        "Certificate": {
          "Path": "certs/localhost.pem",
          "KeyPath": "certs/localhost-key.pem"
        },
        "EnvironmentVariables": {
          "ConnectionStrings__OrderDb": "Host=localhost;Database=orders;Username=dev;Password=secret",
          "IdentityUrl": "https://localhost:5001"
        }
      },
      "shop-web": {
        "Type": "Client",
        "Group": "Frontend/Web",
        "WorkingDirectory": "C:\\YourProject\\src\\shop-web",
        "Port": 4200,
        "Active": false,
        "InstallCommand": "npm install --force --legacy-peer-deps",
        "DevCommand": "npm run start"
      },
      "store-portal": {
        "Type": "NodeJs",
        "Group": "Frontend/Web",
        "Https": true,
        "WorkingDirectory": "C:\\YourProject\\src\\store-portal",
        "ScriptName": "dev",
        "Port": 3000,
        "Active": true
      },
      "ml-service": {
        "Type": "Python",
        "Group": "Backend/AI",
        "WorkingDirectory": "C:\\YourProject\\src\\ml-service",
        "ScriptPath": "app.py",
        "VirtualEnvironmentPath": ".venv",
        "Port": 8000,
        "Active": true
      }
    }
  }
}
```

---

### Service Properties Reference

#### Common (all service types)

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Type` | `DotNet` \| `NodeJs` \| `Python` \| `Client` \| `Container` | Yes | — | What kind of service this is |
| `Active` | bool | No | `false` | Set `true` to include in the run |
| `Port` | int | No | — | Host port (required for Client, optional for DotNet/Container) |
| `Group` | string | No | — | Display group in console output (e.g. "Backend", "Frontend") |
| `Https` | bool | No | `false` | Mark as HTTPS without a certificate (for dev servers that handle TLS themselves) |
| `EnvironmentVariables` | object | No | `{}` | Key-value env vars injected into the service |

#### DotNet

For .NET APIs and background workers.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ProjectPath` | string | **Yes** | Path to `.csproj` file |
| `SolutionPath` | string | No | Path to `.sln` — builds the solution instead of individual project |
| `Certificate` | object | No | HTTPS certificate — see [Certificate Config](#certificate-config) |

> **Background workers**: If your worker uses `WebApplication.CreateBuilder` but has no endpoint, omit `Port`. Aspire.Nexus assigns an ephemeral port automatically so multiple workers don't fight over the default port 5000.

#### Client

For frontend dev servers (React, Angular, Vue, Next.js, etc.).

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `WorkingDirectory` | string | **Yes** | — | Path to frontend project root |
| `Port` | int | **Yes** | — | Port the dev server listens on |
| `InstallCommand` | string | No | `npm install` | Custom install command |
| `DevCommand` | string | No | `npm run dev` | Custom dev server command |
| `Https` | bool | No | `false` | Set `true` if your dev server runs HTTPS (e.g. Vite with TLS) |

> **Tip:** If your framework uses the defaults (`npm install` + `npm run dev`), omit `InstallCommand` and `DevCommand` entirely.

**Framework cheat sheet (Client type):**

| Framework | DevCommand | Port | InstallCommand |
|-----------|-----------|------|----------------|
| **PHP** (Laravel) | `php artisan serve` | 8000 | `composer install` |
| **Go** (Air) | `air` | 8080 | — |
| **Ruby** (Rails) | `rails server` | 3000 | `bundle install` |

> **JavaScript frameworks?** Use the `NodeJs` type instead — see [NodeJs (Aspire-native)](#nodejs-aspire-native) above.

#### NodeJs (Aspire-native)

For Node.js applications managed via npm, yarn, or pnpm. Uses Aspire's native `AddJavaScriptApp` — Aspire handles the full lifecycle including dependency install, startup, health checks, and dashboard integration.

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `WorkingDirectory` | string | **Yes** | — | Path to npm project root (must contain `package.json`) |
| `Port` | int | **Yes** | — | Port the app listens on |
| `ScriptName` | string | No | `start` | npm script to run (e.g. "dev", "start", "serve") |
| `PackageManager` | string | No | `npm` | Package manager: `npm`, `yarn`, or `pnpm` |

> **Install is automatic.** Aspire runs the package manager's install command before starting. You don't need `InstallCommand` for NodeJs services.

**Examples:**

```json
// Next.js with npm (simplest — just 4 properties)
{
  "Type": "NodeJs",
  "WorkingDirectory": "C:\\src\\my-next-app",
  "Port": 3000,
  "Active": true
}

// Vite React with yarn and custom script
{
  "Type": "NodeJs",
  "WorkingDirectory": "C:\\src\\my-react-app",
  "ScriptName": "dev",
  "PackageManager": "yarn",
  "Port": 5173,
  "Active": true
}

// Angular with pnpm
{
  "Type": "NodeJs",
  "WorkingDirectory": "C:\\src\\my-angular-app",
  "ScriptName": "start",
  "PackageManager": "pnpm",
  "Port": 4200,
  "Active": true
}
```

#### Python (Aspire-native)

For Python applications. Uses Aspire's native `AddPythonApp` — Aspire manages the Python process lifecycle, virtual environment activation, and dashboard integration.

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `WorkingDirectory` | string | **Yes** | — | Path to Python project root |
| `Port` | int | **Yes** | — | Port the app listens on |
| `ScriptPath` | string | **Yes** | — | Entry-point script relative to WorkingDirectory (e.g. `app.py`) |
| `VirtualEnvironmentPath` | string | No | `.venv` | Path to virtual environment directory relative to WorkingDirectory |
| `InstallCommand` | string | No | `pip install -r requirements.txt` | Dependency install command |

> **Virtual environment:** Aspire defaults to `.venv` in the project directory. Set `VirtualEnvironmentPath` only if your venv is in a different location. Run `python -m venv .venv` in your project to create one.

**Examples:**

```json
// FastAPI app with default .venv
{
  "Type": "Python",
  "WorkingDirectory": "C:\\src\\my-fastapi-app",
  "ScriptPath": "main.py",
  "Port": 8000,
  "Active": true
}

// Flask app with custom venv location
{
  "Type": "Python",
  "WorkingDirectory": "C:\\src\\my-flask-app",
  "ScriptPath": "app.py",
  "VirtualEnvironmentPath": "venv",
  "Port": 5000,
  "Active": true,
  "EnvironmentVariables": {
    "FLASK_ENV": "development",
    "FLASK_DEBUG": "1"
  }
}

// Django app
{
  "Type": "Python",
  "WorkingDirectory": "C:\\src\\my-django-app",
  "ScriptPath": "manage.py",
  "Port": 8000,
  "Active": true,
  "EnvironmentVariables": {
    "DJANGO_SETTINGS_MODULE": "myapp.settings.dev"
  }
}
```

#### Client (generic fallback)

For dev servers that aren't Node.js or Python — PHP, Go, Ruby, or anything with a custom launch command. Uses `AddExecutable` under the hood.

> **Prefer `NodeJs` type** for JavaScript/TypeScript projects. It provides Aspire-native lifecycle management that `Client` doesn't.

#### Container (fallback when no docker-compose)

For infrastructure services managed directly by Aspire (only used when docker-compose is not configured).

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Image` | string | **Yes** | — | Docker image name |
| `Tag` | string | No | `latest` | Image tag |
| `TargetPort` | int | No | same as `Port` | Container-side port |
| `AdditionalPorts` | list | No | `[]` | Extra port mappings `[{ Port, TargetPort }]` |
| `Volumes` | object | No | `{}` | Volume mounts `{ "volume-name": "/container/path" }` |
| `Args` | list | No | `[]` | Container command arguments |

#### Certificate Config

Two formats supported:

**PEM (recommended):**
```json
{
  "Certificate": {
    "Path": "certs/localhost.pem",
    "KeyPath": "certs/localhost-key.pem"
  }
}
```

**PFX:**
```json
{
  "Certificate": {
    "Path": "certs/aspnetapp.pfx",
    "Password": "certpass"
  }
}
```

---

### Environment Variables

Aspire.Nexus injects environment variables at three levels:

| Level | Applies to | Set in | Example |
|-------|-----------|--------|---------|
| **ASP.NET defaults** | DotNet services only | `AppHost.Environment` | `ASPNETCORE_ENVIRONMENT`, `Logging__LogLevel__Default` |
| **Global extras** | All service types | `AppHost.Environment.ExtraVariables` | Shared API keys, feature flags |
| **Per-service** | That service only | `Services.<name>.EnvironmentVariables` | Connection strings, service URLs |

Per-service variables override global variables when they share the same key.

**Common pattern — connection strings:**
```json
{
  "EnvironmentVariables": {
    "ConnectionStrings__MyDb": "Host=localhost;Port=5432;Database=mydb;Username=dev;Password=secret",
    "ConnectionStrings__rabbitmq": "amqp://guest:guest@localhost:5672/"
  }
}
```

---

## Startup Validation

Aspire.Nexus validates your configuration before starting anything. If something is wrong, you get a clear message:

```
Configuration errors found:
  - "order-api" (DotNet): "ProjectPath" is required.
  - "shop-web" (Client): "WorkingDirectory" is required.
  - "shop-web" (Client): "Port" is required for frontend dev servers.
  - "cache" (Container): "Image" is required.
Fix these in your user secrets (setup-secrets.cmd) and try again.
```

No more cryptic `NullReferenceException` stack traces.

---

## Project Structure

```
Aspire.Nexus/
├── Program.cs                          → Entry point: validate → pre-run → register → start
├── ServiceOrchestrator.cs              → Lifecycle orchestration: validate, pre-run, register, rebuild
├── GlobalUsings.cs                     → Global using for sub-namespaces
├── Configuration/                      → Pure data models (JSON DTOs)
│   ├── AppHostConfig.cs                → Top-level config + ServiceType enum
│   ├── ServiceDef.cs                   → Service definition with per-type properties
│   ├── InfrastructureConfig.cs         → Docker Compose settings
│   ├── ServiceEnvironmentConfig.cs     → Global environment variables
│   ├── CertificateConfig.cs            → PEM/PFX certificate settings
│   └── PortMapping.cs                  → Container port mappings
├── Handlers/                           → Strategy pattern — one file per framework
│   ├── IServiceHandler.cs              → Interface + RegistrationContext record
│   ├── ServiceHandlerBase.cs           → Abstract base with shared validation/install helpers
│   ├── DotNetHandler.cs                → dotnet build, AddProject, certificates
│   ├── NodeJsHandler.cs                → AddJavaScriptApp, WithNpm/Yarn/Pnpm
│   ├── PythonHandler.cs                → AddPythonApp, venv pip, WithVirtualEnvironment
│   ├── ClientHandler.cs                → AddExecutable, generic commands
│   └── ContainerHandler.cs             → AddContainer, ports, volumes, args
├── Infrastructure/                     → Cross-cutting utilities
│   ├── ProcessRunner.cs                → Cross-platform process execution + PATH resolution
│   ├── BuildLogger.cs                  → Colored console output
│   └── ResourceBuilderExtensions.cs    → Fluent extensions for endpoints, certs, env vars
├── START-ASPIRE.cmd                    → Launch script (double-click to start)
├── setup-secrets.cmd/.ps1              → User secrets setup (customize for your project)
├── appsettings.json                    → Config schema defaults (empty — real config in user secrets)
└── Properties/
    └── launchSettings.json             → Dashboard ports (15178 / 17178)
```

### Architecture

Each service type is self-contained in a single handler class implementing `IServiceHandler`:

| Handler | Validate | Register | PreRun | Rebuild on Restart |
|---------|----------|----------|--------|--------------------|
| `DotNetHandler` | ProjectPath, cert | `AddProject` | `dotnet build` (solution-dedup) | `dotnet build` (single) |
| `NodeJsHandler` | WorkingDirectory, Port | `AddJavaScriptApp` | — (Aspire handles) | — (Aspire handles) |
| `PythonHandler` | WorkingDirectory, ScriptPath, Port | `AddPythonApp` | `pip install` (venv-aware) | Re-run install |
| `ClientHandler` | WorkingDirectory, Port | `AddExecutable` | Install command | Re-run install |
| `ContainerHandler` | Image | `AddContainer` | — | — |

Adding a new framework (e.g. Go, Ruby) = create one file in `Handlers/` and register it in `ServiceOrchestrator.Handlers`.

## Aspire Dashboard

| Profile | URL |
|---------|-----|
| http | http://localhost:15178 |
| https | https://localhost:17178 |

---

## Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| **Configuration error on startup** | Missing required property | Read the error message — it tells you exactly which service and property |
| **Aspire exits immediately** | Build failure or config error | Run `START-ASPIRE.cmd` — it pauses on exit so you can read the error |
| **`npm` not found on Windows** | npm not in PATH | Install Node.js and restart your terminal. Aspire.Nexus resolves `npm.cmd` via PATH automatically |
| **Python app can't find modules** | venv not activated | Set `VirtualEnvironmentPath` (e.g. `.venv`). Run `python -m venv .venv && pip install -r requirements.txt` first |
| **NodeJs install fails** | Wrong package manager | Set `PackageManager` to `yarn` or `pnpm` if your project uses a lock file other than `package-lock.json` |
| **Infrastructure not starting** | Docker not running | Ensure Docker Desktop is running and `DockerComposePath` in secrets is correct |
| **Port already in use** | Previous instance still running | Check Docker Desktop or Task Manager for existing instances |
| **Data lost on shutdown** | Using Aspire-managed containers | Switch to docker-compose — containers persist. See [Why docker-compose?](#why-docker-compose-for-infrastructure) |
| **Two workers crash on port 5000** | Both bind Kestrel's default | Omit `Port` for workers — Aspire.Nexus assigns ephemeral ports automatically |
| **HTTPS not working for frontend** | Aspire shows HTTP URL | Set `"Https": true` on the Client service (dev server must handle TLS itself) |

---

## Comparison

| | Standard Aspire AppHost | Aspire.Nexus |
|---|---|---|
| Add a service | Modify C#, rebuild | Add JSON entry |
| Toggle services | Comment/uncomment code | Set `Active: true/false` |
| Misconfiguration | NullReferenceException at runtime | Clear validation error at startup |
| Infrastructure persistence | Containers destroyed on stop | docker-compose keeps them alive |
| Connection strings | Hardcoded in C# or user secrets per project | Per-service `EnvironmentVariables` in one place |
| Sensitive config | In source code | In user secrets |
| Frontend support | Manual `AddExecutable` setup | Aspire-native Node.js/Python + generic Client fallback |
| Multiple developers | Everyone uses same services | Each dev configures their own secrets |
| HTTPS | Manual Kestrel config per project | PEM/PFX certificate config or `Https` flag |

---

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE).
