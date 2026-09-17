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

**Every provider that has migrations gets its own dedicated migrations project — never add a second provider's migrations into an existing folder/assembly.** EF Core's migration discovery scans one whole assembly for `Migration`-derived classes and treats all of them as one linear history; mixing e.g. a SQLite-typed (`TEXT`/`INTEGER`) `Initial` and a SQL-Server-typed (`nvarchar`/`decimal`) `Initial` in the same assembly makes `migrations add` diff the new model against the *other* provider's snapshot and scaffold a bogus ALTER-from-SQLite-to-SqlServer migration instead of a clean create. Three providers follow this pattern today, all nested under `ModernWMS.Migrations/` (a physical folder, and a matching solution folder in `ModernWMS.sln`) — `ModernWMS.Migrations/ModernWMS.Migrations.Sqlite`, `.../ModernWMS.Migrations.SqlServer`, `.../ModernWMS.Migrations.Postgres`, each a plain class library referencing `ModernWMS.Core`/`ModernWMS.WMS` (via `..\..\`, one level deeper than the other projects) and holding nothing but its own `Migrations/` folder — wired up identically:

- `StartupExtensions`' matching `UseXxx(...)` call sets `MigrationsAssembly("ModernWMS.Migrations.<Provider>")`.
- `ModernWMS.csproj` carries an otherwise-unused `ProjectReference` to each one, purely so the DLL ships alongside `ModernWMS.dll` — without that reference, `MigrationsAssembly`'s `Assembly.Load` at runtime (and `dotnet ef` at design time) can't find it, which fails immediately with "doesn't match your migrations assembly".
- All three are checked into git (unlike the old SQLite-migrations-live-in-the-startup-project setup this replaced, generating any of them needs the assembly-per-provider setup above and is easy to get subtly wrong otherwise).

**MySQL is the one gap**: `UseMySql(...)` has no `MigrationsAssembly` override and no dedicated project, so `dotnet ef migrations add` against it fails the same way SQL Server's used to. Follow the exact same pattern (new `ModernWMS.Migrations/ModernWMS.Migrations.MySql` project alongside the other three, `MigrationsAssembly("ModernWMS.Migrations.MySql")`, `ProjectReference` from `ModernWMS.csproj`) if that's ever needed — nobody has, yet.

```powershell
# --startup-project is always ModernWMS (it owns DI/appsettings); --project picks the provider.
# Database:db must match the provider you're targeting, e.g.: $env:Database__db="SQLSERVER"
dotnet ef migrations add <Name> --project ModernWMS.Migrations/ModernWMS.Migrations.Sqlite    --startup-project ModernWMS --output-dir Migrations
dotnet ef migrations add <Name> --project ModernWMS.Migrations/ModernWMS.Migrations.SqlServer --startup-project ModernWMS --output-dir Migrations
dotnet ef migrations add <Name> --project ModernWMS.Migrations/ModernWMS.Migrations.Postgres  --startup-project ModernWMS --output-dir Migrations
dotnet ef database update --project ModernWMS.Migrations/ModernWMS.Migrations.<Provider> --startup-project ModernWMS
```

Every `decimal` property gets an explicit `decimal(18,6)` from `SqlDBContext.ConfigureConventions` (added because SQL Server has no fallback and silently defaults unannotated decimals to `decimal(18,2)`, which is a real truncation risk for `weight`/`volume`/`price`-shaped columns) — don't add a narrower precision to an individual property without checking whether real data needs the extra room. This convention is provider-agnostic (applies to SQLite/MySQL/Postgres too, not just SQL Server), so touching it invalidates every provider's migration history at once — expect `has-pending-model-changes` to go red for whichever ones you don't regenerate. The README/Docker deploy flow still ships a pre-built `wms.db` rather than running SQLite migrations — that file was NOT rebuilt from the current `Initial` migration, so don't assume it already reflects the `decimal(18,6)` change or any other model change made since it was captured.

`SQL/Tables.sql` and `SQL/Columns.sql` are hand-maintained patch scripts used instead of migrations for several fixes (see recent commits about `action_log`, `global_unique_serial`, `asnmaster`, `rolemenu.menu_actions_authority`). They contain a **mix of PostgreSQL and SQLite dialects** — check which one a statement targets before running it.

## Database selection

The provider is chosen at startup from `appsettings*.json` → `Database:db`, one of `SQLITE` | `MYSQL` | `SQLSERVER` | `POSTGRES`, each with its own entry in `ConnectionStrings`. `appsettings.json` currently says `MySql`; `appsettings.Development.json` says `SQLITE` and points at the checked-in `ModernWMS/wms.db`. Postgres additionally flips the two legacy Npgsql `AppContext` switches.

## Architecture

Three domain/host projects, strictly layered, plus one migrations-only project per DB provider:

- **`ModernWMS`** — thin ASP.NET Core host. `Program.cs` (NLog) → `Startup.cs`, which delegates everything to `AddExtensionsService` / `UseExtensionsConfigure`. Owns `appsettings`, `nlog.config`, and `wms.db` (migrations themselves live in the per-provider `ModernWMS.Migrations.*` projects below, not here).
- **`ModernWMS.Core`** — infrastructure only, no warehouse domain: `SqlDBContext`, JWT, dynamic search, middleware, Hangfire jobs, localization, Swagger, and `AccountController` (login / refresh-token — the only controller outside the WMS project).
- **`ModernWMS.WMS`** — the domain. `Controllers/`, `IServices/`, `Services/`, `Entities/Models/` (EF entities), `Entities/ViewModels/` (API contracts), all sharded into per-module folders (Asn, Stock, Dispatchlist, Sku, StockMove, StockTaking, …).
- **`ModernWMS.Migrations/ModernWMS.Migrations.{Sqlite,SqlServer,Postgres}`** — three projects nested under one `ModernWMS.Migrations/` folder (physical, and mirrored as a "Migrations" solution folder in `ModernWMS.sln`), each holding only that provider's EF Core migrations (see Migrations below; MySQL has no such project yet). Nothing in them is referenced by name from other code; `ModernWMS.csproj` carries a `ProjectReference` to each purely so its DLL ships next to `ModernWMS.dll` for `MigrationsAssembly` to `Assembly.Load` at runtime.

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
- **Module type names have been migrated from concatenated-lowercase to split PascalCase** (e.g. `Stockadjust` → `StockAdjust`, `Warehousearea` → `WarehouseArea`, `Goodsowner` → `GoodsOwner`, `Asnmaster` → `AsnMaster`, `Dispatchlist` → `DispatchList`). As of this writing every `IXxxService` interface, `XxxController` class, concrete `XxxService` class, `Entities/Models` entity, and `Entities/ViewModels` view model in `ModernWMS.WMS` is split — but treat that as a snapshot, not a guarantee: verify the module you're touching rather than assuming. Routes (`[Route("stockadjust")]`), XML-doc comment *text*, and DB table names (`[Table("stockadjust")]`) are deliberately left in the old lowercase form; only C# type identifiers get split, and even single-word module folders that happened to be lowercase (`company` → `Company`, `user` → `User`) were normalized to match the rest for consistency. Renaming an entity class is schema-safe because every entity pins its table explicitly via `[Table("...")]` — confirmed with `dotnet ef migrations has-pending-model-changes --project ModernWMS.Migrations/ModernWMS.Migrations.<Provider> --startup-project ModernWMS` (see Migrations below), which in practice ignores the CLR type name entirely and diffs off the table mapping, so a stale entity-name string left behind in a migration snapshot does **not** trip it up; still worth syncing each provider's `Migrations/*.Designer.cs`/`SqlDBContextModelSnapshot.cs` by hand for readability if you touch those folders — and note there are now three of them (Sqlite/SqlServer/Postgres) to keep in sync, not one. The recurring gotcha doing this rename: on this case-insensitive Windows filesystem, a plain OS-level folder/file case rename does **not** register with git — `git mv X X_tmp && git mv X_tmp Y` (two steps, source must byte-match the index's tracked casing or you get "fatal: not under version control") is required, and it's easy for individual files to slip through even after the containing folder is fixed. Don't trust `git status`'s displayed casing to verify this (it silently folds case on this filesystem) — instead run `git ls-files <path> | sort` vs `find <path> -name "*.cs" | sort` and diff the two; any file only on one side has an unregistered rename.
- **All user-facing strings are localized** through `IStringLocalizer<ModernWMS.Core.MultiLanguage>` against `Core/Models/MultiLanguage.{en-us,zh-cn}.resx` (~180 keys, e.g. `exists_entity`, `save_success`, `not_exists_entity`). Add new keys to **both** resx files. Supported cultures are `zh-cn` (default) and `en-us`, selected per request (`?culture=en-us`).
- **Workflow status is an untyped `byte`** (`asn_status`, `dispatch_status`, …) with literal values assigned inline in the services; there are no enums. Read the owning service to learn what a value means before changing it.

### Auth

JWT bearer, configured from `TokenSettings` (Audience / Issuer / SigningKey / ExpireMinute), `ClockSkew = Zero`. `/login` and `/refresh-token` are `[AllowAnonymous]`; refresh tokens are held in the in-memory `CacheManager`. Two things to know before touching auth:

- `[Authorize]` is **commented out** on `BaseController`, so WMS endpoints are effectively unauthenticated at the framework level. That is existing behavior, not something to rely on.
- `Core/Utility/GlobalConsts.SigningKey` duplicates the appsettings signing key, and `FunctionHelper.GetCurrentUser` validates against that constant rather than configuration — the two must stay in sync or token parsing there breaks.

CORS is handled by a hand-rolled `Core/Middleware/CorsMiddleware` that reflects the request `Origin`, not by `AddCors`.

### Where the complexity actually is

`Services/DispatchList/DispatchListService.cs` (~1800 lines) and `Services/Asn/AsnService.cs` (~1400) hold the outbound and inbound flows; `StockService`, `SpuService`, and `UserService` follow. Stock movement is spread across `Stock`, `StockMove`, `StockAdjust`, `StockFreeze`, `StockProcess`, and `StockTaking` — a change to quantities usually has to touch several of these consistently. `Services/Approve/FlowSetService.cs` has entities and a service but no controller or interface: an unfinished approval-flow module.
