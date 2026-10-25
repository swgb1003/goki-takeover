"""Realistic resident: sculpt-style anatomy built from overlapping primitives, fused by voxel
remesh and relaxed by smoothing, then decimated to three game LODs. Clothes, skin, hair, eyes
and slippers are separate shells, so hems and hairlines stay crisp like real layered clothing.
Colour, smoothness and ambient occlusion are baked into vertex colours (no texture memory).
Authoring coordinates are Unity x/right, y/up, z/front; bvec() converts to Blender.
"""
import bpy, math
from mathutils import Vector, Matrix, noise
from mathutils.bvhtree import BVHTree

def bvec(p): return Vector((p[0], -p[2], p[1]))
def uvec(p): return Vector((p[0], p[2], -p[1]))
def V(*a): return Vector(a)
def clamp(x,a=0.0,b=1.0): return max(a,min(b,x))
def smooth(a,b,x):
    t=clamp((x-a)/(b-a)) if b!=a else (1.0 if x>=a else 0.0)
    return t*t*(3-2*t)
def lerp(a,b,t): return a+(b-a)*t

# Adult proportions for a 4.1-unit person (≈1.75 m): head 1/7.6, shoulder 0.82h, hip 0.51h, knee 0.285h.
BONES=[
    ('Root','',V(0,0,0),V(0,.3,0)),
    ('Pelvis','Root',V(0,2.0,0),V(0,2.42,0)),
    ('Spine','Pelvis',V(0,2.42,0),V(0,2.92,0)),
    ('Chest','Spine',V(0,2.92,0),V(0,3.42,-.03)),
    ('Neck','Chest',V(0,3.42,-.03),V(0,3.64,-.03)),
    ('Head','Neck',V(0,3.64,-.03),V(0,4.1,-.03)),
]
def side_bones(s):
    t='L' if s<0 else 'R'
    return [
        ('Thigh_'+t,'Pelvis',V(s*.215,2.06,0),V(s*.215,1.17,.03)),
        ('Shin_'+t,'Thigh_'+t,V(s*.215,1.17,.03),V(s*.22,.17,-.03)),
        ('Foot_'+t,'Shin_'+t,V(s*.22,.17,-.03),V(s*.23,.06,.40)),
        ('Arm_'+t,'Chest',V(s*.42,3.30,-.02),V(s*.58,2.60,-.06)),
        ('Forearm_'+t,'Arm_'+t,V(s*.58,2.60,-.06),V(s*.62,2.02,.02)),
        ('Hand_'+t,'Forearm_'+t,V(s*.62,2.02,.02),V(s*.62,1.62,.04)),
    ]
BONES+=side_bones(-1)+side_bones(1)
# Held slipper: its heel is pressed into the right palm, toe hangs down (-Y), tread faces away from the palm (-X).
GRIP=V(.62-.061,1.85,.02)  # palm surface is 0.036 medial of the wrist; slab is 0.05 thick
BONES.append(('Weapon','Hand_R',GRIP,GRIP+V(0,-.6,0)))
BONE=dict((b[0],b) for b in BONES)
def P(name): return BONE[name][2]

SKIN=(.50,.31,.23,.34); LIP=(.40,.17,.15,.45); SHIRT=(.068,.165,.155,.12); PANTS=(.046,.051,.062,.10)
HAIR=(.032,.023,.018,.30); SCLERA=(.74,.71,.67,.80); IRIS=(.11,.065,.035,.85); PUPIL=(.008,.007,.006,.9)
SLIPPER_UP=(.030,.050,.090,.14); SLIPPER_SOLE=(.13,.12,.11,.18); WEAPON_UP=(.085,.040,.024,.14); WEAPON_SOLE=(.36,.30,.22,.22)
BUTTON=(.58,.56,.50,.55); NAIL=(.62,.43,.37,.60)

class Shape:
    """A watertight primitive soup in Unity coordinates; voxel remesh unions it."""
    def __init__(self): self.v=[]; self.f=[]
    def add(self,verts,faces):
        o=len(self.v); self.v+=verts; self.f+=[tuple(i+o for i in face) for face in faces]
    def ellipsoid(self,c,r,rot=None,seg=28,rings=18):
        rot=rot or Matrix.Identity(3); verts=[c+rot@V(0,-r[1],0)]
        for j in range(1,rings):
            phi=-math.pi/2+math.pi*j/rings
            for i in range(seg):
                th=math.tau*i/seg
                verts.append(c+rot@V(r[0]*math.cos(phi)*math.cos(th),r[1]*math.sin(phi),r[2]*math.cos(phi)*math.sin(th)))
        verts.append(c+rot@V(0,r[1],0)); top=len(verts)-1; faces=[]
        for i in range(seg):
            faces.append((0,1+(i+1)%seg,1+i))
            last=1+(rings-2)*seg; faces.append((last+i,last+(i+1)%seg,top))
        for j in range(rings-2):
            for i in range(seg):
                a=1+j*seg+i;b=1+j*seg+(i+1)%seg;faces.append((a,b,b+seg,a+seg))
        self.add(verts,faces)
    def sweep(self,points,rx,ry=None,front=V(0,0,1),power=2.0,seg=24,caps=True):
        """Tube through points; cross-section superellipse rx (side) x ry (front axis)."""
        ry=ry or rx; verts=[]; n=len(points)
        for j,p in enumerate(points):
            d=(points[min(j+1,n-1)]-points[max(j-1,0)]).normalized()
            f=(front-d*front.dot(d)); f=f.normalized() if f.length>1e-5 else d.orthogonal().normalized()
            sx=d.cross(f).normalized()
            for i in range(seg):
                a=math.tau*i/seg; ca=math.cos(a); sa=math.sin(a)
                ex=math.copysign(abs(ca)**(2/power),ca); ey=math.copysign(abs(sa)**(2/power),sa)
                verts.append(p+sx*(ex*rx[j])+f*(ey*ry[j]))
        faces=[]
        for j in range(n-1):
            for i in range(seg):
                a=j*seg+i;b=j*seg+(i+1)%seg;faces.append((a,a+seg,b+seg,b))
        if caps:
            verts.append(points[0]); verts.append(points[-1]); c0=len(verts)-2; c1=len(verts)-1
            for i in range(seg):
                faces.append((c0,(i+1)%seg,i)); e=(n-1)*seg; faces.append((c1,e+i,e+(i+1)%seg))
        self.add(verts,faces)
    def capsule(self,a,b,ra,rb,seg=20):
        self.sweep([a,b],[ra,rb],seg=seg); self.ellipsoid(a,V(ra,ra,ra),seg=seg,rings=12); self.ellipsoid(b,V(rb,rb,rb),seg=seg,rings=12)
    def chain(self,pts,radii,seg=16):
        for i in range(len(pts)-1): self.capsule(pts[i],pts[i+1],radii[i],radii[i+1],seg)

def rot_xyz(x=0,y=0,z=0):
    return (Matrix.Rotation(math.radians(y),3,'Y')@Matrix.Rotation(math.radians(x),3,'X')@Matrix.Rotation(math.radians(z),3,'Z'))

def to_object(name,shape):
    mesh=bpy.data.meshes.new(name); mesh.from_pydata([bvec(p) for p in shape.v],[],shape.f); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj); return obj

def activate(obj):
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=obj; obj.select_set(True)

def apply_mod(obj,kind,**kw):
    activate(obj); m=obj.modifiers.new(kind,kind)
    for k,v in kw.items(): setattr(m,k,v)
    bpy.ops.object.modifier_apply(modifier=m.name)

def displace_along_normals(obj,displace):
    # Read every normal before moving anything: touching co marks normals dirty and recomputes them.
    mesh=obj.data; frame=[(uvec(v.co),uvec(v.normal)) for v in mesh.vertices]
    for v,(p,n) in zip(mesh.vertices,frame): v.co=bvec(p+n*displace(p,n))
    mesh.update()

def fuse(obj,voxel,smooth_iter,displace=None):
    apply_mod(obj,'REMESH',mode='VOXEL',voxel_size=voxel,adaptivity=0.0)
    if smooth_iter: apply_mod(obj,'SMOOTH',factor=.5,iterations=smooth_iter)
    if displace:
        displace_along_normals(obj,displace)
        apply_mod(obj,'SMOOTH',factor=.35,iterations=1)
    return obj

def decimated(obj,tris,name):
    copy=obj.copy(); copy.data=obj.data.copy(); copy.name=name; bpy.context.collection.objects.link(copy)
    current=sum(len(p.vertices)-2 for p in copy.data.polygons)
    if current>tris: apply_mod(copy,'DECIMATE',decimate_type='COLLAPSE',ratio=tris/current,use_collapse_triangulate=True)
    apply_mod(copy,'TRIANGULATE')
    activate(copy); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    for poly in copy.data.polygons: poly.use_smooth=True
    return copy

# ---------------------------------------------------------------- anatomy
# Head: a smooth lofted skull/face profile, then sculpt-style Gaussian features displaced along
# the surface normal. Gaussians blend without the creases that primitive unions produce.
HEAD_RINGS=[(4.098,.03,.02,-.07),(4.085,.075,.05,-.13),(4.05,.125,.095,-.19),(4.00,.155,.13,-.23),(3.95,.170,.152,-.25),
            (3.90,.177,.163,-.26),(3.85,.179,.168,-.26),(3.80,.177,.170,-.25),(3.76,.172,.176,-.232),(3.72,.160,.180,-.205),
            (3.68,.147,.178,-.176),(3.64,.133,.168,-.148),(3.605,.115,.155,-.115),(3.58,.09,.145,-.078),(3.56,.06,.125,-.042),(3.548,.025,.095,-.012)]
EYE_C=V(.072,3.815,.097); EYE_R=.030

def gauss(p,c,s,amp):
    """Front-projected (x,y) Gaussian; the z of c is ignored, so features sit on the surface."""
    d=p-c; return amp*math.exp(-((d.x/s[0])**2+(d.y/s[1])**2))
def gauss3(p,c,s,amp):
    d=p-c; return amp*math.exp(-((d.x/s[0])**2+(d.y/s[1])**2+(d.z/s[2])**2))
def seg_gauss(p,a,b,s0,s1,amp0,amp1,power=1.0):
    a2=V(a.x,a.y,0);ab=V(b.x,b.y,0)-a2;q=V(p.x,p.y,0);t=clamp((q-a2).dot(ab)/ab.length_squared);d=(a2+ab*t)-q;s=lerp(s0,s1,t)
    return lerp(amp0,amp1,t**power)*math.exp(-(d.x/s[0])**2-(d.y/s[1])**2)

def face_features(p):
    f=seg_gauss(p,V(0,3.86,0),V(0,3.752,0),V(.011,.02,0),V(.021,.02,0),.006,.046,1.5)  # nose
    f+=gauss(p,V(0,3.748,0),(.023,.017),.014)+gauss(p,V(0,3.862,0),(.03,.016),-.006)   # tip, nasion
    f+=gauss(p,V(0,3.735,0),(.016,.008),-.010)                                           # columella underside
    f+=gauss(p,V(0,3.684,0),(.036,.009),.011)+gauss(p,V(0,3.70,0),(.0065,.011),-.0035) # upper lip, philtrum
    f+=gauss(p,V(0,3.669,0),(.041,.0032),-.0065)+gauss(p,V(0,3.656,0),(.031,.009),.011)  # mouth line, lower lip
    f+=gauss(p,V(0,3.634,0),(.034,.0065),-.006)+gauss(p,V(0,3.594,0),(.035,.022),.013)   # labiomental groove, chin
    for x in (-1,1):
        f+=gauss(p,V(x*EYE_C.x,EYE_C.y,0),(.06,.028),-.030)              # orbit
        f+=gauss(p,V(x*EYE_C.x,EYE_C.y+.017,0),(.04,.009),.019)          # upper lid
        f+=gauss(p,V(x*(EYE_C.x+.003),EYE_C.y+.034,0),(.04,.005),-.004)  # lid crease
        f+=gauss(p,V(x*EYE_C.x,EYE_C.y-.018,0),(.036,.007),.012)         # lower lid
        f+=gauss(p,V(x*.072,3.874,0),(.055,.012),.010)                    # brow ridge
        f+=gauss(p,V(x*.108,3.772,0),(.04,.026),.011)                     # zygoma
        f+=gauss(p,V(x*.112,3.70,0),(.035,.03),-.006)                     # buccal hollow
        f+=gauss(p,V(x*.027,3.747,0),(.015,.012),.017)                    # nostril wing
        f+=gauss(p,V(x*.013,3.736,0),(.007,.004),-.006)                   # nostril
        f+=seg_gauss(p,V(x*.042,3.742,0),V(x*.068,3.668,0),V(.006,.008,0),V(.006,.008,0),-.004,-.003)  # nasolabial
    f*=smooth(.0,.08,p.z)
    for x in (-1,1):
        f+=gauss3(p,V(x*.168,3.86,.06),(.03,.04,.05),-.006)               # temple
        f+=gauss3(p,V(x*.128,3.63,-.05),(.025,.04,.05),.006)              # jaw angle
    return f

def head_object():
    def ring(y0,y1,t): return y0+(y1-y0)*t
    rows=[]
    for (a,b) in zip(HEAD_RINGS,HEAD_RINGS[1:]):
        for k in range(6): rows.append(tuple(lerp(a[i],b[i],k/6) for i in range(4)))
    rows.append(HEAD_RINGS[-1]); seg=96; pts=[]
    for y,hw,zf,zb in rows:
        for i in range(seg):
            a=math.tau*i/seg; c=math.cos(a); s=math.sin(a); n=2.35 if s>0 else 2.0
            pts.append(V(math.copysign(abs(c)**(2/n),c)*hw,y,math.copysign(abs(s)**(2/n),s)*(zf if s>0 else -zb)-.02))
    faces=[]
    for j in range(len(rows)-1):
        for i in range(seg):
            a=j*seg+i;b=j*seg+(i+1)%seg;faces.append((a,b,b+seg,a+seg))
    pts.append(V(0,HEAD_RINGS[0][0]+.004,-.04)); pts.append(V(0,HEAD_RINGS[-1][0]-.004,.03)); top=len(pts)-2; bot=len(pts)-1
    last=(len(rows)-1)*seg
    for i in range(seg): faces.append((top,(i+1)%seg,i)); faces.append((bot,last+i,last+(i+1)%seg))
    s=Shape(); s.v=pts; s.f=faces; obj=to_object('Resident_HeadSculpt',s)
    activate(obj); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    apply_mod(obj,'SUBSURF',levels=1,render_levels=1)
    displace_along_normals(obj,lambda p,n:face_features(p))
    extra=Shape()
    for x in (-1,1):
        r=rot_xyz(x=-12,y=x*18)
        extra.ellipsoid(V(x*.174,3.80,-.045),V(.022,.064,.040),r)
        extra.ellipsoid(V(x*.180,3.745,-.04),V(.016,.022,.02),r)
        rim=[V(x*.176,3.745,-.03),V(x*.186,3.80,-.078),V(x*.184,3.858,-.06),V(x*.178,3.866,-.015)]
        extra.chain(rim,[.008,.011,.011,.008],10)
    extra.sweep([V(0,3.36,-.04),V(0,3.52,-.04),V(0,3.66,-.06)],[.135,.128,.12],[.13,.122,.115],seg=32)
    extra.ellipsoid(V(0,3.535,.08),V(.019,.028,.02))
    add=to_object('Resident_HeadExtras',extra); activate(obj); add.select_set(True); bpy.ops.object.join()
    return fuse(bpy.context.object,.0068,1)

def eye_shape(x):
    s=Shape(); s.ellipsoid(V(x,EYE_C.y,EYE_C.z),V(EYE_R,EYE_R,EYE_R),seg=24,rings=16); return s

def shirt_shape():
    s=Shape()
    # (y, half width, half depth, centre z); boxy superellipse cross-section reads as woven cloth.
    rings=[(1.98,.405,.265,0),(2.10,.405,.262,0),(2.30,.385,.258,.005),(2.50,.375,.262,.012),(2.70,.385,.27,.02),(2.90,.395,.278,.025),
           (3.05,.40,.27,.02),(3.18,.405,.245,.005),(3.28,.395,.21,-.01),(3.36,.30,.17,-.02),(3.43,.17,.145,-.035)]
    pts=[V(0,y,cz) for y,_,_,cz in rings]
    s.sweep(pts,[w for _,w,_,_ in rings],[d for _,_,d,_ in rings],V(0,0,1),2.6,40)
    for x in (-1,1):
        sh=P('Arm_R');sh=V(x*sh.x,sh.y,sh.z);el=P('Forearm_R');el=V(x*el.x,el.y,el.z)
        d=(el-sh).normalized()
        s.ellipsoid(sh+V(x*.025,-.01,0),V(.135,.105,.13))                              # deltoid under the cloth
        s.sweep([sh+d*.02,sh+d*.17,sh+d*.38],[.145,.145,.138],[.145,.143,.136],V(0,0,1),2.2,28)  # short sleeve
        s.ellipsoid(V(x*.10,3.44,.07),V(.075,.016,.055),rot_xyz(x=-25,y=x*30))           # collar points
    collar=[V(math.sin(a)*.155,3.43+.012*math.cos(a),-.04+math.cos(a)*.15) for a in [i*math.tau/28 for i in range(29)]]
    s.chain(collar,[.02]*29,10)
    s.sweep([V(-.20,2.72,.278),V(-.20,2.93,.276)],[.085,.085],[.012,.012],V(0,0,1),4,16)    # chest pocket
    return s

def shirt_detail(p,n):
    fold=.0045*noise.noise(V(p.x*5,p.y*14,p.z*5))+.0022*noise.noise(p*24)
    waist=smooth(2.45,2.1,p.y)*.004*math.sin(p.y*58+p.x*9)          # draped hem ripples
    sleeve=smooth(.40,.48,abs(p.x))*.004*math.sin(p.y*44+p.z*20)
    return fold+waist+sleeve

def arm_shape(x):
    t='L' if x<0 else 'R'; s=Shape(); sh=P('Arm_'+t); el=P('Forearm_'+t); wr=P('Hand_'+t)
    def on(a,b,t): return a.lerp(b,t)
    d=hand_frame(x>0)[1]
    pts=[on(sh,el,.07),on(sh,el,.25),on(sh,el,.55),on(sh,el,.88),el,on(el,wr,.22),on(el,wr,.55),on(el,wr,.86),wr,wr+d*.035]
    side=[.128,.116,.11,.094,.090,.098,.083,.066,.058,.062]
    front=[.13,.118,.116,.098,.096,.094,.074,.052,.043,.04]
    s.sweep(pts,side,front,V(0,0,1),2.0,22)
    s.ellipsoid(el+V(0,.0,-.07),V(.045,.05,.035))                 # olecranon
    s.ellipsoid(on(el,wr,.18)+V(x*.035,0,.02),V(.06,.13,.055))  # brachioradialis
    s.ellipsoid(on(sh,el,.5)+V(0,0,.04),V(.08,.17,.07))          # biceps
    return s

def hand_frame(right):
    if right:  # fingers down, palm medial and pressed on the slipper heel
        return P('Hand_R'),V(0,-1,0),V(-1,0,0),V(0,0,1)
    w=P('Hand_L'); return w,V(0,-.99,.12).normalized(),V(1,0,0),V(0,.12,.99).normalized()

def finger_points(base,d,p,lengths,angles):
    pts=[base]; direction=d.copy(); axis=d.cross(p).normalized()
    for L,a in zip(lengths,angles):
        direction=(Matrix.Rotation(math.radians(a),3,axis)@direction).normalized()
        pts.append(pts[-1]+direction*L)
    return pts

def hand_shape(right):
    s=Shape(); w,d,p,t=hand_frame(right)
    # Palm: wrist to knuckles, thicker under the thumb (thenar) and the outer edge (hypothenar).
    pal=[w+d*.0,w+d*.08,w+d*.17,w+d*.235]
    s.sweep(pal,[.072,.092,.102,.100],[.036,.039,.036,.032],p,3.0,24)
    s.ellipsoid(w+d*.075+t*.05+p*.025,V(.045,.06,.035),None,16,12)
    s.ellipsoid(w+d*.11-t*.065+p*.015,V(.035,.075,.03),None,16,12)
    curl=[(22,28,18),(24,30,18),(26,30,18),(28,30,16)] if right else [(14,20,14),(16,22,14),(18,24,16),(22,26,16)]
    specs=[(.072,(.112,.066,.05),.0225),(.024,(.122,.072,.055),.0235),(-.024,(.114,.069,.05),.022),(-.07,(.09,.054,.045),.019)]
    tips=[]
    for (off,lens,rad),ang in zip(specs,curl):
        base=w+d*.225+t*off+p*(-.004)
        pts=finger_points(base,d,p,lens,ang)
        s.chain(pts,[rad,rad*.92,rad*.84,rad*.74],14); tips.append((pts[-1],pts[-2]))
        s.ellipsoid(base+d*.0-p*.012,V(rad*1.05,rad*1.05,rad*1.0),None,12,8)   # knuckle
    tb=w+d*.055+t*.075+p*.03
    tdir=(d*.55+t*.6+p*.55).normalized() if not right else (d*.75+t*.35+p*.35).normalized()
    tp=[tb,tb+tdir*.09]; td=(tdir*.8+(p*.4 if right else -t*.1)).normalized(); tp.append(tp[-1]+td*.068); tp.append(tp[-1]+(td*.85+p*.3).normalized()*.05)
    s.chain(tp,[.034,.028,.024,.02],14); tips.append((tp[-1],tp[-2]))
    return s,tips

def pants_shape():
    s=Shape()
    s.sweep([V(0,1.86,0),V(0,2.0,0),V(0,2.14,.005),V(0,2.24,.01)],[.36,.39,.392,.385],[.22,.245,.248,.242],V(0,0,1),2.4,32)
    for x in (-1,1):
        L=[(2.02,.205,.225,.215,0),(1.82,.196,.205,.218,.005),(1.58,.178,.186,.218,.012),(1.36,.158,.166,.216,.02),
           (1.18,.15,.158,.215,.03),(1.0,.143,.148,.217,.012),(.8,.136,.137,.219,-.01),(.58,.118,.12,.22,-.02),(.38,.104,.104,.22,-.03),(.27,.102,.104,.221,-.03)]
        s.sweep([V(x*cx,y,cz) for y,_,_,cx,cz in L],[a for _,a,_,_,_ in L],[b for _,_,b,_,_ in L],V(0,0,1),2.1,28)
    return s

def pants_detail(p,n):
    knee=math.exp(-((p.y-1.15)/.09)**2)*.006*math.sin(p.y*70)*(1 if n.z<0 else .4)
    hem=smooth(.62,.3,p.y)*.006*math.sin(p.y*52+p.z*18)
    return .004*noise.noise(V(p.x*4,p.y*10,p.z*4))+knee+hem

def foot_shape(x):
    s=Shape(); a=V(x*.22,.17,-.03); sx=1 if x>0 else -1
    s.capsule(V(x*.22,.42,-.035),V(x*.22,.17,-.035),.066,.064,18)
    s.ellipsoid(V(x*.22,.115,-.11),V(.064,.066,.08))
    s.sweep([V(x*.22,.12,-.08),V(x*.22+sx*.008,.11,.08),V(x*.22+sx*.018,.088,.26),V(x*.22+sx*.02,.075,.33)],[.07,.08,.098,.094],[.06,.058,.042,.033],V(0,1,0),2.4,20)
    for i,(tx,tz,r) in enumerate([(-.06,.395,.024),(-.025,.40,.019),(.01,.392,.018),(.04,.38,.017),(.066,.36,.016)]):
        s.ellipsoid(V(x*.22+sx*tx*(-1),.068,tz),V(r,r*.85,r*1.2),None,12,8)
    return s

def slipper_shape(origin,length_dir,width_dir,up_dir,length,width,sole,dome=(1.25,.29,.69,.49,.75)):
    """Foot-shaped sole slab plus a domed toe cover; built in slipper space."""
    s=Shape(); pts=[];rx=[];ry=[]
    outline=[(0,.70),(.08,.86),(.25,.9),(.45,.95),(.62,1.0),(.8,.96),(.92,.82),(1.0,.45)]
    for t,wf in outline:
        pts.append(origin+length_dir*(t*length)+up_dir*(sole*.5)); rx.append(width*.5*wf); ry.append(sole*.5)
    s.sweep(pts,rx,ry,up_dir,4.0,28)
    height,span,centre,wide,lift=dome
    c=origin+length_dir*(length*centre)+up_dir*(sole*lift)
    m=Matrix((width_dir,up_dir,length_dir)).transposed()
    s.ellipsoid(c,V(width*wide,sole*height,length*span),m,28,16)
    return s

def worn_slipper(x):
    sx=1 if x>0 else -1
    return slipper_shape(V(x*.22+sx*.005,0,-.235),V(0,0,1),V(1,0,0),V(0,1,0),.68,.25,.05,(1.7,.30,.70,.50,1.0))

def held_slipper():
    # Origin is the tread face at the heel; top/upper faces the palm (+X), toe hangs down (-Y).
    return slipper_shape(GRIP+V(-.025,.10,0),V(0,-1,0),V(0,0,1),V(1,0,0),.78,.29,.05,(1.9,.29,.70,.5,.75))

def hair_object(head,shape_obj):
    head_bvh=BVHTree.FromObject(head,bpy.context.evaluated_depsgraph_get())
    c=V(0,3.86,-.02); seg=72; rings=48; verts=[]
    for j in range(rings+1):
        phi=-math.pi/2+math.pi*j/rings
        for i in range(seg):
            th=math.tau*i/seg; d=V(math.cos(phi)*math.sin(th),math.sin(phi),math.cos(phi)*math.cos(th))
            hit=head_bvh.ray_cast(bvec(c+d*.6),bvec(-d)-bvec(V(0,0,0)),.8)
            p=uvec(hit[0]) if hit[0] else c+d*.18
            front=d.z; side=abs(d.x)
            line=lerp(3.70,3.948,smooth(-.45,.62,front))-(.075*smooth(.55,.9,side)*smooth(-.1,.5,front))
            line+=.022*smooth(.15,.45,side)*smooth(.55,.85,front)-.006*math.cos(th*9)*smooth(.6,.9,front)
            ear=math.exp(-((d.z+.05)/.22)**2)*smooth(.75,.95,side)*.08
            line+=ear
            depth=.03*smooth(line,line+.08,p.y)+.006*smooth(line-.01,line+.02,p.y)
            clump=.006*noise.noise(V(d.x*9,d.y*5,d.z*9))+.003*math.sin(th*38+phi*9)
            off=depth+clump*smooth(line,line+.05,p.y) if p.y>line else -.03
            # Under the jaw/neck the surface is concave; keep the unused shell well inside the skull.
            verts.append(p+d*off if d.y>-.3 or p.y>line else c+d*.06)
    faces=[]
    for j in range(rings):
        for i in range(seg):
            a=j*seg+i;b=j*seg+(i+1)%seg;faces.append((a,b,b+seg,a+seg))
    s=Shape(); s.v=verts; s.f=faces
    # Close poles with small fans so the shell is watertight for remesh.
    s.v.append(c+V(0,-.3,0)); s.v.append(c+V(0,.25,0)); bot=len(s.v)-2; top=len(s.v)-1
    for i in range(seg):
        s.f.append((bot,(i+1)%seg,i)); e=rings*seg; s.f.append((top,e+i,e+(i+1)%seg))
    return fuse(to_object('Resident_Hair',s),.0095,3)

# ---------------------------------------------------------------- colour / weights
def skin_color(p,n):
    r,g,b,a=SKIN
    blush=math.exp(-((p.x**2-.0098)**2)/.0003-((p.y-3.76)/.05)**2)*.08
    nose=math.exp(-(p.x**2+(p.y-3.745)**2+(p.z-.23)**2)/.0012)*.07
    ear=smooth(.15,.17,abs(p.x))*smooth(3.70,3.75,p.y)*.06
    k=1+.035*noise.noise(p*30)
    r=(r+blush+nose+ear)*k; g=(g+.2*(blush+nose))*k*(1-.5*(blush+nose+ear)); b=b*k*(1-.6*(blush+nose+ear))
    if 3.52<p.y<3.78 and p.z>.02:
        beard=smooth(3.73,3.69,p.y)*smooth(.02,.09,p.z)*(1-smooth(.05,.09,abs(p.x)-.06))*.13
        r*=1-beard; g*=1-beard; b*=1-beard*.6
    if p.z>.15 and abs(p.x)<.05 and 3.648<p.y<3.69: r,g,b,a=lerp(r,LIP[0],.85),lerp(g,LIP[1],.85),lerp(b,LIP[2],.85),LIP[3]
    if 3.664<p.y<3.672 and abs(p.x)<.038 and p.z>.17: r,g,b=r*.55,g*.45,b*.45
    if abs(abs(p.x)-.026)<.012 and 3.733<p.y<3.745 and p.z>.19 and n.y<-.2: r,g,b=r*.35,g*.3,b*.3
    for x in (-1,1):
        u=(p.x-x*.072)/.052; arch=3.872+.010*(1-u*u)-.004*u*x
        if abs(u)<1 and abs(p.y-arch)<.011*(1-.45*abs(u)) and p.z>.1:
            r,g,b,a=HAIR[0]*1.3,HAIR[1]*1.3,HAIR[2]*1.3,.25
    return (r,g,b,a)

def eye_color(p,n,x):
    d=(p-V(x,EYE_C.y,EYE_C.z)).normalized(); ang=math.degrees(math.acos(clamp(d.dot(V(-x*.25,0,1).normalized()),-1,1)))
    if ang<10: return PUPIL
    if ang<25:
        k=.8+.4*noise.noise(d*40); return (IRIS[0]*k,IRIS[1]*k,IRIS[2]*k,IRIS[3])
    vein=.03*smooth(55,85,ang); return (SCLERA[0],SCLERA[1]-vein,SCLERA[2]-vein,SCLERA[3])

def fabric(base,p,amount=.07,scale=40):
    k=1+amount*noise.noise(p*scale)+.03*math.sin((p.x+p.y*1.7)*260)
    return (base[0]*k,base[1]*k,base[2]*k,base[3])

def shirt_color(p,n):
    # Large-scale tone only: decimated cloth triangles are too coarse for printed buttons.
    k=1-.08*smooth(3.25,3.45,p.y)*smooth(.12,.2,abs(p.x))
    c=fabric(SHIRT,p); return (c[0]*k,c[1]*k,c[2]*k,c[3])

def hair_color(p,n):
    k=1+.25*noise.noise(V(p.x*60,p.y*25,p.z*60)); return (HAIR[0]*k,HAIR[1]*k,HAIR[2]*k,HAIR[3])

def slipper_color(up,sole,height):
    def f(p,n):
        h=height(p)
        return fabric(sole if h<.045 else up,p,.08,55)
    return f

def W(*pairs): return [(b,w) for b,w in pairs if w>1e-4]

def head_weights(p): t=smooth(3.50,3.62,p.y); return W(('Head',t),('Neck',1-t)) if p.y>3.45 else W(('Neck',smooth(3.36,3.47,p.y)),('Chest',1-smooth(3.36,3.47,p.y)))

def torso_weights(p):
    a=smooth(2.18,2.42,p.y); b=smooth(2.62,2.86,p.y)
    return [('Pelvis',(1-a)),('Spine',a*(1-b)),('Chest',b)]

SHIRT_HW=[(1.98,.405),(2.10,.405),(2.30,.385),(2.50,.375),(2.70,.385),(2.90,.395),(3.05,.40),(3.18,.405),(3.28,.395),(3.36,.30),(3.43,.17),(3.6,.1)]
def half_width(y):
    for (y0,w0),(y1,w1) in zip(SHIRT_HW,SHIRT_HW[1:]):
        if y<=y1: return lerp(w0,w1,clamp((y-y0)/(y1-y0)))
    return SHIRT_HW[-1][1]

def shirt_weights(p):
    side='L' if p.x<0 else 'R'
    # Only the sleeve beyond the torso side follows the arm; the side/back panel stays with the chest.
    arm=smooth(half_width(p.y)-.005,half_width(p.y)+.055,abs(p.x))*(1-smooth(3.36,3.46,p.y))
    if p.y>3.24: arm*=smooth(.28,.40,abs(p.x))
    return [(b,w*(1-arm)) for b,w in torso_weights(p)]+[('Arm_'+side,arm)]

def seg_dist(p,a,b):
    ab=b-a; t=clamp((p-a).dot(ab)/ab.length_squared); return (a+ab*t-p).length,t

def arm_weights(p):
    side='L' if p.x<0 else 'R'; sh=P('Arm_'+side); el=P('Forearm_'+side); wr=P('Hand_'+side)
    _,t=seg_dist(p,sh,wr); split=(el-sh).length/((el-sh).length+(wr-el).length)
    e=smooth(split-.07,split+.06,t); h=smooth(.965,1.0,t)
    return W(('Arm_'+side,1-e),('Forearm_'+side,e*(1-h)),('Hand_'+side,e*h))

def hand_weights(right):
    w,d,_,_=hand_frame(right); side='R' if right else 'L'
    def f(p):
        k=smooth(-.035,.04,(p-w).dot(d)); return W(('Hand_'+side,k),('Forearm_'+side,1-k))
    return f

def pants_weights(p):
    side='L' if p.x<0 else 'R'
    pel=smooth(1.84,2.12,p.y)*(.6+.4*smooth(.22,.0,abs(p.x)))
    pel=max(pel,smooth(1.97,2.12,p.y))
    knee=smooth(1.08,1.27,p.y)
    return W(('Pelvis',pel),('Thigh_'+side,(1-pel)*knee),('Shin_'+side,(1-pel)*(1-knee)))

def foot_weights(p):
    side='L' if p.x<0 else 'R'; k=smooth(.22,.36,p.y); return W(('Shin_'+side,k),('Foot_'+side,1-k))

def const(bone): return lambda p:[(bone,1.0)]

def top2(weights,ids):
    acc={}
    for b,w in weights: acc[b]=acc.get(b,0)+w
    pairs=sorted([(w,b) for b,w in acc.items() if w>1e-4],reverse=True)[:2] or [(1.0,'Root')]
    s=sum(w for w,_ in pairs)
    out=dict(bone0=ids[pairs[0][1]],weight0=pairs[0][0]/s,bone1=0,weight1=0.0)
    if len(pairs)>1: out.update(bone1=ids[pairs[1][1]],weight1=pairs[1][0]/s)
    out['weight0']=1-out['weight1']; return out

# ---------------------------------------------------------------- build
def arm_hand_weights(right):
    w,d,_,_=hand_frame(right); hand=hand_weights(right)
    return lambda p: hand(p) if (p-w).dot(d)>-.015 else arm_weights(p)

def build_parts(only=None):
    parts=[]  # (object, budget per LOD, colour fn(p,n), weight fn(p), ao strength)
    want=lambda k: not only or k in only
    head=head_object()
    if want('head'): parts.append((head,(2700,1250,520),skin_color,head_weights,1.0))
    if want('hair'): parts.append((hair_object(head,None),(950,430,190),hair_color,const('Head'),.8))
    if want('head'):
        for x in (-EYE_C.x,EYE_C.x):
            eye=to_object('Resident_Eye',eye_shape(x)); parts.append((eye,(260,110,40),lambda p,n,x=x:eye_color(p,n,x),const('Head'),.9))
    else: bpy.data.objects.remove(head,do_unlink=True)
    if want('shirt'):
        shirt=fuse(to_object('Resident_Shirt',shirt_shape()),.0105,3,shirt_detail)
        parts.append((shirt,(2150,1100,480),shirt_color,shirt_weights,1.0))
    for x in (-1,1):
        if want('arm'):
            hs,tips=hand_shape(x>0); limb=arm_shape(x); limb.add(hs.v,hs.f)
            arm=fuse(to_object('Resident_ArmHand',limb),.0056,2)
            def hcol(p,n,tips=tips):
                for tip,prev in tips:
                    d=(tip-prev).normalized()
                    if (p-tip).length<.024 and n.dot(d)>-.2: return NAIL
                return skin_color(p,n)
            parts.append((arm,(1120,540,210),hcol,arm_hand_weights(x>0),1.0))
        if want('feet'):
            foot=fuse(to_object('Resident_Foot',foot_shape(x)),.0085,3)
            parts.append((foot,(260,120,50),skin_color,foot_weights,1.0))
            ws=fuse(to_object('Resident_HouseSlipper',worn_slipper(x)),.0085,2)
            parts.append((ws,(320,150,60),slipper_color(SLIPPER_UP,SLIPPER_SOLE,lambda p:p.y),const('Foot_'+('L' if x<0 else 'R')),.9))
    if want('pants'):
        pants=fuse(to_object('Resident_Trousers',pants_shape()),.0115,3,pants_detail)
        parts.append((pants,(1550,780,340),lambda p,n:fabric(PANTS,p,.08,32),pants_weights,1.0))
    if want('weapon'):
        weapon=fuse(to_object('Resident_HeldSlipper',held_slipper()),.0075,2)
        parts.append((weapon,(480,240,90),slipper_color(WEAPON_UP,WEAPON_SOLE,lambda p:p.x-(GRIP.x-.025)),const('Weapon'),.7))
    return parts

def bake_ao(objs,colors_per_vertex,strength,rays=22,dist=.28):
    deps=bpy.context.evaluated_depsgraph_get()
    import bmesh
    bm=bmesh.new()
    for o in objs: bm.from_mesh(o.data)
    bvh=BVHTree.FromBMesh(bm); bm.free()
    dirs=[]
    golden=math.pi*(3-math.sqrt(5))
    for i in range(rays):
        z=1-(i+.5)/rays; r=math.sqrt(1-z*z); a=golden*i; dirs.append(Vector((math.cos(a)*r,math.sin(a)*r,z)))
    for o,cols,k in zip(objs,colors_per_vertex,strength):
        for v,c in zip(o.data.vertices,cols):
            n=v.normal; q=n.to_track_quat('Z','Y'); hits=0.0; tot=0.0
            for d in dirs:
                w=d.z; dd=q@d; tot+=w
                hit=bvh.ray_cast(v.co+n*.004,dd,dist)
                if hit[0] is not None: hits+=w*(1-hit[3]/dist)**.5
            vis=1-hits/tot
            m=lerp(1,.42+.58*vis**1.3,k)
            c[0]*=m; c[1]*=m; c[2]*=m

def build(material,only=None):
    """Returns (bones, levels(json dicts), lod objects, report rows)."""
    ids={b[0]:i for i,b in enumerate(BONES)}
    parts=build_parts(only); levels=[]; lod_objects=[]; report=[]
    for lod in range(3):
        objs=[]; cols=[]; weights=[]; ao=[]
        for obj,budget,colour,weight,strength in parts:
            d=decimated(obj,budget[lod],obj.name+'_L'+str(lod)); objs.append(d)
            cols.append([list(colour(uvec(v.co),uvec(v.normal))) for v in d.data.vertices])
            weights.append([top2(weight(uvec(v.co)),ids) for v in d.data.vertices]); ao.append(strength)
        bake_ao(objs,cols,ao)
        activate(objs[0])
        for o in objs[1:]: o.select_set(True)
        bpy.ops.object.join(); obj=bpy.context.object; obj.name='Resident_Detail'+str(lod)
        flat_cols=[c for group in cols for c in group]; flat_w=[w for group in weights for w in group]
        mesh=obj.data; mesh.materials.clear(); mesh.materials.append(material)
        attr=mesh.color_attributes.new(name='Surface',type='FLOAT_COLOR',domain='POINT')
        for i,c in enumerate(flat_cols): attr.data[i].color=(min(1,c[0]),min(1,c[1]),min(1,c[2]),c[3])
        for b in BONES: obj.vertex_groups.new(name=b[0])
        for i,w in enumerate(flat_w):
            obj.vertex_groups[w['bone0']].add([i],w['weight0'],'REPLACE')
            if w['weight1']>0: obj.vertex_groups[w['bone1']].add([i],w['weight1'],'REPLACE')
        mesh.calc_loop_triangles()
        tris=[i for t in mesh.loop_triangles for i in t.vertices]
        r7=lambda x:round(x,7)
        levels.append(dict(name='Resident_Detail'+str(lod),
            positions=[dict(zip('xyz',map(r7,uvec(v.co)))) for v in mesh.vertices],
            normals=[dict(zip('xyz',map(r7,uvec(v.normal)))) for v in mesh.vertices],
            colors=[dict(zip('rgba',(min(1,c[0]),min(1,c[1]),min(1,c[2]),c[3]))) for c in flat_cols],
            uv=[dict(x=0.0,y=0.0) for _ in mesh.vertices],triangles=tris,weights=flat_w))
        report.append(dict(lod=lod,vertices=len(mesh.vertices),triangles=len(tris)//3,bones=len(BONES),renderers=1,materials=1))
        lod_objects.append(obj)
    for obj,*_ in parts: bpy.data.objects.remove(obj,do_unlink=True)
    bones=[dict(name=n,parent=p,position=dict(zip('xyz',(h.x,h.y,h.z))),tail=(t.x,t.y,t.z)) for n,p,h,t in BONES]
    return bones,levels,lod_objects,report
