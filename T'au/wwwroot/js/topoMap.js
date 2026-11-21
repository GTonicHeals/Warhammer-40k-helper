// World Surface renderer: paints a worldSurface.buildWorld result onto a canvas.
// Painting only; the model (heights, water, rivers, features, roads, Biome styles)
// lives in worldSurface.js, which must load first.
window.topoMapInterop = (function () {
    'use strict';

    var RES = 2;                    // pixel block size for the height pass
    var MAX = 4800;                 // cap on the canvas pixel buffer width
    var LUT_SIZE = 512;             // relative land height -> colour table entries
    var PATCH_FREQ = 9;             // texture: patchy colour shifts (cycles per map height)
    var GRAIN_TILE = 256;           // texture: grain tile size in blocks (repeats every 512 px)
    var COARSE = 2;                 // height lattice step in blocks (interpolated in between)
    var PATCH_SHIFT = 0.06;         // how far patches move along the Biome colour ramp
    var GRAIN_LIGHT = 0.14;         // grain: max +/- brightness change
    var MICRO_RELIEF = 0.012;       // grain added to the shading height (not to land/water)
    var MEANDER = 3.5;              // river meander amplitude, in grid cells (256 per map height)
    var MEANDER_LENGTH = 9;         // river meander wavelength-ish, in river points
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

    // LUT_SIZE-entry relative-land-height -> colour table from the Biome bands. The band
    // colours are anchors: the shore colour holds over the lower half of its (narrow) band
    // and hands over to the next colour just past it; every other band colour sits at its
    // band's centre, and colours blend smoothly in between, so relief reads as a
    // continuous surface instead of contour rings.
    function bandTable(bands) {
        var shore = bands[0][0], centre1 = (bands[0][0] + bands[1][0]) / 2;
        var stops = [[0, bands[0]], [shore * 0.5, bands[0]], [Math.min(shore * 1.5, centre1), bands[1]]];
        for (var k = 1; k < bands.length; k++) stops.push([(bands[k - 1][0] + bands[k][0]) / 2, bands[k]]);
        var lut = new Uint8Array(LUT_SIZE * 3), s = 0;
        for (var i = 0; i < LUT_SIZE; i++) {
            var rel = i / (LUT_SIZE - 1);
            while (s < stops.length - 2 && rel > stops[s + 1][0]) s++;
            var lo = stops[s], hi = stops[Math.min(s + 1, stops.length - 1)];
            var t = hi[0] > lo[0] ? clamp((rel - lo[0]) / (hi[0] - lo[0]), 0, 1) : 1;
            t = t * t * (3 - 2 * t);
            for (var c = 1; c <= 3; c++) lut[i * 3 + c - 1] = lo[1][c] + (hi[1][c] - lo[1][c]) * t;
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

    // Grain: a GRAIN_TILE x GRAIN_TILE block tile of periodic value noise (octaves of
    // 16, 8, 4 and 2 blocks), repeated across the canvas. It lives in pixel space, so it
    // stays crisp at every canvas size; it only textures colour and shading, never the World.
    function grainTile(rng) {
        var T = GRAIN_TILE, tile = new Float32Array(T * T);
        var octaves = [[16, 0.4], [8, 0.3], [4, 0.2], [2, 0.1]];
        for (var o = 0; o < octaves.length; o++) {
            var cell = octaves[o][0], amp = octaves[o][1], p = T / cell;
            var lat = new Float32Array(p * p);
            for (var i = 0; i < lat.length; i++) lat[i] = rng() * 2 - 1;
            for (var y = 0; y < T; y++) {
                var gy = y / cell, y0 = Math.floor(gy), v = gy - y0, y1 = (y0 + 1) % p;
                v = v * v * (3 - 2 * v);
                for (var x = 0; x < T; x++) {
                    var gx = x / cell, x0 = Math.floor(gx), u = gx - x0, x1 = (x0 + 1) % p;
                    u = u * u * (3 - 2 * u);
                    var a = lat[y0 * p + x0], b = lat[y0 * p + x1], c = lat[y1 * p + x0], d = lat[y1 * p + x1];
                    tile[y * T + x] += amp * (a + (b - a) * u + (c - a + (a - b + d - c) * u) * v);
                }
            }
        }
        return tile;
    }

    // The height and the patch texture are smooth at block scale, so they are sampled on
    // a lattice every COARSE blocks and interpolated bilinearly in between.
    // texture(x, y) is seeded noise; x and y are in map heights so patches stay round.
    function sampleBlocks(world, sampler, texture, rng, W, H) {
        var cols = Math.ceil(W / RES), rows = Math.ceil(H / RES);
        var total = cols * rows;
        var height = new Float32Array(total);
        var cls = new Uint8Array(total);
        var shore = new Float32Array(total);    // signed distance-like value, negative = water
        var depth = new Float32Array(total);    // water depth, for shading sea and lakes
        var patch = new Float32Array(total);    // low-frequency texture, colour patches
        var grain = new Float32Array(total);    // high-frequency texture, grain and micro-relief
        var n = world.size, last = n - 1;
        var sea = world.seaLevel, water = world.water, lakeDepth = lakeDepthGrid(world);
        var aspect = W / H, tile = grainTile(rng), T = GRAIN_TILE;

        var ccols = Math.ceil((cols - 1) / COARSE) + 1, crows = Math.ceil((rows - 1) / COARSE) + 1;
        var ch = new Float32Array(ccols * crows), cp = new Float32Array(ccols * crows);
        for (var rr = 0; rr < crows; rr++) {
            var cny = Math.min(1, (rr * COARSE * RES + RES * 0.5) / H);
            for (var cc = 0; cc < ccols; cc++) {
                var cnx = Math.min(1, (cc * COARSE * RES + RES * 0.5) / W);
                ch[rr * ccols + cc] = sampler(cnx, cny);
                cp[rr * ccols + cc] = texture(cnx * aspect * PATCH_FREQ, cny * PATCH_FREQ);
            }
        }

        for (var r = 0; r < rows; r++) {
            var ny = Math.min(1, (r * RES + RES * 0.5) / H);
            var gyf = ny * last;
            var gy = Math.round(gyf), y0 = Math.min(Math.floor(gyf), last - 1), ty = gyf - y0;
            var cr = (r / COARSE) | 0, cv = (r - cr * COARSE) / COARSE, crow = cr * ccols;
            var crow2 = Math.min(cr + 1, crows - 1) * ccols, trow = (r % T) * T;
            for (var c = 0; c < cols; c++) {
                var b = r * cols + c;
                var nx = Math.min(1, (c * RES + RES * 0.5) / W);
                var gxf = nx * last;
                var gx = Math.round(gxf), x0 = Math.min(Math.floor(gxf), last - 1), tx = gxf - x0;
                var ci = (c / COARSE) | 0, cu = (c - ci * COARSE) / COARSE, ci2 = Math.min(ci + 1, ccols - 1);
                var h0 = ch[crow + ci] + (ch[crow + ci2] - ch[crow + ci]) * cu;
                var h1 = ch[crow2 + ci] + (ch[crow2 + ci2] - ch[crow2 + ci]) * cu;
                var h = Math.fround(h0 + (h1 - h0) * cv);
                height[b] = h;
                var p0 = cp[crow + ci] + (cp[crow + ci2] - cp[crow + ci]) * cu;
                var p1 = cp[crow2 + ci] + (cp[crow2 + ci2] - cp[crow2 + ci]) * cu;
                patch[b] = p0 + (p1 - p0) * cv;
                grain[b] = tile[trow + (c % T)];

                var i = y0 * n + x0;
                var top = lakeDepth[i] + (lakeDepth[i + 1] - lakeDepth[i]) * tx;
                var bot = lakeDepth[i + n] + (lakeDepth[i + n + 1] - lakeDepth[i + n]) * tx;
                var lake = top + (bot - top) * ty;

                if (h < sea) {
                    var wc = water[gy * n + gx];
                    cls[b] = wc !== 0 ? wc : nearbyWater(world, gx, gy);
                    shore[b] = h - sea;
                    // a below-sea pocket inside a pooled lake takes the lake's depth too, so
                    // the lake shades as one body instead of showing the pocket's outline
                    depth[b] = cls[b] === 2 ? Math.max(sea - h, lake) : sea - h;
                } else if (lake > 0) {
                    cls[b] = 2;
                    shore[b] = -lake;
                    depth[b] = lake;
                } else {
                    shore[b] = Math.min(h - sea, -lake);
                }
            }
        }
        return { cols: cols, rows: rows, height: height, cls: cls, shore: shore, depth: depth, patch: patch, grain: grain };
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

        var top = LUT_SIZE - 1, patch = blocks.patch, grain = blocks.grain;
        for (var b = 0; b < total; b++) {
            var h = blocks.height[b], o = b * 3, t, light;
            if (blocks.cls[b] === 0) {
                // patches nudge the colour along the ramp (fade out at the shore so beaches stay put)
                var rel = (h - sea) / landSpan;
                rel += PATCH_SHIFT * patch[b] * clamp(rel * 8, 0, 1);
                var k = clamp((rel * top) | 0, 0, top) * 3;
                light = 1 + GRAIN_LIGHT * grain[b];
                out[o] = lut[k] * light; out[o + 1] = lut[k + 1] * light; out[o + 2] = lut[k + 2] * light;
            } else if (blocks.cls[b] === 1) {
                t = clamp(blocks.depth[b] / shelf + 0.12 * patch[b], 0, 1);
                t = t * t * (3 - 2 * t);
                light = 1 + 0.5 * GRAIN_LIGHT * grain[b];
                out[o]     = (shallow[0] + (deep[0] - shallow[0]) * t) * light;
                out[o + 1] = (shallow[1] + (deep[1] - shallow[1]) * t) * light;
                out[o + 2] = (shallow[2] + (deep[2] - shallow[2]) * t) * light;
            } else {
                t = (1 - 0.25 * clamp(blocks.depth[b] / 0.05, 0, 1)) * (1 + 0.5 * GRAIN_LIGHT * grain[b]);
                out[o] = lake[0] * t; out[o + 1] = lake[1] * t; out[o + 2] = lake[2] * t;
            }
        }
        return out;
    }

    // ── (2) Hill-shade ────────────────────────────────────────────────────────
    // Light from the north-west: slopes facing NW brighten, slopes facing SE darken. Land only.
    function applyHillShade(colours, blocks, W, H) {
        var cols = blocks.cols, rows = blocks.rows, cls = blocks.cls;
        var h = new Float32Array(blocks.height.length);       // height plus fine micro-relief
        for (var i = 0; i < h.length; i++) h[i] = blocks.height[i] + MICRO_RELIEF * blocks.grain[i];
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

    // Seeded meander, then two [1,2,1] smoothing passes on interior points. Each interior
    // point is pushed sideways (perpendicular to the flow) by smooth 1-D noise along the
    // river, tapering to nothing at both ends so sources, mouths and junctions stay put.
    // Drawing only: the grid path in the model (downhill on `filled`) is unchanged.
    function smoothPoints(points, W, H, wiggle, phase) {
        var p = points.map(function (q) { return [q[0] * W, q[1] * H]; });
        var amp = MEANDER * Math.min(W, H) / 255, last = p.length - 1;
        for (var i = 1; i < last; i++) {
            var dx = p[i + 1][0] - p[i - 1][0], dy = p[i + 1][1] - p[i - 1][1], len = Math.hypot(dx, dy) || 1;
            var off = amp * Math.min(1, i / 4, (last - i) / 4) * wiggle(i / MEANDER_LENGTH + phase, phase * 0.37);
            p[i][0] += -dy / len * off;
            p[i][1] += dx / len * off;
        }
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

    function drawRivers(ctx, world, style, wiggle, W, H) {
        var layers = (RIVER_LAYERS[style.river] || RIVER_LAYERS.water)(style);
        var unit = W / 1000, CHUNK = 6;
        var paths = world.rivers.map(function (river, k) {
            return quadPieces(smoothPoints(river.points, W, H, wiggle, 17.3 * (k + 1)));
        });
        ctx.save();
        ctx.lineJoin = 'round';
        for (var l = 0; l < layers.length; l++) {
            var colour = layers[l][0], alpha = layers[l][1], mul = layers[l][2], dashed = layers[l][3];
            ctx.strokeStyle = rgba(colour, alpha);
            for (var k = 0; k < world.rivers.length; k++) {
                var river = world.rivers[k];
                var pieces = paths[k];
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
    // Vector glyphs centred on (0,0) with radius r; ctx is already translated. Each draws a
    // dark shadow and a light, north-west-lit edge so it reads on pale and dark Biomes
    // alike. look: { dark, light, lake, glow, tree }; rnd: a PRNG seeded per draw, for
    // per-feature variation (tree layout, rubble, crack shape).
    var TAU = Math.PI * 2;

    function circle(ctx, x, y, r) { ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); }

    // Soft tinted patch: colour at the centre fading to transparent at radius r.
    function patchFill(ctx, r, colour, alpha) {
        var g = ctx.createRadialGradient(0, 0, 0, 0, 0, r);
        g.addColorStop(0, rgba(colour, alpha));
        g.addColorStop(0.6, rgba(colour, alpha * 0.6));
        g.addColorStop(1, rgba(colour, 0));
        ctx.fillStyle = g;
        circle(ctx, 0, 0, r);
        ctx.fill();
    }

    var GLYPHS = {
        // bowl with a shadowed floor, a lit south-east inner wall and a raised rim
        crater: function (ctx, r, look) {
            circle(ctx, 0, 0, r * 1.15); ctx.fillStyle = rgba(look.dark, 0.35); ctx.fill();
            circle(ctx, 0, 0, r * 0.8); ctx.fillStyle = rgba(look.dark, 0.55); ctx.fill();
            ctx.lineWidth = Math.max(1, r * 0.22);
            ctx.strokeStyle = rgb(look.light);
            ctx.beginPath(); ctx.arc(0, 0, r * 0.8, -0.2 * Math.PI, 0.8 * Math.PI); ctx.stroke();       // lit inner wall
            ctx.beginPath(); ctx.arc(0, 0, r * 1.05, 0.85 * Math.PI, 1.65 * Math.PI); ctx.stroke();     // lit outer rim
            ctx.strokeStyle = rgb(look.dark); ctx.lineWidth = Math.max(0.75, r * 0.14);
            ctx.beginPath(); ctx.arc(0, 0, r * 1.15, -0.25 * Math.PI, 0.75 * Math.PI); ctx.stroke();    // rim shadow
        },
        // a clump of round canopies, back to front, each with a lit top-left
        forest: function (ctx, r, look, rnd) {
            var trees = [], count = 7 + ((rnd() * 5) | 0), i;
            r *= 1.3;                                                            // an area, wider than a point feature
            for (i = 0; i < count; i++) {
                var a = rnd() * TAU, d = Math.sqrt(rnd()) * r;
                trees.push([Math.cos(a) * d * 1.25, Math.sin(a) * d * 0.85, r * (0.28 + rnd() * 0.14)]);
            }
            trees.sort(function (p, q) { return p[1] - q[1]; });
            for (i = 0; i < trees.length; i++) {
                var t = trees[i];
                circle(ctx, t[0] + t[2] * 0.25, t[1] + t[2] * 0.3, t[2]); ctx.fillStyle = rgba(look.dark, 0.7); ctx.fill();
                circle(ctx, t[0], t[1], t[2]); ctx.fillStyle = rgb(look.tree); ctx.fill();
                circle(ctx, t[0] - t[2] * 0.3, t[1] - t[2] * 0.3, t[2] * 0.45); ctx.fillStyle = rgba(look.light, 0.5); ctx.fill();
            }
        },
        // broken walls and scattered blocks of rubble
        ruins: function (ctx, r, look, rnd) {
            ctx.lineCap = 'butt';
            var walls = [[-1, -0.7, 0.9, -0.7], [-1, -0.7, -1, 0.5], [0.9, -0.1, 0.9, 0.7], [-0.4, 0.7, 0.9, 0.7]], i;
            for (i = 0; i < walls.length; i++) {
                if (rnd() < 0.3) continue;                                      // a wall gone entirely
                var w = walls[i], cut = 0.45 + rnd() * 0.4;                      // and the rest broken short
                var x2 = w[0] + (w[2] - w[0]) * cut, y2 = w[1] + (w[3] - w[1]) * cut;
                ctx.lineWidth = Math.max(2, r * 0.4);
                ctx.strokeStyle = rgb(look.dark);
                ctx.beginPath(); ctx.moveTo(w[0] * r + r * 0.12, w[1] * r + r * 0.12); ctx.lineTo(x2 * r + r * 0.12, y2 * r + r * 0.12); ctx.stroke();
                ctx.lineWidth = Math.max(1.25, r * 0.26);
                ctx.strokeStyle = rgb(look.light);
                ctx.beginPath(); ctx.moveTo(w[0] * r, w[1] * r); ctx.lineTo(x2 * r, y2 * r); ctx.stroke();
            }
            for (i = 0; i < 4; i++) {
                var bx = (rnd() - 0.5) * r * 1.4, by = (rnd() - 0.5) * r * 1.2, bs = r * (0.18 + rnd() * 0.2);
                ctx.fillStyle = rgb(look.dark); ctx.fillRect(bx + bs * 0.3, by + bs * 0.3, bs, bs);
                ctx.fillStyle = rgb(look.light); ctx.fillRect(bx, by, bs, bs);
            }
        },
        // a tall needle with a lit west face, plus a smaller one beside it
        spire: function (ctx, r, look, rnd) {
            var towers = [[0, 1.0], [(rnd() < 0.5 ? -1 : 1) * r * 0.6, 0.6]];
            for (var i = towers.length - 1; i >= 0; i--) {
                var x = towers[i][0], hgt = r * 2.4 * towers[i][1], w = r * 0.42 * towers[i][1], base = r * 0.8;
                ctx.fillStyle = rgba(look.dark, 0.6);                               // cast shadow to the south-east
                ctx.beginPath(); ctx.moveTo(x - w, base); ctx.lineTo(x + hgt * 0.45, base + hgt * 0.2); ctx.lineTo(x + w, base); ctx.fill();
                ctx.fillStyle = rgb(look.dark);
                ctx.beginPath(); ctx.moveTo(x, base - hgt); ctx.lineTo(x + w, base); ctx.lineTo(x - w, base); ctx.closePath(); ctx.fill();
                ctx.fillStyle = rgb(look.light);
                ctx.beginPath(); ctx.moveTo(x, base - hgt); ctx.lineTo(x, base); ctx.lineTo(x - w, base); ctx.closePath(); ctx.fill();
                circle(ctx, x, base - hgt * 0.55, Math.max(0.75, w * 0.3)); ctx.fillStyle = rgb(SPIRE_LIGHT); ctx.fill();   // a lit window
            }
        },
        // a stepped triangle, lit west face, shadowed east face, glowing apex where the Biome has a glow
        pyramid: function (ctx, r, look) {
            var top = -r * 1.1, base = r * 0.75;
            ctx.fillStyle = rgba(look.dark, 0.6);
            ctx.beginPath(); ctx.moveTo(-r, base); ctx.lineTo(r * 1.9, base + r * 0.35); ctx.lineTo(r, base); ctx.fill();
            ctx.fillStyle = rgb(look.light);
            ctx.beginPath(); ctx.moveTo(0, top); ctx.lineTo(r * 0.15, base); ctx.lineTo(-r, base); ctx.closePath(); ctx.fill();
            ctx.fillStyle = rgb(look.dark);
            ctx.beginPath(); ctx.moveTo(0, top); ctx.lineTo(r, base); ctx.lineTo(r * 0.15, base); ctx.closePath(); ctx.fill();
            ctx.strokeStyle = rgba(look.dark, 0.8); ctx.lineWidth = Math.max(0.75, r * 0.08);
            ctx.beginPath();
            for (var k = 1; k <= 2; k++) {                                         // steps
                var y = top + (base - top) * k / 3, half = r * k / 3;
                ctx.moveTo(-half, y); ctx.lineTo(half, y);
            }
            ctx.stroke();
            if (look.glow) {
                ctx.save(); ctx.translate(0, top + r * 0.15);
                patchFill(ctx, r * 0.8, look.glow, 0.5);
                circle(ctx, 0, 0, Math.max(1, r * 0.14)); ctx.fillStyle = rgb(look.glow); ctx.fill();
                ctx.restore();
            }
        },
        // a jagged crack with a branch: dark cut, wide soft glow, bright core
        fissure: function (ctx, r, look, rnd) {
            var pts = [], n = 6, ang = rnd() * Math.PI;
            var ca = Math.cos(ang), sa = Math.sin(ang);
            for (var i = 0; i <= n; i++) {
                var u = (i / n - 0.5) * 2.6 * r, v = (rnd() - 0.5) * r * 0.7;
                pts.push([u * ca - v * sa, u * sa + v * ca]);
            }
            var mid = pts[3], bx = mid[0] - sa * r * 0.9 + ca * r * 0.4, by = mid[1] + ca * r * 0.9 + sa * r * 0.4;
            function crack() {
                ctx.beginPath(); ctx.moveTo(pts[0][0], pts[0][1]);
                for (var j = 1; j < pts.length; j++) ctx.lineTo(pts[j][0], pts[j][1]);
                ctx.moveTo(mid[0], mid[1]); ctx.lineTo(bx, by);
            }
            var glow = look.glow || look.light;
            ctx.strokeStyle = rgb(look.dark); ctx.lineWidth = Math.max(2, r * 0.55); crack(); ctx.stroke();
            ctx.strokeStyle = rgba(glow, 0.45); ctx.lineWidth = Math.max(2, r * 0.45); crack(); ctx.stroke();
            ctx.strokeStyle = rgb(glow); ctx.lineWidth = Math.max(1, r * 0.14); crack(); ctx.stroke();
        },
        // three crescent dunes: lit crest over a shadowed lee side
        dune: function (ctx, r, look, rnd) {
            for (var i = 0; i < 3; i++) {
                var x = (i - 1) * r * 0.8 + (rnd() - 0.5) * r * 0.3, y = (i % 2 ? -0.35 : 0.3) * r, w = r * (0.55 + rnd() * 0.25);
                ctx.lineWidth = Math.max(2, r * 0.34); ctx.strokeStyle = rgb(look.dark);
                ctx.beginPath(); ctx.arc(x, y + w * 0.35, w, 1.15 * Math.PI, 1.85 * Math.PI); ctx.stroke();
                ctx.lineWidth = Math.max(1, r * 0.17); ctx.strokeStyle = rgb(look.light);
                ctx.beginPath(); ctx.arc(x, y + w * 0.2, w, 1.15 * Math.PI, 1.85 * Math.PI); ctx.stroke();
            }
        },
        // a pool in the lake colour, ringed by green, with two palms
        oasis: function (ctx, r, look) {
            patchFill(ctx, r * 1.5, [60, 110, 40], 0.6);
            ctx.fillStyle = rgb(look.lake);
            ctx.beginPath(); ctx.ellipse(0, r * 0.15, r * 0.7, r * 0.42, 0, 0, TAU); ctx.fill();
            ctx.strokeStyle = rgb(look.dark); ctx.lineWidth = Math.max(1, r * 0.14);
            ctx.beginPath();
            for (var i = 0; i < 2; i++) {
                var x = (i ? 0.75 : -0.8) * r, y = -0.1 * r, top = y - r * 0.8;
                ctx.moveTo(x, y); ctx.lineTo(x + r * 0.08, top);
                ctx.moveTo(x + r * 0.08, top); ctx.lineTo(x - r * 0.35, top + r * 0.25);
                ctx.moveTo(x + r * 0.08, top); ctx.lineTo(x + r * 0.5, top + r * 0.22);
                ctx.moveTo(x + r * 0.08, top); ctx.lineTo(x + r * 0.05, top - r * 0.25);
            }
            ctx.stroke();
        },
        // a murky green patch with dark pools and reeds
        swamp: function (ctx, r, look, rnd) {
            patchFill(ctx, r * 1.6, [70, 85, 35], 0.6);
            var i;
            for (i = 0; i < 3; i++) {
                ctx.fillStyle = rgba(look.lake, 0.9);
                ctx.beginPath(); ctx.ellipse((rnd() - 0.5) * r * 1.4, (rnd() - 0.5) * r * 0.9, r * (0.25 + rnd() * 0.2), r * 0.16, 0, 0, TAU); ctx.fill();
            }
            ctx.strokeStyle = rgb(look.dark); ctx.lineWidth = Math.max(0.75, r * 0.1);
            ctx.beginPath();
            for (i = 0; i < 5; i++) {
                var x = (rnd() - 0.5) * r * 1.6, y = (rnd() - 0.2) * r * 0.8;
                ctx.moveTo(x, y); ctx.lineTo(x - r * 0.12, y - r * 0.5);
                ctx.moveTo(x, y); ctx.lineTo(x + r * 0.1, y - r * 0.45);
            }
            ctx.stroke();
        },
        // a cluster of pale domes on a dark footing
        settlement: function (ctx, r, look) {
            var domes = [[-0.6, 0.45, 0.45], [0.2, 0.45, 0.68], [0.95, 0.45, 0.33]];
            ctx.fillStyle = rgba(look.dark, 0.7);
            ctx.beginPath(); ctx.ellipse(r * 0.25, r * 0.55, r * 1.45, r * 0.3, 0, 0, TAU); ctx.fill();
            ctx.lineWidth = Math.max(0.75, r * 0.12);
            for (var i = 0; i < domes.length; i++) {
                var x = domes[i][0] * r, y = domes[i][1] * r, dr = domes[i][2] * r;
                ctx.beginPath(); ctx.arc(x, y, dr, Math.PI, 0); ctx.closePath();
                ctx.fillStyle = rgb(look.light); ctx.fill();
                ctx.strokeStyle = rgb(look.dark); ctx.stroke();
                ctx.beginPath(); ctx.arc(x, y, dr * 0.6, 1.15 * Math.PI, 1.45 * Math.PI);
                ctx.strokeStyle = 'rgb(255,250,235)'; ctx.stroke();
            }
        }
    };

    // Fissures and pyramids glow where the Biome has something hot or eldritch underneath.
    var FISSURE_GLOW = { ash: [255, 120, 30], tomb: [70, 255, 140] };
    // Canopy colour per Biome (forest glyphs).
    // A lit window on hive spires.
    var SPIRE_LIGHT = [255, 190, 90];
    var TREE_COLOUR = { agri: [20, 46, 18], death: [44, 70, 10], jungle: [8, 34, 14], sept: [48, 62, 24] };

    // Glyph radius in pixels: grows with the canvas but less than linearly, so features
    // read on the small Map Generator preview and stay below node size on the 200% Campaign Map.
    function featureRadius(W) { return clamp(3 + 3.2 * W / 1000, 4, 13); }

    function drawFeatures(ctx, world, style, biome, rng, W, H) {
        if (!world.features.length) return;
        var look = { dark: DARK_INK, light: LIGHT_INK, lake: style.lake, glow: FISSURE_GLOW[biome] || null,
            tree: TREE_COLOUR[biome] || TREE_COLOUR.agri };
        var base = featureRadius(W);
        ctx.save();
        ctx.globalAlpha = FEATURE_ALPHA;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        for (var i = 0; i < world.features.length; i++) {
            var f = world.features[i], glyph = GLYPHS[f.kind];
            if (!glyph) continue;
            ctx.save();
            ctx.translate(f.x * W, f.y * H);
            glyph(ctx, base * (0.75 + 0.25 * f.size), look, rng);
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
        var texture = ws.createNoise(ws.normalise(settings).seed * 7 + 9001);
        var blocks = sampleBlocks(world, ws.makeSampler(settings), texture, ws.mulberry32(ws.normalise(settings).seed * 13 + 4001), W, H);
        var colours = colourBlocks(blocks, world, style);
        applyHillShade(colours, blocks, W, H);
        blitBlocks(ctx, colours, blocks, W, H);
        strokeCoastline(ctx, blocks, W);
        drawRivers(ctx, world, style, texture, W, H);
        drawRoads(ctx, world, style, W, H);
        drawFeatures(ctx, world, style, biome, ws.mulberry32(ws.normalise(settings).seed * 29 + 7), W, H);
        drawVignette(ctx, W, H);
    }

    function drawById(canvasId, settings) {
        var el = document.getElementById(canvasId);
        if (!el) { console.warn('[topoMap] canvas not found:', canvasId); return; }
        draw(el, settings);
    }

    return { draw: draw, drawById: drawById };
})();
