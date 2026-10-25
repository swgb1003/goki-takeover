"""Blender 5.x authoring of the exterminator (the final defence's professional) -> .blend + engine JSON.
Run: blender --background --threads 2 --python Tools/Blender/build_exterminator_model.py [-- --render-preview]
Same skeleton, proportions and pipeline as the resident (resident_model.py): primitives fused by voxel remesh,
cloth folds displaced along normals, three LODs, colour/smoothness/AO in vertex colours, two bones per vertex.
Because the bones match, the game can put this mesh on the resident's skin when the exterminator arrives.
Look: a hooded white disposable coverall with a zip and a 'no roaches' badge, a full-face respirator with a dark
visor and two pink filter cartridges, green nitrile gloves over elastic cuffs, white rubber boots, a yellow
backpack pump sprayer on the back - and, held in the right hand like everyone else in this house, a slipper.
Only the Exterminator outputs are written. No third-party assets.
"""
import bpy, math, json, sys
from mathutils import Vector, Matrix, noise
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import resident_model as R
from resident_model import V, Shape, smooth, lerp, clamp, fuse, to_object, uvec, P

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_GokiTakeover/Art/BlenderModels/Source'
AUTHOR = ROOT / 'ArtSource/Blender'
bpy.ops.wm.read_factory_settings(use_empty=True)

TYVEK = (.84, .855, .87, .16); SEAM = (.70, .72, .74, .14); ZIP = (.22, .23, .25, .55)
RUBBER = (.045, .05, .055, .34); VISOR = (.025, .04, .055, .93); FILTER = (.74, .17, .42, .30); FILTER_CAP = (.20, .20, .22, .40)
GLOVE = (.09, .40, .21, .58); BOOT = (.80, .82, .82, .45); SOLE = (.09, .09, .09, .20)
TANK = (.88, .70, .10, .50); TANK_DARK = (.10, .10, .11, .35); STRAP = (.05, .05, .055, .20)
BADGE_RED = (.72, .07, .06, .30); BADGE_WHITE = (.92, .92, .90, .25)
SLIP_UP = (.05, .17, .33, .16); SLIP_SOLE = (.30, .31, .32, .22)

def tyvek(p, n, base=TYVEK):
    # Disposable coverall: cool white, faint crinkle, slightly greyer in the deep folds (AO adds the rest).
    k = 1 + .045 * noise.noise(p * 34) + .02 * noise.noise(V(p.x * 90, p.y * 30, p.z * 90))
    return (base[0] * k, base[1] * k, base[2] * k * 1.01, base[3])

def crinkle(p, n):
    # Stiff paper-like fabric: sharp, irregular creases rather than soft drapes.
    c = .006 * noise.noise(V(p.x * 6, p.y * 13, p.z * 6)) + .003 * abs(noise.noise(p * 20)) - .0015
    knee = math.exp(-((p.y - 1.15) / .12) ** 2) * .008 * math.sin(p.y * 60) * smooth(.05, .2, abs(p.x))
    elbow = 0.0
    for s in (-1, 1):
        el = P('Forearm_R'); el = V(s * el.x, el.y, el.z)
        elbow += math.exp(-((p - el).length / .14) ** 2) * .007 * math.sin(p.y * 70)
    waist = math.exp(-((p.y - 2.30) / .08) ** 2) * .007 * math.sin(p.x * 40 + p.z * 30)   # belted gather
    return c + knee + elbow + waist

# ---------------------------------------------------------------- shapes (Unity coordinates)
HOOD = [(4.175, .05, .04, -.09), (4.15, .12, .085, -.17), (4.09, .185, .14, -.245), (4.00, .214, .175, -.29), (3.90, .222, .19, -.30),
        (3.80, .220, .19, -.295), (3.70, .208, .18, -.275), (3.60, .192, .165, -.245), (3.52, .186, .16, -.225), (3.44, .215, .175, -.215), (3.38, .27, .19, -.22)]

def hood_shape():
    rows = []
    for a, b in zip(HOOD, HOOD[1:]):
        for k in range(4): rows.append(tuple(lerp(a[i], b[i], k / 4) for i in range(4)))
    rows.append(HOOD[-1]); seg = 64; pts = []
    for y, hw, zf, zb in rows:
        for i in range(seg):
            a = math.tau * i / seg; c = math.cos(a); s = math.sin(a); n = 2.3
            pts.append(V(math.copysign(abs(c) ** (2 / n), c) * hw, y, math.copysign(abs(s) ** (2 / n), s) * (zf if s > 0 else -zb) - .02))
    faces = []
    for j in range(len(rows) - 1):
        for i in range(seg):
            a = j * seg + i; b = j * seg + (i + 1) % seg; faces.append((a, b, b + seg, a + seg))
    pts.append(V(0, HOOD[0][0] + .004, -.04)); pts.append(V(0, HOOD[-1][0] - .004, -.02)); top = len(pts) - 2; bot = len(pts) - 1
    last = (len(rows) - 1) * seg
    for i in range(seg): faces.append((top, (i + 1) % seg, i)); faces.append((bot, last + i, last + (i + 1) % seg))
    s = Shape(); s.v = pts; s.f = faces
    # Elastic hood rim pressed around the mask.
    rim = [V(math.sin(a) * .185, 3.76 + math.cos(a) * .235, .13 + .03 * math.cos(a) ** 2) for a in [i * math.tau / 36 for i in range(37)]]
    s.chain(rim, [.022] * 37, 10)
    for x in (-1, 1):   # straps around the back of the hood
        s.sweep([V(x * .17, 3.88, .06), V(x * .215, 3.90, -.12), V(0 * x + x * .05, 3.93, -.31)], [.012] * 3, [.004] * 3, V(0, 1, 0), 4, 10)
        s.sweep([V(x * .17, 3.64, .07), V(x * .205, 3.64, -.12), V(x * .05, 3.66, -.27)], [.012] * 3, [.004] * 3, V(0, 1, 0), 4, 10)
    return s

def mask_shape():
    s = Shape()
    s.ellipsoid(V(0, 3.745, .115), V(.175, .215, .13), R.rot_xyz(x=-6), 36, 24)                 # face seal and body
    s.ellipsoid(V(0, 3.62, .19), V(.10, .09, .09), None, 24, 16)                                   # chin cup
    s.capsule(V(0, 3.605, .24), V(0, 3.60, .285), .048, .046, 18)                                  # exhalation valve
    for x in (-1, 1):
        base = V(x * .118, 3.645, .175); d = V(x * .62, -.30, .72).normalized()
        s.capsule(base, base + d * .05, .05, .05, 18)                                              # filter socket
    return s

def visor_shape():
    s = Shape()
    s.ellipsoid(V(0, 3.80, .175), V(.145, .118, .095), R.rot_xyz(x=-10), 40, 24)
    return s

def visor_frame():
    s = Shape()
    ring = [V(math.sin(a) * .148, 3.80 + math.cos(a) * .121, .205 + .025 * math.cos(a)) for a in [i * math.tau / 40 for i in range(41)]]
    s.chain(ring, [.013] * 41, 10)
    return s

def filters_shape():
    s = Shape(); caps = []
    for x in (-1, 1):
        base = V(x * .118, 3.645, .175); d = V(x * .62, -.30, .72).normalized()
        a = base + d * .045; b = base + d * .135
        s.sweep([a, a + d * .01, b - d * .01, b], [.068, .072, .072, .066], None, d.orthogonal(), 2.0, 28)
        caps.append((b, d))
    return s, caps

def torso_shape():
    s = Shape()
    rings = [(1.96, .425, .285, 0), (2.10, .425, .282, 0), (2.30, .40, .272, .005), (2.50, .395, .278, .012), (2.70, .405, .288, .02),
             (2.90, .415, .296, .025), (3.05, .42, .29, .02), (3.18, .425, .262, .005), (3.28, .41, .225, -.01), (3.36, .31, .18, -.02), (3.44, .20, .16, -.03)]
    s.sweep([V(0, y, cz) for y, _, _, cz in rings], [w for _, w, _, _ in rings], [d for _, _, d, _ in rings], V(0, 0, 1), 2.6, 40)
    for x in (-1, 1):
        sh = P('Arm_R'); sh = V(x * sh.x, sh.y, sh.z)
        s.ellipsoid(sh + V(x * .01, -.05, 0), V(.13, .10, .13))
    collar = [V(math.sin(a) * .2, 3.44 + .03 * math.cos(a), -.03 + math.cos(a) * .17) for a in [i * math.tau / 30 for i in range(31)]]
    s.chain(collar, [.035] * 31, 10)
    return s, rings

def sleeve_shape(x):
    t = 'L' if x < 0 else 'R'; s = Shape(); sh = P('Arm_' + t); el = P('Forearm_' + t); wr = P('Hand_' + t)
    pts = [sh.lerp(el, .14), sh.lerp(el, .38), sh.lerp(el, .7), el, el.lerp(wr, .4), el.lerp(wr, .75), el.lerp(wr, .9)]
    rad = [.135, .145, .14, .134, .124, .108, .088]
    s.sweep(pts, rad, [r * .98 for r in rad], V(0, 0, 1), 2.0, 26)
    return s

def glove_shape(right):
    t = 'R' if right else 'L'; s, tips = R.hand_shape(right)
    el = P('Forearm_' + t); wr = P('Hand_' + t); d = (wr - el).normalized()
    s.sweep([wr - d * .2, wr - d * .1, wr - d * .02, wr + d * .03], [.098, .094, .082, .074], None, V(0, 0, 1), 2.0, 22)   # gauntlet over the cuff
    return s

def legs_shape():
    s = Shape()
    s.sweep([V(0, 1.84, 0), V(0, 2.0, 0), V(0, 2.14, .005), V(0, 2.26, .01)], [.39, .42, .425, .42], [.25, .27, .275, .27], V(0, 0, 1), 2.4, 32)
    for x in (-1, 1):
        L = [(2.02, .23, .25, .22, 0), (1.82, .222, .232, .222, .005), (1.58, .205, .212, .222, .012), (1.36, .186, .194, .22, .02),
             (1.18, .178, .186, .219, .03), (1.0, .17, .175, .221, .012), (.8, .162, .162, .222, -.01), (.66, .15, .15, .222, -.02), (.58, .128, .128, .222, -.025)]
        s.sweep([V(x * cx, y, cz) for y, _, _, cx, cz in L], [a for _, a, _, _, _ in L], [b for _, _, b, _, _ in L], V(0, 0, 1), 2.1, 28)
    return s

def boot_shape(x):
    s = Shape(); sx = 1 if x > 0 else -1; cx = x * .222
    s.sweep([V(cx, .70, -.03), V(cx, .45, -.035), V(cx, .22, -.045)], [.122, .118, .114], [.125, .118, .112], V(0, 0, 1), 2.2, 24)
    s.ellipsoid(V(cx, .14, -.10), V(.105, .11, .12))
    s.sweep([V(cx, .14, -.13), V(cx + sx * .006, .13, .08), V(cx + sx * .016, .10, .27), V(cx + sx * .018, .085, .37)], [.112, .118, .122, .11], [.10, .09, .07, .05], V(0, 1, 0), 2.6, 22)
    s.sweep([V(cx, .03, -.20), V(cx, .03, .38)], [.125, .12], [.03, .03], V(0, 1, 0), 4.0, 22)   # sole
    s.capsule(V(cx, .70, -.03), V(cx, .70, -.03), .128, .128, 22)                                 # rolled top
    return s

def tank_shape():
    s = Shape()
    c = V(0, 2.92, -.47)
    s.sweep([c + V(0, -.36, 0), c + V(0, -.32, 0), c + V(0, .30, 0), c + V(0, .34, 0)], [.17, .215, .215, .17], [.13, .165, .165, .13], V(0, 0, 1), 2.4, 32)
    s.capsule(c + V(0, .34, 0), c + V(0, .42, 0), .09, .085, 18)                               # filler cap
    s.capsule(c + V(.24, -.25, 0), c + V(.24, .26, -.02), .022, .022, 12)                      # pump lever
    s.capsule(c + V(.24, .26, -.02), c + V(.12, .36, -.04), .022, .022, 12)
    return s, c

def straps_shape(tank_c):
    s = Shape()
    for x in (-1, 1):
        top = tank_c + V(x * .12, .28, .12)
        pts = [top, V(x * .19, 3.40, -.14), V(x * .22, 3.40, .10), V(x * .21, 3.10, .30), V(x * .20, 2.70, .30), tank_c + V(x * .14, -.30, .14)]
        s.sweep([top, V(x * .19, 3.38, -.16), V(x * .23, 3.40, .06), V(x * .215, 3.16, .29), V(x * .205, 2.76, .305)],
                [.032] * 5, [.008] * 5, V(0, 0, 1), 4, 10)
    return s

# The spray lance in the left hand: rubber grip through the fist, a trigger, a long steel lance and a brass
# nozzle. Held as a continuation of the arm, so raising the arm aims it (the game turns the arm toward the target).
def lance_frame():
    w, d, p, t = R.hand_frame(False)
    return w, d.normalized()
def lance_shape():
    s = Shape(); w, d = lance_frame()
    s.capsule(w + d * .01, w + d * .25, .034, .032, 18)                                           # grip
    s.capsule(w + d * .08 + V(0, 0, .05), w + d * .18 + V(0, 0, .07), .012, .012, 10)            # trigger lever
    s.capsule(w + d * .25, w + d * 1.10, .017, .017, 12)                                          # lance tube
    s.capsule(w + d * 1.10, w + d * 1.21, .028, .034, 16)                                         # nozzle
    s.ellipsoid(w + d * 1.235, V(.04, .04, .04), None, 14, 8)                                     # nozzle mouth
    return s
def nozzle_tip():
    w, d = lance_frame(); return w + d * 1.27
def lance_color(p, n):
    w, d = lance_frame(); t = (p - w).dot(d)
    if t < .26: return (.06, .06, .065, .30)          # black rubber grip
    if t < 1.09: return (.62, .64, .66, .70)          # steel lance
    return (.74, .55, .20, .75)                        # brass nozzle
def hose_shape(tank_c):
    s = Shape(); w, d = lance_frame()
    pts = [tank_c + V(-.16, -.34, .04), V(-.36, 2.40, -.24), V(-.47, 2.12, -.10), V(-.55, 1.95, -.02), w + d * .26 + V(0, 0, -.02)]
    s.chain(pts, [.03, .03, .03, .028, .026], 12)
    return s
def hose_weights(p):
    w, _ = lance_frame()
    near = (p - w).length
    if near < .16: return [('Hand_L', 1.0)]
    if near < .34: k = smooth(.34, .16, near); return R.W(('Hand_L', k), ('Forearm_L', 1 - k))
    if p.y > 2.35: return [('Chest', 1.0)]
    return R.W(('Pelvis', smooth(2.35, 2.15, p.y) * .7 + .3), ('Forearm_L', .0))

def badge_shape():
    s = Shape(); c = V(-.20, 2.96, .312)
    s.ellipsoid(c, V(.10, .10, .014), R.rot_xyz(x=-4), 32, 14)
    return s, c

def badge_color(c):
    def f(p, n):
        q = p - c; r = math.sqrt(q.x * q.x + q.y * q.y) / .1
        if n.z < .3: return BADGE_WHITE
        if .74 < r < 1.0: return BADGE_RED
        # a little roach silhouette, crossed out
        u, v = q.x / .1, q.y / .1
        body = (u / .30) ** 2 + (v / .48) ** 2 < 1
        slash = abs(u + v) < .16 and r < .9
        if slash: return BADGE_RED
        if body: return (.12, .07, .04, .3)
        return BADGE_WHITE
    return f

def filter_color(caps):
    def f(p, n):
        for b, d in caps:
            if (p - b).dot(d) > -.012 and (p - b).length < .09: return FILTER_CAP
        band = abs(math.sin((p.y + p.x) * 90)) > .93
        return (FILTER[0] * (1.15 if band else 1), FILTER[1], FILTER[2], FILTER[3])
    return f

def tank_color(c):
    def f(p, n):
        dy = p.y - c.y
        if dy > .33 or abs(p.x - .24) < .04 and p.z < c.z + .06: return TANK_DARK
        label = abs(dy) < .09 and n.z < -.5
        return (.92, .92, .88, .3) if label else (TANK[0] * (1 + .04 * noise.noise(p * 20)), TANK[1], TANK[2], TANK[3])
    return f

# ---------------------------------------------------------------- weights
def torso_w(p):
    side = 'L' if p.x < 0 else 'R'
    arm = smooth(R.half_width(p.y) + .015, R.half_width(p.y) + .075, abs(p.x)) * (1 - smooth(3.36, 3.46, p.y))
    if p.y > 3.24: arm *= smooth(.30, .42, abs(p.x))
    base = R.torso_weights(p) if p.y < 3.38 else R.head_weights(p) if p.y > 3.46 else [('Chest', 1.0)]
    return [(b, w * (1 - arm)) for b, w in base] + [('Arm_' + side, arm)]

def head_w(p):
    return R.head_weights(p) if p.y > 3.47 else [('Chest', 1 - smooth(3.38, 3.47, p.y)), ('Neck', smooth(3.38, 3.47, p.y))]

def boot_w(p):
    side = 'L' if p.x < 0 else 'R'; k = smooth(.24, .40, p.y); return R.W(('Shin_' + side, k), ('Foot_' + side, 1 - k))

def legs_w(p):
    w = R.pants_weights(p)
    return w

def build_parts():
    parts = []   # (object, budget per LOD, colour fn, weight fn, ao strength)
    hood = fuse(to_object('Ext_Hood', hood_shape()), .009, 3, lambda p, n: crinkle(p, n) * .7)
    parts.append((hood, (1500, 700, 300), tyvek, head_w, 1.0))
    mask = fuse(to_object('Ext_Mask', mask_shape()), .0062, 2)
    parts.append((mask, (950, 430, 170), lambda p, n: RUBBER, R.const('Head'), .9))
    parts.append((fuse(to_object('Ext_Visor', visor_shape()), .006, 2), (520, 240, 90), lambda p, n: VISOR, R.const('Head'), .5))
    parts.append((fuse(to_object('Ext_VisorFrame', visor_frame()), .006, 1), (420, 180, 70), lambda p, n: RUBBER, R.const('Head'), .8))
    fs, caps = filters_shape()
    parts.append((fuse(to_object('Ext_Filters', fs), .006, 1), (500, 230, 90), filter_color(caps), R.const('Head'), .8))
    ts, rings = torso_shape()
    ls = legs_shape(); ts.add(ls.v, ls.f)   # one piece: no hem between top and trousers
    torso = fuse(to_object('Ext_Coverall', ts), .011, 3, crinkle)
    def torso_col(p, n):
        front = p.z > 0 and abs(p.x) < .016 and 2.0 < p.y < 3.43
        return ZIP if front else tyvek(p, n)
    parts.append((torso, (3900, 1950, 850), torso_col, lambda p: torso_w(p) if p.y > 2.15 else legs_w(p), 1.0))
    zs = Shape()
    def front_z(y):
        for (y0, _, d0, c0), (y1, _, d1, c1) in zip(rings, rings[1:]):
            if y <= y1: t = clamp((y - y0) / (y1 - y0)); return lerp(d0 + c0, d1 + c1, t)
        return rings[-1][2] + rings[-1][3]
    zs.sweep([V(0, y, front_z(y) + .006) for y in [2.02 + i * .1 for i in range(14)]], [.016] * 14, [.007] * 14, V(0, 0, 1), 4, 10)
    parts.append((fuse(to_object('Ext_Zip', zs), .0055, 1), (260, 110, 40), lambda p, n: ZIP, R.torso_weights, .8))
    bs, bc = badge_shape()
    parts.append((fuse(to_object('Ext_Badge', bs), .0045, 1), (420, 160, 40), badge_color(bc), R.torso_weights, .5))
    for x in (-1, 1):
        sleeve = fuse(to_object('Ext_Sleeve', sleeve_shape(x)), .0095, 2, crinkle)
        parts.append((sleeve, (900, 420, 180), tyvek, R.arm_weights, 1.0))
        glove = fuse(to_object('Ext_Glove', glove_shape(x > 0)), .0056, 2)
        parts.append((glove, (1050, 500, 200), lambda p, n: (GLOVE[0] * (1 + .05 * noise.noise(p * 50)), GLOVE[1], GLOVE[2], GLOVE[3]), R.arm_hand_weights(x > 0), 1.0))
        boot = fuse(to_object('Ext_Boot', boot_shape(x)), .0085, 2)
        parts.append((boot, (520, 240, 100), lambda p, n: SOLE if p.y < .055 else BOOT, boot_w, 1.0))
    tank, tc = tank_shape()
    parts.append((fuse(to_object('Ext_Tank', tank), .008, 2), (900, 420, 160), tank_color(tc), R.const('Chest'), 1.0))
    parts.append((fuse(to_object('Ext_Straps', straps_shape(tc)), .0065, 1), (700, 300, 110), lambda p, n: STRAP, lambda p: R.torso_weights(p) if p.x < .3 else [('Pelvis', 1.0)], .8))
    parts.append((fuse(to_object('Ext_Lance', lance_shape()), .005, 1), (640, 300, 110), lance_color, R.const('Hand_L'), .6))
    parts.append((fuse(to_object('Ext_Hose', hose_shape(tc)), .006, 1), (360, 160, 70), lambda p, n: (.07, .08, .07, .35), hose_weights, .8))
    weapon = fuse(to_object('Ext_HeldSlipper', R.held_slipper()), .0075, 2)
    parts.append((weapon, (480, 240, 90), R.slipper_color(SLIP_UP, SLIP_SOLE, lambda p: p.x - (R.GRIP.x - .025)), R.const('Weapon'), .7))
    return parts

def build(material):
    ids = {b[0]: i for i, b in enumerate(R.BONES)}
    parts = build_parts(); levels = []; lods = []; report = []
    for lod in range(3):
        objs = []; cols = []; weights = []; ao = []
        for obj, budget, colour, weight, strength in parts:
            d = R.decimated(obj, budget[lod], obj.name + '_L' + str(lod)); objs.append(d)
            cols.append([list(colour(uvec(v.co), uvec(v.normal))) for v in d.data.vertices])
            weights.append([R.top2(weight(uvec(v.co)), ids) for v in d.data.vertices]); ao.append(strength)
        R.bake_ao(objs, cols, ao)
        R.activate(objs[0])
        for o in objs[1:]: o.select_set(True)
        bpy.ops.object.join(); obj = bpy.context.object; obj.name = 'Exterminator_Detail' + str(lod)
        flat_cols = [c for g in cols for c in g]; flat_w = [w for g in weights for w in g]
        mesh = obj.data; mesh.materials.clear(); mesh.materials.append(material)
        attr = mesh.color_attributes.new(name='Surface', type='FLOAT_COLOR', domain='POINT')
        for i, c in enumerate(flat_cols): attr.data[i].color = (min(1, c[0]), min(1, c[1]), min(1, c[2]), c[3])
        mesh.calc_loop_triangles()
        tris = [i for t in mesh.loop_triangles for i in t.vertices]
        r7 = lambda x: round(x, 6)
        levels.append(dict(name='Exterminator_Detail' + str(lod),
            positions=[dict(zip('xyz', map(r7, uvec(v.co)))) for v in mesh.vertices],
            normals=[dict(zip('xyz', map(r7, uvec(v.normal)))) for v in mesh.vertices],
            colors=[dict(zip('rgba', (round(min(1, c[0]), 4), round(min(1, c[1]), 4), round(min(1, c[2]), 4), c[3]))) for c in flat_cols],
            uv=[dict(x=0.0, y=0.0) for _ in mesh.vertices], triangles=tris, weights=flat_w))
        report.append(dict(lod=lod, vertices=len(mesh.vertices), triangles=len(tris) // 3, bones=len(R.BONES)))
        lods.append(obj)
    for obj, *_ in parts: bpy.data.objects.remove(obj, do_unlink=True)
    bones = [dict(name=n, parent=p, position=dict(zip('xyz', (h.x, h.y, h.z)))) for n, p, h, t in R.BONES]
    return bones, levels, lods, report

mat = bpy.data.materials.new('VertexAlbedo_Roughness'); mat.use_nodes = True
nodes = mat.node_tree.nodes; links = mat.node_tree.links; nodes.clear()
bsdf = nodes.new('ShaderNodeBsdfPrincipled'); out = nodes.new('ShaderNodeOutputMaterial'); links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
vc = nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Surface'; links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
inv = nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1
links.new(vc.outputs['Alpha'], inv.inputs[1]); links.new(inv.outputs[0], bsdf.inputs['Roughness'])

bones, levels, lods, report = build(mat)
tip = nozzle_tip()
(OUT / 'Exterminator.json').write_text(json.dumps(dict(version=1, actor='Exterminator', bones=bones, levels=levels,
    nozzleTip=dict(x=round(tip.x, 5), y=round(tip.y, 5), z=round(tip.z, 5))), separators=(',', ':')), encoding='utf8')
(AUTHOR / 'exterminator-budget.json').write_text(json.dumps(report, indent=2), encoding='utf8')
print('EXTERMINATOR_BUDGET ' + json.dumps(report))
for i, o in enumerate(lods): o.hide_render = i > 0; o.hide_set(i > 0)

scene = bpy.context.scene
world = bpy.data.worlds.new('World'); scene.world = world; world.color = (.10, .10, .10)
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
scene.render.threads_mode = 'FIXED'; scene.render.threads = 2
scene.render.resolution_x = 1100; scene.render.resolution_y = 1100
scene.view_settings.view_transform = 'AgX'
def aim(o, p): o.rotation_euler = (Vector(p) - o.location).to_track_quat('-Z', 'Y').to_euler()
cam_data = bpy.data.cameras.new('Camera'); cam = bpy.data.objects.new('Camera', cam_data); scene.collection.objects.link(cam); scene.camera = cam
cam_data.type = 'ORTHO'; cam_data.ortho_scale = 4.6
for pos, power, size in [((4, -6, 7), 420, 5), ((-5, -3, 4), 130, 4), ((1, 6, 6), 260, 3)]:
    ld = bpy.data.lights.new('Key', 'AREA'); ld.energy = power; ld.size = size; lo = bpy.data.objects.new('Key', ld); scene.collection.objects.link(lo); lo.location = pos; aim(lo, (0, 0, 2))
bpy.ops.wm.save_as_mainfile(filepath=str(AUTHOR / 'GokiTakeover_Exterminator.blend'))
if '--render-preview' in sys.argv:
    for name, loc in [('front', (2.2, -8, 2.6)), ('back', (-3.0, 7.5, 2.8)), ('face', (1.0, -8, 3.6))]:
        cam.location = loc; aim(cam, (0, 0, 2.05 if name != 'face' else 3.75))
        cam_data.ortho_scale = 4.6 if name != 'face' else 1.1
        scene.render.filepath = str(AUTHOR / ('exterminator-' + name + '.png')); bpy.ops.render.render(write_still=True)
