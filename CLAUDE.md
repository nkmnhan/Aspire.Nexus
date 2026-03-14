# Aspire.Nexus — Configuration-Driven .NET Aspire Orchestrator

## What This Is

A **public, reusable** .NET Aspire AppHost that orchestrates dev environments from JSON config (user secrets) instead of hardcoded C#. MIT licensed, project-agnostic.

**Problem:** Standard Aspire requires hardcoding every service in Program.cs. Adding/removing/toggling services requires code changes.

**Solution:** Define services in JSON → toggle with `"Active": true/false` → zero code changes.

---

## Architecture

### Three-Phase Lifecycle

```
Program.cs → Validate → PreRunAsync → RegisterAll → app.Run()
```

| Phase | What happens |
|-------|-------------|
| **Validate** | Fail fast — check all service configs before anything starts |
| **PreRunAsync** | Start docker-compose infra + pre-build DotNet/Client/Python services |
| **RegisterAll** | Register active services with Aspire builder via handlers |

### Strategy Pattern — 5 Service Handlers

| Handler | Type | PreRun | Rebuild on Restart | Registration |
|---------|------|--------|-------------------|--------------|
| `DotNetHandler` | `DotNet` | Build solution/project | Yes | `AddProject` + endpoints + certs |
| `NodeJsHandler` | `NodeJs` | No | No | `AddJavaScriptApp` (npm/yarn/pnpm) |
| `PythonHandler` | `Python` | venv + pip install | Yes | `AddPythonApp` |
| `ClientHandler` | `Client` | npm/composer install | Yes | `AddExecutable` |
| `ContainerHandler` | `Container` | No | No | `AddContainer` + ports + volumes |

### Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point — load config, run 3 phases |
| `ServiceOrchestrator.cs` | Lifecycle orchestrator (validate → pre-run → register) |
| `Configuration/AppHostConfig.cs` | Top-level config model + `ServiceType` enum |
| `Configuration/ServiceDef.cs` | Per-service definition (type, port, path, env vars, etc.) |
| `Configuration/InfrastructureConfig.cs` | Docker Compose settings |
| `Handlers/IServiceHandler.cs` | Strategy interface + `RegistrationContext` record |
| `Handlers/ServiceHandlerBase.cs` | Shared helpers (env vars, endpoints, certs) |
| `Handlers/DotNetHandler.cs` | .NET project build + registration |
| `Infrastructure/ProcessRunner.cs` | Cross-platform process execution + Windows PATH resolution |
| `Infrastructure/ResourceBuilderExtensions.cs` | Fluent Aspire extensions (endpoints, certs, env vars) |

---

## Design Principles (MANDATORY)

This is a **public tool** — maintain these qualities:

- **Project-agnostic**: Never reference MediTrack, specific service names, or domain concepts in source code. All project specifics live in user secrets JSON
- **Strategy pattern**: New service types = new handler implementing `IServiceHandler`. Never modify existing handlers to add type-specific logic
- **Fail fast**: Validate ALL configs before starting anything. Collect all errors and report together
- **Cross-platform**: Windows PATH resolution (`ProcessRunner.ResolveCommand`), forward slashes, no OS-specific assumptions in config models
- **Zero runtime dependencies**: No database, no external services. Only reads JSON config + runs processes
- **Isolated build**: `Directory.Build.props` blocks parent repo inheritance. `Directory.Packages.props` has CPM disabled — versions are in `.csproj` directly (this is intentional, unlike MediTrack's CPM)

## Adding a New Service Type

1. Add value to `ServiceType` enum in `Configuration/AppHostConfig.cs`
2. Create `Handlers/NewTypeHandler.cs` implementing `IServiceHandler`
3. Add handler to `ServiceOrchestrator.Handlers` array
4. Add type-specific properties to `ServiceDef.cs` if needed
5. Update README.md with configuration example

## Configuration

All service config lives in **user secrets** (not appsettings.json):

```bash
# User secrets location:
# Windows: %APPDATA%\Microsoft\UserSecrets\aspire-nexus\secrets.json
dotnet user-secrets set "AppHost:Services:my-service:Type" "DotNet"
dotnet user-secrets set "AppHost:Services:my-service:Active" "true"
```

## Build & Run

```bash
dotnet run --launch-profile http    # Start Aspire dashboard (port 15178)
```

## Dependencies

- `Aspire.AppHost.Sdk` 13.1.2
- `Aspire.Hosting.JavaScript` + `Aspire.Hosting.Python`
- `Microsoft.Extensions.Configuration.Binder` + `.UserSecrets`
- Target: `net10.0`
