window.topoMapInterop = (function () {

    // ── Seeded PRNG (mulberry32) ──────────────────────────────────────────────
    function mulberry32(seed) {
        return function () {
            seed |= 0; seed = seed + 0x6D2B79F5 | 0;
            var t = Math.imul(seed ^ seed >>> 15, 1 | seed);
            t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
            return ((t ^ t >>> 14) >>> 0) / 4294967296;
        };
    }

    // ── Self-contained gradient noise (Perlin-style, no CDN) ─────────────────
    // Returns a noise2D(x, y) function seeded from the given integer.
    function createNoise(seed) {
        var rng = mulberry32(seed);

        // Shuffle a permutation table with the seeded PRNG
        var perm = new Uint8Array(256);
        for (var i = 0; i < 256; i++) perm[i] = i;
        for (var i = 255; i > 0; i--) {
            var j = (rng() * (i + 1)) | 0;
            var tmp = perm[i]; perm[i] = perm[j]; perm[j] = tmp;
        }
        // Double the table to avoid index wrapping
        var p = new Uint8Array(512);
        for (var i = 0; i < 512; i++) p[i] = perm[i & 255];

        // 8 unit gradient vectors for 2D
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

    // ── WH40k biome colour bands ──────────────────────────────────────────────
    // Each band: [heightThreshold, r, g, b]. Heights in [0,1]; first match wins.
    var BIOMES = {
        agri: [
            [0.30,  15,  55, 120],   // deep ocean
            [0.38,  40, 100, 160],   // shallow coast
            [0.42, 180, 160,  80],   // sandy shore
            [0.58,  55, 120,  40],   // green plains
            [0.70,  90, 150,  30],   // crop fields
            [0.82, 110,  80,  40],   // brown ridge
            [1.00, 130, 120, 110]    // grey peak
        ],
        death: [
            [0.28,  30,   5,  50],   // dark purple depths
            [0.38,  55,  10,  80],   // deep murk
            [0.45, 120,  10,  80],   // toxic shore
            [0.60,  40, 110,  20],   // toxic green plains
            [0.72, 130, 150,  20],   // sickly yellow mid
            [0.84,  80,  80,  75],   // grey rock
            [1.00, 100, 100,  90]    // ash peak
        ],
        ice: [
            [0.32,  10,  30,  70],   // dark arctic ocean
            [0.42,  30,  70, 130],   // cold shallows
            [0.50, 160, 200, 220],   // ice shelf
            [0.65, 190, 220, 240],   // pale glacier
            [0.78, 150, 190, 230],   // glacier blue
            [0.90, 220, 235, 245],   // snowfield
            [1.00, 245, 250, 255]    // white peak
        ],
        ash: [
            [0.25,  10,  10,  10],   // black ash sea
            [0.38,  30,  25,  20],   // dark grey ash
            [0.50,  55,  45,  40],   // mid grey cinder
            [0.63,  90,  30,  15],   // cinder red
            [0.75, 140,  55,  10],   // orange glow
            [0.87, 160, 120,  80],   // pale hot rock
            [1.00, 200, 190, 170]    // pale dust peak
        ]
    };

    function getBandColor(biomeKey, height) {
        var bands = BIOMES[biomeKey] || BIOMES.agri;
        for (var i = 0; i < bands.length; i++) {
            if (height <= bands[i][0]) return bands[i];
        }
        return bands[bands.length - 1];
    }

    // Multi-octave fBm sample → [0, 1]
    function sample(noise2D, nx, ny, opts) {
        var octaves     = (opts && opts.octaves)     || 4;
        var lacunarity  = (opts && opts.lacunarity)  || 2.0;
        var persistence = (opts && opts.persistence) || 0.5;
        var scale       = (opts && opts.scale)       || 2.0;
        var offsetX     = (opts && opts.offsetX)     || 0;
        var offsetY     = (opts && opts.offsetY)     || 0;
        var v = 0, amp = 1.0, freq = scale, totalAmp = 0;
        for (var i = 0; i < octaves; i++) {
            v        += amp * noise2D(nx * freq + offsetX, ny * freq + offsetY);
            totalAmp += amp;
            amp      *= persistence;
            freq     *= lacunarity;
        }
        return (v / totalAmp + 1) / 2;
    }

    // ── Public draw function ──────────────────────────────────────────────────
    function draw(canvasEl, seed, biome, opts) {
        if (!canvasEl || !canvasEl.getContext) return;

        var noise2D  = createNoise(seed || 1);
        var biomeKey = (biome && BIOMES[biome]) ? biome : 'agri';

        // Cap the pixel buffer to avoid excessive memory on very large CSS sizes.
        var rawW = canvasEl.offsetWidth  || 800;
        var rawH = canvasEl.offsetHeight || 600;
        var MAX  = 4800;
        var scale = rawW > MAX ? MAX / rawW : 1;
        var W   = canvasEl.width  = Math.round(rawW * scale);
        var H   = canvasEl.height = Math.round(rawH * scale);
        var ctx = canvasEl.getContext('2d');
        var RES = 2;
        var cols = Math.ceil(W / RES);
        var rows = Math.ceil(H / RES);

        // Build height map
        var heights = new Float32Array(cols * rows);
        for (var row = 0; row < rows; row++) {
            for (var col = 0; col < cols; col++) {
                heights[row * cols + col] = sample(noise2D, col / cols - 0.5, row / rows - 0.5, opts);
            }
        }

        // Paint elevation bands
        for (var row = 0; row < rows; row++) {
            for (var col = 0; col < cols; col++) {
                var band = getBandColor(biomeKey, heights[row * cols + col]);
                ctx.fillStyle = 'rgb(' + band[1] + ',' + band[2] + ',' + band[3] + ')';
                ctx.fillRect(col * RES, row * RES, RES, RES);
            }
        }

        // Contour lines — mark elevation threshold crossings
        var LEVELS = [0.30, 0.38, 0.45, 0.58, 0.70, 0.82];
        ctx.strokeStyle = 'rgba(0,0,0,0.22)';
        ctx.lineWidth = 1;
        for (var li = 0; li < LEVELS.length; li++) {
            var lv = LEVELS[li];
            ctx.beginPath();
            for (var row = 0; row < rows - 1; row++) {
                for (var col = 0; col < cols - 1; col++) {
                    var h00 = heights[ row      * cols + col    ];
                    var h10 = heights[ row      * cols + col + 1];
                    var h01 = heights[(row + 1) * cols + col    ];
                    if ((h00 < lv) !== (h10 < lv)) {
                        var cx = (col + (lv - h00) / (h10 - h00)) * RES;
                        ctx.moveTo(cx, row * RES); ctx.lineTo(cx, (row + 1) * RES);
                    }
                    if ((h00 < lv) !== (h01 < lv)) {
                        var cy = (row + (lv - h00) / (h01 - h00)) * RES;
                        ctx.moveTo(col * RES, cy); ctx.lineTo((col + 1) * RES, cy);
                    }
                }
            }
            ctx.stroke();
        }

        // Vignette — darken edges so node overlays pop
        var vg = ctx.createRadialGradient(W / 2, H / 2, H * 0.3, W / 2, H / 2, H * 0.85);
        vg.addColorStop(0, 'rgba(0,0,0,0)');
        vg.addColorStop(1, 'rgba(0,0,0,0.45)');
        ctx.fillStyle = vg;
        ctx.fillRect(0, 0, W, H);
    }

    function drawById(canvasId, seed, biome, opts) {
        var el = document.getElementById(canvasId);
        if (!el) { console.warn('[topoMap] canvas not found:', canvasId); return; }
        draw(el, seed, biome, opts);
    }

    return { draw: draw, drawById: drawById };
})();
