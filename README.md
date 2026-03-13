# Aspire.Nexus

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Aspire](https://img.shields.io/badge/Aspire-9.2-blueviolet)](https://learn.microsoft.com/en-us/dotnet/aspire/)

A **configuration-driven** .NET Aspire AppHost that orchestrates your entire dev environment — infrastructure, APIs, and frontend clients — from JSON config. No C# changes needed to add, remove, or toggle services.

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
- **Three service types**: Container (databases, cache), DotNet (APIs), Client (Angular, React, Next.js, Vue)
- **Infrastructure persistence** — docker-compose keeps your databases alive after Aspire stops
- **Reuse existing containers** — already running MongoDB from docker-compose? Aspire.Nexus reuses it
- **Auto pre-build** — builds your .NET solution before Aspire starts
- **Auto npm install** — custom install commands per frontend (e.g. `npm install --force --legacy-peer-deps`)
- **Rebuild on restart** — restart a service from the Aspire dashboard, it rebuilds automatically
- **HTTPS support** — per-service certificate configuration
- **User secrets** — passwords, paths, and certs stay out of source control
- **Windows-ready** — handles `npm.cmd` resolution, IPv4 binding, and more

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Node.js (for frontend clients)

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
1. Start your infrastructure via docker-compose (databases, cache, messaging)
2. Build your .NET projects
3. Install frontend dependencies
4. Launch the Aspire dashboard

### 4. Open the dashboard

Navigate to **http://localhost:15178** to see all your services, logs, and traces.

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
| `DotNet` | .NET backend APIs | Your .csproj microservices | Aspire |
| `Client` | Frontend dev servers | Angular, React, Next.js, Vue apps via npm | Aspire |

### Why docker-compose for infrastructure?

Aspire normally creates its own containers — but they get **destroyed when Aspire stops**. Your database data is gone.

Aspire.Nexus delegates infrastructure to docker-compose instead:
- Containers **persist** after Aspire shutdown
- Containers appear **grouped** in Docker Desktop (easy to manage)
- If containers are **already running**, they're reused — no duplicates
- Volumes keep your data safe across restarts

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
          "Path": "certs/aspnetapp.pfx",
          "Password": "certpass"
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
        "Type": "Client",
        "Group": "Frontend/Web",
        "WorkingDirectory": "C:\\YourProject\\src\\store-portal",
        "Port": 3000,
        "Active": true
      }
    }
  }
}
```

### Service Properties Reference

#### Common (all types)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Type` | `DotNet` \| `Client` \| `Container` | Yes | Service type |
| `Port` | int | Yes | Host port |
| `Active` | bool | No | Include in the run (default: `false`) |
| `Group` | string | No | Display group for console output |
| `EnvironmentVariables` | dict | No | Key-value env vars |

#### DotNet

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ProjectPath` | string | Yes | Path to `.csproj` |
| `SolutionPath` | string | No | Path to `.sln` — builds solution instead of individual project |
| `Certificate` | object | No | `{ Path, Password }` — enables HTTPS |

#### Client

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `WorkingDirectory` | string | Yes | Path to frontend project root |
| `InstallCommand` | string | No | Custom install (default: `npm install`) |
| `DevCommand` | string | No | Custom dev server (default: `npm run dev`) |

#### Frontend Framework Examples

Next.js, React, Angular, Vue — all work out of the box. Just set the right `DevCommand` and `Port`:

| Framework | DevCommand | Default Port | InstallCommand |
|-----------|-----------|--------------|----------------|
| **Next.js** | `npm run dev` (default) | 3000 | `npm install` (default) |
| **React** (Create React App) | `npm run start` | 3000 | `npm install` |
| **React** (Vite) | `npm run dev` (default) | 5173 | `npm install` |
| **Angular** | `npm run start` | 4200 | `npm install --force --legacy-peer-deps` |
| **Vue** (Vite) | `npm run dev` (default) | 5173 | `npm install` |

> **Tip:** If your framework uses the defaults (`npm install` + `npm run dev`), you can omit `InstallCommand` and `DevCommand` entirely — Aspire.Nexus uses those defaults automatically.

#### Container (fallback when no docker-compose)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Image` | string | Yes | Docker image name |
| `Tag` | string | No | Image tag (default: `latest`) |
| `TargetPort` | int | No | Container port (default: same as `Port`) |
| `AdditionalPorts` | list | No | Extra port mappings `[{ Port, TargetPort }]` |
| `Volumes` | dict | No | Volume mounts `{ "volume-name": "/container/path" }` |
| `Args` | list | No | Container command arguments |

## Project Structure

```
Aspire.Nexus/
├── Program.cs              → entry point — runs phases, starts Aspire
├── ServiceConfig.cs        → configuration models
├── ServiceRegistrar.cs     → registers services with Aspire
├── PreRunPhases.cs         → docker-compose, dotnet build, npm install
├── ProcessRunner.cs        → cross-platform process execution
├── BuildLogger.cs          → colored console output
├── START-ASPIRE.cmd        → launch script
├── setup-secrets.cmd/.ps1  → user secrets setup (customize for your project)
├── appsettings.json        → config schema defaults
└── Properties/
    └── launchSettings.json → dashboard ports (15178 / 17178)
```

## Aspire Dashboard

| Profile | URL |
|---------|-----|
| http | http://localhost:15178 |
| https | https://localhost:17178 |

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Aspire exits immediately | Run `START-ASPIRE.cmd` — it pauses on exit so you can read the error |
| `npm` not found | Handled automatically — wraps through `cmd.exe /c` on Windows |
| Infrastructure not starting | Ensure Docker Desktop is running and `DockerComposePath` in secrets is correct |
| Port already in use | Check Docker Desktop or Task Manager for existing instances |
| Data lost on shutdown | Infrastructure uses docker-compose with persistent volumes — containers survive Aspire shutdown |

## Comparison

| | Standard Aspire AppHost | Aspire.Nexus |
|---|---|---|
| Add a service | Modify C#, rebuild | Add JSON entry |
| Toggle services | Comment/uncomment code | Set `Active: true/false` |
| Infrastructure persistence | Containers destroyed on stop | docker-compose keeps them alive |
| Sensitive config | In source code | In user secrets |
| Frontend support | Manual `AddExecutable` setup | Built-in with custom install/dev commands |
| Multiple developers | Everyone uses same services | Each dev configures their own secrets |

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE).
