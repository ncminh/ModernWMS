# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Scope

This directory is the **backend** of ModernWMS (open-source warehouse management system). The Vue 3 / Vuetify frontend lives in `../frontend` and talks to this API over HTTP (see `../frontend/.env.development`: dev backend port `16453`, prod `20011`). The repo root `README.md` / `README.zh_CN.md` document end-to-end deployment (Nginx + Docker) and `docker/Dockerfile` builds the image; all three now correctly reference **net8.0** (matching the three `.csproj` files, commit `be50e2b Upgraded to NET8`) — keep them in sync if the target framework changes again.

## Commands

```powershell
dotnet build ModernWMS.sln                 # build all three projects
dotnet run --project ModernWMS             # http://localhost:5056, opens /swagger
dotnet publish ModernWMS                   # output under ModernWMS/bin/Debug/net8.0/publish
dotnet test ModernWMS.sln                  # runs _tests/MWMS.UnitTests (xUnit + Shouldly)
```

`_tests/MWMS.UnitTests` (project file `ModernWMS.UnitTests.csproj`, root namespace `ModernWMS.UnitTests`) is wired into `ModernWMS.sln` and references `ModernWMS.Core`/`ModernWMS.WMS`. Services take a concrete `SqlDBContext` (no repository interface), so tests exercise a real EF Core/SQLite provider rather than mocking `DbContext`: `TestSupport/SqliteTestDbContextScope` opens an in-memory SQLite connection and builds a real `SqlDBContext` against it (`Database.EnsureCreated()`), and `TestSupport/FakeStringLocalizer<T>` stands in for `IStringLocalizer<Core.MultiLanguage>`. Assertions use **Shouldly** (`actual.ShouldBe(expected)`, `flag.ShouldBeTrue()`, …), not xUnit's `Assert.*` — `<Using Include="Shouldly" />` is already global in the test csproj. See `_docs/unit-testing-plan.md` for the phased coverage rollout — no linter config exists, and this baseline predates most module coverage, so don't assume more is tested than the plan's phase status says.

Swagger UI is the primary manual-testing surface: `/swagger`, split into two docs (`Base`, `WMS`) driven by the `CustomApiVersion.ApiVersions` enum. Hangfire's dashboard is mounted at `/hangfire`.

Local toolchain here: .NET SDK 9.0.314 with `dotnet-ef` 10.x installed globally, building net8.0 targets.

### Migrations

Migrations live in the **startup project** `ModernWMS/Migrations` (`MigrationsAssembly("ModernWMS")` is set only on the SQLite branch in `StartupExtensions`). The existing `Initial` migration was scaffolded against **SQLite**, so its column types (`TEXT`/`INTEGER`) are provider-specific — switching providers means regenerating it, not reusing it.

```powershell
dotnet ef migrations add <Name> --project ModernWMS --startup-project ModernWMS
dotnet ef database update --project ModernWMS --startup-project ModernWMS
```

`SQL/Tables.sql` and `SQL/Columns.sql` are hand-maintained patch scripts used instead of migrations for several fixes (see recent commits about `action_log`, `global_unique_serial`, `asnmaster`, `rolemenu.menu_actions_authority`). They contain a **mix of PostgreSQL and SQLite dialects** — check which one a statement targets before running it.

## Database selection

The provider is chosen at startup from `appsettings*.json` → `Database:db`, one of `SQLITE` | `MYSQL` | `SQLSERVER` | `POSTGRES`, each with its own entry in `ConnectionStrings`. `appsettings.json` currently says `MySql`; `appsettings.Development.json` says `SQLITE` and points at the checked-in `ModernWMS/wms.db`. Postgres additionally flips the two legacy Npgsql `AppContext` switches.

## Architecture

Three projects, strictly layered:

- **`ModernWMS`** — thin ASP.NET Core host. `Program.cs` (NLog) → `Startup.cs`, which delegates everything to `AddExtensionsService` / `UseExtensionsConfigure`. Owns `appsettings`, `nlog.config`, migrations, and `wms.db`.
- **`ModernWMS.Core`** — infrastructure only, no warehouse domain: `SqlDBContext`, JWT, dynamic search, middleware, Hangfire jobs, localization, Swagger, and `AccountController` (login / refresh-token — the only controller outside the WMS project).
- **`ModernWMS.WMS`** — the domain. `Controllers/`, `IServices/`, `Services/`, `Entities/Models/` (EF entities), `Entities/ViewModels/` (API contracts), all sharded into per-module folders (Asn, Stock, Dispatchlist, Sku, Stockmove, Stocktaking, …).

`ModernWMS.Core/Extentions/StartupExtensions.cs` is the single wiring file — read it first when anything about DI, auth, Swagger, CORS, or jobs is in question.

### Convention-driven registration (the non-obvious part)

Three separate reflection passes scan `ModernWMS*.dll` in the output directory; nothing is registered by hand:

1. **Services** — any type implementing `ModernWMS.Core.DI.IDependency` is matched to its interface and registered `AddScoped`. `IBaseService<TEntity> : IDependency`, so every `IXxxService : IBaseService<XxxEntity>` is auto-wired just by existing. `BaseService<TEntity>` is deliberately empty — it is a marker, not shared implementation.
2. **Entities** — `SqlDBContext.MappingEntityTypes` adds every subclass of `Models.BaseModel` to the model. There are **no `DbSet<T>` properties**; code calls `_dBContext.GetDbSet<XxxEntity>()`, which throws if the type was never discovered.
3. **Jobs** — every `Core.Job.IJob` implementation is registered as a Hangfire `RecurringJob` on its own `CronExpression`, queue `"wms"` (see `Job/TestJob.cs`).

Consequence: a new module needs no registration code, but the entity **must** derive from `BaseModel` and the service interface **must** derive from `IBaseService<>`, or it silently disappears.

### Request shape

Every endpoint returns `ResultModel<T>` (`IsSuccess` / `Code` / `ErrorMessage` / `Data`); note `ResultModel<T>.Success(null)` degrades into an error result. Controllers derive from `Core.Controller.BaseController`, which exposes `CurrentUser` decoded from the JWT's JSON claim. Controllers stay thin: call the service, map the returned `(value, msg)` or `(flag, msg)` tuple onto `Success`/`Error`. Services return tuples rather than throwing for business failures.

Paged list endpoints are `POST {module}/list` taking `PageSearch` (`pageIndex`, `pageSize`, `sqlTitle`, `searchObjects`). `searchObjects` is compiled into an EF `Expression` by `Core/DynamicSearch/QueryCollection.AsExpression<T>()` via reflection over property names — so a search field `Name` is a **property name on the entity**, and an unknown name is silently skipped. `sqlTitle` is a second, ad-hoc channel: e.g. `AsnService` string-parses it as `asn_status=<n>` to filter by status tab.

Entity↔ViewModel conversion is **Mapster** (`.Adapt<T>()`) with no explicit config — the two sides are matched by identical property names, which is why entities and view models repeat the same `snake_case` names.

### Cross-cutting rules the domain code follows

- **Multi-tenancy is manual.** Almost every entity has `tenant_id`, and every query filters `t.tenant_id.Equals(currentUser.tenant_id)` by hand. The global query filter in `OnModelCreating` is commented out and `TenantProvider` always returns the default tenant — omitting the filter in a new query is a data leak, not a style slip.
- **Audit columns are set in the service**, not by EF: `creator`, `create_time`, `last_update_time`, plus `entity.id = 0` before insert.
- **Document numbers** come from `Core.FunctionHelper.GetFormNoAsync` / `GetFormNoListAsync`, backed by the `global_unique_serial` table with a prefix and a day/month/year reset rule — never hand-build an `asn_no` / `dispatch_no`.
- **`snake_case` for all entity and view-model properties and table names** (matching the DB), PascalCase for types and methods. Both the Mapster mapping and the DynamicSearch reflection depend on this.
- **Module type names are mid-migration from concatenated-lowercase to split PascalCase** (e.g. `Stockadjust` → `StockAdjust`, `Warehousearea` → `WarehouseArea`, `Goodsowner` → `GoodsOwner`), so don't assume every type follows one convention — check the module you're touching. The rename is layered: `IXxxService` interfaces and `XxxController` classes have been split for nearly every module; concrete `XxxService` classes and the `Entities/Models`/`Entities/ViewModels` types have only been split for the `Stock*` family so far (`Asnmaster` and `Dispatchlist` — the two largest/most complex modules below — haven't been touched at all). Routes (`[Route("stockadjust")]`), XML-doc comment text, and DB table names are deliberately left in the old lowercase form; only C# type identifiers get split. Because every entity pins its table via an explicit `[Table("...")]` attribute, renaming an entity class is schema-safe — confirm with `dotnet ef migrations has-pending-model-changes --project ModernWMS --startup-project ModernWMS` after any entity rename, and keep the string-literal type names inside `ModernWMS/Migrations/*.Designer.cs` and `SqlDBContextModelSnapshot.cs` in sync (they aren't auto-updated by a C# rename). Also: on this case-insensitive Windows filesystem, a plain OS-level folder/file case rename does **not** register with git as a rename — use `git mv X X_tmp && git mv X_tmp Y` (two steps) or `git status` will keep showing the old path as merely modified.
- **All user-facing strings are localized** through `IStringLocalizer<ModernWMS.Core.MultiLanguage>` against `Core/Models/MultiLanguage.{en-us,zh-cn}.resx` (~180 keys, e.g. `exists_entity`, `save_success`, `not_exists_entity`). Add new keys to **both** resx files. Supported cultures are `zh-cn` (default) and `en-us`, selected per request (`?culture=en-us`).
- **Workflow status is an untyped `byte`** (`asn_status`, `dispatch_status`, …) with literal values assigned inline in the services; there are no enums. Read the owning service to learn what a value means before changing it.

### Auth

JWT bearer, configured from `TokenSettings` (Audience / Issuer / SigningKey / ExpireMinute), `ClockSkew = Zero`. `/login` and `/refresh-token` are `[AllowAnonymous]`; refresh tokens are held in the in-memory `CacheManager`. Two things to know before touching auth:

- `[Authorize]` is **commented out** on `BaseController`, so WMS endpoints are effectively unauthenticated at the framework level. That is existing behavior, not something to rely on.
- `Core/Utility/GlobalConsts.SigningKey` duplicates the appsettings signing key, and `FunctionHelper.GetCurrentUser` validates against that constant rather than configuration — the two must stay in sync or token parsing there breaks.

CORS is handled by a hand-rolled `Core/Middleware/CorsMiddleware` that reflects the request `Origin`, not by `AddCors`.

### Where the complexity actually is

`Services/Dispatchlist/DispatchlistService.cs` (~1800 lines) and `Services/Asn/AsnService.cs` (~1400) hold the outbound and inbound flows; `StockService`, `SpuService`, and `UserService` follow. Stock movement is spread across `Stock`, `StockMove`, `StockAdjust`, `StockFreeze`, `StockProcess`, and `StockTaking` — a change to quantities usually has to touch several of these consistently. `Services/Approve/FlowSetService.cs` has entities and a service but no controller or interface: an unfinished approval-flow module.
