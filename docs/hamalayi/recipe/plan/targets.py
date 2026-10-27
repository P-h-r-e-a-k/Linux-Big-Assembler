# The reference for painting LBA1 scene 96's ground by hand (a checking aid, not an import): from lba1ground's ground.csv, each LBA2
# vertex's height (the highest of its four cells' tops, so a terrace keeps its whole cells and its cliff falls in the cell below it, as
# LBA1 draws it), the trees (small bumps of snow-coded bricks standing well over the ground round them) taken off the ground, and the
# vertices of each height split into squares -- one square-brush dab each (centre vertex, radius). Writes targets.json and prints a summary.
import csv, json, collections
cells = {}
for row in csv.DictReader(open(r'E:\dump\TEMP\hamalayi\s96\ground.csv')):
    cells[(int(row['x']), int(row['z']))] = float(row['top'])
# trees: connected same-top pieces of up to 6 cells standing 2 layers and more over every neighbour round them
seen, trees = set(), []
for c in list(cells):
    if c in seen: continue
    h = cells[c]; piece = []; q = [c]; seen.add(c)
    while q:
        p = q.pop(); piece.append(p)
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (p[0] + d[0], p[1] + d[1])
            if n in cells and n not in seen and cells[n] == h: seen.add(n); q.append(n)
    if len(piece) > 6: continue
    ring = {(p[0] + dx, p[1] + dz) for p in piece for dx in (-1, 0, 1) for dz in (-1, 0, 1)} - set(piece)
    ring = [cells[r] for r in ring if r in cells]
    if ring and h - max(ring) >= 2:
        trees.append({'cells': piece, 'top': h, 'ground': max(ring)})
        for p in piece: cells[p] = max(ring)
# the vertices: 65 x 65, world units (a layer 256); no ground (the scene's edge) is the sea's floor
SEA = -256
def top(x, z): return cells.get((x, z))
V = {}
for j in range(65):
    for i in range(65):
        around = [t for t in (top(i - 1, j - 1), top(i, j - 1), top(i - 1, j), top(i, j)) if t is not None]
        V[(i, j)] = int(round(max(around) * 256)) if around else SEA
# squares: greedy, the biggest first, each square's vertices all of one height and not yet painted
levels = collections.defaultdict(set)
for k, h in V.items(): levels[h].add(k)
dabs = []
for h in sorted(levels):
    left = set(levels[h])
    for r in range(16, -1, -1):
        for (i, j) in sorted(left):
            if (i, j) not in left: continue
            sq = [(i + dx, j + dz) for dx in range(-r, r + 1) for dz in range(-r, r + 1)]
            if all(p in left for p in sq):
                dabs.append({'level': h, 'x': i, 'z': j, 'r': r})
                left -= set(sq)
json.dump({'vertices': {f'{i},{j}': h for (i, j), h in V.items()}, 'dabs': dabs, 'trees': trees}, open(r'E:\dump\TEMP\hamalayi\s96\targets.json', 'w'))
by = collections.Counter(d['r'] for d in dabs)
print(f'{len(levels)} heights, {len(dabs)} dabs (by radius: {sorted(by.items())}), {len(trees)} trees')
# runs: each height's vertices along each row (z), contiguous -- one stroke each (a dab at its start, a stroke to its end, radius 0)
runs = []
for h in sorted(levels):
    for j in range(65):
        xs = sorted(i for (i, jj) in levels[h] if jj == j)
        k = 0
        while k < len(xs):
            s = k
            while k + 1 < len(xs) and xs[k + 1] == xs[k] + 1: k += 1
            runs.append({'level': h, 'z': j, 'x0': xs[s], 'x1': xs[k]})
            k += 1
d = json.load(open(r'E:\dump\TEMP\hamalayi\s96\targets.json'))
d['runs'] = runs
json.dump(d, open(r'E:\dump\TEMP\hamalayi\s96\targets.json', 'w'))
print(f'{len(runs)} row strokes')
