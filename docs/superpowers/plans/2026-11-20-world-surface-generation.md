# World Surface Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat noise backdrop with a World Surface that has controllable seas, flow-traced rivers and lakes, hill-shading, Biome-styled scatter features, and nine Biomes.

**Architecture:** A pure, deterministic `worldSurface.js` builds the world on a fixed 256×256 analysis grid (heights, water classes, rivers, features, roads) and is unit-tested in Node. `topoMap.js` becomes a renderer that paints that result at canvas resolution. C# gains five `Campaign` columns, a `BiomeCatalog`, and a `WorldSurfaceSettings` record that every map host passes to `CampaignMap`.

**Tech Stack:** Blazor Server (.NET 10), EF Core 10 SQLite + `dotnet ef`, xUnit + FluentAssertions + bUnit, vanilla JS (ES5-style IIFE, matching the existing files), Node 24 `node:test`.

**Spec:** `docs/superpowers/specs/2026-11-20-world-surface-generation-design.md`

## Global Constraints

- Scenery only: no code reads the World Surface back, and `worldSurface.js` takes no territory/node input.
- Biome keys, exactly: `agri, death, ice, ash, desert, jungle, hive, sept, tomb`. Unknown key → `agri` (C# and JS).
- Landmass keys, exactly: `continent, archipelago, pangea, random`. Unknown → `continent`.
- Biome defaults (key → label, sea level, rivers): agri "Agri World" 0.40/5 · death "Death World" 0.30/4 · ice "Ice World" 0.45/3 · ash "Ash Wastes" 0.20/3 · desert "Desert World" 0.08/2 · jungle "Jungle World" 0.30/9 · hive "Hive World" 0.25/2 · sept "Sept World" 0.35/4 · tomb "Tomb World" 0.15/0.
- New-campaign defaults: `MapSeaLevel = 0.35`, `MapLandmass = "continent"`, `MapRivers = 4`, `MapRuggedness = 0.5`, `MapFeatures = 0.5`. Existing rows get the same values via the migration.
- Slider ranges: Sea level 0.00–0.85 step 0.01 (shown as %), Rivers 0–12 step 1, Ruggedness 0–1 step 0.05, Features 0–1 step 0.05.
- Analysis grid is fixed at 256×256 in normalised map space, so the same settings yield the same world at any canvas size.
- No new npm/NuGet dependencies. JS stays dependency-free.
- Use the glossary terms in code comments and UI copy: **World Surface**, **Biome**. Avoid "terrain" for the backdrop.
- Don't start the dev server except for the final manual check (Task 8), via `dotnet run --launch-profile https`.
- Commit own files only, no push.

## Review Focus

1. **Sea level 0** (desert/tomb near it): no sea at all → rivers must still terminate (lake, or map edge) without looping, and painting must not divide by zero on an empty water class.
2. **Sea level 0.85 with `random` or `archipelago`**: very little highland → `rivers.length` may be below the request; no duplicate/overlapping river starts and no crash.
3. **Legacy or garbage Biome/Landmass value** stored in the DB → falls back to agri/continent everywhere (dropdown shows Agri, renderer paints Agri) rather than throwing or painting black.
4. **Slider drag latency**: `buildWorld` runs on every `oninput`; it must stay well under a frame budget burst (< 300 ms in Node on this machine).
5. **Campaign Editor Biome dropdown** must change only `MapBiome`, never `MapSeaLevel`/`MapRivers`; only the Map Generator applies Biome defaults.

---

### Task 1: Campaign columns, BiomeCatalog, WorldSurfaceSettings

**Files:**
- Modify: `T'au/Models/Campaign/Campaign.cs`
- Create: `T'au/Helpers/BiomeCatalog.cs`
- Create: `T'au/Models/Campaign/WorldSurfaceSettings.cs`
- Create: `T'au/Data/Migrations/<timestamp>_AddWorldSurfaceControls.cs` (generated, then edited)
- Test: `Warhammer.Tests/Helpers/BiomeCatalogTests.cs`, `Warhammer.Tests/Campaign/WorldSurfaceSettingsTests.cs`, `Warhammer.Tests/Data/DbBootstrapTests.cs`

**Interfaces:**
- Produces:
  - `Campaign`: `double MapSeaLevel = 0.35`, `string MapLandmass = "continent"`, `int MapRivers = 4`, `double MapRuggedness = 0.5`, `double MapFeatures = 0.5`.
  - `namespace Warhammer.Helpers; public sealed record BiomeInfo(string Key, string Label, double DefaultSeaLevel, int DefaultRivers);`
  - `public static class BiomeCatalog { IReadOnlyList<BiomeInfo> All; BiomeInfo Get(string? key); }` — `All` in the order of Global Constraints; `Get` falls back to agri.
  - `namespace Warhammer.Models.Campaign; public sealed record WorldSurfaceSettings(int Seed, string Biome, double SeaLevel, string Landmass, int Rivers, double Ruggedness, double Features, int Octaves, double Lacunarity, double Persistence, double Scale, double OffsetX, double OffsetY) { public static WorldSurfaceSettings From(Campaign c); }` — `From` normalises Biome via `BiomeCatalog.Get(c.MapBiome).Key` and Landmass to a known key. Serialises camelCase over JS interop (Blazor default), so JS sees `seed, biome, seaLevel, landmass, rivers, ruggedness, features, octaves, lacunarity, persistence, scale, offsetX, offsetY`.

- [ ] **Step 1: Write failing tests**

```csharp
// BiomeCatalogTests
[Fact] public void Has_the_nine_biomes_in_order() =>
    BiomeCatalog.All.Select(b => b.Key).Should().Equal("agri","death","ice","ash","desert","jungle","hive","sept","tomb");
[Fact] public void Desert_defaults() { var d = BiomeCatalog.Get("desert"); d.Label.Should().Be("Desert World"); d.DefaultSeaLevel.Should().Be(0.08); d.DefaultRivers.Should().Be(2); }
[Theory, InlineData(null), InlineData(""), InlineData("forge")]
public void Unknown_key_falls_back_to_agri(string? key) => BiomeCatalog.Get(key).Key.Should().Be("agri");

// WorldSurfaceSettingsTests
[Fact] public void From_copies_campaign_values() { var c = new Campaign { MapSeed = 7, MapBiome = "jungle", MapSeaLevel = 0.3, MapLandmass = "archipelago", MapRivers = 9 };
    var s = WorldSurfaceSettings.From(c); s.Seed.Should().Be(7); s.Biome.Should().Be("jungle"); s.Landmass.Should().Be("archipelago"); s.Rivers.Should().Be(9); s.SeaLevel.Should().Be(0.3); }
[Fact] public void From_normalises_unknown_biome_and_landmass() { var s = WorldSurfaceSettings.From(new Campaign { MapBiome = "x", MapLandmass = "y" });
    s.Biome.Should().Be("agri"); s.Landmass.Should().Be("continent"); }
[Fact] public void New_campaign_defaults() { var c = new Campaign(); c.MapSeaLevel.Should().Be(0.35); c.MapLandmass.Should().Be("continent"); c.MapRivers.Should().Be(4); c.MapRuggedness.Should().Be(0.5); c.MapFeatures.Should().Be(0.5); }

// DbBootstrapTests — new fact, same pattern as Territories_that_existed_before_hiding_stay_revealed
[Fact] public async Task Campaigns_from_before_world_surface_controls_get_the_defaults()
// migrate to "20261119020149_AddGmNoteTableMode", raw-insert the same Campaigns row, DbBootstrap.MigrateAsync,
// then: live.MapSeaLevel == 0.35, MapLandmass == "continent", MapRivers == 4, MapRuggedness == 0.5, MapFeatures == 0.5
```

- [ ] **Step 2: Run, expect FAIL (types/props missing)**
Run: `dotnet test Warhammer.Tests --filter "BiomeCatalogTests|WorldSurfaceSettingsTests|DbBootstrapTests" --nologo`

- [ ] **Step 3: Implement the props, `BiomeCatalog`, `WorldSurfaceSettings`**

- [ ] **Step 4: Generate the migration and set real defaults**
Run: `dotnet ef migrations add AddWorldSurfaceControls --project "T'au/Warhammer.csproj" --output-dir Data/Migrations`
Then edit each `AddColumn` in the generated `Up` so `defaultValue` is the Global Constraints value (EF emits 0/"" for non-nullable columns).

- [ ] **Step 5: Run the filter again, expect PASS; then full suite `dotnet test Warhammer.Tests --nologo`, expect PASS**

- [ ] **Step 6: Commit** — `feat: add World Surface controls to Campaign`

---

### Task 2: worldSurface.js — heightfield, landmass, sea level

**Files:**
- Create: `T'au/wwwroot/js/worldSurface.js`
- Test: `Warhammer.Tests/js/worldSurface.test.js`

**Interfaces:**
- Consumes: the camelCase settings object from Task 1.
- Produces (on `window.worldSurface` in the browser, `module.exports` in Node — end the IIFE with `if (typeof module !== 'undefined') module.exports = api; else window.worldSurface = api;`):
  - `GRID = 256`
  - `normalise(settings) -> settings` — fills missing fields with Global Constraints defaults, clamps ranges, unknown biome → `agri`, unknown landmass → `continent`.
  - `sampleHeight(settings, nx, ny) -> number` — nx, ny in [0,1]; same field the grid uses, for full-res painting.
  - `buildWorld(settings) -> World` where `World = { size, height: Float32Array, seaLevel: number, water: Uint8Array /* 0 land, 1 sea, 2 lake */, rivers: [], features: [], roads: [] }` (rivers/features/roads filled by Tasks 3–4; empty arrays here).

Approach: move `mulberry32`/`createNoise` from `topoMap.js`. Height = fBm (existing octaves/lacunarity/persistence/scale/offset semantics) blended toward ridged noise by `ruggedness`, multiplied by a Landmass mask (continent: one radial falloff; archipelago: several seeded blobs + higher-frequency noise; pangea: broad falloff covering most of the map; random: no mask). `seaLevel` threshold = the `settings.seaLevel` quantile of `height`. `water`: cells below threshold flood-filled from the grid border → 1; remaining below-threshold cells → 2.

- [ ] **Step 1: Write failing tests** (`node:test` + `node:assert/strict`, `require('../../T\'au/wwwroot/js/worldSurface.js')` via `path.join(__dirname, '..', '..', "T'au", 'wwwroot', 'js', 'worldSurface.js')`)

```js
test('same settings build the same world', () => deepEqual(buildWorld(S).height, buildWorld(S).height));
test('different seeds differ', ...);
for (const sea of [0.05, 0.35, 0.8])
  test(`sea level ${sea} puts that share underwater`, () => { const w = buildWorld({...S, seaLevel: sea});
     const under = w.height.filter(h => h < w.seaLevel).length / (w.size*w.size);
     assert.ok(Math.abs(under - sea) < 0.02); });
test('sea level 0 has no water', () => assert.equal(buildWorld({...S, seaLevel: 0}).water.filter(v => v).length, 0));
test('continent: the grid border is mostly sea at sea level 0.35', () => /* >= 80% of border cells have water === 1 */);
test('normalise: unknown biome and landmass fall back', () => { const n = normalise({biome:'forge', landmass:'moon'}); equal(n.biome,'agri'); equal(n.landmass,'continent'); });
test('buildWorld completes under 300 ms', ...);   // Review Focus 4
```
`S = { seed: 42, biome: 'agri', seaLevel: 0.35, landmass: 'continent', rivers: 4, ruggedness: 0.5, features: 0.5 }`

- [ ] **Step 2: Run, expect FAIL** — `node --test Warhammer.Tests/js/`
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run, expect PASS**
- [ ] **Step 5: Commit** — `feat: World Surface heightfield with sea level and landmass`

---

### Task 3: worldSurface.js — rivers and lakes

**Files:**
- Modify: `T'au/wwwroot/js/worldSurface.js`
- Test: `Warhammer.Tests/js/worldSurface.test.js`

**Interfaces:**
- Produces: `World.rivers: Array<{ points: Array<[number, number]>, width: number }>` — points in [0,1] map space, source first; `width` grows with accumulated flow (1–4 px at 1000-px reference). `World.water` gains lake cells (2) where rivers pool.

Algorithm (not determined by the tests):
1. Priority-flood depression fill from the border and sea cells over `height` (binary min-heap) → `filled`.
2. D8 flow direction on `filled` (ties broken by tiny seeded epsilon so flats drain); flow accumulation.
3. Depressions where `filled - height > 0.004` and accumulation above a threshold become lakes (`water = 2`).
4. Sources: land cells in the top 30% of land height, spaced ≥ 20 cells apart, chosen in seeded order; trace downstream until a water cell or the border. Keep the first `settings.rivers` traces of ≥ 12 cells. A trace that joins an earlier river stops at the junction.

- [ ] **Step 1: Write failing tests**

```js
test('rivers: 0 gives none', () => equal(buildWorld({...S, rivers: 0}).rivers.length, 0));
test('rivers: never more than asked, at least one when asked on a normal map', ...);
test('every river ends in water, the border, or another river', ...);
test('every river runs downhill or flat on the filled surface', ...); // sampleHeight at successive points, allow 0.002 slack
test('sea level 0 still terminates rivers', () => /* buildWorld returns; every river length < GRID*GRID */);  // Review Focus 1
test('sea level 0.85 archipelago: no crash, river starts are distinct', ...);                          // Review Focus 2
test('still under 300 ms with 12 rivers', ...);
```

- [ ] **Step 2: Run, expect FAIL** · **Step 3: Implement** · **Step 4: Run, expect PASS**
- [ ] **Step 5: Commit** — `feat: flow-traced rivers and lakes on the World Surface`

---

### Task 4: worldSurface.js — Biome styles, features, roads

**Files:**
- Modify: `T'au/wwwroot/js/worldSurface.js`
- Test: `Warhammer.Tests/js/worldSurface.test.js`

**Interfaces:**
- Produces:
  - `BIOME_STYLES[key] = { bands: [[maxLandHeight01, r, g, b], ...], sea: [r,g,b], shallows: [r,g,b], lake: [r,g,b], river: 'water'|'sludge'|'lava'|'frozen'|'dry', features: string[], roads: boolean }` for all nine keys. `bands` thresholds are relative to land (0 = shoreline, 1 = highest peak), so colours track the sea level instead of fixed heights.
  - `World.features: Array<{ kind: string, x: number, y: number, size: number }>` (x, y in [0,1]); `World.roads: Array<Array<[number, number]>>`.
- Feature kinds per Biome (river style, roads):
  - agri: forest, ruins, crater — water, roads
  - death: forest, crater, ruins — sludge
  - ice: crater, ruins — frozen, roads
  - ash: crater, ruins, fissure — lava
  - desert: dune, oasis, ruins, crater — dry
  - jungle: forest, swamp, ruins — water
  - hive: spire, ruins — sludge, roads
  - sept: settlement, forest — water, roads
  - tomb: pyramid, fissure, crater — dry
- Placement: seeded rejection sampling on land only (except `oasis`/`swamp`: low land next to water), count = `round(features * 60)`, min spacing 8 grid cells. Roads: when `roads` is true, connect each `ruins`/`spire`/`settlement` to its nearest unconnected neighbour within 0.3 map units, at most `round(features * 8)` roads, skipping any whose straight segment crosses sea.

- [ ] **Step 1: Write failing tests**

```js
test('every biome key has a style', () => deepEqual(Object.keys(BIOME_STYLES).sort(), ['agri','ash','death','desert','hive','ice','jungle','sept','tomb']));
test('features 0 gives no features and no roads', ...);
test('more features density gives more features', ...);
test('features never sit on sea', ...);
test('feature kinds belong to the biome', () => for each key: every feature.kind in BIOME_STYLES[key].features);
test('biomes without roads produce none', () => equal(buildWorld({...S, biome:'tomb', features: 1}).roads.length, 0));
```

- [ ] **Step 2: Run, expect FAIL** · **Step 3: Implement** · **Step 4: Run, expect PASS**
- [ ] **Step 5: Commit** — `feat: Biome styles, scatter features and roads`

---

### Task 5: topoMap.js renderer

**Files:**
- Modify (rewrite): `T'au/wwwroot/js/topoMap.js`
- Modify: `T'au/Components/App.razor` — add `<script src="@Assets["js/worldSurface.js"]"></script>` before `topoMap.js`.

**Interfaces:**
- Consumes: `window.worldSurface.{normalise, buildWorld, sampleHeight, BIOME_STYLES}`.
- Produces: `topoMapInterop.draw(canvasEl, settings)` and `topoMapInterop.drawById(canvasId, settings)` — the old `(seed, biome, opts)` signature is removed.

Paint order: (1) per pixel block (`RES = 2`, written into one `ImageData`, not `fillRect` per cell): `sampleHeight` at full resolution, water class from the grid (nearest cell), land colour from `bands` by relative land height, sea/shallows by depth below `seaLevel`; (2) hill-shade from neighbouring full-res heights, land only, NW light; (3) coastline stroke; (4) rivers as smoothed quadratic paths scaled by canvas width / 1000, styled by `river` (`lava` gets a glow, `dry` is a dashed darker line, `frozen` pale blue-white); (5) roads (thin, dashed, low alpha); (6) features as small vector glyphs; (7) the existing vignette. Feature/road alpha ≤ 0.6 so nodes stay readable (spec anti-goal).

- [ ] **Step 1: Implement**
- [ ] **Step 2: `node --test Warhammer.Tests/js/` still PASS; `dotnet build "T'au/Warhammer.csproj" --nologo` succeeds**
- [ ] **Step 3: Commit** — `feat: render the World Surface with water, relief and features`

(Visual verification happens in Task 8, once the pages pass the new settings.)

---

### Task 6: CampaignMap and its hosts take WorldSurfaceSettings

**Files:**
- Modify: `T'au/Components/UI/CampaignMap.razor` — replace the eight `Map*` parameters with `[Parameter, EditorRequired] public WorldSurfaceSettings Surface { get; set; }`; redraw when `Surface` differs from the last drawn record (record equality replaces `DrawKey()`); call `topoMapInterop.drawById("campaign-topo-canvas", Surface)`.
- Modify: `T'au/Components/Pages/Campaign/CampaignEditor.razor` (lines ~104–135), `T'au/Components/Pages/Campaign/WarRoom.razor` (~73–76), `T'au/Components/UI/GmTableMode.razor` (~80–83) — pass `Surface="WorldSurfaceSettings.From(<campaign>)"`.
- Modify: `CampaignEditor.razor` Biome `<select>` — options rendered from `BiomeCatalog.All`; `OnBiomeChanged` unchanged (sets only `MapBiome`).
- Test: `Warhammer.Tests/Components/CampaignMapSurfaceTests.cs`, plus an assertion in an existing CampaignEditor test class.

**Interfaces:**
- Consumes: `WorldSurfaceSettings`, `BiomeCatalog` (Task 1); `topoMapInterop.drawById(canvasId, settings)` (Task 5).

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Draws_the_surface_it_is_given()
// render CampaignMap with Surface = WorldSurfaceSettings.From(new Campaign { MapSeed = 9, MapBiome = "tomb" });
// _ctx.JSInterop.VerifyInvoke("topoMapInterop.drawById").Arguments[1].Should().Be(that record)
[Fact] public void Redraws_only_when_the_surface_changes()
// re-render with an equal record → still 1 invocation; with MapRivers changed → 2
[Fact] public void Editor_biome_dropdown_lists_all_nine()       // GM-authorised CampaignEditor; .ce-biome-select option count == 9
[Fact] public void Editor_biome_change_keeps_sea_level_and_rivers() // Review Focus 5: change to "desert"; campaign MapSeaLevel/MapRivers unchanged
```

- [ ] **Step 2: Run, expect FAIL** — `dotnet test Warhammer.Tests --filter "CampaignMapSurfaceTests|CampaignEditor" --nologo`
- [ ] **Step 3: Implement**
- [ ] **Step 4: Full suite PASS** — `dotnet test Warhammer.Tests --nologo` (existing GmTableMode/WarRoom tests must still pass with the new parameter)
- [ ] **Step 5: Commit** — `refactor: CampaignMap takes WorldSurfaceSettings`

---

### Task 7: Map Generator controls

**Files:**
- Modify: `T'au/Components/Pages/Campaign/MapGenerator.razor`
- Test: `Warhammer.Tests/Components/MapGeneratorTests.cs`

Changes:
- Fields `_seaLevel, _landmass, _rivers, _ruggedness, _features` loaded from / saved to the matching `Campaign` props; preview calls `topoMapInterop.drawById("map-gen-canvas", CurrentSettings())` where `CurrentSettings()` builds a `WorldSurfaceSettings` from the page fields.
- Control order: Seed, Biome, Sea level (`id="mg-sea"`, shown as `"{0:P0}"`), Landmass `<select id="mg-landmass">` (Continent / Archipelago / Pangea / Random), Rivers (`id="mg-rivers"`), Ruggedness, Features, Offset X, Offset Y, then a collapsed `<details class="mg-advanced"><summary>ADVANCED</summary>` holding Scale, Octaves, Lacunarity, Persistence.
- Biome `<select id="mg-biome">` options from `BiomeCatalog.All`; on change also set `_seaLevel`/`_rivers` to that Biome's defaults.
- `.mg-canvas` gets `aspect-ratio: 16 / 9; height: auto; max-height: 100%;` so the preview has the Campaign Map's proportions (spec anti-goal).

- [ ] **Step 1: Write failing tests** (same setup as `CampaignEditorGmGateTests`: Loose JS, real `CampaignManagerService` on `SqliteTestBase`)

```csharp
[Fact] public void Choosing_a_biome_applies_its_sea_level_and_rivers()
// change #mg-biome to "desert" → #mg-sea value "0.08", #mg-rivers value "2"
[Fact] public void Apply_saves_the_world_surface_controls()
// set sea 0.6, landmass archipelago, rivers 7, click Apply → reloaded campaign has those values
[Fact] public void Noise_parameters_live_under_advanced()
// cut.Find(".mg-advanced") contains the Octaves/Lacunarity/Persistence/Scale inputs; details has no "open" attribute
[Fact] public void Legacy_biome_shows_as_agri()   // Review Focus 3: campaign saved with MapBiome "forge" → #mg-biome value "agri"
```

- [ ] **Step 2: Run, expect FAIL** — `dotnet test Warhammer.Tests --filter "MapGeneratorTests" --nologo`
- [ ] **Step 3: Implement**
- [ ] **Step 4: Full suite PASS**
- [ ] **Step 5: Commit** — `feat: world-level controls on the Map Generator`

---

### Task 8: Visual check in the real app

- [ ] **Step 1:** `dotnet run --launch-profile https --project "T'au/Warhammer.csproj"` (background), open `/map-generator`.
- [ ] **Step 2:** For each of the nine Biomes: confirm visible sea at its default sea level (except near-dry desert/tomb), at least one river reaching water when Rivers > 0, hill-shade on highland, features of the Biome's kinds, labels on `/campaign-editor` still readable over the busiest setting (Features 1.0). Drag Sea level 0 → 85% and Rivers 0 → 12 and confirm the preview keeps up.
- [ ] **Step 3:** Confirm the Campaign Editor map, War Room and GM Table Mode show the same world as the generator preview for the applied settings.
- [ ] **Step 4:** Fix anything found (own commit per fix), stop the server, run `node --test Warhammer.Tests/js/` and `dotnet test Warhammer.Tests --nologo` once more, both PASS.
