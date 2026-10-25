"""Blender 5.x authoring of the Stage 2 family (the house's mother and son) -> .blend + engine JSON.
Run: blender --background --threads 2 --python Tools/Blender/build_family_models.py [-- --render-preview]
Same skeleton and pipeline as the resident (resident_model.py): primitives fused by voxel remesh, cloth detail
displaced along normals, three LODs, colour/smoothness/AO in vertex colours, two bones per vertex. The bones
match the resident's, so the game puts these meshes on a copy of the resident's skin.
Mother: hair in a bun, mustard knit sweater, sage apron with a pocket, denim trousers, pink house slippers, a
slipper in the right hand and a cordless stick vacuum in the left (held like the exterminator's lance, so the
game can aim it at the floor). Son: authored at adult size with a bigger head (the game scales him to 0.72):
red cap, yellow striped T-shirt, navy shorts, bare shins, sneakers, and a bug net in the left hand.
Only the Mother and Son outputs are written. No third-party assets.
"""
import bpy, math, json, sys
from mathutils import Vector, Matrix, noise
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import resident_model as R
from resident_model import V, Shape, smooth, lerp, clamp, fuse, to_object, uvec, bvec, P

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_GokiTakeover/Art/BlenderModels/Source'
AUTHOR = ROOT / 'ArtSource/Blender'
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- palette (r, g, b, smoothness)
MOM_SKIN = (.56, .37, .28, .36); MOM_LIP = (.52, .17, .19, .50); MOM_HAIR = (.075, .042, .026, .34)
KNIT = (.62, .43, .10, .10); KNIT_RIB = (.52, .35, .08, .10)
APRON = (.42, .52, .40, .14); APRON_DOT = (.86, .86, .78, .14); APRON_TIE = (.30, .38, .29, .14)
DENIM = (.11, .16, .27, .12); SOCK = (.80, .76, .70, .08)
PINK_UP = (.80, .38, .46, .16); PINK_SOLE = (.40, .36, .34, .20)
WEAPON_UP = (.70, .30, .40, .16); WEAPON_SOLE = (.36, .30, .26, .22)
VAC_BODY = (.88, .88, .86, .62); VAC_CUP = (.30, .48, .56, .80); VAC_PIPE = (.62, .10, .10, .72); VAC_DARK = (.06, .06, .07, .40)
VAC_BUTTON = (.95, .45, .08, .55); VAC_BRUSH = (.18, .18, .19, .20)
KID_SKIN = (.62, .42, .32, .34); KID_HAIR = (.04, .03, .025, .30)
TEE = (.92, .70, .08, .10); TEE_STRIPE = (.10, .30, .62, .10); SHORTS = (.07, .10, .22, .10)
CAP = (.72, .08, .07, .16); CAP_FRONT = (.92, .90, .86, .16)
SNEAKER = (.86, .87, .88, .30); SNEAKER_RED = (.70, .10, .10, .30); SNEAKER_SOLE = (.95, .95, .93, .25)
NET_POLE = (.20, .48, .18, .45); NET_RING = (.92, .78, .10, .55); NET_MESH = (.86, .92, .84, .10)

# ---------------------------------------------------------------- shared helpers
def head_pivot_scale(obj, pivot, k):
    """Scales a part's vertices about pivot (the son's bigger head)."""
    for v in obj.data.vertices:
        p = uvec(v.co); v.co = bvec(pivot + (p - pivot) * k)
    obj.data.update()

def skin_tone(base, blush_amount, lips):
    def f(p, n):
        r, g, b, a = base
        blush = math.exp(-((p.x ** 2 - .0098) ** 2) / .0003 - ((p.y - 3.76) / .05) ** 2) * blush_amount
        nose = math.exp(-(p.x ** 2 + (p.y - 3.745) ** 2 + (p.z - .23) ** 2) / .0012) * .06
        k = 1 + .03 * noise.noise(p * 30)
        r = (r + blush + nose) * k; g = (g + .15 * (blush + nose)) * k * (1 - .4 * (blush + nose)); b = b * k * (1 - .5 * (blush + nose))
        if p.z > .15 and abs(p.x) < .05 and 3.648 < p.y < 3.69: r, g, b, a = lerp(r, lips[0], .8), lerp(g, lips[1], .8), lerp(b, lips[2], .8), lips[3]
        if 3.664 < p.y < 3.672 and abs(p.x) < .038 and p.z > .17: r, g, b = r * .55, g * .45, b * .45
        if abs(abs(p.x) - .026) < .012 and 3.733 < p.y < 3.745 and p.z > .19 and n.y < -.2: r, g, b = r * .35, g * .3, b * .3
        for x in (-1, 1):   # brows
            u = (p.x - x * .072) / .05; arch = 3.874 + .010 * (1 - u * u)
            if abs(u) < 1 and abs(p.y - arch) < .008 * (1 - .45 * abs(u)) and p.z > .1: r, g, b, a = .06, .04, .03, .25
        return (r, g, b, a)
    return f

def knit(base):
    def f(p, n):
        rib = .06 * (1 if math.sin(p.x * 160 + p.z * 160) > 0 else -1)   # vertical cable ribs
        k = 1 + rib * .5 + .05 * noise.noise(p * 45)
        return (base[0] * k, base[1] * k, base[2] * k, base[3])
    return f

def cloth_folds(p, n):
    return .004 * noise.noise(V(p.x * 5, p.y * 12, p.z * 5)) + .002 * noise.noise(p * 22)

def curved_panel(y_top, y_bot, half_w, z_of, thick, rows=26, cols=30):
    """A thick sheet hanging in front of the body: x across, y down, front face at z_of(x, y)."""
    s = Shape(); verts = []
    for side in (0, 1):
        for j in range(rows + 1):
            y = lerp(y_top, y_bot, j / rows); w = half_w(y)
            for i in range(cols + 1):
                x = lerp(-w, w, i / cols); z = z_of(x, y) - (thick if side else 0)
                verts.append(V(x, y, z))
    per = (rows + 1) * (cols + 1); faces = []
    idx = lambda side, j, i: side * per + j * (cols + 1) + i
    for j in range(rows):
        for i in range(cols):
            faces.append((idx(0, j, i), idx(0, j + 1, i), idx(0, j + 1, i + 1), idx(0, j, i + 1)))
            faces.append((idx(1, j, i), idx(1, j, i + 1), idx(1, j + 1, i + 1), idx(1, j + 1, i)))
    for j in range(rows):   # side walls
        for i in (0, cols):
            a, b = idx(0, j, i), idx(0, j + 1, i); c, d = idx(1, j + 1, i), idx(1, j, i)
            faces.append((a, d, c, b) if i == 0 else (a, b, c, d))
    for j in (0, rows):
        for i in range(cols):
            a, b = idx(0, j, i), idx(0, j, i + 1); c, d = idx(1, j, i + 1), idx(1, j, i)
            faces.append((a, b, c, d) if j == 0 else (a, d, c, b))
    s.add(verts, faces)
    return s

def sleeve_shape(x, cuff_t=.93, rad=None):
    t = 'L' if x < 0 else 'R'; s = Shape(); sh = P('Arm_' + t); el = P('Forearm_' + t); wr = P('Hand_' + t)
    pts = [sh.lerp(el, .10), sh.lerp(el, .38), sh.lerp(el, .7), el, el.lerp(wr, .4), el.lerp(wr, .75), el.lerp(wr, cuff_t)]
    rad = rad or [.122, .126, .118, .112, .104, .092, .080]
    s.sweep(pts, rad, [r * .98 for r in rad], V(0, 0, 1), 2.0, 26)
    return s

def sweater_shape():
    s = Shape()
    rings = [(1.96, .42, .282, 0), (2.04, .40, .27, 0), (2.30, .392, .268, .005), (2.50, .39, .274, .012), (2.70, .40, .284, .02),
             (2.90, .41, .292, .025), (3.05, .415, .286, .02), (3.18, .418, .258, .005), (3.28, .405, .222, -.01), (3.36, .31, .178, -.02), (3.44, .19, .155, -.03)]
    s.sweep([V(0, y, cz) for y, _, _, cz in rings], [w for _, w, _, _ in rings], [d for _, _, d, _ in rings], V(0, 0, 1), 2.6, 40)
    for x in (-1, 1):
        sh = P('Arm_R'); sh = V(x * sh.x, sh.y, sh.z)
        s.ellipsoid(sh + V(x * .0, -.05, 0), V(.115, .09, .118))
    collar = [V(math.sin(a) * .175, 3.44 + .02 * math.cos(a), -.035 + math.cos(a) * .16) for a in [i * math.tau / 30 for i in range(31)]]
    s.chain(collar, [.03] * 31, 10)
    return s

def sweater_weights(p):
    side = 'L' if p.x < 0 else 'R'
    arm = smooth(R.half_width(p.y) + .015, R.half_width(p.y) + .075, abs(p.x)) * (1 - smooth(3.36, 3.46, p.y))
    if p.y > 3.24: arm *= smooth(.30, .42, abs(p.x))
    base = R.torso_weights(p) if p.y < 3.4 else [('Chest', 1 - smooth(3.42, 3.5, p.y)), ('Neck', smooth(3.42, 3.5, p.y))]
    return [(b, w * (1 - arm)) for b, w in base] + [('Arm_' + side, arm)]

def sock_foot(x, sock_top=.40):
    s = R.foot_shape(x)
    return s

def lower_body_weights(p):
    # Apron skirt / hems below the hips: mostly the pelvis, a little of the leg on that side.
    side = 'L' if p.x < 0 else 'R'
    k = smooth(1.98, 1.62, p.y) * .55 * smooth(.02, .14, abs(p.x))
    return R.W(('Pelvis', 1 - k), ('Thigh_' + side, k))

def tool_frame():
    w, d, p, t = R.hand_frame(False)
    return w, d.normalized(), p.normalized(), t.normalized()

# ---------------------------------------------------------------- mother
def bun_shape():
    s = Shape()
    s.ellipsoid(V(0, 3.97, -.235), V(.105, .095, .095), R.rot_xyz(x=-25), 28, 18)
    s.ellipsoid(V(0, 3.93, -.20), V(.12, .05, .07), R.rot_xyz(x=-35), 24, 12)   # gathered hair under the bun
    for x in (-1, 1): s.ellipsoid(V(x * .15, 3.80, .08), V(.035, .10, .04), R.rot_xyz(z=x * 8), 16, 12)   # side locks
    return s

def tie_shape():
    s = Shape(); c = V(0, 3.925, -.205)
    s.sweep([c + V(0, 0, -.02), c + V(0, .02, .02)], [.075, .075], [.02, .02], V(0, 0, 1), 2.0, 24)
    return s

def apron_shape():
    def hw(y):
        if y > 2.98: return .17                       # bib
        if y > 2.62: return lerp(.17, .37, smooth(2.98, 2.62, y))
        return lerp(.37, .40, smooth(2.62, 1.6, y))
    def zf(x, y):
        body = .345 if y > 2.5 else .335 if y > 2.1 else .305
        if y > 2.98: body = lerp(.34, .30, smooth(2.98, 3.25, y))
        return body - .75 * x * x
    s = curved_panel(3.26, 1.62, hw, zf, .016)
    # neck strap, waist ties, front pocket
    for x in (-1, 1):
        s.sweep([V(x * .15, 3.25, .25), V(x * .16, 3.40, .14), V(x * .12, 3.47, -.04), V(0, 3.49, -.17)], [.02] * 4, [.006] * 4, V(0, 0, 1), 4, 10)
        s.sweep([V(x * .37, 2.42, .2), V(x * .40, 2.42, 0), V(x * .30, 2.42, -.24), V(x * .03, 2.40, -.29)], [.02] * 4, [.006] * 4, V(0, 1, 0), 4, 10)
    s.add(*_pocket())
    s.ellipsoid(V(0, 2.40, -.31), V(.07, .035, .025), None, 16, 10)   # bow at the back
    return s

def _pocket():
    p = curved_panel(2.22, 1.98, lambda y: .17, lambda x, y: .335 - .75 * x * x + .018, .01, 6, 12)
    return p.v, p.f

def apron_color(p, n):
    if p.y > 3.27 or abs(p.y - 2.42) < .03 and abs(p.x) > .3 or p.z < -.15: return APRON_TIE
    dot = (math.sin(p.x * 70) * math.sin(p.y * 70)) > .82
    return APRON_DOT if dot else (APRON[0] * (1 + .04 * noise.noise(p * 30)), APRON[1], APRON[2], APRON[3])

def apron_weights(p):
    if p.y > 2.5: return sweater_weights(p) if abs(p.x) < .3 or p.y < 3.3 else R.head_weights(p) if p.y > 3.46 else [('Chest', 1.0)]
    return lower_body_weights(p)

# Cordless stick vacuum in the left hand, run on from the arm like the exterminator's lance: grip through the fist,
# motor and dust cup just beyond it, a red pipe, and a floor head across the end.
def vacuum_parts():
    w, d, p, t = tool_frame()
    body = Shape()
    body.capsule(w - d * .03, w + d * .20, .036, .034, 18)                           # grip
    body.capsule(w + d * .20 + p * .02, w + d * .44 + p * .05, .085, .09, 22)        # motor body
    body.capsule(w + d * .02 + p * .07, w + d * .20 + p * .07, .02, .02, 10)          # handle loop
    cup = Shape(); cup.capsule(w + d * .24 - p * .08, w + d * .46 - p * .07, .065, .065, 22)   # clear dust cup
    pipe = Shape(); pipe.capsule(w + d * .44, w + d * 1.18, .026, .026, 14)
    head = Shape()
    hc = w + d * 1.24
    head.sweep([hc - t * .22, hc + t * .22], [.055, .055], [.06, .06], d, 3.0, 22)       # floor head (across)
    head.capsule(w + d * 1.16, hc, .04, .045, 14)                                         # swivel neck
    brush = Shape(); brush.sweep([hc + d * .05 - t * .2, hc + d * .05 + t * .2], [.03, .03], [.02, .02], d, 3.0, 16)
    button = Shape(); button.ellipsoid(w + d * .12 + p * .045, V(.016, .016, .016), None, 12, 8)
    tip = hc + d * .08
    return body, cup, pipe, head, brush, button, tip

def mother_parts():
    parts = []
    head = R.head_object()
    hair = R.hair_object(head, None)
    parts.append((head, (2700, 1250, 520), skin_tone(MOM_SKIN, .10, MOM_LIP), R.head_weights, 1.0))
    parts.append((hair, (950, 430, 190), lambda p, n: (MOM_HAIR[0] * (1 + .25 * noise.noise(V(p.x * 60, p.y * 25, p.z * 60))), MOM_HAIR[1], MOM_HAIR[2], MOM_HAIR[3]), R.const('Head'), .8))
    parts.append((fuse(to_object('Mom_Bun', bun_shape()), .008, 2, lambda p, n: .004 * math.sin(p.y * 120 + p.x * 60)), (700, 320, 130),
                  lambda p, n: (MOM_HAIR[0] * (1 + .2 * math.sin(p.x * 150 + p.y * 90)), MOM_HAIR[1], MOM_HAIR[2], MOM_HAIR[3]), R.const('Head'), .8))
    parts.append((fuse(to_object('Mom_HairTie', tie_shape()), .006, 1), (180, 80, 30), lambda p, n: (.55, .12, .14, .4), R.const('Head'), .7))
    for x in (-R.EYE_C.x, R.EYE_C.x):
        parts.append((to_object('Mom_Eye', R.eye_shape(x)), (260, 110, 40), lambda p, n, x=x: R.eye_color(p, n, x), R.const('Head'), .9))
    sweater = fuse(to_object('Mom_Sweater', sweater_shape()), .0105, 3, cloth_folds)
    parts.append((sweater, (2300, 1150, 500), lambda p, n: knit(KNIT_RIB if p.y > 3.4 or p.y < 2.02 else KNIT)(p, n), sweater_weights, 1.0))
    parts.append((fuse(to_object('Mom_Apron', apron_shape()), .0085, 2, lambda p, n: .003 * noise.noise(p * 14)), (1700, 800, 340), apron_color, apron_weights, .9))
    for x in (-1, 1):
        sl = fuse(to_object('Mom_Sleeve', sleeve_shape(x)), .0095, 2, cloth_folds)
        parts.append((sl, (950, 450, 190), lambda p, n: knit(KNIT)(p, n), R.arm_weights, 1.0))
        hs, tips = R.hand_shape(x > 0)
        hand = fuse(to_object('Mom_Hand', hs), .0056, 2)
        parts.append((hand, (700, 330, 130), skin_tone(MOM_SKIN, 0, MOM_LIP), R.hand_weights(x > 0), 1.0))
        foot = fuse(to_object('Mom_Sock', R.foot_shape(x)), .0085, 3)
        parts.append((foot, (260, 120, 50), lambda p, n: R.fabric(SOCK, p, .06, 50), R.foot_weights, 1.0))
        ws = fuse(to_object('Mom_Slipper', R.worn_slipper(x)), .0085, 2)
        parts.append((ws, (320, 150, 60), R.slipper_color(PINK_UP, PINK_SOLE, lambda p: p.y), R.const('Foot_' + ('L' if x < 0 else 'R')), .9))
    pants = fuse(to_object('Mom_Jeans', R.pants_shape()), .0115, 3, R.pants_detail)
    parts.append((pants, (1550, 780, 340), lambda p, n: R.fabric(DENIM, p, .1, 60), R.pants_weights, 1.0))
    weapon = fuse(to_object('Mom_HeldSlipper', R.held_slipper()), .0075, 2)
    parts.append((weapon, (480, 240, 90), R.slipper_color(WEAPON_UP, WEAPON_SOLE, lambda p: p.x - (R.GRIP.x - .025)), R.const('Weapon'), .7))
    body, cup, pipe, head_s, brush, button, tip = vacuum_parts()
    hand_l = R.const('Hand_L')
    parts.append((fuse(to_object('Vac_Body', body), .006, 1), (700, 320, 120), lambda p, n: VAC_BODY, hand_l, .6))
    parts.append((fuse(to_object('Vac_Cup', cup), .006, 1), (380, 170, 70), lambda p, n: VAC_CUP, hand_l, .5))
    parts.append((fuse(to_object('Vac_Pipe', pipe), .005, 1), (260, 120, 50), lambda p, n: VAC_PIPE, hand_l, .5))
    parts.append((fuse(to_object('Vac_Head', head_s), .006, 1), (420, 200, 80), lambda p, n: VAC_DARK, hand_l, .7))
    parts.append((fuse(to_object('Vac_Brush', brush), .005, 1), (200, 90, 40), lambda p, n: VAC_BRUSH, hand_l, .7))
    parts.append((fuse(to_object('Vac_Button', button), .004, 1), (80, 40, 20), lambda p, n: VAC_BUTTON, hand_l, .4))
    return parts, tip

# ---------------------------------------------------------------- son
KID_PIVOT = V(0, 3.45, -.03); KID_HEAD = 1.2
def kid_unscale(p): return KID_PIVOT + (p - KID_PIVOT) / KID_HEAD

def cap_shape():
    s = Shape()
    s.ellipsoid(V(0, 3.955, -.035), V(.192, .145, .205), R.rot_xyz(x=-8), 36, 20)
    s.ellipsoid(V(0, 3.905, .19), V(.16, .014, .135), R.rot_xyz(x=-8), 32, 10)          # brim
    s.ellipsoid(V(0, 4.095, -.04), V(.022, .012, .022), None, 12, 8)                     # button
    return s

def cap_color(p, n):
    q = kid_unscale(p)
    if q.y > 4.08: return CAP_FRONT
    if q.z > .12 and q.y > 3.95 and abs(q.x) < .1: return CAP_FRONT
    return CAP

def tshirt_shape():
    s = Shape()
    rings = [(2.02, .40, .262, 0), (2.30, .385, .258, .005), (2.50, .375, .262, .012), (2.70, .385, .27, .02), (2.90, .395, .278, .025),
             (3.05, .40, .27, .02), (3.18, .405, .245, .005), (3.28, .395, .21, -.01), (3.36, .30, .17, -.02), (3.43, .17, .145, -.035)]
    s.sweep([V(0, y, cz) for y, _, _, cz in rings], [w for _, w, _, _ in rings], [d for _, _, d, _ in rings], V(0, 0, 1), 2.6, 40)
    for x in (-1, 1):
        sh = P('Arm_R'); sh = V(x * sh.x, sh.y, sh.z); el = P('Forearm_R'); el = V(x * el.x, el.y, el.z)
        d = (el - sh).normalized()
        s.ellipsoid(sh + V(x * .025, -.01, 0), V(.135, .105, .13))
        s.sweep([sh + d * .02, sh + d * .17, sh + d * .32], [.145, .145, .14], [.145, .143, .138], V(0, 0, 1), 2.2, 28)
    collar = [V(math.sin(a) * .158, 3.43 + .012 * math.cos(a), -.04 + math.cos(a) * .15) for a in [i * math.tau / 28 for i in range(29)]]
    s.chain(collar, [.018] * 29, 10)
    return s

def tshirt_color(p, n):
    if p.y > 3.41: return TEE_STRIPE
    band = abs(p.y - 2.82) < .07 or abs(p.y - 2.62) < .035
    return R.fabric(TEE_STRIPE if band else TEE, p, .05, 40)

def shorts_shape():
    s = Shape()
    s.sweep([V(0, 1.86, 0), V(0, 2.0, 0), V(0, 2.14, .005), V(0, 2.26, .01)], [.37, .40, .40, .392], [.23, .252, .255, .248], V(0, 0, 1), 2.4, 32)
    for x in (-1, 1):
        L = [(2.02, .215, .235, .215, 0), (1.82, .212, .222, .218, .005), (1.58, .205, .212, .222, .012), (1.38, .2, .204, .222, .02), (1.30, .198, .2, .222, .02)]
        s.sweep([V(x * cx, y, cz) for y, _, _, cx, cz in L], [a for _, a, _, _, _ in L], [b for _, _, b, _, _ in L], V(0, 0, 1), 2.1, 28)
    return s

def shin_shape(x):
    s = Shape(); cx = x * .218
    s.sweep([V(cx, 1.36, .02), V(cx, 1.17, .03), V(cx, .95, .01), V(cx, .70, -.01), V(cx, .45, -.03), V(cx, .30, -.035)],
            [.12, .105, .098, .088, .07, .064], [.12, .11, .102, .09, .07, .064], V(0, 0, 1), 2.0, 22)
    s.ellipsoid(V(cx, 1.17, .11), V(.06, .06, .035))   # kneecap
    return s

def leg_skin_weights(p):
    side = 'L' if p.x < 0 else 'R'
    knee = smooth(1.08, 1.27, p.y); ank = smooth(.22, .36, p.y)
    if p.y < .4: return R.W(('Shin_' + side, ank), ('Foot_' + side, 1 - ank))
    return R.W(('Thigh_' + side, knee), ('Shin_' + side, 1 - knee))

def sneaker_shape(x):
    s = Shape(); sx = 1 if x > 0 else -1; cx = x * .222
    s.sweep([V(cx, .34, -.035), V(cx, .22, -.045)], [.085, .095], [.09, .1], V(0, 0, 1), 2.2, 24)      # sock + collar
    s.ellipsoid(V(cx, .13, -.10), V(.10, .105, .115))
    s.sweep([V(cx, .13, -.13), V(cx + sx * .006, .12, .08), V(cx + sx * .016, .095, .27), V(cx + sx * .018, .08, .36)], [.106, .112, .116, .104], [.095, .085, .066, .046], V(0, 1, 0), 2.6, 22)
    s.sweep([V(cx, .025, -.20), V(cx, .025, .38)], [.12, .115], [.026, .026], V(0, 1, 0), 4.0, 22)   # sole
    return s

def sneaker_color(p, n):
    if p.y < .05: return SNEAKER_SOLE
    if p.y > .26: return SOCK
    stripe = abs((p.z + .05) - (p.y - .1) * 1.6) < .035 and abs(abs(p.x) - .222) > .07
    return SNEAKER_RED if stripe else SNEAKER

# Bug net in the left hand: green pole run on from the arm, a yellow hoop across its end and a mesh bag beyond it.
def net_parts():
    w, d, p, t = tool_frame()
    pole = Shape(); pole.capsule(w - d * .06, w + d * 1.55, .026, .022, 14)
    hc = w + d * 1.86; ring = []
    for i in range(41):
        a = i * math.tau / 40
        ring.append(hc + (p * math.cos(a) + t * math.sin(a)) * .30)
    hoop = Shape(); hoop.chain(ring, [.018] * 41, 10)
    for s in (-1, 1): hoop.capsule(w + d * 1.50, hc + p * (.30 * s), .016, .016, 10)   # the Y where the pole meets the hoop
    bag = Shape()
    pts = [hc + d * k for k in (0, .12, .26, .38, .46)]
    bag.sweep(pts, [.30, .28, .22, .13, .04], [.30, .28, .22, .13, .04], p, 2.0, 32)
    return pole, hoop, bag, hc + d * .2

def net_color(p, n):
    k = (math.sin(p.x * 140) * math.sin(p.y * 140) * math.sin(p.z * 140))
    return NET_MESH if k > -.1 else (NET_MESH[0] * .7, NET_MESH[1] * .75, NET_MESH[2] * .7, NET_MESH[3])

def son_parts():
    parts = []
    head = R.head_object(); hair = R.hair_object(head, None)
    cap = fuse(to_object('Kid_Cap', cap_shape()), .007, 2)
    for o in (head, hair, cap): head_pivot_scale(o, KID_PIVOT, KID_HEAD)
    kid_skin = skin_tone(KID_SKIN, .16, (.60, .30, .27, .45))
    parts.append((head, (2700, 1250, 520), lambda p, n: kid_skin(kid_unscale(p), n), lambda p: R.head_weights(kid_unscale(p)), 1.0))
    parts.append((hair, (900, 400, 180), lambda p, n: (KID_HAIR[0] * (1 + .25 * noise.noise(p * 50)), KID_HAIR[1], KID_HAIR[2], KID_HAIR[3]), R.const('Head'), .8))
    parts.append((cap, (900, 420, 170), cap_color, R.const('Head'), .9))
    for x in (-R.EYE_C.x, R.EYE_C.x):
        eye = to_object('Kid_Eye', R.eye_shape(x)); head_pivot_scale(eye, KID_PIVOT, KID_HEAD)
        parts.append((eye, (260, 110, 40), lambda p, n, x=x: R.eye_color(kid_unscale(p), n, x), R.const('Head'), .9))
    tee = fuse(to_object('Kid_Tee', tshirt_shape()), .0105, 3, cloth_folds)
    parts.append((tee, (2150, 1100, 480), tshirt_color, R.shirt_weights, 1.0))
    shorts = fuse(to_object('Kid_Shorts', shorts_shape()), .011, 3, cloth_folds)
    parts.append((shorts, (1100, 540, 230), lambda p, n: R.fabric(SHORTS, p, .08, 40), R.pants_weights, 1.0))
    for x in (-1, 1):
        hs, tips = R.hand_shape(x > 0); limb = R.arm_shape(x); limb.add(hs.v, hs.f)
        arm = fuse(to_object('Kid_ArmHand', limb), .0056, 2)
        parts.append((arm, (1120, 540, 210), skin_tone(KID_SKIN, 0, (.6, .3, .27, .45)), R.arm_hand_weights(x > 0), 1.0))
        shin = fuse(to_object('Kid_Shin', shin_shape(x)), .008, 2)
        parts.append((shin, (520, 240, 100), skin_tone(KID_SKIN, 0, (.6, .3, .27, .45)), leg_skin_weights, 1.0))
        sn = fuse(to_object('Kid_Sneaker', sneaker_shape(x)), .0075, 2)
        parts.append((sn, (600, 280, 110), sneaker_color, lambda p, x=x: R.W(('Shin_' + ('L' if x < 0 else 'R'), smooth(.22, .36, p.y)), ('Foot_' + ('L' if x < 0 else 'R'), 1 - smooth(.22, .36, p.y))), 1.0))
    pole, hoop, bag, tip = net_parts()
    hand_l = R.const('Hand_L')
    parts.append((fuse(to_object('Net_Pole', pole), .005, 1), (260, 120, 50), lambda p, n: NET_POLE, hand_l, .5))
    parts.append((fuse(to_object('Net_Hoop', hoop), .005, 1), (420, 200, 80), lambda p, n: NET_RING, hand_l, .5))
    parts.append((fuse(to_object('Net_Bag', bag), .006, 1), (700, 320, 120), net_color, hand_l, .4))
    return parts, tip

# ---------------------------------------------------------------- build (same as the exterminator's)
def build(actor, parts, material):
    ids = {b[0]: i for i, b in enumerate(R.BONES)}
    levels = []; lods = []; report = []
    for lod in range(3):
        objs = []; cols = []; weights = []; ao = []
        for obj, budget, colour, weight, strength in parts:
            d = R.decimated(obj, budget[lod], obj.name + '_L' + str(lod)); objs.append(d)
            cols.append([list(colour(uvec(v.co), uvec(v.normal))) for v in d.data.vertices])
            weights.append([R.top2(weight(uvec(v.co)), ids) for v in d.data.vertices]); ao.append(strength)
        R.bake_ao(objs, cols, ao)
        R.activate(objs[0])
        for o in objs[1:]: o.select_set(True)
        bpy.ops.object.join(); obj = bpy.context.object; obj.name = actor + '_Detail' + str(lod)
        flat_cols = [c for g in cols for c in g]; flat_w = [w for g in weights for w in g]
        mesh = obj.data; mesh.materials.clear(); mesh.materials.append(material)
        attr = mesh.color_attributes.new(name='Surface', type='FLOAT_COLOR', domain='POINT')
        for i, c in enumerate(flat_cols): attr.data[i].color = (min(1, c[0]), min(1, c[1]), min(1, c[2]), c[3])
        mesh.calc_loop_triangles()
        tris = [i for t in mesh.loop_triangles for i in t.vertices]
        r7 = lambda x: round(x, 6)
        levels.append(dict(name=actor + '_Detail' + str(lod),
            positions=[dict(zip('xyz', map(r7, uvec(v.co)))) for v in mesh.vertices],
            normals=[dict(zip('xyz', map(r7, uvec(v.normal)))) for v in mesh.vertices],
            colors=[dict(zip('rgba', (round(min(1, c[0]), 4), round(min(1, c[1]), 4), round(min(1, c[2]), 4), c[3]))) for c in flat_cols],
            uv=[dict(x=0.0, y=0.0) for _ in mesh.vertices], triangles=tris, weights=flat_w))
        report.append(dict(actor=actor, lod=lod, vertices=len(mesh.vertices), triangles=len(tris) // 3, bones=len(R.BONES)))
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

all_reports = []; all_lods = {}
for actor, maker in (('Mother', mother_parts), ('Son', son_parts)):
    parts, tip = maker()
    bones, levels, lods, report = build(actor, parts, mat)
    (OUT / (actor + '.json')).write_text(json.dumps(dict(version=1, actor=actor, bones=bones, levels=levels,
        nozzleTip=dict(x=round(tip.x, 5), y=round(tip.y, 5), z=round(tip.z, 5))), separators=(',', ':')), encoding='utf8')
    all_reports += report; all_lods[actor] = lods
    print('FAMILY_BUDGET ' + json.dumps(report))
(AUTHOR / 'family-budget.json').write_text(json.dumps(all_reports, indent=2), encoding='utf8')

# Preview: mother on the left, son (scaled as in the game) on the right.
for actor, lods in all_lods.items():
    for i, o in enumerate(lods):
        o.hide_render = i > 0; o.hide_set(i > 0)
        o.location = (-0.75, 0, 0) if actor == 'Mother' else (0.75, 0, 0)
        if actor == 'Son': o.scale = (.72, .72, .72)
scene = bpy.context.scene
world = bpy.data.worlds.new('World'); scene.world = world; world.color = (.10, .10, .10)
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
scene.render.threads_mode = 'FIXED'; scene.render.threads = 2
scene.render.resolution_x = 1400; scene.render.resolution_y = 1100
scene.view_settings.view_transform = 'AgX'
def aim(o, p): o.rotation_euler = (Vector(p) - o.location).to_track_quat('-Z', 'Y').to_euler()
cam_data = bpy.data.cameras.new('Camera'); cam = bpy.data.objects.new('Camera', cam_data); scene.collection.objects.link(cam); scene.camera = cam
cam_data.type = 'ORTHO'; cam_data.ortho_scale = 5.6
for pos, power, size in [((4, -6, 7), 420, 5), ((-5, -3, 4), 130, 4), ((1, 6, 6), 260, 3)]:
    ld = bpy.data.lights.new('Key', 'AREA'); ld.energy = power; ld.size = size; lo = bpy.data.objects.new('Key', ld); scene.collection.objects.link(lo); lo.location = pos; aim(lo, (0, 0, 2))
bpy.ops.wm.save_as_mainfile(filepath=str(AUTHOR / 'GokiTakeover_Family.blend'))
if '--render-preview' in sys.argv:
    for name, loc, target, scale in [('front', (1.6, -8, 2.4), (0, 0, 2.0), 5.6), ('back', (-2.4, 7.5, 2.6), (0, 0, 2.0), 5.6),
                                     ('mother-face', (-.4, -8, 3.6), (-.75, 0, 3.75), 1.1), ('son-face', (1.1, -8, 2.6), (.75, 0, 2.75), 1.0)]:
        cam.location = loc; aim(cam, target); cam_data.ortho_scale = scale
        scene.render.filepath = str(AUTHOR / ('family-' + name + '.png')); bpy.ops.render.render(write_still=True)
