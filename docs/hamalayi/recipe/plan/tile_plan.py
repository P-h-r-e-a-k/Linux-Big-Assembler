# The atlas and painting plan (a planning aid): which LBA1 brick (or ice-cliff pair) goes into which atlas slot, and for each slot the
# runs of cells along each row to paint with it. Slots are 32 x 32 squares of the 256 x 256 atlas, row by row, skipping the slot the
# island's sea tile is in (read from the map's ILE: the sea cell's texture).
import json, struct, sys, collections
sys.path.insert(0, r'E:\dump\TEMP\demo96'); import hqr
plan = json.load(open(r'E:\dump\TEMP\hamalayi\s96\paint_plan.json'))
kinds = {tuple(map(int, k.split(','))): v for k, v in plan['kinds'].items()}
ile = r'E:\dump\TEMP\hamalayi\s96\saved.ile'
polys = struct.unpack('<%dI' % (64 * 64 * 2), hqr.read(ile, 3 + 2))
defs = struct.unpack('<%dH' % (len(hqr.read(ile, 3 + 3)) // 2), hqr.read(ile, 3 + 3))
# the sea cell's tile (the corner cell with no ground): its first triangle's texture definition's corner
sea_cells = [c for c, (k, _) in kinds.items() if k == 'sea']
sx, sz = sea_cells[0]
ti = (polys[sz * 128 + sx * 2] >> 19) & 0x1FFF
u, v = defs[ti * 6] // 256, defs[ti * 6 + 1] // 256
sea_slot = (v // 32) * 8 + u // 32
CLIFFS = [(3033, 3034), (3040, 3041), (3042, 3048), (3049, 3050)]
tops = [b for b, _ in plan['use']][:46]
slots, s = [], 0
def next_slot():
    global s
    while s == sea_slot: s += 1
    r = s; s += 1; return r
for i, pair in enumerate(CLIFFS): slots.append({'slot': next_slot(), 'cliff': pair})
for b in tops: slots.append({'slot': next_slot(), 'brick': b})
slot_of_brick = {e['brick']: e['slot'] for e in slots if 'brick' in e}
cells_of = collections.defaultdict(list)
for (x, z), (k, b) in kinds.items():
    if k == 'sea': continue
    if k == 'cliff':
        # (the cliff tiles by turns: a hash of the cell)
        cells_of[slots[(x * 7 + z * 13) % 4]['slot']].append((x, z))
    else:
        cells_of[slot_of_brick.get(b, slot_of_brick[2713])].append((x, z))
for e in slots:
    cs = sorted(cells_of[e['slot']], key=lambda c: (c[1], c[0]))
    runs = []
    for (x, z) in cs:
        if runs and runs[-1]['z'] == z and runs[-1]['x1'] == x - 1: runs[-1]['x1'] = x
        else: runs.append({'z': z, 'x0': x, 'x1': x})
    e['x'] = (e['slot'] % 8) * 32; e['y'] = (e['slot'] // 8) * 32; e['runs'] = runs
json.dump({'sea_slot': sea_slot, 'sea_cells': sea_cells, 'slots': slots}, open(r'E:\dump\TEMP\hamalayi\s96\tile_plan.json', 'w'))
print('sea slot', sea_slot, 'at', (u, v), '; slots', len(slots), '; runs', sum(len(e['runs']) for e in slots), '; cells', sum(len(cells_of[e['slot']]) for e in slots))
