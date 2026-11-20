'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('path');

const { GRID, normalise, makeSampler, sampleHeight, buildWorld, _classifyWater } =
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

test('world shape: size, arrays and empty feature lists', () => {
    const w = buildWorld(S);
    assert.equal(w.size, 256);
    assert.ok(w.height instanceof Float32Array);
    assert.ok(w.water instanceof Uint8Array);
    assert.equal(w.height.length, 256 * 256);
    assert.ok(Array.isArray(w.rivers));
    assert.ok(w.filled instanceof Float32Array);
    assert.equal(w.filled.length, 256 * 256);
    assert.deepEqual([w.features, w.roads], [[], []]);
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
        console.log(`# buildWorld ${landmass}: ${ms.toFixed(1)} ms`);
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
            console.log(`# buildWorld ${landmass} sea ${seaLevel} 12 rivers: ${ms.toFixed(1)} ms`);
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
    console.log(`# junctions checked: ${junctions}`);
});
