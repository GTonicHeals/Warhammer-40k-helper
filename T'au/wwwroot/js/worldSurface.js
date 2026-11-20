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

    // Landmass mask in (0, 1]; never exactly 0 so the sea-level quantile has no big ties.
    function makeMask(s) {
        var floor = 0.02;
        if (s.landmass === 'random') return function () { return 1; };

        if (s.landmass === 'archipelago') {
            var rng = mulberry32(s.seed * 7919 + 13);
            var blobs = [];
            for (var b = 0; b < 8; b++) {
                blobs.push({ x: 0.12 + rng() * 0.76, y: 0.12 + rng() * 0.76, r: 0.07 + rng() * 0.11 });
            }
            var detail = createNoise(s.seed + 101);
            return function (nx, ny) {
                var m = 0;
                for (var i = 0; i < blobs.length; i++) {
                    var dx = nx - blobs[i].x, dy = ny - blobs[i].y;
                    var d = Math.sqrt(dx * dx + dy * dy) / blobs[i].r;
                    var v = 1 - smoothstep(0.5, 1.3, d);
                    if (v > m) m = v;
                }
                var n = (detail(nx * 9 + 3.1, ny * 9 + 7.7) + 1) / 2;   // higher-frequency breakup
                return floor + (1 - floor) * m * (0.55 + 0.9 * n);
            };
        }

        // continent: one radial falloff; pangea: broad falloff covering most of the map.
        var inner = s.landmass === 'pangea' ? 0.38 : 0.18;
        var outer = s.landmass === 'pangea' ? 0.72 : 0.50;
        return function (nx, ny) {
            var dx = nx - 0.5, dy = ny - 0.5;
            var d = Math.sqrt(dx * dx + dy * dy);
            return floor + (1 - floor) * (1 - smoothstep(inner, outer, d));
        };
    }

    // Returns field(nx, ny) -> height in roughly [0, 1] for already-normalised settings.
    function makeField(s) {
        var noise = createNoise(s.seed);
        var mask = makeMask(s);
        var rug = s.ruggedness;
        return function (nx, ny) {
            var fx = nx - 0.5, fy = ny - 0.5;      // same centring as topoMap.js
            var smooth = 0, ridged = 0, amp = 1.0, freq = s.scale, total = 0;
            for (var i = 0; i < s.octaves; i++) {
                var n = noise(fx * freq + s.offsetX, fy * freq + s.offsetY);
                smooth += amp * n;
                ridged += amp * (1 - Math.abs(n) * 2);   // sharp crests where noise crosses 0
                total  += amp;
                amp    *= s.persistence;
                freq   *= s.lacunarity;
            }
            var f = (smooth / total + 1) / 2;
            var r = clamp((ridged / total + 1) / 2, 0, 1);
            var base = f + (r - f) * rug;
            return base * mask(nx, ny);
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

    // Depressions that collect enough flow become lakes (2). Sea cells stay 1.
    function markLakes(water, height, filled, acc) {
        for (var i = 0; i < water.length; i++) {
            if (water[i] === 0 && filled[i] - height[i] > LAKE_DEPTH && acc[i] > LAKE_FLOW) water[i] = 2;
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

    function buildWorld(settings) {
        var s = normalise(settings);
        var height = buildHeights(s);
        var seaLevel = seaThreshold(height, s.seaLevel);
        var water = classifyWater(height, seaLevel);
        var flood = fillDepressions(height, water);
        var acc = accumulateFlow(flood.drain, flood.order);
        markLakes(water, height, flood.filled, acc);
        return {
            size: GRID,
            height: height,
            filled: flood.filled,
            seaLevel: seaLevel,
            water: water,
            rivers: traceRivers(s, height, water, flood.drain, acc),
            features: [],   // Task 4
            roads: []
        };
    }

    var api = {
        GRID: GRID,
        normalise: normalise,
        makeSampler: makeSampler,
        sampleHeight: sampleHeight,
        buildWorld: buildWorld,
        _classifyWater: classifyWater
    };
    if (typeof module !== 'undefined') module.exports = api; else window.worldSurface = api;
})();
