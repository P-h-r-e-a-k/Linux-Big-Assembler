# The objects of LBA1 scene 96 as Object maker designs (a planning aid, read from lba1ground's bricks.csv): each object's bricks, cell by
# cell relative to its own corner (layer 0 its foot), what a user fills in behind (LBA1 leaves the cells it never shows empty), and where
# each copy of it stands in the map's cube (cube-local world units: the cube (7,7) is LBA1's grid, a cell 512, a layer 256).
import csv, json, collections
B = [(int(r['x']), int(r['y']), int(r['z']), int(r['brick']), int(r['block'])) for r in csv.DictReader(open(r'E:\dump\TEMP\hamalayi\s96\bricks.csv'))]
at = {(x, y, z): (b, k) for x, y, z, b, k in B}

def design(cells):
    x0 = min(c[0] for c in cells); y0 = min(c[1] for c in cells); z0 = min(c[2] for c in cells)
    return [[x - x0, y - y0, z - z0, b] for (x, y, z), b in sorted(cells.items(), key=lambda c: (c[0][1], c[0][2], c[0][0]))], (x0, y0, z0)

def place(cells):
    xs = [c[0] for c in cells]; ys = [c[1] for c in cells]; zs = [c[2] for c in cells]
    return {'X': round((min(xs) + max(xs) + 1) / 2 * 512), 'Y': min(ys) * 256, 'Z': round((min(zs) + max(zs) + 1) / 2 * 512)}

objects = []

# the snowy tree (block 16): 2 x 2 cells, 6 layers, its back cell only the top brick (2830) -- the column under it filled with its front
# neighbour's (+z) bricks
tree_at = [(x, y, z) for (x, y, z), (b, k) in at.items() if b == 2830 and k == 16]
ax, ay, az = (50, 7, 6)
tree = {(x, y, z): at[(x, y, z)][0] for x in (ax, ax + 1) for z in (az, az + 1) for y in range(ay - 5, ay + 1) if (x, y, z) in at}
fill = {(ax, y, az): at[(ax, y, az + 1)][0] for y in range(ay - 5, ay)}
cells, _ = design({**tree, **fill})
objects.append({'name': 'tree', 'cells': cells, 'places': [place({(x, y - 5, z), (x + 1, y, z + 1)}) for x, y, z in sorted(tree_at, key=lambda t: (t[2], t[0]))]})

# the big tree (block 37)
big = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k == 37}
fill = {(23, y, 2): at[(23, y, 3)][0] for y in range(17, 22)}
cells, _ = design({**big, **fill})
objects.append({'name': 'bigtree', 'cells': cells, 'places': [place(big)]})

# the hut (block 196): 3 x 3 cells, 9 layers; the cells behind its two walls LBA1 shows filled with its wall brick
hut = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k == 196}
for x in (39, 40):
    for z in (54, 55):
        for y in range(1, 9): hut.setdefault((x, y, z), 7863)
cells, _ = design(hut)
objects.append({'name': 'hut', 'cells': cells, 'places': [place(hut)]})

# the ski lift's pylon (blocks 122, 119, 123: the post; 198: the arm with the cable's wheel), four of them
posts = sorted({(x, z) for (x, y, z), (b, k) in at.items() if k == 119})
pylon_places = []
first = None
for px, pz in posts:
    pc = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k in (119, 122, 123, 198) and x == px and pz - 2 <= z <= pz}
    if first is None: first = pc
    pylon_places.append(place(pc))
cells, _ = design(first)
objects.append({'name': 'pylon', 'cells': cells, 'places': pylon_places})

# the fence: a piece three cells long along z (block 184, four of them) and the corner piece along x (block 183)
f184 = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k == 184}
starts = sorted({(x, z) for (x, y, z), b in f184.items() if b == 7849})
piece = {(x, y, z): b for (x, y, z), b in f184.items() if x == starts[0][0] and starts[0][1] <= z <= starts[0][1] + 2}
cells, _ = design(piece)
objects.append({'name': 'fence', 'cells': cells, 'places': [place({(x, 1, z), (x, 3, z + 2)}) for x, z in starts]})
f183 = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k == 183}
cells, _ = design(f183)
objects.append({'name': 'fencecorner', 'cells': cells, 'places': [place(f183)]})

# the snowman (blocks 139 and 145)
snow = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k in (139, 145)}
cells, _ = design(snow)
objects.append({'name': 'snowman', 'cells': cells, 'places': [place(snow)]})

# the little tree (block 8), five of them
small_at = [(x, y, z) for (x, y, z), (b, k) in at.items() if b == 2759 and k == 8]
x, y, z = small_at[0]
cells, _ = design({(x, y + i, z): at[(x, y + i, z)][0] for i in range(3)})
objects.append({'name': 'smalltree', 'cells': cells, 'places': [place({(x, y, z), (x, y + 2, z)}) for x, y, z in small_at]})

# the stone by the first pylon's foot (block 144)
stone = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k == 144}
cells, _ = design(stone)
objects.append({'name': 'stone', 'cells': cells, 'places': [place(stone)]})

# the rock walls: block 191 along the far edge (two objects: a body holds 500 faces), block 190 down the side
r191 = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k == 191}
for half, (lo, hi) in enumerate([(44, 48), (49, 53), (54, 58), (59, 63)]):
    part = {c: b for c, b in r191.items() if lo <= c[0] <= hi}
    cells, _ = design(part)
    objects.append({'name': f'rockwall{half + 1}', 'cells': cells, 'places': [place(part)]})
r190 = {(x, y, z): b for (x, y, z), (b, k) in at.items() if k == 190}
cells, _ = design(r190)
objects.append({'name': 'rockwall5', 'cells': cells, 'places': [place(r190)]})

# the designs as fills where a whole box of cells shares a brick (fewer steps), else one cell at a time
for o in objects:
    o['bricks'] = len(o['cells'])
json.dump({'objects': objects}, open(r'E:\dump\TEMP\hamalayi\s96\object_plan.json', 'w'))
for o in objects: print(o['name'], o['bricks'], 'bricks,', len(o['places']), 'copies:', o['places'][:3])
