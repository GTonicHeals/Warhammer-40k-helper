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
        assert.equal(w.water[i] !== 0, w.height[i] < w.seaLevel);
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
    assert.deepEqual([w.rivers, w.features, w.roads], [[], [], []]);
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
