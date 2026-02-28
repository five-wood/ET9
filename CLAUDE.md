# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ET9 is a C# game framework providing both Unity client and .NET server in a single codebase. It uses a Fiber-based multi-threading model (similar to Erlang processes), Actor messaging, strict Entity-Component separation, and HybridCLR for client hot-update. Designed for AI-assisted development with compile-time analyzers that enforce all conventions.

**Tech stack**: Unity 6000.0.25, C# 11 (.NET 8), MemoryPack serialization, KCP/TCP/WebSocket networking.

## Build & Run

```bash
# Build entire solution
dotnet build ET.sln

# Start server standalone (run from project root, NOT from Bin/)
dotnet Bin/ET.App.dll --Console=1

# Create test robots via server console
CreateRobot --Num=10
```

**In Unity**: F6 to compile, F7 to hot-reload. First-time setup: menu `ET -> StateSync -> Init` (exports Excel, Proto, adds INITED macro).

**TreatWarningsAsErrors is ON** (`Directory.Build.props`) — all warnings are compile errors.

## Architecture

### Package-Based Structure

All code lives in `Packages/cn.etetet.*` as Unity packages. Key packages:

- **core** — Entity, Fiber, Network, ETTask, ObjectPool (framework base, rarely modify)
- **loader** — Entry point, loads the four assembly layers
- **statesync** — State-sync demo with full client/server/robot example
- **login** — Login flow: Realm, Gate, Scene fibers
- **sourcegenerator** — Compile-time analyzers and code generators

### Four Assembly Layers (Critical)

Each package splits code into exactly four layers. **Data and logic are strictly separated**:

| Layer | Hot-reloadable | Purpose |
|-------|---------------|---------|
| **Model** | No | Entity/Component definitions — data fields only, NO methods |
| **ModelView** | No | Client-only view data (Unity-specific) |
| **Hotfix** | Yes | All business logic as static extension methods |
| **HotfixView** | Yes | Client-only view logic (UI, rendering) |

Within each layer, code is split into `Share/` (client+server), `Client/`, and `Server/` directories.

### Entity-Component Pattern

**Component** (in Model/) — pure data, annotated with parent type:
```csharp
[ComponentOf(typeof(Scene))]
public class MyComponent : Entity, IAwake, IDestroy
{
    public int Value;  // NO methods allowed here
}
```

**System** (in Hotfix/) — logic via extension methods, must use `[EntitySystemOf]`:
```csharp
[EntitySystemOf(typeof(MyComponent))]
[FriendOf(typeof(MyComponent))]
public static partial class MyComponentSystem
{
    [EntitySystem]
    private static void Awake(this MyComponent self) { }

    [EntitySystem]
    private static void Destroy(this MyComponent self) { }

    public static void DoSomething(this MyComponent self) { }
}
```

### Fiber Model

Fiber is the lightweight concurrency unit. Each Fiber has its own `Scene` root entity.
- Three scheduling modes: main thread, thread pool, dedicated thread
- Created via: `FiberManager.Instance.Create(SchedulerType.ThreadPool, sceneId, zone, sceneType, name)`
- Fiber initialization: `[Invoke(SceneType.Map)]` class implementing `AInvokeHandler<FiberInit, ETTask>`
- Fibers communicate via Actor messages (location-transparent)

### Entry/Startup Flow

Application startup fires sequential events per SceneType:
1. `EntryEvent1_InitShare` — shared init (timers, coroutine locks, network)
2. `EntryEvent2_InitServer` — server init (creates Fibers per config)
3. `EntryEvent3_InitClient` — client init (UI, resources, player)

### Message System

**Proto files** in each package's `Proto/` directory:
- `*Outer_C_*.proto` — client-facing, number suffix is the starting opcode
- `*Inner_S_*.proto` — server internal messages
- Comment `// IRequest`, `// IResponse`, `// ILocationRequest` on message definitions determines handler type
- `// ResponseType M2C_Foo` comment links request to response type

**Handler naming convention**: `{Source}2{Target}_{Name}Handler` — e.g., `C2M_PathfindingResultHandler`, `M2C_CreateMyUnitHandler`

### Key Conventions Enforced by Analyzers

These are **compile errors**, not suggestions:
- Entity classes: no methods, no delegate fields, no multi-level inheritance, no generic entities, no float fields on LSEntity
- Entity fields referencing other entities must use `EntityRef<T>`
- All static fields must have `[StaticField]` attribute
- Model/ModelView assemblies can only declare Entity-derived classes (use `[EnableClass]` for exceptions)
- ETTask return type required for async methods (never `async void`)
- ETTask calls in sync methods must use `.Coroutine()` suffix
- ETTask calls in async methods must use `await` or `.Coroutine()`
- Components must declare parent via `[ComponentOf]`; children via `[ChildOf]`
- Server assemblies cannot reference `ET.Client` namespace
- Functions with Entity parameters must use Fiber-based logging (not `Log.Debug`)

### Namespace Convention

- `namespace ET` — shared code
- `namespace ET.Client` — client-only code
- `namespace ET.Server` — server-only code

### Robot Testing

Robots share client logic code. Create via server console: `CreateRobot --Num=10`. Each robot is a separate Fiber with its own sandbox environment. Robot cases are defined in `Model/Server/Robot/Case/`.

### Adding a New Feature (Typical Steps)

1. Define proto messages in `Proto/` (if networking needed)
2. Run proto export: Unity menu `ET -> Proto`
3. Add Component in `Model/{Share|Client|Server}/` (data only)
4. Add System in `Hotfix/{Share|Client|Server}/` (logic, `[EntitySystemOf]`)
5. Add Handler in `Hotfix/` for message handling
6. Add View in `ModelView/` + `HotfixView/` (if client UI needed)
7. Export Excel if config needed: Unity menu `ET -> Excel`
