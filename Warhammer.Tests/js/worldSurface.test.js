'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('path');

const { GRID, normalise, makeSampler, sampleHeight, buildWorld, _classifyWater, BIOME_STYLES } =
    require(path.join(__dirname, '..', '..', "T'au", 'wwwroot', 'js', 'worldSurface.js'));

const S = { seed: 42, biome: 'agri', seaLevel: 0.35, landmass: 'continent', rivers: 4, ruggedness: 0.5, features: 0.5 };

test('GRID is 256', () => assert.equal(GRID, 256));

test('same settings build the same world', () => {
    assert.deepEqual(buildWorld(S).height, buildWorld(S).height);
});

test('different seeds differ', () => {
    assert.notDeepEqual(buildWorld(S).height, buildWorld({ ...S, seed: 43 }).height);
});

for (const sea of [0.05, 0.35, 0.8]) {
    test(`sea level ${sea} puts that share underwater`, () => {
        const w = buildWorld({ ...S, seaLevel: sea });
        const under = w.height.filter(h => h < w.seaLevel).length / (w.size * w.size);
        assert.ok(Math.abs(under - sea) < 0.02, `under=${under}`);
    });
}

for (const landmass of ['continent', 'archipelago', 'pangea', 'random']) {
    test(`sea level holds for landmass ${landmass}`, () => {
        const w = buildWorld({ ...S, landmass });
        const under = w.height.filter(h => h < w.seaLevel).length / (w.size * w.size);
        assert.ok(Math.abs(under - 0.35) < 0.02, `under=${under}`);
    });
}

test('sea level 0 has no sea cells', () => {
    const w = buildWorld({ ...S, seaLevel: 0 });
    assert.equal(w.water.filter(v => v === 1).length, 0);
});

test('water classes: below-threshold cells are sea or lake, others land', () => {
    const w = buildWorld(S);
    for (let i = 0; i < w.height.length; i++) {
        if (w.height[i] < w.seaLevel) assert.notEqual(w.water[i], 0);
        // extra water above sea level is a river-pooling lake sitting in a filled depression
        else if (w.water[i] !== 0) {
            assert.equal(w.water[i], 2);
            assert.ok(w.filled[i] - w.height[i] > 0.004);
        }
    }
});

test('continent: the grid border is mostly sea at sea level 0.35', () => {
    const w = buildWorld(S);
    const n = w.size;
    let sea = 0, total = 0;
    for (let i = 0; i < n; i++) {
        for (const idx of [i, (n - 1) * n + i, i * n, i * n + n - 1]) {
            total++;
            if (w.water[idx] === 1) sea++;
        }
    }
    assert.ok(sea / total >= 0.8, `sea share ${sea / total}`);
});

// ── Landmass shape: the mask biases, the noise shapes ─────────────────────
// Land-sea 4-neighbour edges per sqrt(land area). A digital disc scores ~4.5 and the old
// bullseye continent scored 4.5 on every seed; bays, peninsulas and islets push it up.
function coastRatio(w) {
    const n = w.size, land = i => w.water[i] !== 1;
    let edges = 0, area = 0;
    for (let y = 0; y < n; y++) for (let x = 0; x < n; x++) {
        const i = y * n + x;
        if (!land(i)) continue;
        area++;
        if (x > 0 && !land(i - 1)) edges++;
        if (x < n - 1 && !land(i + 1)) edges++;
        if (y > 0 && !land(i - n)) edges++;
        if (y < n - 1 && !land(i + n)) edges++;
    }
    return edges / Math.sqrt(area);
}

test('continent: the coastline is irregular, not an ellipse', () => {
    const ratios = [42, 7, 99, 4242, 777].map(seed => coastRatio(buildWorld({ ...S, seed })));
    const mean = ratios.reduce((a, b) => a + b) / ratios.length;
    for (const r of ratios) assert.ok(r > 5.5, `ratio ${r.toFixed(2)}`);
    assert.ok(mean > 6.5, `mean ratio ${mean.toFixed(2)}`);
});

test('no flat plateau at the sea threshold', () => {
    // A flat floor (e.g. a multiplied mask hitting its minimum) piles heights up at one value;
    // the sea threshold then lands inside it and paints a huge shore band.
    for (const landmass of ['continent', 'archipelago', 'pangea', 'random']) for (const seed of [42, 777]) {
        const w = buildWorld({ ...S, landmass, seed });
        const sorted = Float32Array.from(w.height).sort();
        const spread = sorted[Math.floor(0.95 * sorted.length)] - sorted[Math.floor(0.05 * sorted.length)];
        const near = w.height.filter(h => Math.abs(h - w.seaLevel) < spread * 0.02).length / w.height.length;
        assert.ok(near < 0.2, `${landmass} seed ${seed}: ${near.toFixed(3)} of cells within 2% of the threshold`);
    }
});

test('archipelago: many separate islands at sea level 0.6', () => {
    for (const seed of [42, 7, 99]) {
        const w = buildWorld({ ...S, seed, landmass: 'archipelago', seaLevel: 0.6 });
        const n = w.size, seen = new Uint8Array(n * n);
        let islands = 0;
        for (let s = 0; s < n * n; s++) {
            if (seen[s] || w.water[s] === 1) continue;
            let size = 0;
            const stack = [s];
            seen[s] = 1;
            while (stack.length) {
                const c = stack.pop(), x = c % n;
                size++;
                for (const m of [x > 0 ? c - 1 : -1, x < n - 1 ? c + 1 : -1, c - n, c + n]) {
                    if (m >= 0 && m < n * n && !seen[m] && w.water[m] !== 1) { seen[m] = 1; stack.push(m); }
                }
            }
            if (size >= 50) islands++;
        }
        assert.ok(islands >= 6, `seed ${seed}: ${islands} islands`);
    }
});

test('world shape: size, arrays and feature lists', () => {
    const w = buildWorld(S);
    assert.equal(w.size, 256);
    assert.ok(w.height instanceof Float32Array);
    assert.ok(w.water instanceof Uint8Array);
    assert.equal(w.height.length, 256 * 256);
    assert.ok(Array.isArray(w.rivers));
    assert.ok(w.filled instanceof Float32Array);
    assert.equal(w.filled.length, 256 * 256);
    assert.ok(Array.isArray(w.features) && Array.isArray(w.roads));
});

test('sampleHeight matches the grid at cell positions', () => {
    const w = buildWorld(S);
    const n = w.size;
    for (const [x, y] of [[10, 20], [128, 128], [200, 50]]) {
        const h = sampleHeight(S, x / (n - 1), y / (n - 1));
        assert.ok(Math.abs(h - w.height[y * n + x]) < 1e-5);
    }
});

test('makeSampler equals sampleHeight and the grid', () => {
    const w = buildWorld(S);
    const n = w.size;
    const sampler = makeSampler(S);
    for (const [x, y] of [[0, 0], [10, 20], [128, 128], [200, 50]]) {
        const h = sampler(x / (n - 1), y / (n - 1));
        assert.equal(h, sampleHeight(S, x / (n - 1), y / (n - 1)));
        assert.ok(Math.abs(h - w.height[y * n + x]) < 1e-5);
    }
});

test('classifyWater: border-connected basin is sea, enclosed basin is lake', () => {
    const n = GRID;
    const h = new Float32Array(n * n).fill(1);
    const at = (x, y) => y * n + x;
    // sea: a notch reaching the left border
    for (let x = 0; x < 10; x++) for (let y = 100; y < 105; y++) h[at(x, y)] = 0;
    // lake: enclosed pool, ringed by land
    for (let x = 100; x < 105; x++) for (let y = 100; y < 105; y++) h[at(x, y)] = 0;
    // diagonal-only neighbour of the sea notch must not join it (4-connected)
    h[at(10, 105)] = 0;
    const w = _classifyWater(h, 0.5);
    assert.equal(w[at(0, 100)], 1);
    assert.equal(w[at(9, 104)], 1);
    assert.equal(w[at(102, 102)], 2);
    assert.equal(w[at(10, 105)], 2);
    assert.equal(w[at(50, 50)], 0);
});

test('normalise: unknown biome and landmass fall back', () => {
    const n = normalise({ biome: 'forge', landmass: 'moon' });
    assert.equal(n.biome, 'agri');
    assert.equal(n.landmass, 'continent');
});

test('normalise: fills defaults for missing fields and tolerates garbage', () => {
    const n = normalise({});
    assert.deepEqual(
        [n.seed, n.octaves, n.lacunarity, n.persistence, n.scale, n.offsetX, n.offsetY],
        [1, 4, 2.0, 0.5, 2.0, 0, 0]);
    assert.deepEqual(
        [n.seaLevel, n.rivers, n.ruggedness, n.features], [0.35, 4, 0.5, 0.5]);
    const m = normalise(null);
    assert.equal(m.biome, 'agri');
    const g = normalise({ seaLevel: 9, rivers: -3, ruggedness: 'x', features: 5 });
    assert.deepEqual([g.seaLevel, g.rivers, g.ruggedness, g.features], [0.85, 0, 0.5, 1]);
});

test('buildWorld survives garbage settings', () => {
    const w = buildWorld({ biome: 'forge', landmass: 'moon', seed: 'abc', seaLevel: NaN });
    assert.equal(w.height.length, 256 * 256);
});

test('buildWorld completes under 300 ms', () => {
    buildWorld(S); // warm up
    for (const landmass of ['continent', 'archipelago', 'pangea', 'random']) {
        const t0 = process.hrtime.bigint();
        buildWorld({ ...S, landmass, seaLevel: 0.85 });
        const ms = Number(process.hrtime.bigint() - t0) / 1e6;
        assert.ok(ms < 300, `${landmass} took ${ms} ms`);
    }
});

// ── Rivers and lakes ──────────────────────────────────────────────────────
const cellOf = (w, p) => Math.round(p[1] * (w.size - 1)) * w.size + Math.round(p[0] * (w.size - 1));

test('rivers: 0 gives none', () => assert.equal(buildWorld({ ...S, rivers: 0 }).rivers.length, 0));

test('rivers: never more than asked, at least one when asked on a normal map', () => {
    for (const n of [1, 4, 12]) {
        const w = buildWorld({ ...S, rivers: n });
        assert.ok(w.rivers.length <= n, `${w.rivers.length} > ${n}`);
        assert.ok(w.rivers.length >= 1);
    }
});

test('rivers: shape, width range, points inside the map, deterministic', () => {
    const w = buildWorld({ ...S, rivers: 8 });
    for (const r of w.rivers) {
        assert.ok(r.points.length >= 12);
        assert.ok(r.width >= 1 && r.width <= 4, `width ${r.width}`);
        for (const [x, y] of r.points) assert.ok(x >= 0 && x <= 1 && y >= 0 && y <= 1);
    }
    assert.deepEqual(buildWorld({ ...S, rivers: 8 }).rivers, w.rivers);
});

test('every river ends in water, the border, or another river', () => {
    for (const seed of [42, 7, 99]) {
        const w = buildWorld({ ...S, seed, rivers: 12 });
        const n = w.size;
        const own = w.rivers.map(r => new Set(r.points.map(p => cellOf(w, p))));
        w.rivers.forEach((r, k) => {
            const last = cellOf(w, r.points[r.points.length - 1]);
            const x = last % n, y = (last - x) / n;
            const border = x === 0 || y === 0 || x === n - 1 || y === n - 1;
            const onEarlier = own.slice(0, k).some(set => set.has(last));
            assert.ok(w.water[last] !== 0 || border || onEarlier, `river ${k} (seed ${seed}) dangles`);
        });
    }
});

test('every river runs downhill or flat on the filled surface', () => {
    const w = buildWorld({ ...S, rivers: 12 });
    for (const r of w.rivers) {
        for (let i = 1; i < r.points.length; i++) {
            const a = w.filled[cellOf(w, r.points[i - 1])];
            const b = w.filled[cellOf(w, r.points[i])];
            assert.ok(b <= a + 0.002, `rises ${a} -> ${b}`);
        }
    }
});

test('rivers start on land and no river visits a cell twice', () => {
    const w = buildWorld({ ...S, rivers: 12 });
    for (const r of w.rivers) {
        assert.equal(w.water[cellOf(w, r.points[0])], 0);
        const cells = r.points.map(p => cellOf(w, p));
        assert.equal(new Set(cells).size, cells.length);
    }
});

test('filled is never below height', () => {
    const w = buildWorld(S);
    for (let i = 0; i < w.height.length; i++) assert.ok(w.filled[i] >= w.height[i]);
});

test('sea level 0 still terminates rivers', () => {
    const w = buildWorld({ ...S, seaLevel: 0, rivers: 12 });
    assert.equal(w.water.filter(v => v === 1).length, 0);
    for (const r of w.rivers) assert.ok(r.points.length < w.size * w.size);
});

test('sea level 0.85 archipelago: no crash, river starts are distinct', () => {
    for (const landmass of ['archipelago', 'random']) {
        const w = buildWorld({ ...S, seaLevel: 0.85, landmass, rivers: 12 });
        const starts = w.rivers.map(r => cellOf(w, r.points[0]));
        assert.equal(new Set(starts).size, starts.length);
        assert.ok(w.rivers.length <= 12);
    }
});

test('still under 300 ms with 12 rivers', () => {
    buildWorld({ ...S, rivers: 12 }); // warm up
    for (const landmass of ['continent', 'archipelago', 'pangea', 'random']) {
        for (const seaLevel of [0, 0.35, 0.85]) {
            const t0 = process.hrtime.bigint();
            buildWorld({ ...S, landmass, seaLevel, rivers: 12 });
            const ms = Number(process.hrtime.bigint() - t0) / 1e6;
            assert.ok(ms < 300, `${landmass} took ${ms} ms`);
        }
    }
});

test('lakes form: above-sea-level lake cells exist and sit in filled depressions', () => {
    const w = buildWorld({ ...S, landmass: 'pangea', rivers: 12 });
    let lakes = 0;
    for (let i = 0; i < w.height.length; i++) {
        if (w.height[i] >= w.seaLevel && w.water[i] === 2) {
            lakes++;
            assert.ok(w.filled[i] - w.height[i] > 0.004);
        }
    }
    assert.ok(lakes >= 1, 'no pooled lake cells');
});

// Label depressions (4-connected cells above sea with filled - height > LAKE_DEPTH, 0.004).
function labelDepressions(w) {
    const n = w.size, label = new Int32Array(n * n).fill(-1);
    const groups = [];
    const inDep = i => w.water[i] !== 1 && w.filled[i] - w.height[i] > 0.004;
    for (let s = 0; s < n * n; s++) {
        if (label[s] >= 0 || !inDep(s)) continue;
        const cells = [s], id = groups.length;
        label[s] = id;
        for (let k = 0; k < cells.length; k++) {
            const c = cells[k], x = c % n, y = (c - x) / n;
            const nb = [x > 0 ? c - 1 : -1, x < n - 1 ? c + 1 : -1, y > 0 ? c - n : -1, y < n - 1 ? c + n : -1];
            for (const m of nb) if (m >= 0 && label[m] < 0 && inDep(m)) { label[m] = id; cells.push(m); }
        }
        groups.push(cells);
    }
    return { label, groups };
}

test('lake basins are whole: every depression cell is either all-lake or all-land per depression', () => {
    let lakeDeps = 0;
    for (const seed of [42, 7, 99]) {
        for (const landmass of ['pangea', 'continent']) {
            const w = buildWorld({ ...S, seed, landmass, rivers: 12 });
            for (const cells of labelDepressions(w).groups) {
                const kinds = new Set(cells.map(c => w.water[c]));
                assert.equal(kinds.size, 1, `mixed depression (seed ${seed}, ${landmass})`);
                if (kinds.has(2)) lakeDeps++; else assert.ok(kinds.has(0));
            }
        }
    }
    assert.ok(lakeDeps >= 1, 'no lake depressions');
});

test('a lake has no dry hole inside it', () => {
    for (const seed of [42, 7, 99]) {
        const w = buildWorld({ ...S, seed, landmass: 'pangea', rivers: 12 });
        const n = w.size, { label } = labelDepressions(w);
        for (let i = 0; i < n * n; i++) {
            if (w.water[i] !== 2 || label[i] < 0) continue;
            const x = i % n, y = (i - x) / n;
            const nb = [x > 0 ? i - 1 : -1, x < n - 1 ? i + 1 : -1, y > 0 ? i - n : -1, y < n - 1 ? i + n : -1];
            for (const m of nb) if (m >= 0 && label[m] === label[i]) assert.equal(w.water[m], 2);
        }
    }
});

test('river sources are at least 20 grid cells apart', () => {
    for (const seed of [42, 7, 99]) {
        const w = buildWorld({ ...S, seed, rivers: 12 });
        const n = w.size - 1;
        const starts = w.rivers.map(r => r.points[0]);
        for (let a = 0; a < starts.length; a++) {
            for (let b = a + 1; b < starts.length; b++) {
                const d = Math.hypot((starts[a][0] - starts[b][0]) * n, (starts[a][1] - starts[b][1]) * n);
                assert.ok(d >= 20 - 1e-6, `sources ${a},${b} only ${d} apart (seed ${seed})`);
            }
        }
    }
});

test('a river stops at a junction: only its last point lies on an earlier river', () => {
    let junctions = 0;
    for (const seed of [42, 7, 99, 5, 123]) {
        const w = buildWorld({ ...S, seed, rivers: 12 });
        const earlier = new Set();
        for (const r of w.rivers) {
            const cells = r.points.map(p => cellOf(w, p));
            cells.forEach((c, i) => {
                if (earlier.has(c)) {
                    assert.equal(i, cells.length - 1, `river runs through an earlier river (seed ${seed})`);
                    junctions++;
                }
            });
            cells.forEach(c => earlier.add(c));
        }
    }
    assert.ok(junctions >= 1, 'the sampled seeds should produce at least one junction');
});

// ── Biome styles, features and roads ──────────────────────────────────────
const KEYS = ['agri', 'ash', 'death', 'desert', 'hive', 'ice', 'jungle', 'sept', 'tomb'];
const F1 = { ...S, features: 1 };
const HUBS = ['ruins', 'spire', 'settlement'];

test('every biome key has a style', () => assert.deepEqual(Object.keys(BIOME_STYLES).sort(), KEYS));

test('styles are well formed', () => {
    const isRgb = c => Array.isArray(c) && c.length === 3 && c.every(v => Number.isInteger(v) && v >= 0 && v <= 255);
    for (const k of KEYS) {
        const st = BIOME_STYLES[k];
        assert.ok(st.bands.length >= 4, k);
        let prev = 0;
        for (const b of st.bands) {
            assert.ok(b[0] > prev && b[0] <= 1, `${k} band order`);
            prev = b[0];
            assert.ok(isRgb(b.slice(1)), k);
        }
        assert.equal(st.bands[st.bands.length - 1][0], 1, k);
        for (const c of ['sea', 'shallows', 'lake']) assert.ok(isRgb(st[c]), `${k}.${c}`);
        assert.ok(['water', 'sludge', 'lava', 'frozen', 'dry'].includes(st.river), k);
        assert.ok(st.features.length >= 2);
        assert.equal(typeof st.roads, 'boolean');
    }
});

test('river styles and roads per biome match the brief', () => {
    const want = { agri: ['water', true], death: ['sludge', false], ice: ['frozen', true], ash: ['lava', false],
        desert: ['dry', false], jungle: ['water', false], hive: ['sludge', true], sept: ['water', true], tomb: ['dry', false] };
    for (const k of KEYS) assert.deepEqual([BIOME_STYLES[k].river, BIOME_STYLES[k].roads], want[k], k);
});

test('features 0 gives no features and no roads', () => {
    for (const k of KEYS) {
        const w = buildWorld({ ...S, biome: k, features: 0 });
        assert.equal(w.features.length, 0);
        assert.equal(w.roads.length, 0);
    }
});

test('more features density gives more features', () => {
    const low = buildWorld({ ...S, features: 0.2 }).features.length;
    const high = buildWorld(F1).features.length;
    assert.ok(low >= 5, `low ${low}`);
    assert.ok(high >= 40, `high ${high}`);
    assert.ok(high > low);
    assert.ok(high <= 60);
});

test('features never sit on sea; ordinary ones avoid lakes too', () => {
    let checked = 0;
    for (const k of KEYS) for (const seed of [42, 7]) {
        const w = buildWorld({ ...F1, biome: k, seed });
        for (const f of w.features) {
            const wt = w.water[cellOf(w, [f.x, f.y])];
            assert.notEqual(wt, 1, `${k} ${f.kind} on sea`);
            if (f.kind !== 'oasis' && f.kind !== 'swamp') assert.equal(wt, 0, `${k} ${f.kind} on lake`);
            checked++;
        }
    }
    assert.ok(checked >= 200, `checked ${checked}`);
});

test('feature kinds belong to the biome and fields are sane', () => {
    for (const k of KEYS) {
        const w = buildWorld({ ...F1, biome: k });
        assert.ok(w.features.length >= 10, `${k} has ${w.features.length}`);
        for (const f of w.features) {
            assert.ok(BIOME_STYLES[k].features.includes(f.kind), `${k}: ${f.kind}`);
            assert.ok(f.x >= 0 && f.x <= 1 && f.y >= 0 && f.y <= 1);
            assert.ok(f.size > 0);
        }
    }
});

test('features keep 8 grid cells apart', () => {
    const w = buildWorld(F1);
    const n = w.size - 1;
    for (let a = 0; a < w.features.length; a++) for (let b = a + 1; b < w.features.length; b++) {
        const d = Math.hypot((w.features[a].x - w.features[b].x) * n, (w.features[a].y - w.features[b].y) * n);
        assert.ok(d >= 8 - 1e-6, `features ${a},${b} only ${d} apart`);
    }
});

test('oasis and swamp sit on land next to water', () => {
    let wet = 0;
    for (const [biome, kind] of [['desert', 'oasis'], ['jungle', 'swamp']]) {
        for (const seed of [42, 7, 99]) {
            const w = buildWorld({ ...F1, biome, seed });
            const n = w.size;
            for (const f of w.features.filter(f => f.kind === kind)) {
                const c = cellOf(w, [f.x, f.y]);
                const x = c % n, y = (c - x) / n;
                assert.equal(w.water[c], 0);
                let touches = false;
                for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
                    const nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < n && ny < n && w.water[ny * n + nx] !== 0) touches = true;
                }
                assert.ok(touches, `${kind} not beside water`);
                wet++;
            }
        }
    }
    assert.ok(wet >= 6, `wet features ${wet}`);
});

test('features and roads are deterministic', () => {
    const a = buildWorld(F1), b = buildWorld(F1);
    assert.deepEqual(a.features, b.features);
    assert.deepEqual(a.roads, b.roads);
    assert.notDeepEqual(buildWorld({ ...F1, seed: 43 }).features, a.features);
});

test('biomes without roads produce none', () => {
    for (const k of ['tomb', 'death', 'ash', 'desert', 'jungle'])
        assert.equal(buildWorld({ ...F1, biome: k }).roads.length, 0, k);
});

test('roads: counted, capped, joining hubs, never crossing sea', () => {
    let total = 0;
    for (const biome of ['agri', 'ice', 'hive', 'sept']) for (const seed of [42, 7, 99]) {
        const w = buildWorld({ ...F1, biome, seed });
        assert.ok(w.roads.length <= 8);
        const hubs = w.features.filter(f => HUBS.includes(f.kind));
        for (const r of w.roads) {
            total++;
            assert.ok(r.length >= 2);
            const a = r[0], b = r[r.length - 1];
            assert.ok(hubs.some(h => h.x === a[0] && h.y === a[1]), 'start is a hub');
            assert.ok(hubs.some(h => h.x === b[0] && h.y === b[1]), 'end is a hub');
            assert.ok(Math.hypot(a[0] - b[0], a[1] - b[1]) <= 0.3 + 1e-9);
            for (let i = 1; i < r.length; i++) {
                const steps = Math.ceil(Math.hypot(r[i][0] - r[i - 1][0], r[i][1] - r[i - 1][1]) * (w.size - 1));
                for (let s = 0; s <= steps; s++) {
                    const t = s / steps;
                    const p = [r[i - 1][0] + (r[i][0] - r[i - 1][0]) * t, r[i - 1][1] + (r[i][1] - r[i - 1][1]) * t];
                    assert.notEqual(w.water[cellOf(w, p)], 1, `${biome} road crosses sea`);
                }
            }
        }
    }
    assert.ok(total >= 10, `only ${total} roads`);
});

test('road cap scales with features', () => {
    const w = buildWorld({ ...S, features: 0.25 });
    assert.ok(w.roads.length <= 2);
});

test('features at 1 still build under 300 ms', () => {
    buildWorld(F1);
    for (const biome of ['agri', 'desert', 'hive']) {
        const t0 = process.hrtime.bigint();
        buildWorld({ ...F1, biome, rivers: 12 });
        const ms = Number(process.hrtime.bigint() - t0) / 1e6;
        assert.ok(ms < 300, `${ms} ms`);
    }
});

test('noise parameters are clamped to the slider ranges', () => {
    const n = normalise({ lacunarity: 99, persistence: -1, scale: 0, octaves: 4 });
    assert.deepEqual([n.lacunarity, n.persistence, n.scale], [4, 0.1, 0.5]);
    const m = normalise({ lacunarity: 0.1, persistence: 5, scale: 100, octaves: 99 });
    assert.deepEqual([m.lacunarity, m.persistence, m.scale, m.octaves], [1, 0.95, 8, 8]);
});

test('a negative persistence still yields finite heights', () => {
    const w = buildWorld({ ...S, persistence: -1, octaves: 4 });
    for (const h of w.height) assert.ok(Number.isFinite(h));
});

test('the world exposes the sampler it was built with, identical to a fresh makeSampler', () => {
    for (const landmass of ['continent', 'archipelago', 'random']) {
        const cfg = { ...S, landmass };
        const w = buildWorld(cfg), fresh = makeSampler(cfg);
        for (const [x, y] of [[0, 0], [0.3, 0.7], [0.5, 0.5], [1, 1], [0.123, 0.987]]) {
            assert.equal(w.sample(x, y), fresh(x, y), landmass);
        }
    }
});
