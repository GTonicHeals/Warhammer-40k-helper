// World Surface generation: a pure, DOM-free, deterministic model of the campaign
// map backdrop. Same settings -> same World at any canvas size (fixed analysis grid).
// Scenery only: nothing here reads territory/node data.
(function () {
    'use strict';

    var GRID = 256;

    var BIOMES = ['agri', 'death', 'ice', 'ash', 'desert', 'jungle', 'hive', 'sept', 'tomb'];
    var LANDMASSES = ['continent', 'archipelago', 'pangea', 'random'];

    // ── Seeded PRNG (mulberry32) — copied from topoMap.js ────────────────────
    function mulberry32(seed) {
        return function () {
            seed |= 0; seed = seed + 0x6D2B79F5 | 0;
            var t = Math.imul(seed ^ seed >>> 15, 1 | seed);
            t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
            return ((t ^ t >>> 14) >>> 0) / 4294967296;
        };
    }

    // ── Gradient noise (Perlin-style) — copied from topoMap.js ───────────────
    function createNoise(seed) {
        var rng = mulberry32(seed);
        var perm = new Uint8Array(256);
        var i;
        for (i = 0; i < 256; i++) perm[i] = i;
        for (i = 255; i > 0; i--) {
            var j = (rng() * (i + 1)) | 0;
            var tmp = perm[i]; perm[i] = perm[j]; perm[j] = tmp;
        }
        var p = new Uint8Array(512);
        for (i = 0; i < 512; i++) p[i] = perm[i & 255];

        var GRADS = [[1,1],[-1,1],[1,-1],[-1,-1],[1,0],[-1,0],[0,1],[0,-1]];
        function fade(t) { return t * t * t * (t * (t * 6 - 15) + 10); }
        function lerp(a, b, t) { return a + t * (b - a); }
        function dot2(g, x, y) { return g[0] * x + g[1] * y; }

        return function noise2D(x, y) {
            var X = Math.floor(x) & 255;
            var Y = Math.floor(y) & 255;
            var xf = x - Math.floor(x);
            var yf = y - Math.floor(y);
            var u = fade(xf);
            var v = fade(yf);
            var g00 = GRADS[p[p[X    ] + Y    ] & 7];
            var g10 = GRADS[p[p[X + 1] + Y    ] & 7];
            var g01 = GRADS[p[p[X    ] + Y + 1] & 7];
            var g11 = GRADS[p[p[X + 1] + Y + 1] & 7];
            return lerp(
                lerp(dot2(g00, xf,     yf    ), dot2(g10, xf - 1, yf    ), u),
                lerp(dot2(g01, xf,     yf - 1), dot2(g11, xf - 1, yf - 1), u),
                v
            );
        };
    }

    // ── Settings ──────────────────────────────────────────────────────────────
    function num(v, fallback) {
        v = typeof v === 'string' && v.trim() !== '' ? Number(v) : v;
        return (typeof v === 'number' && isFinite(v)) ? v : fallback;
    }
    function clamp(v, lo, hi) { return v < lo ? lo : (v > hi ? hi : v); }

    function normalise(settings) {
        var s = settings || {};
        return {
            seed:        Math.floor(num(s.seed, 1)),
            biome:       BIOMES.indexOf(s.biome) >= 0 ? s.biome : 'agri',
            landmass:    LANDMASSES.indexOf(s.landmass) >= 0 ? s.landmass : 'continent',
            seaLevel:    clamp(num(s.seaLevel, 0.35), 0, 0.85),
            rivers:      Math.round(clamp(num(s.rivers, 4), 0, 12)),
            ruggedness:  clamp(num(s.ruggedness, 0.5), 0, 1),
            features:    clamp(num(s.features, 0.5), 0, 1),
            octaves:     Math.round(clamp(num(s.octaves, 4), 1, 8)),
            lacunarity:  num(s.lacunarity, 2.0) || 2.0,
            persistence: num(s.persistence, 0.5) || 0.5,
            scale:       num(s.scale, 2.0) || 2.0,
            offsetX:     num(s.offsetX, 0),
            offsetY:     num(s.offsetY, 0)
        };
    }

    // ── Height field ──────────────────────────────────────────────────────────
    function smoothstep(a, b, x) {
        var t = clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    var BIAS_WEIGHT = 0.3;      // Landmass bias weight next to the noise (height = (noise + w*bias) / (1 + w))
    var WARP = 0.18;            // domain-warp amplitude of the bias, in map units
    var EDGE_SINK = 0.1;        // continent: how far the bias dips along the map border...
    var EDGE_WIDTH = 0.12;      // ...and over what width (map units)

    // Landmass bias, about [0, 1] (a continent dips a little below 0 along the map border):
    // where land *tends* to be. It is added to the noise, never
    // multiplied, so the noise still shapes every coast (bays, peninsulas, offshore islets)
    // and no flat floor forms where the bias is 0. Its coordinates are domain-warped by
    // low-frequency noise so the outline is not a circle. null for 'random' (noise only).
    function makeBias(s) {
        if (s.landmass === 'random') return null;
        var warpX = createNoise(s.seed + 211), warpY = createNoise(s.seed + 307);
        var rng = mulberry32(s.seed * 7919 + 13);
        function shape(d, inner, outer) { return 1 - smoothstep(inner, outer, d); }

        var radial;
        if (s.landmass === 'archipelago') {
            // one island per cell of a jittered 4x4 grid (some cells left empty): spread out,
            // never stacked into one blob.
            var blobs = [];
            for (var gy = 0; gy < 4; gy++) {
                for (var gx = 0; gx < 4; gx++) {
                    var keep = rng() < 0.75, bx = (gx + 0.2 + rng() * 0.6) / 4, by = (gy + 0.2 + rng() * 0.6) / 4;
                    var br = 0.06 + rng() * 0.07, amp = 0.55 + rng() * 0.45;
                    if (keep) blobs.push([bx, by, br, amp]);
                }
            }
            radial = function (x, y) {
                var m = 0;
                for (var i = 0; i < blobs.length; i++) {
                    var dx = x - blobs[i][0], dy = y - blobs[i][1];
                    var v = blobs[i][3] * shape(Math.sqrt(dx * dx + dy * dy) / blobs[i][2], 0.25, 1.3);
                    if (v > m) m = v;
                }
                return m;
            };
        } else {
            // continent: one landmass, centre nudged per seed; pangea: covers most of the map.
            var cx = 0.5 + (rng() - 0.5) * 0.12, cy = 0.5 + (rng() - 0.5) * 0.12;
            var inner = s.landmass === 'pangea' ? 0.30 : 0.12;
            var outer = s.landmass === 'pangea' ? 0.75 : 0.55;
            radial = function (x, y) {
                var dx = x - cx, dy = y - cy;
                return shape(Math.sqrt(dx * dx + dy * dy), inner, outer);
            };
        }
        // The bias is smooth and costly (warp noise, blob loop), so it is evaluated once per
        // grid cell and interpolated bilinearly: exact at cell centres, cheap per pixel.
        // A continent also sinks a strip along the map border, so it sits in open sea
        // instead of running off the edge.
        var edgeSink = s.landmass === 'continent' ? EDGE_SINK : 0;
        var n = GRID, last = n - 1, grid = new Float32Array(n * n);
        for (var y = 0; y < n; y++) {
            for (var x = 0; x < n; x++) {
                var nx = x / last, ny = y / last;
                var wx = nx + WARP * (warpX(nx * 2.3 + 5.3, ny * 2.3 + 1.9) + 0.6 * warpX(nx * 6.1 + 2.2, ny * 6.1 + 8.4) + 0.3 * warpX(nx * 13.7 + 4.1, ny * 13.7 + 0.6));
                var wy = ny + WARP * (warpY(nx * 2.3 + 3.7, ny * 2.3 + 7.1) + 0.6 * warpY(nx * 6.1 + 9.6, ny * 6.1 + 4.8) + 0.3 * warpY(nx * 13.7 + 7.7, ny * 13.7 + 3.3));
                var edge = Math.min(nx, 1 - nx, ny, 1 - ny);
                grid[y * n + x] = radial(wx, wy) - edgeSink * (1 - smoothstep(0, EDGE_WIDTH, edge));
            }
        }
        return function (nx, ny) {
            var gx = clamp(nx, 0, 1) * last, gy = clamp(ny, 0, 1) * last;
            var x0 = Math.min(Math.floor(gx), last - 1), y0 = Math.min(Math.floor(gy), last - 1);
            var tx = gx - x0, ty = gy - y0, i = y0 * n + x0;
            var top = grid[i] + (grid[i + 1] - grid[i]) * tx;
            var bot = grid[i + n] + (grid[i + n + 1] - grid[i + n]) * tx;
            return top + (bot - top) * ty;
        };
    }

    // Returns field(nx, ny) -> height in roughly [0, 1] for already-normalised settings.
    function makeField(s) {
        var noise = createNoise(s.seed);
        var bias = makeBias(s);
        var rug = s.ruggedness;
        // Per-octave shift and rotation so the octaves' lattices don't line up: unshifted they
        // all cross zero at the map centre and stack a peak there, and axis-aligned lattice
        // lines show as faint straight seams in the hill-shade.
        var oct = [], rng = mulberry32(s.seed * 31337 + 5);
        for (var o = 0; o < s.octaves; o++) {
            var angle = 0.5 + o * 2.4;
            oct.push([rng() * 64, rng() * 64, Math.cos(angle), Math.sin(angle)]);
        }
        return function (nx, ny) {
            var fx = nx - 0.5, fy = ny - 0.5;      // same centring as topoMap.js
            var smooth = 0, ridged = 0, amp = 1.0, freq = s.scale, total = 0;
            for (var i = 0; i < s.octaves; i++) {
                var q = oct[i], px = fx * freq + s.offsetX, py = fy * freq + s.offsetY;
                var n = noise(px * q[2] - py * q[3] + q[0], px * q[3] + py * q[2] + q[1]);
                smooth += amp * n;
                ridged += amp * (1 - Math.abs(n) * 2);   // sharp crests where noise crosses 0
                total  += amp;
                amp    *= s.persistence;
                freq   *= s.lacunarity;
            }
            var f = (smooth / total + 1) / 2;
            var r = clamp((ridged / total + 1) / 2, 0, 1);
            var base = f + (r - f) * rug;
            return bias ? (base + BIAS_WEIGHT * bias(nx, ny)) / (1 + BIAS_WEIGHT) : base;
        };
    }

    // Build once per draw, then call per pixel: makeSampler(settings)(nx, ny) -> height.
    function makeSampler(settings) {
        return makeField(normalise(settings));
    }

    // Convenience for one-off lookups; use makeSampler in loops.
    function sampleHeight(settings, nx, ny) {
        return makeSampler(settings)(nx, ny);
    }

    // ── World ─────────────────────────────────────────────────────────────────
    // Stage 1: heights over the fixed grid.
    function buildHeights(s) {
        var field = makeField(s);
        var height = new Float32Array(GRID * GRID);
        for (var y = 0; y < GRID; y++) {
            for (var x = 0; x < GRID; x++) {
                height[y * GRID + x] = field(x / (GRID - 1), y / (GRID - 1));
            }
        }
        return height;
    }

    // Stage 2: the sea level is a quantile of the heights, so the slider means "share underwater".
    function seaThreshold(height, share) {
        var sorted = Float32Array.from(height).sort();
        return sorted[Math.min(sorted.length - 1, Math.floor(share * sorted.length))];
    }

    // Stage 3: below-threshold cells connected to the grid border are sea (1); the rest are lakes (2).
    function classifyWater(height, threshold) {
        var n = GRID;
        var water = new Uint8Array(n * n);
        var stack = [];
        var i;
        for (i = 0; i < height.length; i++) if (height[i] < threshold) water[i] = 2;

        function seed(idx) { if (water[idx] === 2) { water[idx] = 1; stack.push(idx); } }
        for (i = 0; i < n; i++) { seed(i); seed((n - 1) * n + i); seed(i * n); seed(i * n + n - 1); }

        while (stack.length) {
            var c = stack.pop();
            var x = c % n, y = (c - x) / n;
            if (x > 0)     seed(c - 1);
            if (x < n - 1) seed(c + 1);
            if (y > 0)     seed(c - n);
            if (y < n - 1) seed(c + n);
        }
        return water;
    }

    // Stage 4: priority-flood depression fill, seeded from the grid border and sea cells.
    // Cells pop lowest-first; each cell's drain is the neighbour that flooded it, so every
    // cell (flats and filled basins included) has a loop-free path to the border or sea.
    // Returns { filled, drain (-1 for outlets), order (pop order, parents before children) }.
    function fillDepressions(height, water) {
        var n = GRID, total = n * n;
        var filled = new Float32Array(total);
        var drain = new Int32Array(total).fill(-1);
        var seen = new Uint8Array(total);
        var order = new Int32Array(total);
        var heap = new Int32Array(total), size = 0, popped = 0;

        function push(idx) {
            var k = size++;
            while (k > 0) {
                var up = (k - 1) >> 1;
                if (filled[heap[up]] <= filled[idx]) break;
                heap[k] = heap[up]; k = up;
            }
            heap[k] = idx;
        }
        function pop() {
            var top = heap[0], last = heap[--size];
            var k = 0, half = size >> 1;
            while (k < half) {
                var c = 2 * k + 1;
                if (c + 1 < size && filled[heap[c + 1]] < filled[heap[c]]) c++;
                if (filled[last] <= filled[heap[c]]) break;
                heap[k] = heap[c]; k = c;
            }
            heap[k] = last;
            return top;
        }

        var i, x, y;
        for (i = 0; i < total; i++) {
            x = i % n; y = (i - x) / n;
            if (water[i] === 1 || x === 0 || y === 0 || x === n - 1 || y === n - 1) {
                seen[i] = 1; filled[i] = height[i]; push(i);
            }
        }
        var DX = [-1, 1, 0, 0, -1, 1, -1, 1], DY = [0, 0, -1, 1, -1, -1, 1, 1];
        while (size > 0) {
            var c = pop();
            order[popped++] = c;
            x = c % n; y = (c - x) / n;
            for (var d = 0; d < 8; d++) {
                var nx = x + DX[d], ny = y + DY[d];
                if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                var nb = ny * n + nx;
                if (seen[nb]) continue;
                seen[nb] = 1;
                drain[nb] = c;
                filled[nb] = height[nb] > filled[c] ? height[nb] : filled[c];
                push(nb);
            }
        }
        return { filled: filled, drain: drain, order: order };
    }

    // Flow accumulation: cells draining through each cell (itself included).
    function accumulateFlow(drain, order) {
        var acc = new Float32Array(drain.length).fill(1);
        for (var k = order.length - 1; k >= 0; k--) {
            var c = order[k];
            if (drain[c] >= 0) acc[drain[c]] += acc[c];
        }
        return acc;
    }

    var LAKE_DEPTH = 0.004;     // filled - height needed for a depression to hold water
    var LAKE_FLOW = 25;         // ...and this much flow must pool into it
    var RIVER_MIN_CELLS = 12;
    var SOURCE_SPACING = 20;

    // Depressions (4-connected land cells with filled - height > LAKE_DEPTH) whose
    // peak flow accumulation exceeds LAKE_FLOW become lakes (2) as a whole basin.
    // A depression already holding below-sea inland water (2) is a lake too. Other
    // depressions stay land. Sea cells stay 1.
    function markLakes(water, height, filled, acc) {
        var n = GRID, total = n * n;
        var seen = new Uint8Array(total);
        var stack = [];
        function inDep(c) { return water[c] !== 1 && filled[c] - height[c] > LAKE_DEPTH; }
        for (var s = 0; s < total; s++) {
            if (seen[s] || !inDep(s)) continue;
            var cells = [s], peak = acc[s], pocket = water[s] === 2;
            seen[s] = 1;
            stack.length = 0;
            stack.push(s);
            while (stack.length) {
                var c = stack.pop(), x = c % n;
                var nb = [x > 0 ? c - 1 : -1, x < n - 1 ? c + 1 : -1, c >= n ? c - n : -1, c < total - n ? c + n : -1];
                for (var k = 0; k < 4; k++) {
                    var m = nb[k];
                    if (m < 0 || seen[m] || !inDep(m)) continue;
                    seen[m] = 1;
                    cells.push(m);
                    stack.push(m);
                    if (acc[m] > peak) peak = acc[m];
                    if (water[m] === 2) pocket = true;
                }
            }
            if (pocket || peak > LAKE_FLOW) for (var j = 0; j < cells.length; j++) water[cells[j]] = 2;
        }
    }

    // Stage 5: trace rivers downstream from seeded high-land sources.
    function traceRivers(s, height, water, drain, acc) {
        var n = GRID, total = n * n;
        var rivers = [];
        if (s.rivers === 0) return rivers;

        var landHeights = [];
        var i;
        for (i = 0; i < total; i++) if (water[i] === 0) landHeights.push(height[i]);
        if (!landHeights.length) return rivers;
        var sorted = Float32Array.from(landHeights).sort();
        var highland = sorted[Math.floor(sorted.length * 0.7)];

        var candidates = [];
        for (i = 0; i < total; i++) if (water[i] === 0 && height[i] >= highland) candidates.push(i);
        var rng = mulberry32(s.seed * 104729 + 7);       // independent of the noise streams
        for (i = candidates.length - 1; i > 0; i--) {
            var j = (rng() * (i + 1)) | 0;
            var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
        }

        var onRiver = new Uint8Array(total);
        var sources = [];
        for (var k = 0; k < candidates.length && rivers.length < s.rivers; k++) {
            var src = candidates[k];
            if (onRiver[src]) continue;
            var sx = src % n, sy = (src - sx) / n, near = false;
            for (var q = 0; q < sources.length; q++) {
                var dx = sources[q][0] - sx, dy = sources[q][1] - sy;
                if (dx * dx + dy * dy < SOURCE_SPACING * SOURCE_SPACING) { near = true; break; }
            }
            if (near) continue;
            sources.push([sx, sy]);

            // Follow the drain until water, the border (no drain) or an earlier river.
            var cells = [src], c = src;
            while (water[c] === 0 && drain[c] >= 0 && !(cells.length > 1 && onRiver[c])) {
                c = drain[c];
                cells.push(c);
            }
            if (cells.length < RIVER_MIN_CELLS) continue;

            var points = [];
            for (var m = 0; m < cells.length; m++) {
                onRiver[cells[m]] = 1;
                var cx = cells[m] % n;
                points.push([cx / (n - 1), (cells[m] - cx) / n / (n - 1)]);
            }
            var mouthFlow = acc[cells[Math.max(0, cells.length - 2)]];
            var width = 1 + 3 * clamp(Math.log(mouthFlow) / Math.log(4000), 0, 1);
            rivers.push({ points: points, width: width });
        }
        return rivers;
    }

    // ── Biome styles ──────────────────────────────────────────────────────────
    // bands: [maxLandHeight01, r, g, b] in ascending order; 0 = shoreline, 1 = highest peak,
    // so colours track the sea level. The renderer uses the band colours as anchors and
    // blends smoothly between them. sea/shallows/lake colour the water classes.
    // river: how a river is painted. features: scatter kinds. roads: join settlements.
    var BIOME_STYLES = {
        agri: {
            bands: [[0.04, 180, 160, 80], [0.30, 55, 120, 40], [0.55, 90, 150, 30], [0.80, 110, 80, 40], [1, 130, 120, 110]],
            sea: [15, 55, 120], shallows: [40, 100, 160], lake: [50, 110, 170], river: 'water',
            features: ['forest', 'ruins', 'crater'], roads: true
        },
        death: {
            bands: [[0.05, 120, 10, 80], [0.35, 40, 110, 20], [0.60, 130, 150, 20], [0.82, 80, 80, 75], [1, 100, 100, 90]],
            sea: [30, 5, 50], shallows: [55, 10, 80], lake: [70, 20, 90], river: 'sludge',
            features: ['forest', 'crater', 'ruins'], roads: false
        },
        ice: {
            bands: [[0.08, 160, 200, 220], [0.35, 190, 220, 240], [0.60, 150, 190, 230], [0.85, 220, 235, 245], [1, 245, 250, 255]],
            sea: [10, 30, 70], shallows: [30, 70, 130], lake: [60, 110, 170], river: 'frozen',
            features: ['crater', 'ruins'], roads: true
        },
        ash: {
            bands: [[0.15, 30, 25, 20], [0.35, 55, 45, 40], [0.55, 90, 30, 15], [0.75, 140, 55, 10], [0.90, 160, 120, 80], [1, 200, 190, 170]],
            sea: [10, 10, 10], shallows: [30, 25, 20], lake: [45, 20, 10], river: 'lava',
            features: ['crater', 'ruins', 'fissure'], roads: false
        },
        desert: {
            bands: [[0.06, 225, 215, 190], [0.30, 215, 180, 110], [0.60, 200, 150, 80], [0.85, 170, 110, 60], [1, 140, 90, 60]],
            sea: [235, 230, 215], shallows: [225, 210, 175], lake: [70, 140, 150], river: 'dry',
            features: ['dune', 'oasis', 'ruins', 'crater'], roads: false
        },
        jungle: {
            bands: [[0.06, 90, 80, 50], [0.30, 25, 85, 35], [0.60, 15, 65, 30], [0.85, 30, 50, 25], [1, 70, 85, 60]],
            sea: [20, 60, 70], shallows: [50, 90, 80], lake: [75, 90, 50], river: 'water',
            features: ['forest', 'swamp', 'ruins'], roads: false
        },
        hive: {
            bands: [[0.06, 85, 85, 80], [0.35, 105, 105, 105], [0.65, 125, 120, 115], [0.88, 90, 90, 95], [1, 150, 150, 155]],
            sea: [55, 60, 30], shallows: [85, 90, 40], lake: [80, 85, 45], river: 'sludge',
            features: ['spire', 'ruins'], roads: true
        },
        sept: {
            bands: [[0.05, 230, 200, 130], [0.35, 200, 150, 70], [0.65, 170, 120, 55], [0.88, 150, 100, 70], [1, 190, 170, 150]],
            sea: [15, 100, 110], shallows: [50, 160, 160], lake: [60, 150, 160], river: 'water',
            features: ['settlement', 'forest'], roads: true
        },
        tomb: {
            bands: [[0.06, 35, 40, 35], [0.35, 20, 28, 22], [0.65, 28, 40, 30], [0.88, 40, 52, 42], [1, 60, 75, 62]],
            sea: [12, 16, 14], shallows: [25, 32, 28], lake: [20, 60, 35], river: 'dry',
            features: ['pyramid', 'fissure', 'crater'], roads: false
        }
    };

    // ── Features and roads ────────────────────────────────────────────────────
    var FEATURE_SPACING = 8;      // grid cells
    var ROAD_REACH = 0.3;         // map units
    var WET_KINDS = { oasis: 1, swamp: 1 };
    var HUB_KINDS = { ruins: 1, spire: 1, settlement: 1 };

    // Land cells (0) touching water (8-neighbourhood): where oases and swamps may sit.
    function shoreCells(water) {
        var n = GRID, out = [];
        for (var y = 0; y < n; y++) {
            for (var x = 0; x < n; x++) {
                if (water[y * n + x] !== 0) continue;
                var wet = false;
                for (var dy = -1; dy <= 1 && !wet; dy++) {
                    for (var dx = -1; dx <= 1; dx++) {
                        var nx = x + dx, ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < n && ny < n && water[ny * n + nx] !== 0) { wet = true; break; }
                    }
                }
                if (wet) out.push(y * n + x);
            }
        }
        return out;
    }

    // Seeded rejection sampling on land, min spacing FEATURE_SPACING cells.
    function scatterFeatures(s, water) {
        var out = [];
        var want = Math.round(s.features * 60);
        if (want === 0) return out;
        var n = GRID;
        var shore = shoreCells(water);
        var kinds = BIOME_STYLES[s.biome].features.filter(function (k) { return !WET_KINDS[k] || shore.length > 0; });
        var rng = mulberry32(s.seed * 15485863 + 31);          // independent of noise and river streams
        var cells = [];
        for (var tries = 0; out.length < want && tries < want * 60; tries++) {
            var kind = kinds[(rng() * kinds.length) | 0];
            var c;
            if (WET_KINDS[kind]) {
                c = shore[(rng() * shore.length) | 0];
            } else {
                c = ((rng() * n) | 0) * n + ((rng() * n) | 0);
                if (water[c] !== 0) continue;
            }
            var x = c % n, y = (c - x) / n, near = false;
            for (var q = 0; q < cells.length; q++) {
                var dx = cells[q][0] - x, dy = cells[q][1] - y;
                if (dx * dx + dy * dy < FEATURE_SPACING * FEATURE_SPACING) { near = true; break; }
            }
            if (near) continue;
            cells.push([x, y]);
            out.push({ kind: kind, x: x / (n - 1), y: y / (n - 1), size: 0.5 + rng() });
        }
        return out;
    }

    // True when the straight segment a->b, sampled at grid resolution, touches no sea.
    function segmentClear(water, a, b) {
        var n = GRID;
        var steps = Math.max(1, Math.ceil(Math.hypot(b.x - a.x, b.y - a.y) * (n - 1)));
        for (var i = 0; i <= steps; i++) {
            var t = i / steps;
            var cx = Math.round((a.x + (b.x - a.x) * t) * (n - 1));
            var cy = Math.round((a.y + (b.y - a.y) * t) * (n - 1));
            if (water[cy * n + cx] === 1) return false;
        }
        return true;
    }

    // Each hub links to its nearest unconnected neighbour within reach, if the way is dry of sea.
    function buildRoads(s, features, water) {
        var roads = [];
        if (!BIOME_STYLES[s.biome].roads) return roads;
        var cap = Math.round(s.features * 8);
        var hubs = features.filter(function (f) { return HUB_KINDS[f.kind]; });
        var linked = new Uint8Array(hubs.length);
        for (var i = 0; i < hubs.length && roads.length < cap; i++) {
            if (linked[i]) continue;
            var best = [];
            for (var j = 0; j < hubs.length; j++) {
                if (j === i || linked[j]) continue;
                var d = Math.hypot(hubs[i].x - hubs[j].x, hubs[i].y - hubs[j].y);
                if (d <= ROAD_REACH) best.push([d, j]);
            }
            best.sort(function (p, q) { return p[0] - q[0] || p[1] - q[1]; });
            for (var k = 0; k < best.length; k++) {
                var other = hubs[best[k][1]];
                if (!segmentClear(water, hubs[i], other)) continue;
                linked[i] = 1; linked[best[k][1]] = 1;
                roads.push([[hubs[i].x, hubs[i].y], [other.x, other.y]]);
                break;
            }
        }
        return roads;
    }

    function buildWorld(settings) {
        var s = normalise(settings);
        var height = buildHeights(s);
        var seaLevel = seaThreshold(height, s.seaLevel);
        var water = classifyWater(height, seaLevel);
        var flood = fillDepressions(height, water);
        var acc = accumulateFlow(flood.drain, flood.order);
        markLakes(water, height, flood.filled, acc);
        var features = scatterFeatures(s, water);
        return {
            size: GRID,
            height: height,
            filled: flood.filled,
            seaLevel: seaLevel,
            water: water,
            rivers: traceRivers(s, height, water, flood.drain, acc),
            features: features,
            roads: buildRoads(s, features, water)
        };
    }

    var api = {
        GRID: GRID,
        normalise: normalise,
        makeSampler: makeSampler,
        sampleHeight: sampleHeight,
        buildWorld: buildWorld,
        BIOME_STYLES: BIOME_STYLES,
        createNoise: createNoise,        // seeded gradient noise and PRNG, for renderer-only texture
        mulberry32: mulberry32,
        _classifyWater: classifyWater
    };
    if (typeof module !== 'undefined') module.exports = api; else window.worldSurface = api;
})();
