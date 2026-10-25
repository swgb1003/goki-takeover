"""Blender 5.x authoring of the Stage 2 house cat "Mike" (a calico) -> .blend + engine JSON.
Run: blender --background --threads 2 --python Tools/Blender/build_cat_model.py [-- --render-preview]
Same pipeline as the resident (resident_model.py): primitives fused by voxel remesh, fur detail displaced along
normals, three LODs, colour/smoothness/AO in vertex colours, two bones per vertex. Its own quadruped skeleton
(spine, neck, head, four three-bone legs, a four-bone tail) for the game's procedural walk, stalk, pounce and nap.
Big next to a person on purpose: the roaches are big too (shoulder 1.2 units; the resident is 4.1).
Only the Cat outputs are written. No third-party assets.
"""
import bpy, math, json, sys
from mathutils import Vector, noise
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import resident_model as R
from resident_model import V, Shape, smooth, lerp, clamp, fuse, to_object, uvec

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_GokiTakeover/Art/BlenderModels/Source'
AUTHOR = ROOT / 'ArtSource/Blender'
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- skeleton (Unity coordinates, the cat faces +z)
BONES = [
    ('Root', '', V(0, 0, 0)),
    ('Pelvis', 'Root', V(0, 1.02, -0.72)),
    ('Spine', 'Pelvis', V(0, 1.08, -0.18)),
    ('Chest', 'Spine', V(0, 1.06, 0.42)),
    ('Neck', 'Chest', V(0, 1.22, 0.72)),
    ('Head', 'Neck', V(0, 1.52, 0.92)),
    ('Tail1', 'Pelvis', V(0, 1.12, -1.02)),
    ('Tail2', 'Tail1', V(0, 1.32, -1.36)),
    ('Tail3', 'Tail2', V(0, 1.66, -1.58)),
    ('Tail4', 'Tail3', V(0, 2.02, -1.62)),
]
LEGS = {}
for s, t in ((-1, 'L'), (1, 'R')):
    LEGS['F' + t] = [V(s * .24, 0.98, 0.50), V(s * .25, 0.52, 0.56), V(s * .25, 0.14, 0.58), V(s * .25, 0.04, 0.70)]   # shoulder, elbow, wrist, toe
    LEGS['H' + t] = [V(s * .27, 0.98, -0.78), V(s * .28, 0.58, -0.56), V(s * .28, 0.30, -0.90), V(s * .28, 0.04, -0.80)]  # hip, knee, hock, toe
    BONES += [('Upper' + 'F' + t, 'Chest', LEGS['F' + t][0]), ('Lower' + 'F' + t, 'UpperF' + t, LEGS['F' + t][1]), ('Paw' + 'F' + t, 'LowerF' + t, LEGS['F' + t][2])]
    BONES += [('Upper' + 'H' + t, 'Pelvis', LEGS['H' + t][0]), ('Lower' + 'H' + t, 'UpperH' + t, LEGS['H' + t][1]), ('Paw' + 'H' + t, 'LowerH' + t, LEGS['H' + t][2])]
BONE = {b[0]: b for b in BONES}
def P(n): return BONE[n][2]
TAIL = [P('Tail1'), P('Tail2'), P('Tail3'), P('Tail4'), V(0, 2.3, -1.52)]
HEAD_C = V(0, 1.58, 1.02)

# ---------------------------------------------------------------- palette: a calico (white, orange, black), green eyes
WHITE = (.86, .84, .78, .20); ORANGE = (.78, .42, .14, .20); BLACK = (.07, .06, .055, .26)
NOSE = (.82, .45, .48, .55); EYE = (.42, .62, .16, .92); PUPIL = (.01, .01, .01, .95); INNER_EAR = (.86, .58, .56, .25); PAD = (.62, .38, .40, .40)
COLLAR = (.75, .08, .10, .45); BELL = (.92, .74, .18, .85)

def patches(p):
    """Calico coat: big soft patches of orange and black on white; belly, paws and muzzle stay white."""
    a = noise.noise(V(p.x * 1.6 + 3.1, p.y * 1.6, p.z * 1.6)); b = noise.noise(V(p.x * 1.9 - 7.0, p.y * 1.9 + 2.0, p.z * 1.9))
    white_zone = smooth(.75, .45, p.y) + smooth(.15, .05, abs(p.x)) * smooth(1.05, .85, p.y) * (1 if p.z > -.6 else 0)
    if p.y < .45: white_zone = 1
    k = 1 + .06 * noise.noise(p * 40)
    if white_zone < .5 and a > .18: c = ORANGE
    elif white_zone < .5 and b > .22: c = BLACK
    else: c = WHITE
    return (c[0] * k, c[1] * k, c[2] * k, c[3])

def fur(p, n):
    return .006 * noise.noise(p * 26) + .004 * abs(noise.noise(V(p.x * 60, p.y * 14, p.z * 60)))

# ---------------------------------------------------------------- shapes
def body_shape():
    s = Shape()
    # rump -> chest along the back, then the neck to the head
    pts = [V(0, 1.02, -1.0), V(0, 1.06, -0.78), V(0, 1.06, -0.45), V(0, 1.02, -0.1), V(0, 1.03, 0.25), V(0, 1.08, 0.52), V(0, 1.22, 0.74), V(0, 1.40, 0.88)]
    side = [.20, .30, .31, .28, .29, .30, .22, .17]
    depth = [.22, .33, .34, .30, .32, .34, .22, .17]
    s.sweep(pts, side, depth, V(0, 1, 0), 2.2, 30)
    s.ellipsoid(V(0, 0.86, -0.15), V(.25, .16, .45))   # belly
    s.ellipsoid(V(0, 0.98, 0.55), V(.26, .22, .22))    # chest ruff
    return s

def head_shape():
    s = Shape(); c = HEAD_C
    s.ellipsoid(c, V(.30, .26, .27), None, 36, 24)
    s.ellipsoid(c + V(0, -.06, .19), V(.15, .10, .12))                     # muzzle
    for x in (-1, 1):
        s.ellipsoid(c + V(x * .17, -.07, .1), V(.12, .11, .12))            # cheeks
        base = c + V(x * .17, .2, -.02); tip = c + V(x * .24, .44, -.04)
        s.sweep([base, base.lerp(tip, .5), tip], [.11, .07, .012], [.05, .04, .01], V(0, 0, 1), 2.0, 16)   # ears
    s.ellipsoid(c + V(0, -.16, .13), V(.08, .05, .07))                     # chin
    return s

def eye_shape(x):
    s = Shape(); s.ellipsoid(HEAD_C + V(x * .115, .04, .22), V(.065, .055, .04), R.rot_xyz(y=x * 18), 24, 16); return s

def nose_shape():
    s = Shape(); s.ellipsoid(HEAD_C + V(0, -.01, .32), V(.04, .025, .025), None, 16, 10); return s

def leg_shape(key):
    s = Shape(); a, b, c, d = LEGS[key]
    hind = key[0] == 'H'
    if hind: s.chain([a, b, c, d + V(0, .03, 0)], [.15, .10, .07, .06], 18)   # hip, knee, hock and the long foot to the toes
    else: s.chain([a, b, c], [.12, .085, .065], 18)
    if hind: s.ellipsoid(a + V(0, -.1, .02), V(.17, .24, .22))              # haunch
    s.ellipsoid(c.lerp(d, .55) + V(0, -.02, 0), V(.085, .05, .12))          # paw
    return s

def tail_shape():
    s = Shape(); s.chain(TAIL, [.085, .08, .075, .07, .05], 16); return s

def collar_shape():
    s = Shape(); c = V(0, 1.30, 0.80)
    ring = [c + V(math.sin(a) * .21, math.cos(a) * .17 * .5, math.cos(a) * .19) for a in [i * math.tau / 30 for i in range(31)]]
    s.chain(ring, [.025] * 31, 10)
    bell = Shape(); bell.ellipsoid(c + V(0, -.10, .22), V(.05, .05, .05), None, 16, 10)
    return s, bell

# ---------------------------------------------------------------- weights: nearest bone segments (two-bone blend)
SEGMENTS = []
for name, parent, head in BONES:
    if name == 'Root': continue
    children = [b for b in BONES if b[1] == name]
    tail = children[0][2] if children else (TAIL[-1] if name == 'Tail4' else head + V(0, -.08, .06))
    if name.startswith('Paw'): tail = LEGS[name[3:]][3]
    if name == 'Pelvis': tail = P('Spine')
    if name == 'Head': tail = HEAD_C + V(0, 0, .3)
    SEGMENTS.append((name, head, tail))

def nearest(p, allowed):
    out = []
    for name, a, b in SEGMENTS:
        if allowed and not any(name.startswith(x) for x in allowed): continue
        d, _ = R.seg_dist(p, a, b)
        out.append((name, 1 / (d + .02) ** 4))
    out.sort(key=lambda t: -t[1])
    return out[:2]

def weights_for(allowed): return lambda p: nearest(p, allowed)

# ---------------------------------------------------------------- build
def eye_color(x):
    def f(p, n):
        q = p - (HEAD_C + V(x * .115, .04, .22))
        if abs(q.x) < .012 and q.z > .02: return PUPIL
        return (EYE[0] * (1 + .2 * noise.noise(q * 90)), EYE[1], EYE[2], EYE[3])
    return f

def head_color(p, n):
    q = p - HEAD_C
    if q.z > .25 and abs(q.x) < .045 and -.05 < q.y < .02: return NOSE
    for x in (-1, 1):
        if abs(q.x - x * .2) < .05 and q.y > .26 and n.z > .3: return INNER_EAR
    if q.z > .12 and q.y < .02 and abs(q.x) < .17: return WHITE                # white muzzle
    return patches(p)

def paw_color(p, n):
    if p.y < .06 and n.y < -.4: return PAD
    return patches(p)

def build_parts():
    parts = []
    parts.append((fuse(to_object('Cat_Body', body_shape()), .012, 3, fur), (3200, 1500, 600), lambda p, n: patches(p), weights_for(['Pelvis', 'Spine', 'Chest', 'Neck', 'Head']), 1.0))
    parts.append((fuse(to_object('Cat_Head', head_shape()), .008, 2, lambda p, n: fur(p, n) * .6), (2400, 1100, 450), head_color, weights_for(['Head', 'Neck']), 1.0))
    for x in (-1, 1):
        parts.append((to_object('Cat_Eye', eye_shape(x)), (220, 100, 40), eye_color(x), R.const('Head'), .6))
    parts.append((to_object('Cat_Nose', nose_shape()), (80, 40, 20), lambda p, n: NOSE, R.const('Head'), .6))
    for key in ('FL', 'FR', 'HL', 'HR'):
        parts.append((fuse(to_object('Cat_Leg', leg_shape(key)), .01, 2, fur), (1000, 460, 180), paw_color,
                      weights_for(['Upper' + key, 'Lower' + key, 'Paw' + key] + (['Pelvis'] if key[0] == 'H' else ['Chest'])), 1.0))
    parts.append((fuse(to_object('Cat_Tail', tail_shape()), .01, 2, fur), (900, 420, 170), lambda p, n: patches(p + V(0, -0.6, 0)), weights_for(['Tail']), 1.0))
    collar, bell = collar_shape()
    parts.append((fuse(to_object('Cat_Collar', collar), .006, 1), (500, 220, 90), lambda p, n: COLLAR, weights_for(['Neck', 'Chest']), .8))
    parts.append((fuse(to_object('Cat_Bell', bell), .005, 1), (160, 80, 30), lambda p, n: BELL, R.const('Neck'), .6))
    return parts

def build(material):
    ids = {b[0]: i for i, b in enumerate(BONES)}
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
        bpy.ops.object.join(); obj = bpy.context.object; obj.name = 'Cat_Detail' + str(lod)
        flat_cols = [c for g in cols for c in g]; flat_w = [w for g in weights for w in g]
        mesh = obj.data; mesh.materials.clear(); mesh.materials.append(material)
        attr = mesh.color_attributes.new(name='Surface', type='FLOAT_COLOR', domain='POINT')
        for i, c in enumerate(flat_cols): attr.data[i].color = (min(1, c[0]), min(1, c[1]), min(1, c[2]), c[3])
        mesh.calc_loop_triangles()
        tris = [i for t in mesh.loop_triangles for i in t.vertices]
        r7 = lambda x: round(x, 6)
        levels.append(dict(name='Cat_Detail' + str(lod),
            positions=[dict(zip('xyz', map(r7, uvec(v.co)))) for v in mesh.vertices],
            normals=[dict(zip('xyz', map(r7, uvec(v.normal)))) for v in mesh.vertices],
            colors=[dict(zip('rgba', (round(min(1, c[0]), 4), round(min(1, c[1]), 4), round(min(1, c[2]), 4), c[3]))) for c in flat_cols],
            uv=[dict(x=0.0, y=0.0) for _ in mesh.vertices], triangles=tris, weights=flat_w))
        report.append(dict(actor='Cat', lod=lod, vertices=len(mesh.vertices), triangles=len(tris) // 3, bones=len(BONES)))
        lods.append(obj)
    for obj, *_ in parts: bpy.data.objects.remove(obj, do_unlink=True)
    bones = [dict(name=n, parent=p, position=dict(zip('xyz', (h.x, h.y, h.z)))) for n, p, h in BONES]
    return bones, levels, lods, report

mat = bpy.data.materials.new('VertexAlbedo_Roughness'); mat.use_nodes = True
nodes = mat.node_tree.nodes; links = mat.node_tree.links; nodes.clear()
bsdf = nodes.new('ShaderNodeBsdfPrincipled'); out = nodes.new('ShaderNodeOutputMaterial'); links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
vc = nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Surface'; links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
inv = nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1
links.new(vc.outputs['Alpha'], inv.inputs[1]); links.new(inv.outputs[0], bsdf.inputs['Roughness'])

bones, levels, lods, report = build(mat)
(OUT / 'Cat.json').write_text(json.dumps(dict(version=1, actor='Cat', bones=bones, levels=levels), separators=(',', ':')), encoding='utf8')
(AUTHOR / 'cat-budget.json').write_text(json.dumps(report, indent=2), encoding='utf8')
print('CAT_BUDGET ' + json.dumps(report))
for i, o in enumerate(lods): o.hide_render = i > 0; o.hide_set(i > 0)

scene = bpy.context.scene
world = bpy.data.worlds.new('World'); scene.world = world; world.color = (.10, .10, .10)
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
scene.render.threads_mode = 'FIXED'; scene.render.threads = 2
scene.render.resolution_x = 1100; scene.render.resolution_y = 900
scene.view_settings.view_transform = 'AgX'
def aim(o, p): o.rotation_euler = (Vector(p) - o.location).to_track_quat('-Z', 'Y').to_euler()
cam_data = bpy.data.cameras.new('Camera'); cam = bpy.data.objects.new('Camera', cam_data); scene.collection.objects.link(cam); scene.camera = cam
cam_data.type = 'ORTHO'
for pos, power, size in [((4, -6, 5), 300, 4), ((-5, -3, 3), 100, 3), ((1, 6, 4), 200, 3)]:
    ld = bpy.data.lights.new('Key', 'AREA'); ld.energy = power; ld.size = size; lo = bpy.data.objects.new('Key', ld); scene.collection.objects.link(lo); lo.location = pos; aim(lo, (0, 0, 1))
bpy.ops.wm.save_as_mainfile(filepath=str(AUTHOR / 'GokiTakeover_Cat.blend'))
if '--render-preview' in sys.argv:
    # Blender coordinates: x right, y = -Unity z (the cat faces -y), z up.
    for name, loc, target, scale in [('side', (6, 0.3, 1.2), (0, 0.3, 1.0), 3.4), ('front', (1.6, -6, 1.4), (0, 0, 1.1), 3.0), ('face', (0.4, -6, 1.6), (0, -1.0, 1.55), 1.0)]:
        cam.location = loc; aim(cam, target); cam_data.ortho_scale = scale
        scene.render.filepath = str(AUTHOR / ('cat-' + name + '.png')); bpy.ops.render.render(write_still=True)
