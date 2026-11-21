# World Surface Generation — Design

## Intent

The Game Master finds the generated map behind the Campaign Map barren, and cannot get an ocean or a river out of it. The **World Surface** (see GLOSSARY.md) should read as a planet: coasts, seas, rivers that run to them, relief, and scattered features that fit the **Biome**.

Why oceans never appear today: `topoMap.js` averages fBm octaves, so heights cluster around 0.5 and the "deep ocean" band (< 0.30) is almost never reached. No slider moves the waterline. Rivers do not exist in the code at all.

## Goals

- Sea level the GM controls directly, and that reliably produces water.
- Rivers that start in highland and run downhill to the sea or a lake; inland lakes.
- Hill-shaded relief.
- Scatter features (craters, vegetation, ruins/spires, roads and biome specials), styled per Biome.
- Nine Biomes: Agri, Death, Ice, Ash (existing), Desert, Jungle, Hive, Sept, Tomb (new).
- Plain-language controls replacing raw noise parameters.

## Non-goals

- No gameplay meaning: nothing on the Campaign Map reads the World Surface, and the World Surface does not read territories or their TerrainType.
- No hand-painted rivers or lakes.
- No "Forge World" Biome (name already belongs to a territory TerrainType).

## Anti-goals

- A surface so busy the territory nodes and labels become hard to read.
- The Map Generator preview showing a different world from the Campaign Map for the same settings.

## Decisions (from the 2026-11-20 grilling)

1. Scenery only (above).
2. Controls on the Map Generator: **Seed**, **Biome**, **Sea level**, **Landmass** (continent / archipelago / pangea / random), **Rivers**, **Ruggedness**, **Features**, **Offset X/Y**. Octaves, Lacunarity, Persistence and Scale move into a collapsed **Advanced** section and keep their stored values.
3. Features as listed in Goals; each Biome styles its water, rivers and features. Roads are the first thing to cut if it looks busy.
4. Rivers are generated only.
5. Existing campaigns (including Kethra: The Willing) will look different with the same seed. Accepted.
6. Choosing a Biome **on the Map Generator** resets Sea level and Rivers to that Biome's defaults. The Campaign Editor's quick Biome dropdown does not touch them.

## Approaches considered (rivers — the only real fork)

| Approach | Trade-off |
|---|---|
| **Flow simulation on a fixed analysis grid** (depression fill → downhill flow → trace from highland sources) — **chosen** | Rivers always connect downhill to sea or a lake; lakes fall out of the depression fill for free. Costs a priority-queue pass, so it runs on a fixed 256×256 grid, not per pixel. |
| Noise "ridge" lines drawn as rivers | Cheap, but rivers don't connect to the sea and ignore terrain. Fails the user's complaint. |
| Greedy downhill walk on raw heights | Cheap, but walks stall in every pit and stop mid-land. |

Sea level as a **coverage fraction**: the slider value is the share of the map underwater, and the height threshold is found from the analysis grid's height distribution (percentile). This is what makes water reliable regardless of noise parameters. (My call.)

## Shape of the build

- `T'au/wwwroot/js/worldSurface.js` — pure, deterministic world builder (heights, water, rivers, lakes, features, roads, Biome style table). No DOM. Loads in the browser as `window.worldSurface` and in Node via `module.exports`, so it is unit-tested with `node --test`.
- `T'au/wwwroot/js/topoMap.js` — renderer only: paints a `buildWorld` result onto the canvas. One call shape: `topoMapInterop.drawById(canvasId, settings)`.
- C#: five new `Campaign` columns (with migration defaults so old rows read cleanly), a `BiomeCatalog` (key, label, default sea level and rivers, used by both dropdowns), and a `WorldSurfaceSettings` record that `CampaignMap` takes in place of its eight Map* parameters.

Biome defaults (my call, tune freely):

| Key | Label | Sea level | Rivers | River style |
|---|---|---|---|---|
| agri | Agri World | 0.40 | 5 | water |
| death | Death World | 0.30 | 4 | sludge |
| ice | Ice World | 0.45 | 3 | frozen |
| ash | Ash Wastes | 0.20 | 3 | lava |
| desert | Desert World | 0.08 | 2 | dry |
| jungle | Jungle World | 0.30 | 9 | water |
| hive | Hive World | 0.25 | 2 | sludge |
| sept | Sept World | 0.35 | 4 | water |
| tomb | Tomb World | 0.15 | 0 | dry |

## Left to the builder

Exact palettes, feature glyph drawing, hill-shade strength, river widths, noise blend for Ruggedness and Landmass masks, within the constraints in the plan.
