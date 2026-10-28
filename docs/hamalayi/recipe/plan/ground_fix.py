# The ground under LBA1 scene 96's objects (a checking aid): every column's top without the bricks of the blocks made into objects (the
# trees 16 and 37, the hut 196, the small trees 8, the snowman 139/145, the stone 144) -- LBA1's trees are snow-coded bricks, so the first
# reference took a clump of them for ground. The vertices (the highest of their four cells, as targets.py) that differ from the island
# now, as runs along each row of one level for paint_ground.ps1's way (Flatten, a stroke a run). Writes ground_fix.json.
import csv, json, struct, sys, collections
import os; HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE); import hqr
OBJECT_BLOCKS = {16, 37, 196, 8, 139, 145, 144}
GROUND_CODE = lambda c: c in (0xF1, 0x06, 0x77, 0x88) or (c & 0xF0) in (0x40, 0x60)
cols = collections.defaultdict(list)
for r in csv.DictReader(open(os.path.join(HERE, 'bricks.csv'))):
    cols[(int(r['x']), int(r['z']))].append((int(r['y']), int(r['block']), int(r['code'], 16), int(r['shape'])))
top = {}
for (x, z), bs in cols.items():
    g = [(y, s) for y, b, c, s in bs if GROUND_CODE(c) and b not in OBJECT_BLOCKS]
    if not g: continue
    y, s = max(g)
    top[(x, z)] = y + (0.5 if 2 <= s <= 13 else 1)
SEA = -256
want = {}
for j in range(65):
    for i in range(65):
        around = [top[c] for c in ((i - 1, j - 1), (i, j - 1), (i - 1, j), (i, j)) if c in top]
        want[(i, j)] = int(round(max(around) * 256)) if around else SEA
ile = sys.argv[1]   # the map workspace's SKIRESRT.ILE (<sandbox>\game\_MAP_WORKSPACE\SKIRESRT.ILE)
h = struct.unpack('<%dh' % (65 * 65), hqr.read(ile, 3 + 4))
# (the hut's block, flattened on purpose before, stays as it is now: 256)
diff = {(i, j): want[(i, j)] for j in range(65) for i in range(65) if h[j * 65 + i] != want[(i, j)] and not (39 <= i <= 42 and 54 <= j <= 57)}
runs = []
for (i, j) in sorted(diff, key=lambda k: (diff[k], k[1], k[0])):
    lvl = diff[(i, j)]
    if runs and runs[-1]['level'] == lvl and runs[-1]['z'] == j and runs[-1]['x1'] == i - 1: runs[-1]['x1'] = i
    else: runs.append({'level': lvl, 'z': j, 'x0': i, 'x1': i})
cells = sorted({(c[0] + dx, c[1] + dz) for c in diff for dx in (-1, 0) for dz in (-1, 0) if 0 <= c[0] + dx < 64 and 0 <= c[1] + dz < 64})
# each cell's tile as tile_plan.py chose them: a cliff (corners a layer and more apart) one of the four cliff tiles stood up the slope,
# else its ground brick's tile (the commonest snow's when it has none of its own)
plan = json.load(open(os.path.join(HERE, 'tile_plan.json')))
brick_tile = {e['brick']: (e['x'], e['y']) for e in plan['slots'] if 'brick' in e}
cliff_tiles = [(e['x'], e['y']) for e in plan['slots'] if 'cliff' in e]
ground_brick = {}
for r in csv.DictReader(open(os.path.join(HERE, 'bricks.csv'))):
    x, z, y, b, c = int(r['x']), int(r['z']), int(r['y']), int(r['block']), int(r['code'], 16)
    if GROUND_CODE(c) and b not in OBJECT_BLOCKS and (x, z) in top and y + 1 >= top[(x, z)] - 0.5:
        ground_brick[(x, z)] = int(r['brick'])
tiles = []
# (the sea's corner cells keep the island's sea tile: they have no LBA1 ground to take a tile from)
sea = {tuple(c) for c in plan['sea_cells']}
for (x, z) in cells:
    if (x, z) in sea: continue
    c = [want[(x, z)], want[(x + 1, z)], want[(x, z + 1)], want[(x + 1, z + 1)]]
    if max(c) - min(c) >= 256: tiles.append({'x': x, 'z': z, 'tile': cliff_tiles[(x * 7 + z * 13) % 4], 'cliff': True})
    else: tiles.append({'x': x, 'z': z, 'tile': brick_tile.get(ground_brick.get((x, z)), brick_tile[2713]), 'cliff': False})
json.dump({'runs': runs, 'cells': cells, 'tiles': tiles}, open(os.path.join(HERE, 'ground_fix.json'), 'w'))
print(len(diff), 'vertices differ;', len(runs), 'runs;', len(cells), 'cells round them; up', sum(1 for k in diff if diff[k] > h[k[1] * 65 + k[0]]), 'down', sum(1 for k in diff if diff[k] < h[k[1] * 65 + k[0]]))
