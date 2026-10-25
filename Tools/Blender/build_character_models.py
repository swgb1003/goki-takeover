"""Blender 5.x authoring -> editable .blend + FBX + engine-neutral skinned meshes.
Run: blender --background --threads 2 --python Tools/Blender/build_character_models.py
Only the new BlenderModels/ArtSource outputs are written. No third-party assets.
The resident is sculpted in resident_model.py (voxel-fused anatomy, baked AO); the roach uses Builder below.
Coordinates used by the authoring helpers are Unity x/right, y/up, z/front.
"""
import bpy, math, json, os, sys, random
from mathutils import Vector
from pathlib import Path
from types import SimpleNamespace
sys.path.insert(0,str(Path(__file__).resolve().parent))
import resident_model

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_GokiTakeover/Art/BlenderModels/Source'
AUTHOR = ROOT / 'ArtSource/Blender'
OUT.mkdir(parents=True, exist_ok=True)
AUTHOR.mkdir(parents=True, exist_ok=True)
random.seed(831)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def bvec(p): return (p[0], -p[2], p[1])
def uvec(p): return (p[0], p[2], -p[1])
def lerp(a,b,t): return tuple(x+(y-x)*t for x,y in zip(a,b))

class Builder:
    def __init__(self, name):
        self.name=name; self.v=[]; self.f=[]; self.c=[]; self.w=[]; self.uv=[]; self.bones=[]
    def bone(self,name,pos,parent='Root',tail=None):
        if name=='Root': parent=''
        self.bones.append(dict(name=name,parent=parent,position=dict(zip('xyz',pos)),tail=tail))
        return name
    def add(self,p,col,bone,uv=(0,0)):
        self.v.append(bvec(p)); self.c.append(col); self.uv.append(uv)
        self.w.append([(bone,1)] if isinstance(bone,str) else bone)
        return len(self.v)-1
    def quad(self,a,b,c,d): self.f.extend([(a,b,c),(a,c,d)])
    def ellipsoid(self,center,scale,col,bone,sides=16,rings=9,deform=None):
        # One vertex per pole avoids zero-area cap triangles and dark shading pinholes.
        bottom=(center[0],center[1]-scale[1],center[2])
        if deform:bottom=deform(bottom,0,0)
        south=self.add(bottom,col,bone,(.5,0));start=len(self.v)
        for j in range(1,rings):
            phi=-math.pi/2+math.pi*j/rings
            for i in range(sides):
                th=math.tau*i/sides
                p=(center[0]+scale[0]*math.cos(phi)*math.cos(th),center[1]+scale[1]*math.sin(phi),center[2]+scale[2]*math.cos(phi)*math.sin(th))
                if deform: p=deform(p,j,i)
                variation=1+.025*math.sin(i*1.7+j*.6)
                c=tuple(min(1,k*variation) for k in col[:3])+(col[3],)
                self.add(p,c,bone,(i/sides,j/rings))
        top=(center[0],center[1]+scale[1],center[2])
        if deform:top=deform(top,rings,0)
        north=self.add(top,col,bone,(.5,1))
        for i in range(sides):
            self.f.append((south,start+(i+1)%sides,start+i))
            last=start+(rings-2)*sides
            self.f.append((last+i,last+(i+1)%sides,north))
        for j in range(rings-2):
            for i in range(sides):
                a=start+j*sides+i;b=start+j*sides+(i+1)%sides
                self.quad(a,b,b+sides,a+sides)
    def tube(self,points,radii,col,bone,sides=8,weight=None,ellipse=1):
        start=len(self.v)
        for j,p in enumerate(points):
            direction=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(j-1,0)])
            direction.normalize();axis=direction.cross(Vector((0,0,1)))
            if axis.length<.1: axis=direction.cross(Vector((0,1,0)))
            axis.normalize();other=direction.cross(axis).normalized()
            for i in range(sides):
                a=math.tau*i/sides
                co=Vector(p)+axis*(math.cos(a)*radii[j])+other*(math.sin(a)*radii[j]*ellipse)
                clr=col(j,i) if callable(col) else col
                self.add(co,clr,weight(j/ max(1,len(points)-1)) if weight else bone,(i/sides,j/max(1,len(points)-1)))
        for j in range(len(points)-1):
            for i in range(sides):
                a=start+j*sides+i;b=start+j*sides+(i+1)%sides
                self.quad(a,b,b+sides,a+sides)
        for i in range(1,sides-1):
            self.f.append((start,start+i+1,start+i))
            end=start+(len(points)-1)*sides;self.f.append((end,end+i,end+i+1))
    def loft(self,profiles,col,weight,sides=20,fold=0):
        # profiles: (height, width radius, depth radius, center x, center z)
        start=len(self.v)
        for j,(y,rx,rz,cx,cz) in enumerate(profiles):
            for i in range(sides):
                a=math.tau*i/sides
                wrinkle=1+fold*(math.sin(a*5+j*1.8)+.35*math.sin(a*9-j*.8))
                p=(cx+rx*math.cos(a)*wrinkle,y,cz+rz*math.sin(a)*wrinkle)
                self.add(p,col(j,i) if callable(col) else col,weight(y),(i/sides,j/max(1,len(profiles)-1)))
        for j in range(len(profiles)-1):
            for i in range(sides):
                a=start+j*sides+i;b=start+j*sides+(i+1)%sides
                self.quad(a,a+sides,b+sides,b)
        for i in range(1,sides-1):
            self.f.append((start,start+i,start+i+1))
            end=start+(len(profiles)-1)*sides;self.f.append((end,end+i+1,end+i))
    def object(self):
        mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(self.v,[],self.f);mesh.update()
        obj=bpy.data.objects.new(self.name,mesh);bpy.context.collection.objects.link(obj)
        attr=mesh.color_attributes.new(name='Surface',type='FLOAT_COLOR',domain='POINT')
        for i,c in enumerate(self.c): attr.data[i].color=c
        uv=mesh.uv_layers.new(name='UVMap')
        for loop in mesh.loops: uv.data[loop.index].uv=self.uv[loop.vertex_index]
        for poly in mesh.polygons: poly.use_smooth=True
        for bone in self.bones: obj.vertex_groups.new(name=bone['name'])
        for i,weights in enumerate(self.w):
            for n,w in weights: obj.vertex_groups[n].add([i],w,'REPLACE')
        return obj

SHELL=(.19,.062,.020,.63); EDGE=(.32,.12,.040,.52); BELLY=(.065,.028,.015,.28)
LIMB=(.12,.049,.022,.40); EYE=(.012,.009,.006,.78)

def roach(lod):
    m=Builder('Cockroach_Detail'+str(lod));m.bone('Root',(0,0,0));m.bone('Body',(0,0,0))
    sides=[24,16,10][lod];rings=[12,8,5][lod]
    # Broad, low abdomen, segmented flank. Wings overlap rather than making two round balls.
    m.ellipsoid((0,.245,-.16),(.35,.115,.69),BELLY,'Body',sides,rings)
    if lod<2:
        for j in range(8):
            z=.19-j*.115;w=.31*math.sqrt(max(.12,1-((z+.16)/.76)**2))
            pts=[(-w,.245,z),(-w*.99,.205,z-.01),(0,.153,z-.025),(w*.99,.205,z-.01),(w,.245,z)]
            m.tube(pts,[.013]*5,(.16,.061,.025,.35),'Body',5)
    m.ellipsoid((0,.325,.37),(.365,.105,.32),SHELL,'Body',sides,rings)
    # Shield perimeter, mostly concealing the head.
    if lod<2:
        pts=[(.35*math.cos(a),.327,.37+.29*math.sin(a)) for a in [i*math.tau/32 for i in range(33)]]
        m.tube(pts,[.008]*len(pts),EDGE,'Body',4)
    m.ellipsoid((0,.268,.64),(.182,.092,.18),LIMB,'Body',sides,rings)
    for s in (-1,1):
        m.ellipsoid((s*.148,.306,.683),(.047,.050,.061),EYE,'Body',12 if lod==0 else 8,6)
        m.tube([(s*.07,.22,.76),(s*.105,.202,.83),(s*.15,.21,.85)],[.022,.015,.004],LIMB,'Body',6)
        # Closed wing surface with center seam, convex hull, vein relief and tapered tip.
        n=[18,11,6][lod];nu=[12,8,4][lod];start=len(m.v)
        for layer in (0,1):
            for j in range(n+1):
                t=j/n;z=.37-1.18*t
                width=.32*(math.sin(math.pi*(.08+.86*t))**.58)
                for i in range(nu+1):
                    u=i/nu;x=s*(.006+width*u)
                    y=.352+.075*math.sin(math.pi*u)*math.sin(math.pi*t*.9)-.085*t
                    if lod==0:y+=.0024*math.sin(u*35+t*8)*math.sin(math.pi*u)
                    y-=layer*.018
                    shade=.83+.17*math.sin(math.pi*u)+.09*t
                    m.add((x,y,z),tuple(c*shade for c in SHELL[:3])+(SHELL[3],),'Body',(u,t))
        plane=(n+1)*(nu+1)
        for layer in (0,1):
            off=start+plane*layer
            for j in range(n):
                for i in range(nu):
                    a=off+j*(nu+1)+i;b=a+1;c=b+nu+1;d=a+nu+1
                    # Opposite side/layer winding; corrected globally after export as well.
                    if (s<0)==(layer==0): m.quad(a,b,c,d)
                    else:m.quad(d,c,b,a)
        for j in range(n):
            for i in (0,nu):
                a=start+j*(nu+1)+i;b=a+nu+1;m.quad(a,b,b+plane,a+plane)
        for j in (0,n):
            for i in range(nu):
                a=start+j*(nu+1)+i;m.quad(a,a+plane,a+1+plane,a+1)
        if lod<2:
            for vein in range(5 if lod==0 else 3):
                points=[]
                for j in range(10 if lod==0 else 6):
                    t=.10+j/(9 if lod==0 else 5)*.78;u=.15+vein*(.15 if lod==0 else .26)+t*.06
                    width=.32*(math.sin(math.pi*(.08+.86*t))**.58)
                    points.append((s*(.006+width*u),.355+.075*math.sin(math.pi*u)*math.sin(math.pi*t*.9)-.085*t,.37-1.18*t))
                m.tube(points,[.0032]*len(points),EDGE,'Body',4)
    for s in (-1,1):
        for row in range(3):
            tag=('L' if s<0 else 'R')+str(row)
            hip=(s*.25,.28,.32-row*.37);foot=(s*(.87 if row==1 else .75),.025,.60-row*.65)
            knee=tuple((a+b)*.5+c for a,b,c in zip(hip,foot,(s*.12,.18,0)))
            upper=m.bone('Upper_'+tag,hip,'Root',knee);lower=m.bone('Lower_'+tag,knee,upper,foot)
            m.tube([hip,lerp(hip,knee,.2),lerp(hip,knee,.75),knee],[.039,.047,.035,.029],LIMB,upper,8 if lod==0 else 6)
            m.tube([knee,lerp(knee,foot,.16),lerp(knee,foot,.80),foot],[.032,.029,.018,.009],LIMB,lower,8 if lod==0 else 5)
            m.ellipsoid(knee,(.037,.038,.037),EDGE,lower,8,5)
            toe=(foot[0]+s*.055,.016,foot[2]+(.13 if row==0 else -.10))
            m.tube([foot,lerp(foot,toe,.5),toe],[.013,.009,.003],LIMB,lower,5)
            if lod==0:
                for j in range(1,6):
                    p=lerp(knee,foot,j/7)
                    m.tube([p,(p[0]+s*.07,p[1]-.025,p[2]-.04)],[.009,.001],LIMB,lower,4)
        anchor=(s*.13,.31,.77);ant=m.bone('Antenna_'+('L' if s<0 else 'R'),anchor,'Body')
        points=[];count=[38,23,12][lod]
        for j in range(count):
            t=j/(count-1);points.append((anchor[0]+s*(.11*t+.20*t*t),anchor[1]+.33*math.sin(t*math.pi*.80),anchor[2]+1.12*t))
        def antcol(j,i):
            v=.80 if j%3==0 else 1
            return (LIMB[0]*v,LIMB[1]*v,LIMB[2]*v,.38)
        m.tube(points,[.014*(1-j/count)**1.2+.001 for j in range(count)],antcol,ant,6 if lod==0 else 4)
    return m

def create_material():
    mat=bpy.data.materials.new('VertexAlbedo_Roughness');mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    bsdf=nodes.new('ShaderNodeBsdfPrincipled');output=nodes.new('ShaderNodeOutputMaterial')
    links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
    vc=nodes.new('ShaderNodeVertexColor');vc.layer_name='Surface'
    links.new(vc.outputs['Color'],bsdf.inputs['Base Color'])
    invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
    links.new(vc.outputs['Alpha'],invert.inputs[1]);links.new(invert.outputs[0],bsdf.inputs['Roughness'])
    return mat

def armature(builder,name):
    data=bpy.data.armatures.new(name);obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for b in builder.bones:
        bone=data.edit_bones.new(b['name']);p=tuple(b['position'][k] for k in 'xyz');bone.head=bvec(p)
        bone.tail=bvec(b['tail'] if b['tail'] else (p[0],p[1]+.12,p[2]))
        if b['parent']:bone.parent=data.edit_bones[b['parent']]
    bpy.ops.object.mode_set(mode='OBJECT');obj.show_in_front=True;return obj

mat=create_material();report={};actors=[]
for actor,make in [('Cockroach',roach),('Resident',None)]:
    levels=[];base=None;objects=[]
    if actor=='Resident':
        bones,levels,objects,report[actor]=resident_model.build(mat);base=SimpleNamespace(bones=bones)
    for lod in (range(3) if make else ()):
        builder=make(lod);obj=builder.object();obj.data.materials.append(mat)
        # Recalculate face winding consistently in Blender, preserving the authored silhouette.
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
        mesh=obj.data;mesh.calc_loop_triangles();mesh.update()
        ids={b['name']:i for i,b in enumerate(builder.bones)}
        # (x,z,-y) is a proper rotation (determinant +1), NOT a reflection.
        # Keep triangle winding consistent with the transformed outward normals.
        indices=[v for t in mesh.loop_triangles for v in t.vertices]
        positions=[dict(zip('xyz',map(lambda x:round(x,7),uvec(v.co)))) for v in mesh.vertices]
        normals=[dict(zip('xyz',map(lambda x:round(x,7),uvec(v.normal)))) for v in mesh.vertices]
        colors=[dict(zip('rgba',c)) for c in builder.c]
        weights=[]
        for w in builder.w:
            w=[(n,v) for n,v in w if v>.0001];w=sorted(w,key=lambda pair:-pair[1])
            weights.append(dict(bone0=ids[w[0][0]],bone1=ids[w[1][0]] if len(w)>1 else 0,weight0=w[0][1],weight1=w[1][1] if len(w)>1 else 0))
        levels.append(dict(name=actor+'_Detail'+str(lod),positions=positions,normals=normals,colors=colors,uv=[dict(x=u,y=v) for u,v in builder.uv],triangles=indices,weights=weights))
        report.setdefault(actor,[]).append(dict(lod=lod,vertices=len(positions),triangles=len(indices)//3,bones=len(builder.bones),renderers=1,materials=1))
        if base is None:base=builder
        objects.append(obj)
    bpy.ops.object.select_all(action='DESELECT');rig=armature(base,actor+'_Rig')
    for i,obj in enumerate(objects):
        mod=obj.modifiers.new('Armature','ARMATURE');mod.object=rig;obj.parent=rig;obj.hide_render=i>0;obj.hide_set(i>0)
    payload=dict(version=1,actor=actor,bones=[{k:v for k,v in b.items() if k!='tail'} for b in base.bones],levels=levels)
    (OUT/(actor+'.json')).write_text(json.dumps(payload,separators=(',',':')),encoding='utf8')
    # Standard interchange file, kept outside Assets: the game uses the deterministic JSON import.
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
    for o in objects:o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(AUTHOR/(actor+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
    for i,o in enumerate(objects):o.hide_set(i>0)
    actors.append((rig,objects))

(AUTHOR/'model-budget.json').write_text(json.dumps(report,indent=2),encoding='utf8')
# Separate the two rigs in the editable authoring workspace after exports.
actors[0][0].location.x=-1.6;actors[1][0].location.x=1.0
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.threads_mode='FIXED';scene.render.threads=2
scene.world.color=(.18,.18,.18)
scene.render.resolution_x=1280;scene.render.resolution_y=900;scene.render.resolution_percentage=100
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(6,-9,5));camera=bpy.context.object;aim(camera,(0,0,1.8));scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=5.8
for pos,power,size in [((2,-4,7),1000,5),((-4,-2,3),600,4),((1,4,5),1000,3)]:
    bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size;aim(light,(0,0,2))
scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(AUTHOR/'GokiTakeover_Characters.blend'))
print('MODEL_BUDGET '+json.dumps(report))
if '--render-preview' in sys.argv:
    scene.render.filepath=str(AUTHOR/'model-preview.png');bpy.ops.render.render(write_still=True)
