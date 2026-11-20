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

    function buildWorld(settings) {
        var s = normalise(settings);
        var height = buildHeights(s);
        var seaLevel = seaThreshold(height, s.seaLevel);
        var water = classifyWater(height, seaLevel);
        return {
            size: GRID,
            height: height,
            seaLevel: seaLevel,
            water: water,
            rivers: [],     // Task 3
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
