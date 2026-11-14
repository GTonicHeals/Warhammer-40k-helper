# Crusade Storage in SQLite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Orders of Battle and Crusade Cards persist in the SQLite campaign database, with EF migrations and a one-time import from browser localStorage.

**Architecture:** The app switches from `EnsureCreated` to EF migrations. Because the live `campaign.db` was created by `EnsureCreated` and the checked-in migration is stale (it predates the Node TPH refactor), a baseline migration is generated from the current model and marked as applied on databases that already have the schema. Then the Crusade entities are added with a second migration. `CrusadeManagerService` keeps its public surface (`LoadAllCrusadesAsync`, `Crusades`, `GetCrusade`, `SaveCrusadeAsync`, `DeleteCrusadeAsync`, `OnCrusadesChanged`) but reads and writes EF instead of JS interop, so the pages change minimally. Honour and scar lists stay JSON columns.

**Tech Stack:** EF Core 10 SQLite, `dotnet ef` 10.0.11 (installed), xUnit + FluentAssertions, `SqliteTestBase`.

**Spec:** `docs/superpowers/specs/2026-11-10-crusade-campaign-and-10th-rules-spec.md` (Crusade section) and `docs/adr/0001-crusade-data-in-sqlite.md`.

## Global Constraints

- `T'au/campaign.db` holds live data: never drop it; schema changes only through migrations.
- Do not start the dev server. Migrations are generated with `dotnet ef migrations add <Name> --project "T'au/Warhammer.csproj"`.
- Crusade Points are derived, never stored (spec: +1 per honour, +2 if TITANIC, relics 1/2/3 by tier, −1 per scar).
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

- A database file that exists but is empty (zero tables) must take the fresh-migrate path, not the baseline path (pinned in Task 1).
- Running the bootstrap twice must be a no-op (pinned in Task 1).
- Legacy localStorage JSON with the old `crusadePoints` number and flat-string honours must still deserialize (pinned in Task 4).
- Deleting an Order of Battle must delete its cards and nothing else (pinned in Task 3).
- Saving an Order of Battle whose card was removed in memory must delete that card row (pinned in Task 3).

---

### Task 1: Migrations with a baseline bootstrap

**Files:**
- Delete: `T'au/Data/Migrations/20260525193750_InitialSqliteCreate.cs`, `T'au/Data/Migrations/20260525193750_InitialSqliteCreate.Designer.cs`, `T'au/Data/Migrations/AppDbContextModelSnapshot.cs`
- Create: `T'au/Data/Migrations/<timestamp>_Baseline.cs` (generated), `T'au/Data/DbBootstrap.cs`
- Modify: `T'au/Program.cs:36-39` (replace `EnsureCreatedAsync`)
- Test: `Warhammer.Tests/Data/DbBootstrapTests.cs`

**Interfaces:**
- Produces: `public static class DbBootstrap { public static Task MigrateAsync(AppDbContext ctx, CancellationToken ct = default); }`
- Produces: migration named `Baseline` that matches the current model exactly (Campaigns, CampaignPlayers, Nodes with `NodeType` discriminator, GmNotes, BattleRecords).

- [ ] **Step 1: Delete the stale migration files, then generate the baseline from the unchanged model**

Run: `dotnet ef migrations add Baseline --project "T'au/Warhammer.csproj" --output-dir Data/Migrations`
Expected: a new `_Baseline.cs` whose `Up` creates `Nodes` with a `NodeType` column and no `TerritoryNode` table. Inspect the file to confirm before continuing.

- [ ] **Step 2: Write the failing tests**

```csharp
public class DbBootstrapTests : IDisposable
{
    // Own connection: SqliteTestBase calls EnsureCreated, which would pre-empt the paths under test.
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;
    public DbBootstrapTests() { _conn.Open(); _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options; }
    private AppDbContext Ctx() => new(_options);

    [Fact] public async Task Empty_database_applies_every_migration()
    {   await using var ctx = Ctx();
        await DbBootstrap.MigrateAsync(ctx);
        (await ctx.Database.GetAppliedMigrationsAsync()).Should().BeEquivalentTo(ctx.Database.GetMigrations());
    }

    [Fact] public async Task Schema_without_history_table_is_baselined_then_migrated()
    {   await using (var ctx = Ctx())
        {   // Simulate a database built by EnsureCreated on the pre-Crusade model.
            await ctx.GetService<IMigrator>().MigrateAsync("Baseline");
            await ctx.Database.ExecuteSqlRawAsync("DROP TABLE __EFMigrationsHistory");
        }
        await using (var ctx = Ctx())
        {   await DbBootstrap.MigrateAsync(ctx);
            (await ctx.Database.GetAppliedMigrationsAsync()).Should().BeEquivalentTo(ctx.Database.GetMigrations());
            (await ctx.Campaigns.CountAsync()).Should().Be(0); // table survived, not recreated
        }
    }

    [Fact] public async Task Bootstrap_is_idempotent()
    {   await using var ctx = Ctx();
        await DbBootstrap.MigrateAsync(ctx);
        await DbBootstrap.MigrateAsync(ctx);
        (await ctx.Database.GetAppliedMigrationsAsync()).Should().HaveCount(ctx.Database.GetMigrations().Count());
    }
    public void Dispose() => _conn.Dispose();
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test Warhammer.Tests --filter "DbBootstrapTests" --nologo`
Expected: build error, `DbBootstrap` not found.

- [ ] **Step 4: Implement `DbBootstrap.MigrateAsync`**

Algorithm: query `sqlite_master` for table names. If `__EFMigrationsHistory` is absent and `Campaigns` is present, create the history table (`CREATE TABLE "__EFMigrationsHistory" ("MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY, "ProductVersion" TEXT NOT NULL)`) and insert one row for the first id in `ctx.Database.GetMigrations()` with `ProductVersion` = `typeof(DbContext).Assembly.GetName().Version!.ToString(3)`. Then `await ctx.Database.MigrateAsync(ct)`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Warhammer.Tests --filter "DbBootstrapTests" --nologo`
Expected: 3 passed.

- [ ] **Step 6: Switch `Program.cs` to the bootstrap**

Replace `await ctx.Database.EnsureCreatedAsync();` with `await DbBootstrap.MigrateAsync(ctx);`. Build: `dotnet build Warhammer.slnx --nologo -v q` → `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add "T'au/Data" "T'au/Program.cs" Warhammer.Tests/Data/DbBootstrapTests.cs
git commit -m "feat: EF migrations with a baseline bootstrap for the existing campaign.db

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Crusade entities in the model

**Files:**
- Modify: `T'au/Models/Crusade/OrderOfBattle.cs`, `T'au/Models/Crusade/CrusadeUnitCard.cs`, `T'au/Data/AppDbContext.cs`
- Create: `T'au/Models/Crusade/CrusadePointsCalculator.cs`, `T'au/Models/Crusade/CrusadeJson.cs`, `T'au/Data/Migrations/<timestamp>_AddCrusadeTables.cs` (generated)
- Test: `Warhammer.Tests/CrusadePointsCalculatorTests.cs`, `Warhammer.Tests/Data/CrusadeMappingTests.cs`

**Interfaces:**
- Produces on `OrderOfBattle`: `[Key] string Id`, `int CampaignId`, `List<CrusadeUnitCard> Units` (navigation), `List<CrusadeTrait> CustomTraits` (JSON column). Existing scalars unchanged.
- Produces on `CrusadeUnitCard`: `[Key] string Id`, `string OrderOfBattleId`, `int BattlesPlayed`, `int BattlesSurvived`, `bool IsTitanic`, `bool XpEligible = true`; `BattleHonours`/`BattleScars` JSON columns; `[NotMapped] int CrusadePoints => CrusadePointsCalculator.For(this)`. The `CrusadePoints` setter is removed.
- Produces: `public static class CrusadePointsCalculator { public static int CostOf(AppliedTrait t, bool isTitanic); public static int For(CrusadeUnitCard card); }`
- Produces: `public static class CrusadeJson { public static readonly JsonSerializerOptions Options; }` with `PropertyNameCaseInsensitive = true` and `AppliedTraitListConverter` registered.
- Produces: `AppDbContext.OrdersOfBattle`, `AppDbContext.CrusadeUnitCards`.

- [ ] **Step 1: Write the failing calculator tests**

```csharp
public class CrusadePointsCalculatorTests
{
    private static AppliedTrait Honour(string type, string category = "") => new() { Name = "x", Type = type, Category = category };

    [Fact] public void Battle_trait_costs_one() =>
        CrusadePointsCalculator.CostOf(Honour(CrusadeTraitTypes.BattleTrait), isTitanic: false).Should().Be(1);
    [Fact] public void Battle_trait_on_titanic_costs_two() =>
        CrusadePointsCalculator.CostOf(Honour(CrusadeTraitTypes.BattleTrait), isTitanic: true).Should().Be(2);
    [Theory] [InlineData("Artificer", 1)] [InlineData("Antiquity", 2)] [InlineData("Legendary", 3)]
    public void Relic_costs_by_tier(string tier, int cost) =>
        CrusadePointsCalculator.CostOf(Honour(CrusadeTraitTypes.CrusadeRelic, tier), isTitanic: false).Should().Be(cost);
    [Fact] public void Card_total_is_honours_minus_scars()
    {   var card = new CrusadeUnitCard { BattleHonours = { Honour(CrusadeTraitTypes.BattleTrait), Honour(CrusadeTraitTypes.CrusadeRelic, "Antiquity") },
                                          BattleScars   = { Honour(CrusadeTraitTypes.BattleScar) } };
        CrusadePointsCalculator.For(card).Should().Be(2);
        card.CrusadePoints.Should().Be(2);
    }
}
```

- [ ] **Step 2: Write the failing mapping test (uses `SqliteTestBase`)**

```csharp
public class CrusadeMappingTests : SqliteTestBase
{
    [Fact] public async Task Order_round_trips_with_cards_and_json_columns()
    {   var campaignId = await SeedCampaignAsync();  // add Campaign via CreateContext(), return Id
        var order = new OrderOfBattle { ArmyName = "Sept", CampaignId = campaignId, PlayerId = "p1",
            CustomTraits = { new CrusadeTrait { Name = "Custom", Type = CrusadeTraitTypes.CrusadeRelic } },
            Units = { new CrusadeUnitCard { UnitName = "Breachers", Xp = 7,
                BattleHonours = { new AppliedTrait { Name = "Brutal", Type = CrusadeTraitTypes.WeaponModification, WeaponName = "Pulse blaster" } } } } };
        await using (var ctx = CreateContext()) { ctx.OrdersOfBattle.Add(order); await ctx.SaveChangesAsync(); }
        await using (var ctx = CreateContext())
        {   var loaded = await ctx.OrdersOfBattle.Include(o => o.Units).SingleAsync();
            loaded.CustomTraits.Should().ContainSingle(t => t.Name == "Custom");
            loaded.Units.Should().ContainSingle().Which.BattleHonours.Should().ContainSingle(h => h.WeaponName == "Pulse blaster");
            loaded.Units[0].CrusadePoints.Should().Be(1);
        }
    }
}
```

- [ ] **Step 3: Run both test classes to verify they fail**

Run: `dotnet test Warhammer.Tests --filter "CrusadePointsCalculatorTests|CrusadeMappingTests" --nologo`
Expected: build errors (`CrusadePointsCalculator`, `OrdersOfBattle`, `CampaignId`).

- [ ] **Step 4: Implement the model changes and the context mapping**

`CrusadePointsCalculator.CostOf`: move the logic of `CrusadeDetail.razor` `CrusadePointsCostOf(type, category)` here; non-relic honours return `isTitanic ? 2 : 1`. `For(card)` = Σ `CostOf(h, card.IsTitanic)` over honours − `BattleScars.Count`.

In `AppDbContext.OnModelCreating`: `OrderOfBattle` → `HasOne<Campaign>().WithMany().HasForeignKey(o => o.CampaignId)`, `HasMany(o => o.Units).WithOne().HasForeignKey(u => u.OrderOfBattleId).OnDelete(DeleteBehavior.Cascade)`, `CustomTraits` as JSON text via `HasConversion` with a `ValueComparer<List<CrusadeTrait>>` (same pattern as `ConnectedNodeIds`). `CrusadeUnitCard.BattleHonours` and `.BattleScars` as JSON text using `CrusadeJson.Options`, each with a `ValueComparer<List<AppliedTrait>>`. Ignore `CrusadePoints` and `Rank` (`[NotMapped]` or `.Ignore`). `SupplyUsed`/`SupplyRemaining` stay `[JsonIgnore]` and get `[NotMapped]`.

- [ ] **Step 5: Generate the migration and run the tests**

Run: `dotnet ef migrations add AddCrusadeTables --project "T'au/Warhammer.csproj" --output-dir Data/Migrations`
Expected: `Up` creates `OrdersOfBattle` and `CrusadeUnitCards` only.
Run: `dotnet test Warhammer.Tests --nologo` → all pass (the `DbBootstrapTests` now see two migrations).

- [ ] **Step 6: Commit**

```bash
git add "T'au/Models/Crusade" "T'au/Data" Warhammer.Tests/CrusadePointsCalculatorTests.cs Warhammer.Tests/Data/CrusadeMappingTests.cs
git commit -m "feat: Crusade entities mapped in SQLite with derived Crusade Points

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: `CrusadeManagerService` over EF

**Files:**
- Modify: `T'au/Services/CrusadeManagerService.cs` (rewrite), `T'au/Program.cs` (registration unchanged, verify)
- Test: `Warhammer.Tests/Services/CrusadeManagerServiceTests.cs`

**Interfaces:**
- Produces (public surface kept, JS dependency dropped):
  ```csharp
  public class CrusadeManagerService(IDbContextFactory<AppDbContext> dbFactory, CampaignManagerService campaigns, ILogger<CrusadeManagerService> logger)
  {
      public IReadOnlyCollection<OrderOfBattle> Crusades { get; }          // snapshot of the last load/save
      public event Action? OnCrusadesChanged;
      public Task<IReadOnlyCollection<OrderOfBattle>> LoadAllCrusadesAsync(); // Include(Units), AsNoTracking, refreshes Crusades
      public OrderOfBattle? GetCrusade(string id);                          // from the snapshot
      public Task SaveCrusadeAsync(OrderOfBattle crusade);                  // upsert; CampaignId defaults to the loaded campaign's Id when 0
      public Task DeleteCrusadeAsync(string id);
  }
  ```

- [ ] **Step 1: Write the failing tests (pattern of `CampaignManagerServiceTests`)**

```csharp
public class CrusadeManagerServiceTests : SqliteTestBase
{
    private CrusadeManagerService Svc() => new(CreateFactory(),
        new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance),
        NullLogger<CrusadeManagerService>.Instance);

    [Fact] public async Task Save_inserts_and_assigns_campaign_id()
    {   var svc = Svc(); var o = new OrderOfBattle { ArmyName = "A", PlayerId = "p1" };
        await svc.SaveCrusadeAsync(o);
        o.CampaignId.Should().BePositive();
        (await svc.LoadAllCrusadesAsync()).Should().ContainSingle(x => x.Id == o.Id);
    }
    [Fact] public async Task Save_updates_scalars_and_reconciles_cards()
    {   var svc = Svc(); var o = new OrderOfBattle { ArmyName = "A", PlayerId = "p1",
            Units = { new CrusadeUnitCard { UnitName = "U1" }, new CrusadeUnitCard { UnitName = "U2" } } };
        await svc.SaveCrusadeAsync(o);
        o.ArmyName = "B"; o.Units.RemoveAt(1); o.Units[0].Xp = 9; o.Units.Add(new CrusadeUnitCard { UnitName = "U3" });
        await svc.SaveCrusadeAsync(o);
        var loaded = (await svc.LoadAllCrusadesAsync()).Single();
        loaded.ArmyName.Should().Be("B");
        loaded.Units.Select(u => u.UnitName).Should().BeEquivalentTo(new[] { "U1", "U3" });
        loaded.Units.Single(u => u.UnitName == "U1").Xp.Should().Be(9);
    }
    [Fact] public async Task Delete_removes_order_and_its_cards_only()
    {   var svc = Svc();
        var a = new OrderOfBattle { ArmyName = "A", PlayerId = "p1", Units = { new CrusadeUnitCard { UnitName = "U" } } };
        var b = new OrderOfBattle { ArmyName = "B", PlayerId = "p2", Units = { new CrusadeUnitCard { UnitName = "V" } } };
        await svc.SaveCrusadeAsync(a); await svc.SaveCrusadeAsync(b);
        await svc.DeleteCrusadeAsync(a.Id);
        await using var ctx = CreateContext();
        (await ctx.OrdersOfBattle.CountAsync()).Should().Be(1);
        (await ctx.CrusadeUnitCards.CountAsync()).Should().Be(1);
    }
    [Fact] public async Task Save_raises_changed_event()
    {   var svc = Svc(); var raised = 0; svc.OnCrusadesChanged += () => raised++;
        await svc.SaveCrusadeAsync(new OrderOfBattle { ArmyName = "A" });
        raised.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Warhammer.Tests --filter "CrusadeManagerServiceTests" --nologo`
Expected: build error, constructor signature mismatch.

- [ ] **Step 3: Rewrite the service**

Save: if `CampaignId == 0`, set it from `(await campaigns.LoadCampaignAsync()).Id`. Load the tracked order with `Include(o => o.Units)`; if absent, `Add`; else `SetValues` on scalars and reconcile `Units` by `Id` exactly like `CampaignManagerService.Reconcile`. Set `UpdatedUtc`. Refresh the snapshot and raise the event after every save and delete. Remove all `IJSRuntime` use.

- [ ] **Step 4: Run the full suite**

Run: `dotnet test Warhammer.Tests --nologo` → all pass.

- [ ] **Step 5: Commit**

```bash
git add "T'au/Services/CrusadeManagerService.cs" Warhammer.Tests/Services/CrusadeManagerServiceTests.cs
git commit -m "feat: CrusadeManagerService reads and writes SQLite

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: One-time import from localStorage

**Files:**
- Create: `T'au/Services/CrusadeLocalStorageImporter.cs`
- Modify: `T'au/Program.cs` (register scoped), `T'au/Components/Pages/Crusade/CrusadeManager.razor:211-214` (call before `LoadAllCrusadesAsync` on first render)
- Test: `Warhammer.Tests/Services/CrusadeLocalStorageImporterTests.cs`

**Interfaces:**
- Produces: `public class CrusadeLocalStorageImporter(IJSRuntime js, CrusadeManagerService crusades, ILogger<...> logger) { public Task<int> ImportAsync(); }` returning the number of Orders imported. Reads `crusadeStorage.getAll`, skips ids already present in `crusades.Crusades` after a load, saves the rest with `CrusadeJson.Options` deserialization, then calls `crusadeStorage.remove` per imported key. Any JS failure (prerender) logs and returns 0.

- [ ] **Step 1: Write the failing tests (Moq `IJSRuntime`, SQLite base)**

```csharp
[Fact] public async Task Imports_legacy_json_then_removes_key()
{   const string legacy = """{"id":"abc","armyName":"Old","playerId":"p1","crusadePoints":4,
        "units":[{"id":"u1","unitName":"Breachers","crusadePoints":3,"battleHonours":["Brutal [Weapon Modification] — S+1"]}]}""";
    var js = new Mock<IJSRuntime>();
    js.Setup(j => j.InvokeAsync<Dictionary<string, string>>("crusadeStorage.getAll", It.IsAny<object[]>()))
      .ReturnsAsync(new Dictionary<string, string> { ["crusade_abc"] = legacy });
    var imported = await new CrusadeLocalStorageImporter(js.Object, Svc(), NullLogger<CrusadeLocalStorageImporter>.Instance).ImportAsync();
    imported.Should().Be(1);
    var loaded = (await Svc().LoadAllCrusadesAsync()).Single();
    loaded.Units.Single().BattleHonours.Single().Name.Should().Be("Brutal");
    js.Verify(j => j.InvokeAsync<IJSVoidResult>("crusadeStorage.remove", It.Is<object[]>(a => (string)a[0] == "crusade_abc")), Times.Once);
}
[Fact] public async Task Skips_orders_already_in_database() { /* seed id "abc" via Svc().SaveCrusadeAsync, same mock → imported == 0, remove still called */ }
[Fact] public async Task Returns_zero_when_js_unavailable() { /* mock throws InvalidOperationException → 0, no exception */ }
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test Warhammer.Tests --filter "CrusadeLocalStorageImporterTests" --nologo` → build error.

- [ ] **Step 3: Implement the importer, register it, call it from `CrusadeManager.razor` first render**

- [ ] **Step 4: Run the full suite** — `dotnet test Warhammer.Tests --nologo` → all pass.

- [ ] **Step 5: Commit**

```bash
git add "T'au/Services/CrusadeLocalStorageImporter.cs" "T'au/Program.cs" "T'au/Components/Pages/Crusade/CrusadeManager.razor" Warhammer.Tests/Services/CrusadeLocalStorageImporterTests.cs
git commit -m "feat: import Orders of Battle from localStorage once

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Pages use derived Crusade Points and the new unit flags

**Files:**
- Modify: `T'au/Components/Pages/Crusade/CrusadeDetail.razor` (`AddUnit` ~line 690, `AddHonour` ~825, `RemoveHonour` ~834, `AddScar` ~851, `RemoveScar` ~862, helper `CrusadePointsCostOf` ~790-805)
- Modify: `README.md` (Crusade section: data now in `campaign.db`). `T'au/wwwroot/js/crusadeStorage.js` stays because the importer uses it.

- [ ] **Step 1: Remove every `unit.CrusadePoints +=` / `-=` line and the local `CrusadePointsCostOf` helpers; call `CrusadePointsCalculator.CostOf(t, unit.IsTitanic)` where `OptionLabel` needs the cost**

- [ ] **Step 2: In `AddUnit`, set the new flags from the roster selection**

`IsTitanic = keywords contains "Titanic"`, `XpEligible = !(keywords contains "Epic Hero" or "Fortification" or "Swarm")`, where keywords = `RosterHelper.GetKeywords(_selectionsByName[name])`, case-insensitive. Keep the existing `IsCharacter` checkbox but default it from the "Character" keyword.

- [ ] **Step 3: Show the tallies on the card**

Next to the XP input add read-only `Battles @unit.BattlesPlayed · Survived @unit.BattlesSurvived · Destroyed @unit.UnitsDestroyed` and the derived `@unit.CrusadePoints` where the typed value was shown.

- [ ] **Step 4: Build and run the suite**

Run: `dotnet build Warhammer.slnx --nologo -v q && dotnet test Warhammer.Tests --nologo` → `Build succeeded.`, all pass.

- [ ] **Step 5: Commit**

```bash
git add "T'au/Components/Pages/Crusade/CrusadeDetail.razor" README.md
git commit -m "feat: Crusade Card shows derived Crusade Points and combat tallies

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
