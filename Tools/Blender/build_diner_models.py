"""Blender 5.x authoring of the Stage 3 diner people (町中華) -> .blend + engine JSON.
Run: blender --background --threads 2 --python Tools/Blender/build_diner_models.py [-- --render-preview] [--only Chef,...]
Same skeleton and pipeline as the resident (resident_model.py) and the Stage 2 family: primitives fused by voxel
remesh, cloth detail displaced along normals, three LODs, colour/smoothness/AO in vertex colours, two bones per
vertex. The bones match the resident's, so the game puts these meshes on a copy of the resident's skin.
Chef (店主): a stocky older cook. White cook's cap (和帽子), white double-breasted cook's coat with a stand collar
and long sleeves rolled to the forearm, a navy waist apron (前掛け) to the knees, charcoal trousers, white kitchen
clogs, a grey moustache. A steel ladle (お玉) in the right hand where the others hold the slipper, and a small
saucepan (片手鍋) of hot water in the left hand, held like the exterminator's lance (its spout is nozzleTip).
Okami (おかみさん, step 2): a white kappogi smock over a navy skirt, a dotted pink headscarf (三角巾), tabi and geta,
a round red lacquered tray (お盆) in the right hand where the others hold the slipper, an alcohol spray bottle in the left.
Baito (バイト, step 2): navy cap, red T-shirt with the shop panel, black waist apron, jeans, black rubber boots, a mop
in the right hand and a deck brush (デッキブラシ) in the left, held like the lance.
Only the actors built here are written. No third-party assets.
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
CHEF_SKIN = (.55, .35, .25, .34); CHEF_LIP = (.42, .18, .15, .45); CHEF_HAIR = (.30, .29, .28, .30)
COAT = (.86, .86, .83, .12); COAT_SHADE = (.78, .78, .75, .12); BUTTON = (.20, .20, .22, .55)
CAP = (.92, .92, .90, .10); APRON = (.07, .10, .22, .10); APRON_TIE = (.06, .08, .18, .10)
TROUSERS = (.10, .10, .11, .10); CLOG = (.90, .90, .88, .45); CLOG_SOLE = (.10, .10, .10, .30)
STEEL = (.66, .67, .68, .82); STEEL_DARK = (.36, .37, .38, .75); HANDLE_BLACK = (.05, .05, .05, .40)

# ---------------------------------------------------------------- shared helpers (as the family's)
def skin_tone(base, blush_amount, lips, brow=(.26, .25, .24), brow_t=.012):
    def f(p, n):
        r, g, b, a = base
        blush = math.exp(-((p.x ** 2 - .0098) ** 2) / .0003 - ((p.y - 3.76) / .05) ** 2) * blush_amount
        nose = math.exp(-(p.x ** 2 + (p.y - 3.745) ** 2 + (p.z - .23) ** 2) / .0012) * .08
        k = 1 + .04 * noise.noise(p * 30)
        r = (r + blush + nose) * k; g = (g + .15 * (blush + nose)) * k * (1 - .4 * (blush + nose)); b = b * k * (1 - .5 * (blush + nose))
        if p.z > .15 and abs(p.x) < .05 and 3.648 < p.y < 3.69: r, g, b, a = lerp(r, lips[0], .7), lerp(g, lips[1], .7), lerp(b, lips[2], .7), lips[3]
        if 3.664 < p.y < 3.672 and abs(p.x) < .038 and p.z > .17: r, g, b = r * .55, g * .45, b * .45
        if abs(abs(p.x) - .026) < .012 and 3.733 < p.y < 3.745 and p.z > .19 and n.y < -.2: r, g, b = r * .35, g * .3, b * .3
        for x in (-1, 1):   # brows (the cook: thick and grey)
            u = (p.x - x * .072) / .055; arch = 3.872 + .008 * (1 - u * u)
            if abs(u) < 1 and abs(p.y - arch) < brow_t * (1 - .4 * abs(u)) and p.z > .1: r, g, b, a = brow[0], brow[1], brow[2], .25
        return (r, g, b, a)
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
    for j in range(rows):
        for i in (0, cols):
            a, b = idx(0, j, i), idx(0, j + 1, i); c, d = idx(1, j + 1, i), idx(1, j, i)
            faces.append((a, d, c, b) if i == 0 else (a, b, c, d))
    for j in (0, rows):
        for i in range(cols):
            a, b = idx(0, j, i), idx(0, j, i + 1); c, d = idx(1, j, i + 1), idx(1, j, i)
            faces.append((a, b, c, d) if j == 0 else (a, d, c, b))
    s.add(verts, faces)
    return s

def sleeve_shape(x, cuff_t=.93, rad=None, top=.10):
    t = 'L' if x < 0 else 'R'; s = Shape(); sh = P('Arm_' + t); el = P('Forearm_' + t); wr = P('Hand_' + t)
    pts = [sh.lerp(el, top), sh.lerp(el, .38), sh.lerp(el, .7), el, el.lerp(wr, .4), el.lerp(wr, .75), el.lerp(wr, cuff_t)]
    rad = rad or [.122, .126, .118, .112, .104, .092, .080]
    s.sweep(pts, rad, [r * .98 for r in rad], V(0, 0, 1), 2.0, 26)
    return s

def tool_frame():
    w, d, p, t = R.hand_frame(False)
    return w, d.normalized(), p.normalized(), t.normalized()

def lower_body_weights(p):
    side = 'L' if p.x < 0 else 'R'
    k = smooth(1.98, 1.30, p.y) * .6 * smooth(.02, .14, abs(p.x))
    return R.W(('Pelvis', 1 - k), ('Thigh_' + side, k))

def coat_weights(p):
    """Coat body: the torso above the hips; its skirt below follows the pelvis and a little of each thigh."""
    side = 'L' if p.x < 0 else 'R'
    arm = smooth(R.half_width(p.y) + .015, R.half_width(p.y) + .075, abs(p.x)) * (1 - smooth(3.36, 3.46, p.y))
    if p.y > 3.24: arm *= smooth(.30, .42, abs(p.x))
    if p.y < 2.0: return lower_body_weights(p)
    base = R.torso_weights(p) if p.y < 3.4 else [('Chest', 1 - smooth(3.42, 3.5, p.y)), ('Neck', smooth(3.42, 3.5, p.y))]
    return [(b, w * (1 - arm)) for b, w in base] + [('Arm_' + side, arm)]

def clog_shape(x):
    s = Shape(); sx = 1 if x > 0 else -1; cx = x * .222
    s.sweep([V(cx, .30, -.04), V(cx, .20, -.045)], [.08, .09], [.085, .095], V(0, 0, 1), 2.2, 24)      # sock
    s.ellipsoid(V(cx, .13, -.10), V(.105, .11, .12))
    s.sweep([V(cx, .14, -.13), V(cx + sx * .006, .13, .08), V(cx + sx * .016, .11, .26), V(cx + sx * .018, .095, .36)], [.112, .118, .122, .11], [.10, .092, .076, .055], V(0, 1, 0), 2.8, 22)
    s.sweep([V(cx, .03, -.21), V(cx, .03, .39)], [.125, .12], [.032, .032], V(0, 1, 0), 4.0, 22)   # thick sole
    return s

def clog_color(p, n):
    if p.y < .06: return CLOG_SOLE
    if p.y > .24: return (.12, .12, .13, .08)   # dark socks
    return CLOG

# ---------------------------------------------------------------- chef
def cap_shape():
    """和帽子: a tall soft dome sitting low on the brow, a folded band around it."""
    s = Shape()
    s.ellipsoid(V(0, 4.00, -.04), V(.218, .17, .235), R.rot_xyz(x=-6), 36, 22)
    s.sweep([V(0, 3.84, -.03), V(0, 3.93, -.035)], [.214, .218], [.232, .236], V(0, 0, 1), 2.4, 40)   # band
    return s

def cap_color(p, n):
    k = 1 - .05 * smooth(3.94, 3.86, p.y) + .03 * noise.noise(p * 25)
    return (CAP[0] * k, CAP[1] * k, CAP[2] * k, CAP[3])

def moustache_shape():
    s = Shape()
    for x in (-1, 1):
        s.ellipsoid(V(x * .032, 3.706, .228), V(.04, .014, .02), R.rot_xyz(z=x * -14, y=x * 18), 16, 10)
    return s

def coat_shape():
    s = Shape()
    # (y, half width, half depth, centre z): a roomy coat over a big belly, the skirt to mid-thigh.
    rings = [(1.66, .43, .29, 0), (1.80, .425, .29, 0), (1.98, .42, .292, .005), (2.20, .41, .30, .02), (2.45, .42, .315, .035),
             (2.70, .43, .32, .04), (2.90, .425, .30, .03), (3.05, .42, .285, .02), (3.18, .42, .258, .005), (3.28, .405, .222, -.01),
             (3.36, .31, .178, -.02), (3.44, .185, .155, -.03)]
    s.sweep([V(0, y, cz) for y, _, _, cz in rings], [w for _, w, _, _ in rings], [d for _, _, d, _ in rings], V(0, 0, 1), 2.6, 40)
    for x in (-1, 1):
        sh = P('Arm_R'); sh = V(x * sh.x, sh.y, sh.z)
        s.ellipsoid(sh + V(0, -.03, 0), V(.125, .10, .128))   # shoulders, over the sleeve tops
    # stand collar
    collar = [V(math.sin(a) * .178, 3.47, -.035 + math.cos(a) * .162) for a in [i * math.tau / 30 for i in range(31)]]
    s.chain(collar, [.045] * 31, 10)
    # the double-breasted front flap: a raised panel from the collar down to the hem on the wearer's left
    flap = curved_panel(3.40, 1.70, lambda y: .14, lambda x, y: coat_front_z(y) + .006 - 1.1 * (x + .07) ** 2, .02, 20, 10)
    s.add(flap.v, flap.f)
    return s

def coat_front_z(y):
    for (y0, z0), (y1, z1) in zip([(1.66, .29), (2.20, .32), (2.45, .35), (2.70, .36), (2.90, .33), (3.05, .305), (3.18, .263), (3.28, .212), (3.40, .16)],
                                  [(2.20, .32), (2.45, .35), (2.70, .36), (2.90, .33), (3.05, .305), (3.18, .263), (3.28, .212), (3.40, .16), (3.50, .14)]):
        if y0 <= y <= y1: return lerp(z0, z1, (y - y0) / (y1 - y0))
    return .29

BUTTONS = [V(x, y, 0) for y in (2.28, 2.58, 2.88, 3.16) for x in (-.15, .12)]
def buttons_shape():
    s = Shape()
    for b in BUTTONS:
        s.ellipsoid(V(b.x, b.y, coat_front_z(b.y) + .025), V(.024, .024, .014), None, 14, 8)
    return s

def coat_color(p, n):
    shade = .5 + .5 * noise.noise(V(p.x * 6, p.y * 14, p.z * 6))
    c = COAT if shade > .35 else COAT_SHADE
    if p.y > 3.43: c = COAT_SHADE                               # collar
    if abs(p.y - 1.70) < .04: c = COAT_SHADE                    # hem
    return R.fabric(c, p, .04, 50)

def apron_shape():
    def hw(y): return lerp(.43, .46, smooth(2.2, 1.1, y))
    def zf(x, y):
        body = .345 if y > 1.95 else lerp(.345, .30, smooth(1.95, 1.4, y))
        return body - .7 * x * x
    s = curved_panel(2.24, 1.10, hw, zf, .018)
    s.sweep([V(-.44, 2.22, .2), V(-.45, 2.22, 0), V(-.32, 2.22, -.28), V(0, 2.20, -.32), V(.32, 2.22, -.28), V(.45, 2.22, 0), V(.44, 2.22, .2)],
            [.03] * 7, [.007] * 7, V(0, 1, 0), 4, 10)          # waist band around the back
    s.ellipsoid(V(0, 2.18, -.34), V(.07, .04, .03), None, 16, 10)   # knot
    return s

def apron_color(p, n):
    if p.y > 2.19 or p.z < -.1: return APRON_TIE
    k = 1 + .05 * noise.noise(V(p.x * 8, p.y * 30, p.z * 8))
    return (APRON[0] * k, APRON[1] * k, APRON[2] * k, APRON[3])

def apron_weights(p):
    if p.y > 2.05: return R.W(('Pelvis', 1.0))
    return lower_body_weights(p)

def ladle_parts():
    """お玉 in the right fist (the Weapon bone): the handle runs through the palm, the bowl hangs below with its round
    back facing out of the palm (-X), where the slipper's tread is."""
    g = R.GRIP
    handle = Shape(); handle.capsule(g + V(-.02, .16, 0), g + V(-.02, -.58, 0), .018, .016, 12)
    handle.capsule(g + V(-.02, .16, 0), g + V(.01, .22, 0), .018, .02, 10)   # the hook at the top
    bowl = Shape(); bowl.ellipsoid(g + V(-.06, -.70, 0), V(.075, .14, .14), None, 24, 14)
    rim = Shape()
    ring = [g + V(.0, -.70 + math.cos(a) * .135, math.sin(a) * .135) for a in [i * math.tau / 32 for i in range(33)]]
    rim.chain(ring, [.012] * 33, 8)
    return handle, bowl, rim

def pot_parts():
    """片手鍋 in the left hand, like the exterminator's lance: a black handle through the fist, the steel pot beyond it
    with a pouring lip at the far side (the tool tip)."""
    w, d, p, t = tool_frame()
    handle = Shape(); handle.capsule(w - d * .05, w + d * .26, .028, .03, 14)
    neck = Shape(); neck.capsule(w + d * .24, w + d * .34, .02, .02, 10)
    c = w + d * .52
    # The pot's mouth faces t (forward while the arm hangs; at the floor when the arm aims like the lance).
    pot = Shape(); pot.sweep([c - t * .10, c + t * .12], [.19, .195], [.19, .195], d, 2.0, 32)
    lip = Shape(); lip.ellipsoid(c + d * .19 + t * .12, V(.05, .04, .03), None, 14, 8)
    water = Shape(); water.sweep([c + t * .09, c + t * .10], [.175, .175], [.175, .175], d, 2.0, 28)
    return handle, neck, pot, lip, water, c + d * .22 + t * .13

def chef_parts():
    parts = []
    head = R.head_object()
    face = skin_tone(CHEF_SKIN, .12, CHEF_LIP)
    def scalp(p, n):
        # Close-cropped grey hair under the cap: temples and the back of the head (no hair shell to poke through).
        if p.y > 3.76 and (p.z < -.02 or abs(p.x) > .16):
            k = 1 + .3 * noise.noise(V(p.x * 80, p.y * 80, p.z * 80))
            return (CHEF_HAIR[0] * k, CHEF_HAIR[1] * k, CHEF_HAIR[2] * k, CHEF_HAIR[3])
        return face(p, n)
    parts.append((head, (2700, 1250, 520), scalp, R.head_weights, 1.0))
    parts.append((fuse(to_object('Chef_Cap', cap_shape()), .008, 2, lambda p, n: .004 * noise.noise(p * 12)), (900, 420, 170), cap_color, R.const('Head'), .8))
    parts.append((fuse(to_object('Chef_Moustache', moustache_shape()), .005, 1), (200, 90, 40), lambda p, n: (CHEF_HAIR[0] * .9, CHEF_HAIR[1] * .9, CHEF_HAIR[2] * .9, .2), R.const('Head'), .6))
    for x in (-R.EYE_C.x, R.EYE_C.x):
        parts.append((to_object('Chef_Eye', R.eye_shape(x)), (260, 110, 40), lambda p, n, x=x: R.eye_color(p, n, x), R.const('Head'), .9))
    coat = fuse(to_object('Chef_Coat', coat_shape()), .0105, 3, cloth_folds)
    parts.append((coat, (2600, 1300, 560), coat_color, coat_weights, 1.0))
    parts.append((fuse(to_object('Chef_Buttons', buttons_shape()), .005, 1), (240, 110, 50), lambda p, n: BUTTON, coat_weights, .5))
    parts.append((fuse(to_object('Chef_Apron', apron_shape()), .0085, 2, lambda p, n: .003 * noise.noise(p * 14)), (1500, 700, 300), apron_color, apron_weights, .9))
    for x in (-1, 1):
        # long sleeves rolled up to the forearm: a thicker roll at the cuff, bare forearm and hand below it
        sl = fuse(to_object('Chef_Sleeve', sleeve_shape(x, .45, [.14, .134, .124, .118, .12, .112, .102], 0)), .0095, 2, cloth_folds)
        parts.append((sl, (900, 430, 180), coat_color, R.arm_weights, 1.0))
        hs, tips = R.hand_shape(x > 0); limb = R.arm_shape(x); limb.add(hs.v, hs.f)
        arm = fuse(to_object('Chef_ArmHand', limb), .0056, 2)
        parts.append((arm, (1120, 540, 210), skin_tone(CHEF_SKIN, 0, CHEF_LIP), R.arm_hand_weights(x > 0), 1.0))
        cl = fuse(to_object('Chef_Clog', clog_shape(x)), .0075, 2)
        side = 'L' if x < 0 else 'R'
        parts.append((cl, (620, 290, 120), clog_color, lambda p, side=side: R.W(('Shin_' + side, smooth(.22, .34, p.y)), ('Foot_' + side, 1 - smooth(.22, .34, p.y))), 1.0))
    pants = fuse(to_object('Chef_Trousers', R.pants_shape()), .0115, 3, R.pants_detail)
    parts.append((pants, (1500, 760, 330), lambda p, n: R.fabric(TROUSERS, p, .08, 40), R.pants_weights, 1.0))
    handle, bowl, rim = ladle_parts()
    weapon = R.const('Weapon')
    parts.append((fuse(to_object('Ladle_Handle', handle), .005, 1), (260, 120, 50), lambda p, n: STEEL, weapon, .5))
    parts.append((fuse(to_object('Ladle_Bowl', bowl), .006, 1), (420, 200, 80), lambda p, n: STEEL if p.x < R.GRIP.x - .05 else STEEL_DARK, weapon, .5))
    parts.append((fuse(to_object('Ladle_Rim', rim), .005, 1), (220, 100, 40), lambda p, n: STEEL, weapon, .4))
    ph, pn, pot, lip, water, tip = pot_parts()
    hand_l = R.const('Hand_L')
    parts.append((fuse(to_object('Pot_Handle', ph), .005, 1), (240, 110, 50), lambda p, n: HANDLE_BLACK, hand_l, .5))
    parts.append((fuse(to_object('Pot_Neck', pn), .005, 1), (100, 50, 20), lambda p, n: STEEL_DARK, hand_l, .4))
    parts.append((fuse(to_object('Pot_Body', pot), .007, 1), (620, 300, 120), lambda p, n: STEEL, hand_l, .6))
    parts.append((fuse(to_object('Pot_Lip', lip), .005, 1), (120, 60, 24), lambda p, n: STEEL, hand_l, .4))
    parts.append((fuse(to_object('Pot_Water', water), .006, 1), (160, 80, 30), lambda p, n: (.55, .62, .66, .95), hand_l, .3))
    return parts, tip

# ---------------------------------------------------------------- landlady (おかみさん)
OKAMI_SKIN = (.58, .40, .31, .36); OKAMI_LIP = (.55, .20, .22, .50); OKAMI_HAIR = (.06, .04, .03, .30)
KAPPOGI = (.90, .90, .87, .10); KAPPOGI_SHADE = (.80, .80, .77, .10); SCARF = (.72, .30, .36, .12); SCARF_DOT = (.92, .88, .80, .12)
SKIRT = (.10, .12, .20, .10); TABI = (.88, .87, .84, .10); GETA_SOLE = (.40, .28, .18, .30); GETA_STRAP = (.55, .10, .10, .30)
TRAY = (.42, .06, .05, .70); TRAY_RIM = (.08, .05, .04, .70); BOTTLE = (.86, .92, .95, .60); BOTTLE_HEAD = (.12, .45, .78, .50)

def scarf_shape():
    """三角巾: a cloth cap over the head, knotted at the nape with the corner hanging down."""
    s = Shape()
    s.ellipsoid(V(0, 3.97, -.05), V(.215, .165, .232), R.rot_xyz(x=-10), 36, 22)
    s.sweep([V(0, 3.82, -.01), V(0, 3.90, -.02)], [.205, .21], [.222, .228], V(0, 0, 1), 2.4, 40)   # band over the brow
    s.ellipsoid(V(0, 3.80, -.25), V(.07, .05, .05), None, 16, 10)                                    # knot
    s.sweep([V(0, 3.86, -.24), V(0, 3.72, -.27), V(0, 3.62, -.25)], [.11, .07, .02], [.02, .02, .015], V(0, 0, -1), 2.4, 16)   # corner
    return s

def scarf_color(p, n):
    dot = (math.sin(p.x * 90) * math.sin(p.y * 90 + p.z * 40)) > .86
    return SCARF_DOT if dot else (SCARF[0] * (1 + .04 * noise.noise(p * 30)), SCARF[1], SCARF[2], SCARF[3])

def kappogi_shape():
    """割烹着: a white smock with a round neck, full to the knees, gathered sleeves to the wrists."""
    s = Shape()
    rings = [(1.18, .46, .33, .01), (1.40, .45, .32, .01), (1.70, .44, .31, .01), (1.98, .43, .30, .01), (2.30, .41, .29, .015),
             (2.60, .41, .29, .02), (2.90, .415, .29, .025), (3.05, .415, .28, .02), (3.18, .418, .256, .005), (3.28, .405, .222, -.01),
             (3.36, .31, .178, -.02), (3.43, .18, .152, -.035)]
    s.sweep([V(0, y, cz) for y, _, _, cz in rings], [w for _, w, _, _ in rings], [d for _, _, d, _ in rings], V(0, 0, 1), 2.6, 40)
    for x in (-1, 1):
        sh = P('Arm_R'); sh = V(x * sh.x, sh.y, sh.z)
        s.ellipsoid(sh + V(0, -.03, 0), V(.125, .10, .128))
    collar = [V(math.sin(a) * .17, 3.43, -.035 + math.cos(a) * .156) for a in [i * math.tau / 30 for i in range(31)]]
    s.chain(collar, [.022] * 31, 10)
    s.sweep([V(-.26, 2.06, .315), V(.26, 2.06, .315)], [.16, .16], [.012, .012], V(0, 0, 1), 4, 16)   # pocket
    return s

def kappogi_color(p, n):
    c = KAPPOGI if .5 + .5 * noise.noise(V(p.x * 5, p.y * 12, p.z * 5)) > .35 else KAPPOGI_SHADE
    if abs(p.y - 1.19) < .03: c = KAPPOGI_SHADE
    return R.fabric(c, p, .04, 50)

def skirt_shape():
    s = Shape()
    s.sweep([V(0, 1.30, 0), V(0, .95, .005), V(0, .62, .01), V(0, .40, .01)], [.42, .40, .37, .35], [.29, .28, .27, .26], V(0, 0, 1), 2.4, 36)
    return s

def tabi_geta(x):
    s = Shape(); cx = x * .222; sx = 1 if x > 0 else -1
    s.capsule(V(cx, .44, -.03), V(cx, .16, -.035), .07, .068, 18)                       # ankle in a tabi sock
    s.ellipsoid(V(cx, .11, -.09), V(.07, .07, .085))
    s.sweep([V(cx, .11, -.08), V(cx + sx * .008, .10, .10), V(cx + sx * .016, .085, .26)], [.075, .085, .08], [.06, .052, .04], V(0, 1, 0), 2.4, 20)
    return s

def geta(x):
    s = Shape(); cx = x * .222
    s.sweep([V(cx, .03, -.22), V(cx, .03, .34)], [.115, .11], [.03, .03], V(0, 1, 0), 4.0, 20)   # wooden sole
    for z in (-.12, .2): s.sweep([V(cx - .09, -.005, z), V(cx + .09, -.005, z)], [.02, .02], [.035, .035], V(0, 0, 1), 4.0, 10)   # teeth
    s.chain([V(cx - .07, .06, .02), V(cx, .12, .2), V(cx + .07, .06, .02)], [.014] * 3, 8)          # thong
    return s

def tray_parts():
    """お盆 in the right fist (the Weapon bone): a round lacquered tray gripped at its rim, its face out of the palm (-X)."""
    g = R.GRIP; c = g + V(-.05, -.30, 0)
    tray = Shape(); tray.sweep([c + V(.012, 0, 0), c + V(-.012, 0, 0)], [.34, .34], [.34, .34], V(0, 1, 0), 2.0, 40)
    rim = Shape()
    ring = [c + V(-.016, math.cos(a) * .34, math.sin(a) * .34) for a in [i * math.tau / 40 for i in range(41)]]
    rim.chain(ring, [.016] * 41, 8)
    return tray, rim

def bottle_parts():
    """The alcohol spray bottle in the left hand, held like the lance: the trigger head and nozzle beyond the fist."""
    w, d, p, t = tool_frame()
    body = Shape(); body.capsule(w - d * .2, w + d * .16, .1, .1, 20)
    neck = Shape(); neck.capsule(w + d * .16, w + d * .26, .045, .045, 12)
    head = Shape(); head.capsule(w + d * .26, w + d * .38, .06, .055, 14); head.capsule(w + d * .3 - p * .03, w + d * .3 - p * .12, .02, .02, 8)
    return body, neck, head, w + d * .42

def okami_parts():
    parts = []
    head = R.head_object()
    face = skin_tone(OKAMI_SKIN, .14, OKAMI_LIP, (.10, .07, .05), .007)
    def scalp(p, n):
        if p.y > 3.74 and (p.z < -.02 or abs(p.x) > .15):
            k = 1 + .25 * noise.noise(V(p.x * 70, p.y * 30, p.z * 70))
            return (OKAMI_HAIR[0] * k, OKAMI_HAIR[1] * k, OKAMI_HAIR[2] * k, OKAMI_HAIR[3])
        return face(p, n)
    parts.append((head, (2700, 1250, 520), scalp, R.head_weights, 1.0))
    parts.append((fuse(to_object('Okami_Scarf', scarf_shape()), .008, 2, lambda p, n: .004 * noise.noise(p * 14)), (950, 440, 180), scarf_color, R.const('Head'), .8))
    for x in (-R.EYE_C.x, R.EYE_C.x):
        parts.append((to_object('Okami_Eye', R.eye_shape(x)), (260, 110, 40), lambda p, n, x=x: R.eye_color(p, n, x), R.const('Head'), .9))
    parts.append((fuse(to_object('Okami_Kappogi', kappogi_shape()), .0105, 3, cloth_folds), (2600, 1300, 560), kappogi_color, coat_weights, 1.0))
    parts.append((fuse(to_object('Okami_Skirt', skirt_shape()), .011, 3, cloth_folds), (1200, 600, 260), lambda p, n: R.fabric(SKIRT, p, .06, 40), lower_body_weights, 1.0))
    for x in (-1, 1):
        sl = fuse(to_object('Okami_Sleeve', sleeve_shape(x, .9, [.15, .142, .132, .126, .12, .112, .09], 0)), .0095, 2, cloth_folds)
        parts.append((sl, (900, 430, 180), kappogi_color, R.arm_weights, 1.0))
        hs, tips = R.hand_shape(x > 0)
        parts.append((fuse(to_object('Okami_Hand', hs), .0056, 2), (700, 330, 130), skin_tone(OKAMI_SKIN, 0, OKAMI_LIP), R.hand_weights(x > 0), 1.0))
        side = 'L' if x < 0 else 'R'
        foot_w = lambda p, side=side: R.W(('Shin_' + side, smooth(.22, .38, p.y)), ('Foot_' + side, 1 - smooth(.22, .38, p.y)))
        parts.append((fuse(to_object('Okami_Tabi', tabi_geta(x)), .0075, 2), (420, 200, 80), lambda p, n: TABI, foot_w, 1.0))
        parts.append((fuse(to_object('Okami_Geta', geta(x)), .006, 1), (360, 170, 70), lambda p, n: GETA_STRAP if p.y > .055 else GETA_SOLE, R.const('Foot_' + side), .8))
    tray, rim = tray_parts()
    weapon = R.const('Weapon')
    parts.append((fuse(to_object('Tray', tray), .006, 1), (520, 250, 100), lambda p, n: TRAY, weapon, .5))
    parts.append((fuse(to_object('TrayRim', rim), .005, 1), (300, 140, 60), lambda p, n: TRAY_RIM, weapon, .4))
    body, neck, bhead, tip = bottle_parts()
    hand_l = R.const('Hand_L')
    parts.append((fuse(to_object('Bottle', body), .006, 1), (420, 200, 80), lambda p, n: BOTTLE, hand_l, .5))
    parts.append((fuse(to_object('BottleNeck', neck), .005, 1), (120, 60, 24), lambda p, n: BOTTLE_HEAD, hand_l, .4))
    parts.append((fuse(to_object('BottleHead', bhead), .005, 1), (200, 100, 40), lambda p, n: BOTTLE_HEAD, hand_l, .4))
    return parts, tip

# ---------------------------------------------------------------- part-timer (バイト)
BAITO_SKIN = (.60, .42, .32, .34); BAITO_LIP = (.52, .26, .24, .45); BAITO_HAIR = (.05, .035, .025, .30)
CAP_NAVY = (.06, .08, .20, .16); CAP_LOGO = (.88, .70, .10, .2); TEE_RED = (.62, .10, .08, .10); TEE_WHITE = (.90, .88, .84, .10)
JEANS = (.12, .16, .26, .12); BOOT = (.04, .04, .045, .45); APRON_BLACK = (.05, .05, .06, .12)
MOP_STICK = (.75, .75, .72, .55); MOP_HEAD = (.86, .84, .78, .08); BRUSH_POLE = (.12, .45, .30, .50); BRUSH_HEAD = (.92, .66, .08, .40); BRISTLE = (.20, .16, .12, .10)

def baseball_cap():
    s = Shape()
    s.ellipsoid(V(0, 3.955, -.035), V(.205, .15, .218), R.rot_xyz(x=-8), 36, 20)
    s.ellipsoid(V(0, 3.89, .22), V(.17, .016, .14), R.rot_xyz(x=-6), 32, 10)   # brim
    return s

def tee_shape():
    s = Shape()
    rings = [(1.98, .40, .262, 0), (2.30, .385, .258, .005), (2.50, .375, .262, .012), (2.70, .385, .27, .02), (2.90, .395, .278, .025),
             (3.05, .40, .27, .02), (3.18, .405, .245, .005), (3.28, .395, .21, -.01), (3.36, .30, .17, -.02), (3.43, .17, .145, -.035)]
    s.sweep([V(0, y, cz) for y, _, _, cz in rings], [w for _, w, _, _ in rings], [d for _, _, d, _ in rings], V(0, 0, 1), 2.6, 40)
    for x in (-1, 1):
        sh = P('Arm_R'); sh = V(x * sh.x, sh.y, sh.z); el = P('Forearm_R'); el = V(x * el.x, el.y, el.z)
        dd = (el - sh).normalized()
        s.ellipsoid(sh + V(x * .025, -.01, 0), V(.135, .105, .13))
        s.sweep([sh + dd * .02, sh + dd * .17, sh + dd * .32], [.145, .145, .14], [.145, .143, .138], V(0, 0, 1), 2.2, 28)
    collar = [V(math.sin(a) * .158, 3.43 + .012 * math.cos(a), -.04 + math.cos(a) * .15) for a in [i * math.tau / 28 for i in range(29)]]
    s.chain(collar, [.018] * 29, 10)
    return s

def tee_color(p, n):
    if p.y > 3.41: return TEE_WHITE
    if p.z > .2 and abs(p.y - 2.95) < .09 and abs(p.x) < .2: return TEE_WHITE   # the shop's logo panel on the chest
    return R.fabric(TEE_RED, p, .05, 40)

def waist_apron():
    def hw(y): return .44
    def zf(x, y): return (.30 if y > 1.9 else lerp(.30, .27, smooth(1.9, 1.5, y))) - .7 * x * x
    s = curved_panel(2.12, 1.45, hw, zf, .016)
    s.sweep([V(-.44, 2.1, .2), V(-.42, 2.1, -.2), V(0, 2.1, -.29), V(.42, 2.1, -.2), V(.44, 2.1, .2)], [.025] * 5, [.007] * 5, V(0, 1, 0), 4, 10)
    return s

def boot(x):
    s = Shape(); cx = x * .222; sx = 1 if x > 0 else -1
    s.sweep([V(cx, 1.0, -.02), V(cx, .7, -.03), V(cx, .4, -.04), V(cx, .2, -.05)], [.17, .16, .15, .13], [.17, .16, .15, .13], V(0, 0, 1), 2.0, 24)   # shaft over the jeans
    s.ellipsoid(V(cx, .13, -.09), V(.11, .11, .12))
    s.sweep([V(cx, .13, -.12), V(cx + sx * .008, .12, .1), V(cx + sx * .018, .1, .27), V(cx + sx * .02, .085, .37)], [.115, .12, .12, .105], [.1, .09, .07, .05], V(0, 1, 0), 2.6, 22)
    s.sweep([V(cx, .025, -.21), V(cx, .025, .39)], [.12, .115], [.028, .028], V(0, 1, 0), 4.0, 20)
    return s

def mop_parts():
    """The mop in the right fist (the Weapon bone, where the others hold a slipper): stick down past the knee, strands at the end."""
    g = R.GRIP
    stick = Shape(); stick.capsule(g + V(-.02, .5, 0), g + V(-.02, -1.25, 0), .022, .022, 12)
    head = Shape()
    for i in range(14):
        a = i * math.tau / 14
        top = g + V(-.02, -1.25, 0)
        s0 = top + V(math.cos(a) * .05, 0, math.sin(a) * .05)
        s1 = top + V(math.cos(a) * .17, -.28, math.sin(a) * .17)
        head.chain([s0, (s0 + s1) / 2 + V(0, -.04, 0), s1], [.022, .02, .018], 8)
    return stick, head

def brush_parts():
    """The deck brush in the left hand, held like the lance: a long green pole, a yellow head across its end, bristles."""
    w, d, p, t = tool_frame()
    pole = Shape(); pole.capsule(w - d * .1, w + d * 1.45, .025, .025, 12)
    hc = w + d * 1.52
    head = Shape(); head.sweep([hc - t * .26, hc + t * .26], [.06, .06], [.05, .05], d, 4.0, 16)
    bristles = Shape(); bristles.sweep([hc + d * .07 - t * .25, hc + d * .07 + t * .25], [.05, .05], [.045, .045], d, 4.0, 16)
    return pole, head, bristles, hc + d * .12

def baito_parts():
    parts = []
    head = R.head_object()
    face = skin_tone(BAITO_SKIN, .06, BAITO_LIP, (.05, .04, .03), .011)
    def scalp(p, n):
        if p.y > 3.74 and (p.z < -.02 or abs(p.x) > .15):
            k = 1 + .25 * noise.noise(V(p.x * 70, p.y * 30, p.z * 70))
            return (BAITO_HAIR[0] * k, BAITO_HAIR[1] * k, BAITO_HAIR[2] * k, BAITO_HAIR[3])
        return face(p, n)
    parts.append((head, (2700, 1250, 520), scalp, R.head_weights, 1.0))
    parts.append((fuse(to_object('Baito_Cap', baseball_cap()), .007, 2), (900, 420, 170),
                  lambda p, n: CAP_LOGO if p.z > .17 and p.y > 3.95 and abs(p.x) < .06 else CAP_NAVY, R.const('Head'), .9))
    for x in (-R.EYE_C.x, R.EYE_C.x):
        parts.append((to_object('Baito_Eye', R.eye_shape(x)), (260, 110, 40), lambda p, n, x=x: R.eye_color(p, n, x), R.const('Head'), .9))
    parts.append((fuse(to_object('Baito_Tee', tee_shape()), .0105, 3, cloth_folds), (2150, 1100, 480), tee_color, R.shirt_weights, 1.0))
    parts.append((fuse(to_object('Baito_Apron', waist_apron()), .0085, 2, lambda p, n: .003 * noise.noise(p * 14)), (1100, 520, 220),
                  lambda p, n: R.fabric(APRON_BLACK, p, .06, 40), apron_weights, .9))
    parts.append((fuse(to_object('Baito_Jeans', R.pants_shape()), .0115, 3, R.pants_detail), (1500, 760, 330), lambda p, n: R.fabric(JEANS, p, .1, 60), R.pants_weights, 1.0))
    for x in (-1, 1):
        hs, tips = R.hand_shape(x > 0); limb = R.arm_shape(x); limb.add(hs.v, hs.f)
        parts.append((fuse(to_object('Baito_ArmHand', limb), .0056, 2), (1120, 540, 210), skin_tone(BAITO_SKIN, 0, BAITO_LIP), R.arm_hand_weights(x > 0), 1.0))
        side = 'L' if x < 0 else 'R'
        boot_w = lambda p, side=side: R.W(('Shin_' + side, smooth(.24, .40, p.y)), ('Foot_' + side, 1 - smooth(.24, .40, p.y))) if p.y < .9 else R.W(('Shin_' + side, 1.0))
        parts.append((fuse(to_object('Baito_Boot', boot(x)), .0075, 2), (700, 330, 130), lambda p, n: BOOT, boot_w, 1.0))
    stick, mop = mop_parts()
    weapon = R.const('Weapon')
    parts.append((fuse(to_object('MopStick', stick), .005, 1), (200, 90, 40), lambda p, n: MOP_STICK, weapon, .5))
    parts.append((fuse(to_object('MopHead', mop), .006, 1), (700, 320, 130), lambda p, n: (MOP_HEAD[0] * (1 + .1 * noise.noise(p * 40)), MOP_HEAD[1], MOP_HEAD[2], MOP_HEAD[3]), weapon, .6))
    pole, bhead, bristles, tip = brush_parts()
    hand_l = R.const('Hand_L')
    parts.append((fuse(to_object('BrushPole', pole), .005, 1), (220, 100, 40), lambda p, n: BRUSH_POLE, hand_l, .5))
    parts.append((fuse(to_object('BrushHead', bhead), .006, 1), (300, 140, 60), lambda p, n: BRUSH_HEAD, hand_l, .5))
    parts.append((fuse(to_object('Bristles', bristles), .006, 1), (300, 140, 60), lambda p, n: BRISTLE, hand_l, .5))
    return parts, tip

# ---------------------------------------------------------------- build (same as the family's)
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

MAKERS = {'Chef': chef_parts, 'Okami': okami_parts, 'Baito': baito_parts}
only = None
if '--only' in sys.argv: only = sys.argv[sys.argv.index('--only') + 1].split(',')
all_reports = []; all_lods = {}
for actor, maker in MAKERS.items():
    if only and actor not in only: continue
    parts, tip = maker()
    bones, levels, lods, report = build(actor, parts, mat)
    (OUT / (actor + '.json')).write_text(json.dumps(dict(version=1, actor=actor, bones=bones, levels=levels,
        nozzleTip=dict(x=round(tip.x, 5), y=round(tip.y, 5), z=round(tip.z, 5))), separators=(',', ':')), encoding='utf8')
    all_reports += report; all_lods[actor] = lods
    print('DINER_BUDGET ' + json.dumps(report))
(AUTHOR / 'diner-budget.json').write_text(json.dumps(all_reports, indent=2), encoding='utf8')

# Preview: the actors side by side.
names = list(all_lods.keys())
for k, actor in enumerate(names):
    for i, o in enumerate(all_lods[actor]):
        o.hide_render = i > 0; o.hide_set(i > 0)
        o.location = ((k - (len(names) - 1) / 2) * 1.5, 0, 0)
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
bpy.ops.wm.save_as_mainfile(filepath=str(AUTHOR / 'GokiTakeover_Diner.blend'))
if '--render-preview' in sys.argv:
    width = max(5.6, 1.5 * len(names) + 2.5)
    for name, loc, target, scale in [('front', (1.6, -8, 2.4), (0, 0, 2.0), width), ('back', (-2.4, 7.5, 2.6), (0, 0, 2.0), width),
                                     ('face', (.3, -8, 3.6), (0, 0, 3.75), 1.2)]:
        cam.location = loc; aim(cam, target); cam_data.ortho_scale = scale
        scene.render.filepath = str(AUTHOR / ('diner-' + name + '.png')); bpy.ops.render.render(write_still=True)
