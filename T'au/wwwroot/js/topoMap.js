// World Surface renderer: paints a worldSurface.buildWorld result onto a canvas.
// Painting only; the model (heights, water, rivers, features, roads, Biome styles)
// lives in worldSurface.js, which must load first.
window.topoMapInterop = (function () {
    'use strict';

    var RES = 2;                    // pixel block size for the height pass
    var MAX = 4800;                 // cap on the canvas pixel buffer width
    var BAND_BLEND = 0.02;          // soft edge between Biome colour bands (relative land height)
    var SHADE_STRENGTH = 0.22;      // hill-shade: max +/- brightness change
    var SHADE_GAIN = 0.3;           // hill-shade: slope (height per map unit) -> shade; median slope ~0.9
    var LAKE_EDGE = 0.004;          // lake depth given to non-lake grid cells (negated) for lake outlines
    var FEATURE_ALPHA = 0.55;       // features and roads stay <= 0.6 so nodes stay readable
    var ROAD_ALPHA = 0.45;
    var LIGHT_INK = [225, 210, 175];
    var DARK_INK = [30, 22, 14];

    function clamp(v, lo, hi) { return v < lo ? lo : (v > hi ? hi : v); }
    function rgb(c) { return 'rgb(' + c[0] + ',' + c[1] + ',' + c[2] + ')'; }
    function rgba(c, a) { return 'rgba(' + c[0] + ',' + c[1] + ',' + c[2] + ',' + a + ')'; }
    function luminance(c) { return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]; }

    // Ink for glyphs and roads: dark on pale Biomes, parchment on dark ones.
    function inkFor(style) {
        var mid = style.bands[Math.floor(style.bands.length / 2)];
        return luminance([mid[1], mid[2], mid[3]]) > 125 ? DARK_INK : LIGHT_INK;
    }

    // 256-entry relative-land-height -> colour table from the Biome bands (first match wins,
    // with a short blend across each band edge so the bands don't step harshly).
    function bandTable(bands) {
        var lut = new Uint8Array(256 * 3);
        for (var i = 0; i < 256; i++) {
            var rel = i / 255, k = 0, j;
            while (k < bands.length - 1 && rel > bands[k][0]) k++;
            var r = bands[k][1], g = bands[k][2], b = bands[k][3];
            for (j = 0; j < bands.length - 1; j++) {
                var edge = bands[j][0];
                if (Math.abs(rel - edge) < BAND_BLEND) {
                    var t = (rel - edge + BAND_BLEND) / (2 * BAND_BLEND), lo = bands[j], hi = bands[j + 1];
                    r = lo[1] + (hi[1] - lo[1]) * t; g = lo[2] + (hi[2] - lo[2]) * t; b = lo[3] + (hi[3] - lo[3]) * t;
                    break;
                }
            }
            lut[i * 3] = r; lut[i * 3 + 1] = g; lut[i * 3 + 2] = b;
        }
        return lut;
    }

    // ── (1) Height pass ───────────────────────────────────────────────────────
    // Sample the height at every pixel block; classify each block as land (0), sea (1)
    // or lake (2) using the analysis grid's water class, and keep a signed shoreline
    // value (negative = water) for the coastline stroke.
    function nearbyWater(world, gx, gy) {
        var n = world.size, found = 0;
        for (var dy = -1; dy <= 1; dy++) {
            for (var dx = -1; dx <= 1; dx++) {
                var x = gx + dx, y = gy + dy;
                if (x < 0 || y < 0 || x >= n || y >= n) continue;
                var w = world.water[y * n + x];
                if (w === 1) return 1;
                if (w === 2) found = 2;
            }
        }
        return found || 2;          // an isolated sub-grid pit reads as a small lake
    }

    // Per grid cell: lake depth (filled - height) on lake cells, -LAKE_EDGE elsewhere.
    // Interpolated bilinearly, its zero line gives pooled lakes smooth outlines that
    // still agree with the grid at every cell centre.
    function lakeDepthGrid(world) {
        var depth = new Float32Array(world.water.length);
        for (var i = 0; i < depth.length; i++) {
            depth[i] = world.water[i] === 2 ? Math.max(world.filled[i] - world.height[i], LAKE_EDGE) : -LAKE_EDGE;
        }
        return depth;
    }

    function sampleBlocks(world, sampler, W, H) {
        var cols = Math.ceil(W / RES), rows = Math.ceil(H / RES);
        var total = cols * rows;
        var height = new Float32Array(total);
        var cls = new Uint8Array(total);
        var shore = new Float32Array(total);    // signed distance-like value, negative = water
        var depth = new Float32Array(total);    // water depth, for shading sea and lakes
        var n = world.size, last = n - 1;
        var sea = world.seaLevel, water = world.water, lakeDepth = lakeDepthGrid(world);

        for (var r = 0; r < rows; r++) {
            var ny = Math.min(1, (r * RES + RES * 0.5) / H);
            var gyf = ny * last;
            var gy = Math.round(gyf), y0 = Math.min(Math.floor(gyf), last - 1), ty = gyf - y0;
            for (var c = 0; c < cols; c++) {
                var b = r * cols + c;
                var nx = Math.min(1, (c * RES + RES * 0.5) / W);
                var gxf = nx * last;
                var gx = Math.round(gxf), x0 = Math.min(Math.floor(gxf), last - 1), tx = gxf - x0;
                var h = Math.fround(sampler(nx, ny));
                height[b] = h;

                var i = y0 * n + x0;
                var top = lakeDepth[i] + (lakeDepth[i + 1] - lakeDepth[i]) * tx;
                var bot = lakeDepth[i + n] + (lakeDepth[i + n + 1] - lakeDepth[i + n]) * tx;
                var lake = top + (bot - top) * ty;

                if (h < sea) {
                    var wc = water[gy * n + gx];
                    cls[b] = wc !== 0 ? wc : nearbyWater(world, gx, gy);
                    shore[b] = h - sea;
                    depth[b] = sea - h;
                } else if (lake > 0) {
                    cls[b] = 2;
                    shore[b] = -lake;
                    depth[b] = lake;
                } else {
                    shore[b] = Math.min(h - sea, -lake);
                }
            }
        }
        return { cols: cols, rows: rows, height: height, cls: cls, shore: shore, depth: depth };
    }

    // Colour every block: land from the Biome bands, sea shading from shallows to deep,
    // lakes darkening slightly with depth. Writes into an RGB block buffer.
    function colourBlocks(blocks, world, style) {
        var total = blocks.cols * blocks.rows;
        var out = new Uint8ClampedArray(total * 3);
        var lut = bandTable(style.bands);
        var sea = world.seaLevel, minH = Infinity, maxH = -Infinity;
        for (var i = 0; i < world.height.length; i++) {
            var gh = world.height[i];
            if (gh < minH) minH = gh;
            if (gh > maxH) maxH = gh;
        }
        var landSpan = Math.max(maxH - sea, 1e-4);
        var shelf = Math.max((sea - minH) * 0.6, 1e-4);
        var deep = style.sea, shallow = style.shallows, lake = style.lake;

        for (var b = 0; b < total; b++) {
            var h = blocks.height[b], o = b * 3, t;
            if (blocks.cls[b] === 0) {
                var k = Math.min(255, Math.max(0, ((h - sea) / landSpan * 255) | 0)) * 3;
                out[o] = lut[k]; out[o + 1] = lut[k + 1]; out[o + 2] = lut[k + 2];
            } else if (blocks.cls[b] === 1) {
                t = clamp(blocks.depth[b] / shelf, 0, 1);
                t = t * t * (3 - 2 * t);
                out[o]     = shallow[0] + (deep[0] - shallow[0]) * t;
                out[o + 1] = shallow[1] + (deep[1] - shallow[1]) * t;
                out[o + 2] = shallow[2] + (deep[2] - shallow[2]) * t;
            } else {
                t = 1 - 0.25 * clamp(blocks.depth[b] / 0.05, 0, 1);
                out[o] = lake[0] * t; out[o + 1] = lake[1] * t; out[o + 2] = lake[2] * t;
            }
        }
        return out;
    }

    // ── (2) Hill-shade ────────────────────────────────────────────────────────
    // Light from the north-west: slopes facing NW brighten, slopes facing SE darken. Land only.
    function applyHillShade(colours, blocks, W, H) {
        var cols = blocks.cols, rows = blocks.rows, h = blocks.height, cls = blocks.cls;
        var sx = W / (2 * RES), sy = H / (2 * RES);     // height difference -> per map unit
        for (var r = 0; r < rows; r++) {
            var up = Math.max(r - 1, 0) * cols, down = Math.min(r + 1, rows - 1) * cols, row = r * cols;
            for (var c = 0; c < cols; c++) {
                var b = row + c;
                if (cls[b] !== 0) continue;
                var dzdx = (h[row + Math.min(c + 1, cols - 1)] - h[row + Math.max(c - 1, 0)]) * sx;
                var dzdy = (h[down + c] - h[up + c]) * sy;
                var f = 1 + SHADE_STRENGTH * clamp((dzdx + dzdy) * SHADE_GAIN, -1, 1);
                var o = b * 3;
                colours[o] *= f; colours[o + 1] *= f; colours[o + 2] *= f;   // Uint8Clamped clamps
            }
        }
    }

    // Expand the block buffer into one full-size ImageData and put it on the canvas.
    function blitBlocks(ctx, colours, blocks, W, H) {
        var img = ctx.createImageData(W, H), data = img.data, cols = blocks.cols;
        var stride = W * 4;
        for (var r = 0; r < blocks.rows; r++) {
            var y = r * RES;
            if (y >= H) break;
            var p = y * stride;
            for (var x = 0; x < W; x++, p += 4) {
                var o = (r * cols + ((x / RES) | 0)) * 3;
                data[p] = colours[o]; data[p + 1] = colours[o + 1]; data[p + 2] = colours[o + 2]; data[p + 3] = 255;
            }
            for (var dy = 1; dy < RES && y + dy < H; dy++) {
                data.copyWithin((y + dy) * stride, y * stride, (y + 1) * stride);
            }
        }
        ctx.putImageData(img, 0, 0);
    }

    // ── (3) Coastline ─────────────────────────────────────────────────────────
    // Marching squares over the shoreline value at block centres: one path, one stroke.
    function strokeCoastline(ctx, blocks, W) {
        var cols = blocks.cols, rows = blocks.rows, s = blocks.shore;
        var half = RES * 0.5, pts = [0, 0, 0, 0, 0, 0, 0, 0];
        ctx.save();
        ctx.beginPath();
        for (var r = 0; r < rows - 1; r++) {
            for (var c = 0; c < cols - 1; c++) {
                var a = s[r * cols + c], b = s[r * cols + c + 1];
                var d = s[(r + 1) * cols + c], e = s[(r + 1) * cols + c + 1];   // e: bottom-right
                var la = a >= 0, lb = b >= 0, ld = d >= 0, le = e >= 0;
                if (la === lb && la === ld && la === le) continue;
                var x = c * RES + half, y = r * RES + half, m = 0;
                // edges in order: top, right, bottom, left
                if (la !== lb) { pts[m++] = x + RES * a / (a - b); pts[m++] = y; }
                if (lb !== le) { pts[m++] = x + RES; pts[m++] = y + RES * b / (b - e); }
                if (ld !== le) { pts[m++] = x + RES * d / (d - e); pts[m++] = y + RES; }
                if (la !== ld) { pts[m++] = x; pts[m++] = y + RES * a / (a - d); }
                if (m === 4) {
                    ctx.moveTo(pts[0], pts[1]); ctx.lineTo(pts[2], pts[3]);
                } else if (m === 8) {
                    // saddle: if the centre sides with the top-left corner, cut off top-right and bottom-left
                    if (((a + b + d + e) >= 0) === la) {
                        ctx.moveTo(pts[0], pts[1]); ctx.lineTo(pts[2], pts[3]);
                        ctx.moveTo(pts[4], pts[5]); ctx.lineTo(pts[6], pts[7]);
                    } else {
                        ctx.moveTo(pts[0], pts[1]); ctx.lineTo(pts[6], pts[7]);
                        ctx.moveTo(pts[2], pts[3]); ctx.lineTo(pts[4], pts[5]);
                    }
                }
            }
        }
        ctx.strokeStyle = 'rgba(8,6,4,0.55)';
        ctx.lineWidth = Math.max(1, 1.2 * W / 1000);
        ctx.lineJoin = 'round';
        ctx.stroke();
        ctx.restore();
    }

    // ── (4) Rivers ────────────────────────────────────────────────────────────
    // Layers per Biome river style: [colour, alpha, width multiplier, dashed].
    var RIVER_LAYERS = {
        water:  function (st) { return [[[0, 0, 0], 0.3, 1.7], [st.shallows, 0.9, 1]]; },
        sludge: function ()   { return [[[25, 30, 5], 0.45, 1.7], [[135, 150, 40], 0.85, 1], [[175, 190, 70], 0.5, 0.35]]; },
        lava:   function ()   { return [[[255, 70, 0], 0.16, 4.5], [[255, 90, 10], 0.3, 2.4],
                                        [[255, 130, 30], 0.95, 1], [[255, 215, 120], 0.9, 0.4]]; },
        frozen: function ()   { return [[[70, 110, 150], 0.55, 1.7], [[215, 235, 250], 0.95, 1]]; },
        dry:    function ()   { return [[[40, 25, 12], 0.5, 0.8, true]]; }
    };

    // Two [1,2,1] smoothing passes on interior points (ends kept so mouths still meet water).
    function smoothPoints(points, W, H) {
        var p = points.map(function (q) { return [q[0] * W, q[1] * H]; });
        for (var pass = 0; pass < 2; pass++) {
            var next = p.map(function (q) { return [q[0], q[1]]; });
            for (var i = 1; i < p.length - 1; i++) {
                next[i][0] = (p[i - 1][0] + 2 * p[i][0] + p[i + 1][0]) / 4;
                next[i][1] = (p[i - 1][1] + 2 * p[i][1] + p[i + 1][1]) / 4;
            }
            p = next;
        }
        return p;
    }

    // Quadratic pieces through the midpoints: piece i runs from start, via control, to end.
    function quadPieces(p) {
        var pieces = [], prev = p[0];
        for (var i = 1; i < p.length - 1; i++) {
            var mid = [(p[i][0] + p[i + 1][0]) / 2, (p[i][1] + p[i + 1][1]) / 2];
            pieces.push([prev, p[i], mid]);
            prev = mid;
        }
        pieces.push([prev, p[p.length - 1], p[p.length - 1]]);
        return pieces;
    }

    function tracePieces(ctx, pieces, from, to) {
        ctx.beginPath();
        ctx.moveTo(pieces[from][0][0], pieces[from][0][1]);
        for (var i = from; i < to; i++) ctx.quadraticCurveTo(pieces[i][1][0], pieces[i][1][1], pieces[i][2][0], pieces[i][2][1]);
    }

    function drawRivers(ctx, world, style, W, H) {
        var layers = (RIVER_LAYERS[style.river] || RIVER_LAYERS.water)(style);
        var unit = W / 1000, CHUNK = 6;
        ctx.save();
        ctx.lineJoin = 'round';
        for (var l = 0; l < layers.length; l++) {
            var colour = layers[l][0], alpha = layers[l][1], mul = layers[l][2], dashed = layers[l][3];
            ctx.strokeStyle = rgba(colour, alpha);
            for (var k = 0; k < world.rivers.length; k++) {
                var river = world.rivers[k];
                var pieces = quadPieces(smoothPoints(river.points, W, H));
                var full = river.width * unit * mul;
                if (dashed) {
                    // dry bed: one dashed path, no taper (dashes would restart per chunk)
                    ctx.lineCap = 'round';
                    ctx.setLineDash([3 * full, 2.5 * full]);
                    ctx.lineWidth = Math.max(1, full);
                    tracePieces(ctx, pieces, 0, pieces.length);
                    ctx.stroke();
                    ctx.setLineDash([]);
                    continue;
                }
                // tapered: thin at the source, full width at the mouth. Butt caps so
                // translucent layers don't double up into beads where chunks meet.
                ctx.lineCap = 'butt';
                for (var from = 0; from < pieces.length; from += CHUNK) {
                    var to = Math.min(from + CHUNK, pieces.length);
                    var t = to / pieces.length;
                    ctx.lineWidth = Math.max(0.75, full * (0.35 + 0.65 * Math.sqrt(t)));
                    tracePieces(ctx, pieces, from, to);
                    ctx.stroke();
                }
            }
        }
        ctx.restore();
    }

    // ── (5) Roads ─────────────────────────────────────────────────────────────
    function drawRoads(ctx, world, style, W, H) {
        if (!world.roads.length) return;
        var unit = W / 1000;
        ctx.save();
        ctx.strokeStyle = rgba(inkFor(style), ROAD_ALPHA);
        ctx.lineWidth = Math.max(1, 1.1 * unit);
        ctx.setLineDash([4 * unit, 3 * unit]);
        ctx.lineCap = 'round';
        ctx.beginPath();
        for (var i = 0; i < world.roads.length; i++) {
            var road = world.roads[i];
            ctx.moveTo(road[0][0] * W, road[0][1] * H);
            for (var j = 1; j < road.length; j++) ctx.lineTo(road[j][0] * W, road[j][1] * H);
        }
        ctx.stroke();
        ctx.restore();
    }

    // ── (6) Features ──────────────────────────────────────────────────────────
    // Small vector glyphs centred on (0,0), radius r; ctx is already translated.
    var GLYPHS = {
        crater: function (ctx, r, ink) {
            ctx.strokeStyle = rgb(ink); ctx.lineWidth = Math.max(1, r * 0.28);
            ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); ctx.stroke();
            ctx.beginPath(); ctx.arc(r * 0.15, r * 0.15, r * 0.55, Math.PI * 0.9, Math.PI * 1.9); ctx.stroke();
        },
        forest: function (ctx, r, ink) {
            ctx.fillStyle = 'rgb(16,42,18)'; ctx.strokeStyle = rgb(ink); ctx.lineWidth = Math.max(0.75, r * 0.12);
            var trees = [[-0.55, 0.25], [0.55, 0.3], [0, -0.25]];
            for (var i = 0; i < trees.length; i++) {
                var x = trees[i][0] * r, y = trees[i][1] * r;
                ctx.beginPath();
                ctx.moveTo(x, y - r * 0.7); ctx.lineTo(x + r * 0.45, y + r * 0.45); ctx.lineTo(x - r * 0.45, y + r * 0.45);
                ctx.closePath(); ctx.fill(); ctx.stroke();
            }
        },
        ruins: function (ctx, r, ink) {
            ctx.strokeStyle = rgb(ink); ctx.lineWidth = Math.max(1, r * 0.22);
            ctx.beginPath();
            ctx.moveTo(-r, -r * 0.2); ctx.lineTo(-r, r * 0.8); ctx.lineTo(r * 0.3, r * 0.8);
            ctx.moveTo(r * 0.8, r * 0.4); ctx.lineTo(r * 0.8, -r * 0.8); ctx.lineTo(-r * 0.3, -r * 0.8);
            ctx.stroke();
            ctx.fillStyle = rgb(ink); ctx.fillRect(-r * 0.2, -r * 0.1, r * 0.35, r * 0.35);
        },
        spire: function (ctx, r, ink) {
            ctx.fillStyle = rgb(ink); ctx.strokeStyle = rgb(ink); ctx.lineWidth = Math.max(1, r * 0.18);
            ctx.beginPath(); ctx.moveTo(0, -r * 1.4); ctx.lineTo(r * 0.3, r * 0.7); ctx.lineTo(-r * 0.3, r * 0.7); ctx.closePath(); ctx.fill();
            ctx.beginPath(); ctx.moveTo(-r * 0.75, r * 0.7); ctx.lineTo(r * 0.75, r * 0.7); ctx.stroke();
        },
        oasis: function (ctx, r, ink, look) {
            ctx.fillStyle = rgb(look.lake);
            ctx.beginPath(); ctx.ellipse(0, r * 0.25, r * 0.8, r * 0.45, 0, 0, Math.PI * 2); ctx.fill();
            ctx.strokeStyle = 'rgb(40,90,30)'; ctx.lineWidth = Math.max(1, r * 0.18);
            ctx.beginPath();
            ctx.moveTo(-r * 0.5, 0); ctx.lineTo(-r * 0.6, -r * 0.9);
            ctx.moveTo(-r * 0.6, -r * 0.9); ctx.lineTo(-r, -r * 0.6);
            ctx.moveTo(-r * 0.6, -r * 0.9); ctx.lineTo(-r * 0.2, -r * 0.7);
            ctx.stroke();
        },
        swamp: function (ctx, r, ink) {
            ctx.strokeStyle = rgb(ink); ctx.lineWidth = Math.max(0.75, r * 0.15);
            ctx.beginPath();
            for (var i = -1; i <= 1; i++) {
                var x = i * r * 0.7, y = (i === 0 ? -0.2 : 0.3) * r;
                ctx.moveTo(x - r * 0.35, y); ctx.lineTo(x + r * 0.35, y);
                ctx.moveTo(x, y); ctx.lineTo(x - r * 0.15, y - r * 0.45);
                ctx.moveTo(x, y); ctx.lineTo(x + r * 0.15, y - r * 0.45);
            }
            ctx.stroke();
        },
        settlement: function (ctx, r) {
            ctx.fillStyle = rgb(LIGHT_INK); ctx.strokeStyle = rgb(DARK_INK); ctx.lineWidth = Math.max(0.75, r * 0.14);
            var domes = [[-0.55, 0.45, 0.42], [0.25, 0.45, 0.6], [0.85, 0.45, 0.3]];
            for (var i = 0; i < domes.length; i++) {
                ctx.beginPath(); ctx.arc(domes[i][0] * r, domes[i][1] * r, domes[i][2] * r, Math.PI, 0); ctx.closePath();
                ctx.fill(); ctx.stroke();
            }
        },
        pyramid: function (ctx, r, ink) {
            ctx.strokeStyle = rgb(ink); ctx.fillStyle = rgb(ink); ctx.lineWidth = Math.max(1, r * 0.16);
            ctx.beginPath(); ctx.moveTo(0, -r); ctx.lineTo(r * 0.2, r * 0.7); ctx.lineTo(r, r * 0.7); ctx.closePath(); ctx.fill();
            ctx.beginPath(); ctx.moveTo(0, -r); ctx.lineTo(r, r * 0.7); ctx.lineTo(-r, r * 0.7); ctx.closePath(); ctx.stroke();
        },
        fissure: function (ctx, r, ink, look) {
            ctx.strokeStyle = rgb(look.glow || ink); ctx.lineWidth = Math.max(1, r * 0.25);
            ctx.beginPath();
            ctx.moveTo(-r * 1.2, -r * 0.3); ctx.lineTo(-r * 0.5, r * 0.2); ctx.lineTo(0, -r * 0.2);
            ctx.lineTo(r * 0.5, r * 0.35); ctx.lineTo(r * 1.2, 0);
            ctx.stroke();
        },
        dune: function (ctx, r, ink) {
            ctx.strokeStyle = rgb(ink); ctx.lineWidth = Math.max(0.75, r * 0.16);
            ctx.beginPath();
            for (var i = 0; i < 3; i++) {
                var y = (i - 1) * r * 0.5;
                ctx.moveTo(-r, y + r * 0.2); ctx.quadraticCurveTo(0, y - r * 0.35, r, y + r * 0.2);
            }
            ctx.stroke();
        }
    };

    // Fissures glow where the Biome has something hot or eldritch underneath.
    var FISSURE_GLOW = { ash: [255, 120, 30], tomb: [70, 255, 140] };

    function drawFeatures(ctx, world, style, biome, W, H) {
        if (!world.features.length) return;
        var ink = inkFor(style);
        var look = { lake: style.lake, glow: FISSURE_GLOW[biome] || null };
        var unit = clamp(W / 1000, 0.6, 1.4);       // glyphs stay smaller than the 14 px node pips
        ctx.save();
        ctx.globalAlpha = FEATURE_ALPHA;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        for (var i = 0; i < world.features.length; i++) {
            var f = world.features[i], glyph = GLYPHS[f.kind];
            if (!glyph) continue;
            ctx.save();
            ctx.translate(f.x * W, f.y * H);
            glyph(ctx, 2.8 * f.size * unit, ink, look);
            ctx.restore();
        }
        ctx.restore();
    }

    // ── (7) Vignette ──────────────────────────────────────────────────────────
    // Darken edges so node overlays pop.
    function drawVignette(ctx, W, H) {
        var vg = ctx.createRadialGradient(W / 2, H / 2, H * 0.3, W / 2, H / 2, H * 0.85);
        vg.addColorStop(0, 'rgba(0,0,0,0)');
        vg.addColorStop(1, 'rgba(0,0,0,0.45)');
        ctx.fillStyle = vg;
        ctx.fillRect(0, 0, W, H);
    }

    // ── Public draw function ──────────────────────────────────────────────────
    function draw(canvasEl, settings) {
        if (!canvasEl || !canvasEl.getContext) return;
        var ws = window.worldSurface;
        if (!ws) { console.warn('[topoMap] worldSurface.js is not loaded'); return; }

        // Cap the pixel buffer to avoid excessive memory on very large CSS sizes.
        var rawW = canvasEl.offsetWidth  || 800;
        var rawH = canvasEl.offsetHeight || 600;
        var scale = rawW > MAX ? MAX / rawW : 1;
        var W   = canvasEl.width  = Math.round(rawW * scale);
        var H   = canvasEl.height = Math.round(rawH * scale);
        var ctx = canvasEl.getContext('2d');

        var world = ws.buildWorld(settings);
        var biome = ws.normalise(settings).biome;
        var style = ws.BIOME_STYLES[biome];
        var blocks = sampleBlocks(world, ws.makeSampler(settings), W, H);

        var colours = colourBlocks(blocks, world, style);
        applyHillShade(colours, blocks, W, H);
        blitBlocks(ctx, colours, blocks, W, H);
        strokeCoastline(ctx, blocks, W);
        drawRivers(ctx, world, style, W, H);
        drawRoads(ctx, world, style, W, H);
        drawFeatures(ctx, world, style, biome, W, H);
        drawVignette(ctx, W, H);
    }

    function drawById(canvasId, settings) {
        var el = document.getElementById(canvasId);
        if (!el) { console.warn('[topoMap] canvas not found:', canvasId); return; }
        draw(el, settings);
    }

    return { draw: draw, drawById: drawById };
})();
