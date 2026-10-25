"""Blender 5.x authoring of the five foods (and the poison bait look-alike) -> editable .blend + engine JSON.
Run: blender --background --threads 2 --python Tools/Blender/build_food_models.py [-- --render-preview]
Each food is sculpted from primitives: fused with a voxel remesh, relaxed, displaced with procedural noise
(torn crumb, pores, crispy batter, cookie cracks), bitten with boolean cutters, decimated to a budget and
painted with vertex colours (rgb = albedo, a = smoothness) from the same fields that shaped it.
Authoring units are metres, Blender Z up; the export converts to Unity (x, z, -y) like the characters.
The model's floor contact is z = 0. Only the Food outputs are written. No third-party assets.
"""
import bpy, bmesh, math, json, sys, random
from mathutils import Vector, Matrix, noise
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_GokiTakeover/Art/BlenderModels/Source'
AUTHOR = ROOT / 'ArtSource/Blender'
OUT.mkdir(parents=True, exist_ok=True)
AUTHOR.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
BUDGET = 3200   # triangles per food

def uvec(p): return (p[0], p[2], -p[1])
def mix(a, b, t): t = max(0., min(1., t)); return tuple(x + (y - x) * t for x, y in zip(a, b))
def smooth(e0, e1, x): t = max(0., min(1., (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
def fbm(p, scale, octaves=4, seed=0.):
    return noise.fractal(Vector(p) * scale + Vector((seed, seed * 1.7, seed * .3)), 1.0, 2.0, octaves, noise_basis='PERLIN_ORIGINAL')
def cells(p, scale, seed=0.):
    d, _ = noise.voronoi(Vector(p) * scale + Vector((seed, -seed, seed * .5)), distance_metric='DISTANCE', exponent=2.5)
    return d[0], d[1]

# ---------------- mesh helpers ----------------
def link(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob); return ob

def sphere(bm, loc, scale, seg=32, rings=16, rot=(0, 0, 0)):
    g = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    m = Matrix.Translation(loc) @ _euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=m, verts=g['verts']); return g['verts']

def box(bm, loc, scale, rot=(0, 0, 0)):
    g = bmesh.ops.create_cube(bm, size=1.0)
    m = Matrix.Translation(loc) @ _euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=m, verts=g['verts']); return g['verts']

def cylinder(bm, a, b, r1, r2, seg=24):
    a, b = Vector(a), Vector(b); d = b - a
    g = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=d.length)
    m = Matrix.Translation((a + b) / 2) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=g['verts']); return g['verts']

def _euler(rot):
    from mathutils import Euler
    return Euler(tuple(math.radians(x) for x in rot))

def apply(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    old = ob.data; ob.modifiers.clear(); ob.data = me; bpy.data.meshes.remove(old)

def remesh(ob, voxel, relax=6):
    m = ob.modifiers.new('Remesh', 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = voxel; m.adaptivity = 0
    apply(ob)
    if relax:
        s = ob.modifiers.new('Relax', 'SMOOTH'); s.factor = .5; s.iterations = relax; apply(ob)

def displace(ob, fn):
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.normal_update()
    moves = [(v, v.normal * fn(v.co.copy(), v.normal.copy())) for v in bm.verts]
    for v, d in moves: v.co += d
    bm.to_mesh(ob.data); bm.free(); ob.data.update()

def deform(ob, fn):
    bm = bmesh.new(); bm.from_mesh(ob.data)
    for v in bm.verts: v.co = fn(v.co.copy())
    bm.to_mesh(ob.data); bm.free(); ob.data.update()

def cut(ob, makers):
    """Boolean difference with several cutters, each its own closed object (exact solver, no self-intersection)."""
    group = bpy.data.collections.new(ob.name + '_Cutters'); bpy.context.scene.collection.children.link(group)
    for make in makers:
        bm = bmesh.new(); make(bm); me = bpy.data.meshes.new('Cutter'); bm.to_mesh(me); bm.free()
        group.objects.link(bpy.data.objects.new('Cutter', me))
    m = ob.modifiers.new('Bite', 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.operand_type = 'COLLECTION'; m.collection = group; m.solver = 'EXACT'
    apply(ob)
    for o in list(group.objects): bpy.data.objects.remove(o)
    bpy.data.collections.remove(group)

def budget(ob, triangles=BUDGET):
    ob.data.calc_loop_triangles(); n = len(ob.data.loop_triangles)
    if n > triangles:
        m = ob.modifiers.new('Budget', 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = triangles / n; apply(ob)
    t = ob.modifiers.new('Tidy', 'SMOOTH'); t.factor = .25; t.iterations = 1; apply(ob)

def paint(ob, fn):
    me = ob.data; me.update()
    attr = me.color_attributes.new(name='Surface', type='FLOAT_COLOR', domain='POINT')
    for i, v in enumerate(me.vertices): attr.data[i].color = fn(v.co.copy(), v.normal.copy())
    for p in me.polygons: p.use_smooth = True; p.material_index = 0   # boolean faces point at cutter slots

def ground(ob):
    lo = min(v.co.z for v in ob.data.vertices)
    deform(ob, lambda p: p - Vector((0, 0, lo)))

# ---------------- foods ----------------
CRUST_DARK, CRUST = (.40, .19, .055), (.74, .46, .17)
CRUMB, PORE = (.95, .86, .64), (.70, .55, .33)

def bread(name, seed, crust_dark=CRUST_DARK, crust=CRUST, crumb=CRUMB, pore=PORE):
    """A torn chunk of bread (crust on the domed top and the back) with a few loose crumbs."""
    rnd = random.Random(seed); bm = bmesh.new()
    box(bm, (0, 0, .085), (.31, .23, .17), (0, 0, 8))
    loose = []
    for i in range(5):
        a = i * 1.26 + rnd.uniform(-.3, .3); d = rnd.uniform(.23, .30); s = rnd.uniform(.045, .075)
        c = (math.cos(a) * d, math.sin(a) * d * .85, s * .45)
        box(bm, c, (s, s * rnd.uniform(.7, 1.1), s * .8), (rnd.uniform(0, 40), rnd.uniform(0, 40), rnd.uniform(0, 90)))
        loose.append((Vector(c), s))
    ob = link(name, bm)
    remesh(ob, .0055, 4)
    def main(p): return abs(p.x) < .2 and abs(p.y) < .16 and p.z > .02
    # Torn wedge: thinner at the torn end, lumpy silhouette, then the domed crust top of the loaf.
    def wedge(p):
        if not main(p): return p
        t = (p.x + .155) / .31
        q = Vector((p.x, p.y * (1 - .22 * t), p.z * (1 - .28 * t)))
        q += Vector((fbm(q, 6, 2, seed) * .03, fbm(q, 6, 2, seed + 2) * .03, fbm(q, 6, 2, seed + 4) * .025 * smooth(.04, .1, q.z)))
        return q
    deform(ob, wedge)
    deform(ob, lambda p: p + Vector((0, 0, .035 * max(0, 1 - (p.x / .17) ** 2 - (p.y / .13) ** 2) * smooth(.08, .17, p.z))) if main(p) else p)
    def crust_mask(p, n): return smooth(.35, .75, n.z) * smooth(.09, .14, p.z) + smooth(.45, .8, n.y) * smooth(.03, .07, p.z) if main(p) else 0
    def shape(p, n):
        c = min(1, crust_mask(p, n))
        f1, f2 = cells(p, 34, seed)
        torn = .017 * fbm(p, 22, 4, seed) - .011 * smooth(.32, .06, f1) + .006 * fbm(p, 70, 2, seed + 3)
        return (1 - c) * torn + c * (.004 * fbm(p, 14, 3, seed) - .002 * smooth(.1, .0, f2 - f1))
    displace(ob, shape)
    budget(ob); ground(ob)
    def colour(p, n):
        c = min(1, crust_mask(p, n))
        f1, _ = cells(p, 34, seed)
        inner = mix(crumb, pore, smooth(.30, .05, f1) * .9 + .15 * fbm(p, 40, 2, seed))
        toast = mix(crust, crust_dark, .5 + .6 * fbm(p, 9, 3, seed + 5) + .4 * smooth(.6, 1, n.z))
        edge = mix(inner, mix(crumb, crust, .55), smooth(0, .35, c))
        rgb = mix(edge, toast, smooth(.35, .8, c))
        return (*rgb, .12 + .25 * c)
    paint(ob, colour)
    return ob

def rice(name):
    """Seven glossy grains stuck together in a small heap; each has its germ notch."""
    bm = bmesh.new(); grains = []
    spots = [(-.08, -.04, 0, 28), (.06, -.07, 0, -12), (.0, .05, 0, -40), (.12, .03, 0, 55), (-.12, .07, 0, -5),
             (-.03, -.01, .03, 80), (.05, .01, .028, 15)]
    for i, (x, y, z, yaw) in enumerate(spots):
        tilt = 14 if z > 0 else 0
        rot = _euler((tilt, 0, yaw)); centre = Vector((x, y, .031 + z))
        g = bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=14, radius=1.0)
        for v in g['verts']:
            p = v.co.copy(); p.x *= .085; p.y *= .036; p.z *= .031
            p.z -= .005 * (p.y / .036) ** 2            # flatter belly
            if p.x > .058 and p.y > 0: p -= Vector((.010, .006, 0)) * smooth(.058, .085, p.x)   # germ notch
            v.co = centre + rot.to_matrix() @ p
        grains.append((centre, rot))
    ob = link(name, bm)
    remesh(ob, .003, 3)
    displace(ob, lambda p, n: .0012 * fbm(p, 60, 2) + .0008 * math.sin(p.x * 260 + p.y * 40))
    budget(ob); ground(ob)
    def colour(p, n):
        shade = .5 + .5 * fbm(p, 30, 2)
        rgb = mix((.97, .96, .91), (.88, .88, .84), shade * .5)
        rgb = mix(rgb, (.80, .82, .80), smooth(.3, -.4, n.z) * .5)   # cool translucency underneath
        return (*rgb, .62)
    paint(ob, colour)
    return ob

def cookie(name):
    """A chocolate chip cookie with a bite out of it, cracked on top, crumbs spilling from the bite."""
    rnd = random.Random(42); bm = bmesh.new()
    cylinder(bm, (0, 0, 0), (0, 0, .062), .245, .232, 64)
    chips = []
    for i in range(13):
        a = rnd.uniform(0, math.tau); d = math.sqrt(rnd.uniform(0, 1)) * .19
        c = Vector((math.cos(a) * d, math.sin(a) * d, .066 + rnd.uniform(-.006, .004)))
        r = rnd.uniform(.020, .031)
        sphere(bm, c, (r * 1.15, r, r * .8), 14, 8, (0, 0, rnd.uniform(0, 180)))
        chips.append((c, r))
    for i, (a, d) in enumerate([(.55, .33), (.85, .36), (.3, .37)]):
        s = .032 - i * .006; c = Vector((math.cos(a) * d, math.sin(a) * d, s * .4))
        box(bm, c, (s, s * .8, s * .7), (i * 20, i * 33, i * 50))
    ob = link(name, bm)
    remesh(ob, .0042, 4)
    def on_cookie(p): return Vector((p.x, p.y, 0)).length < .26
    deform(ob, lambda p: p + Vector((0, 0, .018 * max(0, 1 - (p.x * p.x + p.y * p.y) / .055) * smooth(.03, .06, p.z))) if on_cookie(p) else p)
    def chip_of(p):
        best = 9
        for c, r in chips: best = min(best, (p - c - Vector((0, 0, .018 * max(0, 1 - (c.x * c.x + c.y * c.y) / .055)))).length / r)
        return best
    def shape(p, n):
        if chip_of(p) < 1.25: return .002 * fbm(p, 50, 2)
        f1, f2 = cells(p, 16, 7)
        crack = smooth(.07, 0, f2 - f1) * smooth(.3, .8, n.z)
        return .007 * fbm(p, 26, 4, 2) + .004 * fbm(p, 80, 2) - .006 * crack
    displace(ob, shape)
    bite = Vector((.27, .10, 0))
    makers = [lambda b: cylinder(b, bite - Vector((0, 0, .05)), bite + Vector((0, 0, .2)), .125, .125, 48)]
    for k in range(6):   # tooth scallops along the bite edge
        a = math.radians(150 + k * 13); c = bite + Vector((math.cos(a), math.sin(a), 0)) * .125
        makers.append(lambda b, c=c: cylinder(b, c - Vector((0, 0, .05)), c + Vector((0, 0, .2)), .024, .024, 16))
    cut(ob, makers)
    budget(ob); ground(ob)
    def colour(p, n):
        if chip_of(p) < 1.3: return (*mix((.20, .10, .045), (.33, .17, .07), .5 + fbm(p, 40, 2)), .55)
        f1, f2 = cells(p, 16, 7)
        crack = smooth(.07, 0, f2 - f1) * smooth(.3, .8, n.z)
        bitten = smooth(.012, 0, abs(Vector((p.x - bite.x, p.y - bite.y, 0)).length - .125)) * smooth(.5, .1, abs(n.z))
        top = mix((.83, .58, .28), (.70, .44, .18), .5 + .8 * fbm(p, 12, 3, 1))
        side = mix((.62, .36, .14), (.48, .26, .09), .5 + fbm(p, 20, 2))
        rgb = mix(side, top, smooth(.0, .7, n.z))
        rgb = mix(rgb, (.93, .76, .48), crack * .8)
        rgb = mix(rgb, (.90, .70, .42), bitten)
        return (*rgb, .22)
    paint(ob, colour)
    return ob

def drumstick(name):
    """A fried drumstick, crispy batter, the bone sticking out, one big bite showing the meat."""
    bm = bmesh.new()
    vs = sphere(bm, (-.08, 0, .135), (.22, .16, .135), 40, 20)
    for v in vs:   # teardrop: plump end, tapering toward the bone, slight belly sag
        t = smooth(-.12, .15, v.co.x)
        v.co.y *= 1 - .55 * t; v.co.z = .135 + (v.co.z - .135) * (1 - .5 * t) - .012 * t
    for i in range(7):   # lumps of batter
        a = i * 2.4; c = Vector((-.11 + .04 * math.cos(a * 1.3), .115 * math.cos(a), .14 + .095 * math.sin(a)))
        sphere(bm, c, (.05, .045, .03), 12, 8, (i * 20, i * 35, 0))
    cylinder(bm, (.06, 0, .13), (.27, .008, .15), .040, .034, 20)
    sphere(bm, (.288, .032, .152), (.05, .047, .045), 16, 10)
    sphere(bm, (.285, -.022, .148), (.048, .045, .043), 16, 10)
    ob = link(name, bm)
    remesh(ob, .0048, 9)
    def batter(p): return smooth(.09, .05, p.x)
    def shape(p, n):
        b = batter(p)
        f1, _ = cells(p, 30, 3)
        crisp = .011 * fbm(p, 18, 4, 4) + .007 * smooth(.45, .1, f1) + .004 * fbm(p, 75, 2, 9)
        return b * crisp + (1 - b) * .0015 * fbm(p, 40, 2)
    displace(ob, shape)
    bite = Vector((-.06, -.15, .26))   # a mouthful off the upper edge: seen from above and from the side
    makers = [lambda b: sphere(b, bite, (.12, .12, .12), 32, 16)]
    for k in range(6):
        a = math.radians(-60 + k * 24); c = bite + Vector((math.sin(a) * .115, .035, math.cos(a) * .115))
        makers.append(lambda b, c=c: sphere(b, c, (.028, .028, .028), 12, 8))
    cut(ob, makers)
    budget(ob); ground(ob)
    lift = Vector((0, 0, 0))
    def colour(p, n):
        b = batter(p)
        meat = smooth(.03, 0, abs((p - bite).length - .12) - .0) if (p - bite).length < .17 else 0
        f1, _ = cells(p, 30, 3)
        fried = mix((.74, .47, .16), (.50, .25, .06), .5 + .9 * fbm(p, 10, 3, 4) + .5 * smooth(.2, .5, f1))
        fried = mix(fried, (.86, .62, .26), smooth(.3, .05, f1) * .6)
        bone = mix((.94, .90, .80), (.80, .72, .58), smooth(.33, .4, p.x) * .6)
        rgb = mix(bone, fried, b)
        fibres = .5 + .5 * math.sin(p.x * 220 + fbm(p, 25, 2) * 6)
        rgb = mix(rgb, mix((.90, .76, .62), (.78, .58, .45), fibres * .6), meat)
        return (*rgb, .48 * b + .3 * (1 - b) - .2 * meat)
    paint(ob, colour)
    return ob

def banana_peel(name):
    """A cartoon peel splayed on the floor: a yellow cup where the petals meet, four petals flopping out
    pale side up with tips curling to show the spotted yellow skin, one petal folded over, the stalk on a tip."""
    rnd = random.Random(9); bm = bmesh.new()
    layer = bm.verts.layers.float_color.new('Surface')
    def put(p, rgb): v = bm.verts.new(p); v[layer] = rgb; return v
    def skin(p, k):
        f1, _ = cells(p, 24, k)
        rgb = mix((.93, .77, .17), (.82, .62, .09), .5 + fbm(p, 8, 2, k))
        return mix(rgb, (.30, .18, .06), smooth(.24, .08, f1) * .95)
    def flesh(p, u, k):
        f = .5 + .5 * math.sin(u * 20 + fbm(p, 18, 2, k) * 4)
        return mix((.96, .92, .76), (.85, .79, .58), f * .5)
    def loft(rings, cap_start=True, cap_end=True):
        n = len(rings[0])
        for i in range(len(rings) - 1):
            for j in range(n):
                bm.faces.new((rings[i][j], rings[i][(j + 1) % n], rings[i + 1][(j + 1) % n], rings[i + 1][j]))
        if cap_start: bm.faces.new(list(reversed(rings[0])))
        if cap_end: bm.faces.new(rings[-1])
    # Cup: the unpeeled bottom of the banana, yellow outside, with the dark flower-end nub underneath.
    cup = []
    for i, (z, r) in enumerate([(0, .018), (.008, .045), (.03, .06), (.06, .062), (.075, .06)]):
        ring = []
        for j in range(24):
            a = j / 24 * math.tau; p = Vector((math.cos(a) * r, math.sin(a) * r, z))
            ring.append(put(p, (*mix(skin(p, 0), (.22, .15, .07), smooth(.012, 0, z)), .45)))
        cup.append(ring)
    loft(cup, True, True)
    angles = [8, 98, 186, 274]
    for k, ang in enumerate(angles):
        a = math.radians(ang + rnd.uniform(-10, 10)); fwd = Vector((math.cos(a), math.sin(a), 0)); side = Vector((-fwd.y, fwd.x, 0))
        flipped = k == 2   # folded over: yellow side up
        length = rnd.uniform(.30, .36); curl = rnd.uniform(.05, .08)
        ns, nu = 28, 9; rings = []
        for i in range(ns + 1):
            s = i / ns
            centre = fwd * (.045 + length * s) + Vector((0, 0, .07 * (1 - smooth(0, .42, s)) + .012 + curl * smooth(.6, 1, s) ** 1.7))
            centre += side * .025 * math.sin(s * 3.2 + k)
            width = .016 + .05 * math.sin(math.pi * min(1, s * 1.05)) ** .6 * (1 - .65 * smooth(.75, 1, s))
            thick = .014 * (1 - .45 * s)
            ring = []
            for j in range(nu * 2):
                top = j < nu; u = (j / (nu - 1)) * 2 - 1 if top else ((2 * nu - 1 - j) / (nu - 1)) * 2 - 1
                cup_lift = (.024 if not flipped else -.012) * u * u * smooth(.1, .5, s)
                p = centre + side * (u * width) + Vector((0, 0, cup_lift - (0 if top else thick)))
                pale = top != flipped
                rgb = flesh(p, u, k) if pale else skin(p, k)
                if pale: rgb = mix(rgb, (.90, .74, .22), smooth(.75, 1, abs(u)) * .75)   # the skin shows at the edge
                rgb = mix(rgb, (.48, .40, .12), smooth(.18, 0, s) * .5)                 # bruised where it was held
                ring.append(put(p, (*rgb, .32 if pale else .48)))
            rings.append(ring)
        loft(rings)
        if k == 0:   # the stalk stays on one petal's tip
            tip = rings[-1]; c = sum((v.co for v in tip), Vector()) / len(tip)
            g = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=.017, radius2=.012, depth=.07)
            rot = (fwd + Vector((0, 0, .5))).normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
            bmesh.ops.transform(bm, matrix=Matrix.Translation(c + (fwd + Vector((0, 0, .5))).normalized() * .03) @ rot, verts=g['verts'])
            for v in g['verts']: v[layer] = (*mix((.42, .36, .12), (.17, .11, .05), smooth(-.03, .035, (v.co - c).dot(fwd))), .25)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = link(name, bm)
    s = ob.modifiers.new('Subdivide', 'SUBSURF'); s.levels = 1; s.render_levels = 1; apply(ob)
    budget(ob); ground(ob)
    for p in ob.data.polygons: p.use_smooth = True
    return ob

# ---------------- build, export ----------------
mat = bpy.data.materials.new('FoodVertexSurface'); mat.use_nodes = True
nodes = mat.node_tree.nodes; links = mat.node_tree.links; nodes.clear()
bsdf = nodes.new('ShaderNodeBsdfPrincipled'); out = nodes.new('ShaderNodeOutputMaterial'); links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
vc = nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Surface'; links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
inv = nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1
links.new(vc.outputs['Alpha'], inv.inputs[1]); links.new(inv.outputs[0], bsdf.inputs['Roughness'])

# Order matches FoodValue.Kind; Bait is the breadcrumb's poisoned twin (a shade greener, Unity adds the pellets).
foods = [('Food_Breadcrumb', lambda n: bread(n, 3)), ('Food_Rice', rice), ('Food_Sweets', cookie),
         ('Food_Leftovers', drumstick), ('Food_Garbage', banana_peel),
         ('Food_Bait', lambda n: bread(n, 11, (.33, .22, .06), (.62, .47, .17), (.86, .80, .55), (.58, .52, .30)))]
models = []; report = {}
for i, (name, make) in enumerate(foods):
    ob = make(name); ob.data.materials.clear(); ob.data.materials.append(mat)
    me = ob.data; me.calc_loop_triangles()
    colours = me.color_attributes['Surface']
    is_point = colours.domain == 'POINT'
    positions = [dict(zip('xyz', [round(c, 6) for c in uvec(v.co)])) for v in me.vertices]
    normals = [dict(zip('xyz', [round(c, 5) for c in uvec(v.normal)])) for v in me.vertices]
    rgba = [dict(zip('rgba', [round(c, 4) for c in colours.data[k].color])) for k in range(len(me.vertices))]
    tris = [v for t in me.loop_triangles for v in t.vertices]
    size = [max(v.co[a] for v in me.vertices) - min(v.co[a] for v in me.vertices) for a in range(3)]
    models.append(dict(name=name, positions=positions, normals=normals, colors=rgba, uv=[dict(x=p['x'], y=p['z']) for p in positions], triangles=tris))
    report[name] = dict(vertices=len(positions), triangles=len(tris) // 3, size_m=[round(s, 3) for s in size])
    ob.location = ((i % 3 - 1) * 1.0, (i // 3 - .5) * -.9, 0)
(OUT / 'Food.json').write_text(json.dumps(dict(version=1, models=models), separators=(',', ':')), encoding='utf8')
(AUTHOR / 'food-budget.json').write_text(json.dumps(report, indent=2), encoding='utf8')
print('FOOD_BUDGET ' + json.dumps(report))

scene = bpy.context.scene
world = bpy.data.worlds.new('World'); scene.world = world; world.color = (.12, .12, .12)
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
scene.render.threads_mode = 'FIXED'; scene.render.threads = 2
scene.render.resolution_x = 1500; scene.render.resolution_y = 1000
scene.view_settings.view_transform = 'AgX'
def aim(o, p): o.rotation_euler = (Vector(p) - o.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0)); floor = bpy.context.object
fm = bpy.data.materials.new('Floor'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.32, .27, .22, 1); floor.data.materials.append(fm)
cam_data = bpy.data.cameras.new('Camera'); cam = bpy.data.objects.new('Camera', cam_data); scene.collection.objects.link(cam)
cam.location = (0, -3.0, 2.4); aim(cam, (0, 0, .05)); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 3.3; scene.camera = cam
for pos, power, size in [((2, -3, 4), 90, 3), ((-3, -1, 2), 25, 3), ((0, 3, 3), 50, 2)]:
    ld = bpy.data.lights.new('Key', 'AREA'); ld.energy = power; ld.size = size; lo = bpy.data.objects.new('Key', ld); scene.collection.objects.link(lo); lo.location = pos; aim(lo, (0, 0, 0))
bpy.ops.wm.save_as_mainfile(filepath=str(AUTHOR / 'GokiTakeover_Food.blend'))
if '--render-preview' in sys.argv:
    scene.render.filepath = str(AUTHOR / 'food-preview.png'); bpy.ops.render.render(write_still=True)
    cam.location = (0, -.01, 4.0); aim(cam, (0, 0, 0))
    scene.render.filepath = str(AUTHOR / 'food-preview-top.png'); bpy.ops.render.render(write_still=True)
