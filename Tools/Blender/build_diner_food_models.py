"""Blender 5.x authoring of the Stage 3 diner foods (町中華) -> editable .blend + engine JSON (Source/DinerFood.json).
Run: blender --background --threads 2 --python Tools/Blender/build_diner_food_models.py [-- --render-preview]
Same method as the Stage 1 foods (build_food_models.py): primitives fused with a voxel remesh, relaxed, displaced
with procedural noise, bitten with boolean cutters, decimated to a budget and painted with vertex colours
(rgb = albedo, a = smoothness). Ten foods, in FoodValue.Kind order after the Stage 1 five:
天かす, 麺の切れ端, チャーハンのこぼれ, 餃子, 唐揚げ, ごま団子, 客の食べ残し (half a spring roll), 小麦粉 (a torn paper
sack corner spilling flour), チャーシューの塊, 焼売のせいろ (a bamboo steamer of four shumai, the big one).
Authoring units are metres, Blender Z up; the export converts to Unity (x, z, -y). Floor contact is z = 0.
Only DinerFood.json is written (the Stage 1 foods stay as they are). No third-party assets.
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
BUDGET = 3200   # triangles per food (the steamer gets more)

def uvec(p): return (p[0], p[2], -p[1])
def mix(a, b, t): t = max(0., min(1., t)); return tuple(x + (y - x) * t for x, y in zip(a, b))
def smooth(e0, e1, x): t = max(0., min(1., (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
def fbm(p, scale, octaves=4, seed=0.):
    return noise.fractal(Vector(p) * scale + Vector((seed, seed * 1.7, seed * .3)), 1.0, 2.0, octaves, noise_basis='PERLIN_ORIGINAL')
def cells(p, scale, seed=0.):
    d, _ = noise.voronoi(Vector(p) * scale + Vector((seed, -seed, seed * .5)), distance_metric='DISTANCE', exponent=2.5)
    return d[0], d[1]

# ---------------- mesh helpers (as build_food_models.py) ----------------
def _euler(rot):
    from mathutils import Euler
    return Euler(tuple(math.radians(x) for x in rot))

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

def tube(bm, pts, r, seg=12):
    """A strand through points: short cylinders with a ball at every joint (the remesh fuses them)."""
    for a, b in zip(pts, pts[1:]): cylinder(bm, a, b, r, r, seg)
    for p in pts: sphere(bm, p, (r, r, r), seg, seg // 2)

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
    for p in me.polygons: p.use_smooth = True; p.material_index = 0

def ground(ob):
    lo = min(v.co.z for v in ob.data.vertices)
    deform(ob, lambda p: p - Vector((0, 0, lo)))

def fried(p, seed, light=(.86, .60, .22), dark=(.52, .27, .07)):
    f1, _ = cells(p, 30, seed)
    c = mix(light, dark, .5 + .9 * fbm(p, 10, 3, seed) + .4 * smooth(.2, .5, f1))
    return mix(c, (.93, .72, .32), smooth(.3, .05, f1) * .5)

# ---------------- foods ----------------
def tenkasu(name):
    """天かす: a scatter of crisp batter bits, puffed and holey, some stuck together."""
    rnd = random.Random(5); bm = bmesh.new()
    for i in range(16):
        a = rnd.uniform(0, math.tau); d = math.sqrt(rnd.uniform(0, 1)) * .17
        s = rnd.uniform(.028, .05)
        sphere(bm, (math.cos(a) * d, math.sin(a) * d, s * .55), (s, s * rnd.uniform(.7, 1.0), s * .62), 14, 8, (rnd.uniform(0, 60), rnd.uniform(0, 60), rnd.uniform(0, 180)))
    ob = link(name, bm)
    remesh(ob, .0042, 3)
    displace(ob, lambda p, n: .010 * fbm(p, 40, 3, 2) - .006 * smooth(.25, .05, cells(p, 70, 1)[0]))
    budget(ob); ground(ob)
    paint(ob, lambda p, n: (*mix(fried(p, 4, (.95, .78, .36), (.74, .50, .16)), (.99, .90, .62), smooth(.15, .0, cells(p, 70, 1)[0]) * .4), .42))
    return ob

def noodles(name):
    """麺の切れ端: a few short wavy ramen noodles tangled on the floor."""
    rnd = random.Random(8); bm = bmesh.new()
    for k in range(6):
        a = rnd.uniform(0, math.tau); fwd = Vector((math.cos(a), math.sin(a), 0)); side = Vector((-fwd.y, fwd.x, 0))
        start = Vector((rnd.uniform(-.08, .08), rnd.uniform(-.08, .08), 0)) - fwd * .17
        pts = []
        for i in range(15):
            s = i / 14
            pts.append(start + fwd * (.34 * s) + side * (.022 * math.sin(s * 22 + k)) + Vector((0, 0, .013 + .012 * (k % 3) * math.sin(s * math.pi))))
        tube(bm, pts, .0115, 10)
    ob = link(name, bm)
    remesh(ob, .0026, 2)
    budget(ob); ground(ob)
    paint(ob, lambda p, n: (*mix((.95, .84, .45), (.86, .70, .30), .5 + fbm(p, 25, 2)), .62))
    return ob

def fried_rice(name):
    """チャーハンのこぼれ: a small heap of fried rice: golden grains, scrambled egg, green onion, chashu bits."""
    rnd = random.Random(12); bm = bmesh.new()
    sphere(bm, (0, 0, .015), (.15, .12, .045), 24, 10)
    for i in range(46):
        a = rnd.uniform(0, math.tau); d = math.sqrt(rnd.uniform(0, 1)) * .17
        h = .055 * max(0, 1 - (d / .18) ** 2)
        sphere(bm, (math.cos(a) * d, math.sin(a) * d * .85, .016 + h * rnd.uniform(.4, 1.0)), (.03, .013, .012), 12, 6, (rnd.uniform(-20, 20), rnd.uniform(-20, 20), rnd.uniform(0, 180)))
    ob = link(name, bm)
    remesh(ob, .0028, 2)
    displace(ob, lambda p, n: .0015 * fbm(p, 60, 2))
    budget(ob); ground(ob)
    def colour(p, n):
        egg = smooth(.12, .3, fbm(p, 9, 2, 3)); onion = smooth(.32, .42, fbm(p, 14, 2, 7)); pork = smooth(.34, .44, fbm(p, 12, 2, 11))
        rgb = mix((.93, .82, .55), (.80, .62, .30), .5 + fbm(p, 30, 2))
        rgb = mix(rgb, (.98, .82, .20), egg * .9)
        rgb = mix(rgb, (.30, .62, .20), onion)
        rgb = mix(rgb, (.62, .28, .20), pork)
        return (*rgb, .50)
    paint(ob, colour)
    return ob

def gyoza(name):
    """餃子: a crescent dumpling, pleated along the top seam, the bottom fried crisp."""
    bm = bmesh.new()
    sphere(bm, (0, 0, .055), (.17, .085, .06), 40, 20)
    ob = link(name, bm)
    remesh(ob, .004, 4)
    def crescent(p):
        q = p.copy()
        q.y += .07 * (1 - (p.x / .17) ** 2)                             # bend into a half moon
        if p.z < .03: q.z = .03 - (.03 - p.z) * .55                     # flat fried bottom
        ridge = math.exp(-((p.y) / .02) ** 2) * smooth(.06, .1, p.z)    # the seam on top, pinched up
        q.z += .03 * ridge * (1 - (p.x / .17) ** 2)
        return q
    deform(ob, crescent)
    displace(ob, lambda p, n: .006 * smooth(.08, .12, p.z) * math.sin(p.x * 120) + .0015 * fbm(p, 40, 2))   # pleats
    budget(ob); ground(ob)
    def colour(p, n):
        skin = mix((.95, .91, .80), (.88, .82, .66), .5 + fbm(p, 20, 2))
        crisp = mix((.80, .52, .18), (.55, .30, .08), .5 + fbm(p, 14, 3, 2))
        return (*mix(skin, crisp, smooth(.025, .0, p.z) + smooth(-.3, -.8, n.z)), .45)
    paint(ob, colour)
    return ob

def karaage(name):
    """唐揚げ: two craggy pieces of fried chicken."""
    rnd = random.Random(21); bm = bmesh.new()
    for c, s in [((-.07, 0, .075), (.11, .09, .075)), ((.09, .03, .065), (.095, .08, .065))]:
        sphere(bm, c, s, 28, 14)
        for i in range(6):
            a = rnd.uniform(0, math.tau)
            sphere(bm, (c[0] + math.cos(a) * s[0] * .8, c[1] + math.sin(a) * s[1] * .8, c[2] + rnd.uniform(-.01, .04)), (.04, .035, .03), 12, 8, (0, 0, rnd.uniform(0, 180)))
    ob = link(name, bm)
    remesh(ob, .0042, 6)
    displace(ob, lambda p, n: .012 * fbm(p, 18, 4, 4) + .007 * smooth(.45, .1, cells(p, 30, 3)[0]) + .004 * fbm(p, 70, 2, 9))
    budget(ob); ground(ob)
    paint(ob, lambda p, n: (*fried(p, 3, (.78, .46, .14), (.44, .20, .05)), .55))
    return ob

def sesame_ball(name):
    """ごま団子: a fried sesame ball, seeds all over it, sitting a little squashed."""
    bm = bmesh.new()
    sphere(bm, (0, 0, .11), (.12, .12, .11), 40, 20)
    ob = link(name, bm)
    remesh(ob, .004, 3)
    def seed_of(p):
        f1, f2 = cells(p, 40, 6)
        return smooth(.18, .08, f1)
    displace(ob, lambda p, n: .004 * seed_of(p) + .002 * fbm(p, 30, 2))
    budget(ob); ground(ob)
    paint(ob, lambda p, n: (*mix(mix((.80, .52, .20), (.62, .36, .10), .5 + fbm(p, 9, 2)), (.95, .88, .70), seed_of(p)), .5))
    return ob

def spring_roll(name):
    """客の食べ残し: half a spring roll left on a plate edge: blistered wrapper, the bitten end showing the filling."""
    bm = bmesh.new()
    cylinder(bm, (-.19, 0, .065), (.13, 0, .065), .065, .062, 32)
    sphere(bm, (-.19, 0, .065), (.03, .065, .065), 24, 12)
    ob = link(name, bm)
    remesh(ob, .004, 5)
    displace(ob, lambda p, n: .004 * fbm(p, 30, 3, 2) + .003 * smooth(.3, .1, cells(p, 45, 5)[0]))
    bite = Vector((.17, -.01, .07))
    makers = [lambda b: sphere(b, bite, (.07, .09, .09), 24, 12)]
    cut(ob, makers)
    budget(ob); ground(ob)
    def colour(p, n):
        inside = (p - bite).length < .095 and p.x > .07
        if inside:
            f = fbm(p, 30, 2, 4)
            return (*mix((.72, .62, .40), (.40, .52, .22), smooth(.0, .3, f)), .4)   # cabbage, glass noodles, pork
        return (*fried(p, 7, (.92, .70, .30), (.66, .40, .12)), .55)
    paint(ob, colour)
    return ob

def flour(name):
    """小麦粉: a torn corner of a paper flour sack, flour spilling out of it into a little drift."""
    bm = bmesh.new()
    box(bm, (-.05, 0, .07), (.22, .17, .14), (0, 0, 10))
    sphere(bm, (.13, .02, .0), (.16, .13, .05), 28, 12)
    ob = link(name, bm)
    remesh(ob, .0045, 3)
    def sack(p): return p.x < .06 and p.z > .015
    deform(ob, lambda p: p + Vector((0, 0, -.03 * smooth(-.05, .06, p.x))) if sack(p) else p)   # sagging toward the tear
    displace(ob, lambda p, n: (.006 * fbm(p, 14, 3, 1) - .004 * abs(math.sin(p.y * 40 + p.z * 30))) if sack(p) else .003 * fbm(p, 30, 2))
    cut(ob, [lambda b: box(b, (.07, 0, .12), (.06, .24, .12), (0, 25, 0))])   # the torn mouth
    budget(ob); ground(ob)
    def colour(p, n):
        if sack(p):
            band = abs(p.z - .095) < .016 and n.z < .6
            kraft = mix((.44, .30, .15), (.32, .21, .10), .5 + fbm(p, 12, 2))   # brown kraft paper (linear colour)
            kraft = mix(kraft, (.98, .97, .94), smooth(.45, .8, n.z) * .45)   # flour dusted on top of the sack
            paper = (.55, .05, .03) if band else kraft
            return (*paper, .14)
        return (*mix((.98, .97, .94), (.90, .89, .86), .5 + fbm(p, 20, 2)), .08)
    paint(ob, colour)
    return ob

def chashu(name):
    """チャーシューの塊: a tied roll of braised pork, the cut end showing the pink meat and the fat swirl."""
    bm = bmesh.new()
    cylinder(bm, (-.17, 0, .12), (.15, 0, .12), .12, .118, 40)
    for x in (-.09, .0, .09):   # string ties
        g = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=32, radius1=.125, radius2=.125, depth=.016)
        bmesh.ops.transform(bm, matrix=Matrix.Translation((x, 0, .12)) @ Matrix.Rotation(math.radians(90), 4, 'Y'), verts=g['verts'])
    ob = link(name, bm)
    remesh(ob, .0045, 4)
    def end(p): return p.x > .145
    def tie(p): return min(abs(p.x - x) for x in (-.09, .0, .09)) < .01
    displace(ob, lambda p, n: 0 if end(p) else .006 * fbm(p, 12, 3, 3) - (.006 if tie(p) else 0))
    deform(ob, lambda p: Vector((p.x, p.y, max(p.z, .012))))   # sits on its side
    budget(ob); ground(ob)
    def colour(p, n):
        if end(p) and n.x > .6:
            r = math.hypot(p.y, p.z - .12); swirl = .5 + .5 * math.sin(r * 70 + math.atan2(p.z - .12, p.y) * 1.0)
            meat = mix((.84, .52, .46), (.95, .86, .76), smooth(.6, .9, swirl))
            return (*mix(meat, (.42, .17, .08), smooth(.10, .12, r)), .5)
        if tie(p): return (.90, .86, .76, .2)
        return (*mix((.46, .18, .07), (.26, .09, .03), .5 + fbm(p, 9, 3)), .72)   # glazed
    paint(ob, colour)
    return ob

def shumai(name):
    """焼売のせいろ: a bamboo steamer (lid off) with four shumai, each with a green pea on top. The big one."""
    bm = bmesh.new()
    cylinder(bm, (0, 0, 0), (0, 0, .15), .36, .36, 64)
    ob = link(name + '_Steamer', bm)
    remesh(ob, .006, 2)
    cut(ob, [lambda b: cylinder(b, (0, 0, .03), (0, 0, .3), .335, .335, 64)])   # hollow, with a slatted floor
    bm = bmesh.new()
    centres = [Vector((math.cos(i * math.tau / 4 + .4) * .16, math.sin(i * math.tau / 4 + .4) * .16, .03)) for i in range(4)]
    for c in centres:
        cylinder(bm, c, c + Vector((0, 0, .14)), .085, .075, 28)
        sphere(bm, c + Vector((0, 0, .14)), (.08, .08, .035), 24, 10)
        sphere(bm, c + Vector((0, 0, .175)), (.026, .026, .024), 12, 8)   # pea
    food = link(name + '_Shumai', bm)
    remesh(food, .0035, 3)
    def pleats(p, n):
        c = min(centres, key=lambda c: (p.x - c.x) ** 2 + (p.y - c.y) ** 2)
        r = math.hypot(p.x - c.x, p.y - c.y)
        if r < .05: return 0   # the open top and the pea stay smooth
        return .006 * smooth(.06, .14, p.z) * math.sin(math.atan2(p.y - c.y, p.x - c.x) * 18 + p.z * 40)
    displace(food, pleats)
    budget(ob, 2600); budget(food, 3000)
    def steamer_colour(p, n):
        slat = .5 + .5 * math.sin(math.atan2(p.y, p.x) * 90) if abs(n.z) < .5 else .5 + .5 * math.sin(p.x * 120)
        rgb = mix((.80, .64, .38), (.66, .50, .26), slat * .5 + .3 * fbm(p, 15, 2))
        if abs(p.z - .12) < .015 or abs(p.z - .03) < .012: rgb = mix(rgb, (.56, .40, .18), .6)   # binding bands
        return (*rgb, .25)
    peas = [c + Vector((0, 0, .175)) for c in centres]
    def shumai_colour(p, n):
        if min((p - q).length for q in peas) < .03: return (.36, .66, .22, .6)
        top = smooth(.15, .18, p.z)   # the pork filling shows at the open top
        return (*mix(mix((.95, .90, .70), (.86, .78, .55), .5 + fbm(p, 25, 2)), (.84, .56, .46), top * .8), .5)
    paint(ob, steamer_colour); paint(food, shumai_colour)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); food.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.join(); ob.name = name
    ground(ob)
    return ob

# ---------------- build, export ----------------
mat = bpy.data.materials.new('FoodVertexSurface'); mat.use_nodes = True
nodes = mat.node_tree.nodes; links = mat.node_tree.links; nodes.clear()
bsdf = nodes.new('ShaderNodeBsdfPrincipled'); out = nodes.new('ShaderNodeOutputMaterial'); links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
vc = nodes.new('ShaderNodeVertexColor'); vc.layer_name = 'Surface'; links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
inv = nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1
links.new(vc.outputs['Alpha'], inv.inputs[1]); links.new(inv.outputs[0], bsdf.inputs['Roughness'])

# Order matches FoodValue.Kind after Garbage.
foods = [('Food_Tenkasu', tenkasu), ('Food_Noodles', noodles), ('Food_FriedRice', fried_rice), ('Food_Gyoza', gyoza),
         ('Food_Karaage', karaage), ('Food_SesameBall', sesame_ball), ('Food_DinerLeftovers', spring_roll), ('Food_Flour', flour),
         ('Food_Chashu', chashu), ('Food_Shumai', shumai)]
models = []; report = {}
for i, (name, make) in enumerate(foods):
    ob = make(name); ob.data.materials.clear(); ob.data.materials.append(mat)
    me = ob.data; me.calc_loop_triangles()
    colours = me.color_attributes['Surface']
    positions = [dict(zip('xyz', [round(c, 6) for c in uvec(v.co)])) for v in me.vertices]
    normals = [dict(zip('xyz', [round(c, 5) for c in uvec(v.normal)])) for v in me.vertices]
    rgba = [dict(zip('rgba', [round(c, 4) for c in colours.data[k].color])) for k in range(len(me.vertices))]
    tris = [v for t in me.loop_triangles for v in t.vertices]
    size = [max(v.co[a] for v in me.vertices) - min(v.co[a] for v in me.vertices) for a in range(3)]
    models.append(dict(name=name, positions=positions, normals=normals, colors=rgba, uv=[dict(x=p['x'], y=p['z']) for p in positions], triangles=tris))
    report[name] = dict(vertices=len(positions), triangles=len(tris) // 3, size_m=[round(s, 3) for s in size])
    ob.location = ((i % 5 - 2) * .9, (i // 5 - .5) * -.9, 0)
(OUT / 'DinerFood.json').write_text(json.dumps(dict(version=1, models=models), separators=(',', ':')), encoding='utf8')
(AUTHOR / 'diner-food-budget.json').write_text(json.dumps(report, indent=2), encoding='utf8')
print('DINER_FOOD_BUDGET ' + json.dumps(report))

scene = bpy.context.scene
world = bpy.data.worlds.new('World'); scene.world = world; world.color = (.12, .12, .12)
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
scene.render.threads_mode = 'FIXED'; scene.render.threads = 2
scene.render.resolution_x = 1600; scene.render.resolution_y = 900
scene.view_settings.view_transform = 'AgX'
def aim(o, p): o.rotation_euler = (Vector(p) - o.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0)); floor = bpy.context.object
fm = bpy.data.materials.new('Floor'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.32, .27, .22, 1); floor.data.materials.append(fm)
cam_data = bpy.data.cameras.new('Camera'); cam = bpy.data.objects.new('Camera', cam_data); scene.collection.objects.link(cam)
cam.location = (0, -3.4, 2.6); aim(cam, (0, 0, .05)); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 5.0; scene.camera = cam
for pos, power, size in [((2, -3, 4), 110, 3), ((-3, -1, 2), 30, 3), ((0, 3, 3), 60, 2)]:
    ld = bpy.data.lights.new('Key', 'AREA'); ld.energy = power; ld.size = size; lo = bpy.data.objects.new('Key', ld); scene.collection.objects.link(lo); lo.location = pos; aim(lo, (0, 0, 0))
bpy.ops.wm.save_as_mainfile(filepath=str(AUTHOR / 'GokiTakeover_DinerFood.blend'))
if '--render-preview' in sys.argv:
    scene.render.filepath = str(AUTHOR / 'diner-food-preview.png'); bpy.ops.render.render(write_still=True)
    cam.location = (0, -.01, 4.0); aim(cam, (0, 0, 0))
    scene.render.filepath = str(AUTHOR / 'diner-food-preview-top.png'); bpy.ops.render.render(write_still=True)
