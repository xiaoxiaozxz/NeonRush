"""Original Loong Vela coupe and modular city kit. Run inside Blender 5.2.

The user's existing Blender scene is retained. Geometry is authored in metres,
X right, Y forward, Z up. Unity import normalizes the visible coupe to 3.2m long.
"""
import bpy
import math
import os
import json
import numpy as np
from mathutils import Vector

ROOT = 'D:/myproject/3dloong'
SOURCE = ROOT + '/ArtSource/NeonHarbor'
ART = ROOT + '/NeonRush/Assets/_Project/Art/NeonHarbor'
TEX = ART + '/Textures'
for folder in [SOURCE, TEX, ART + '/Models', ROOT + '/NeonRush/Captures/NeonHarbor']:
    os.makedirs(folder, exist_ok=True)
if bpy.data.scenes.get('Loong Neon Studio'):
    raise RuntimeError('The authored studio already exists. Inspect it instead of overwriting edits.')
scene = bpy.data.scenes.new('Loong Neon Studio')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
collection = bpy.data.collections.new('Loong Vela - Original Coupe')
scene.collection.children.link(collection)

def link(obj):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def empty(name, parent=None, loc=(0,0,0)):
    o = bpy.data.objects.new(name, None); collection.objects.link(o)
    o.parent=parent; o.location=loc; return o

def mat(name, color, metallic=0, rough=.4, emission=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1)
    bsdf=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Metallic'].default_value=metallic
    bsdf.inputs['Roughness'].default_value=rough
    coat=bsdf.inputs.get('Coat Weight')
    if coat and metallic>.1: coat.default_value=.35
    if emission:
        bsdf.inputs['Emission Color'].default_value=(*color,1)
        bsdf.inputs['Emission Strength'].default_value=emission
    return m

paint=mat('Vela_Paint',(.94,.49,.015),.48,.23)
trim=mat('Vela_Trim',(.012,.018,.028),.25,.35)
glass=mat('Vela_Glass',(.016,.035,.055),.65,.12)
rubber=mat('Vela_Rubber',(.014,.018,.022),0,.72)
metal=mat('Vela_Metal',(.36,.43,.5),.9,.25)
red=mat('Vela_Tail',(.95,.008,.018),.1,.22,4)
white=mat('Vela_Head',(.55,.85,1),.1,.18,5)
cyan=mat('Neon_Cyan',(.01,.7,1),.15,.3,3)
pink=mat('Neon_Pink',(1,.015,.25),.15,.3,3)
wall=mat('Neon_Concrete',(.025,.035,.065),.25,.5)
building=mat('Neon_Tower',(.025,.045,.09),.3,.38)

def image(name, pixels, color=True):
    h,w=pixels.shape[:2]
    im=bpy.data.images.new(name,width=w,height=h,alpha=True)
    if not color: im.colorspace_settings.name='Non-Color'
    im.pixels.foreach_set(np.asarray(pixels,dtype=np.float32).ravel())
    im.filepath_raw=TEX+'/'+name+'.png'; im.file_format='PNG'; im.save()
    return im

rng=np.random.default_rng(41021)
n=2048
grain=rng.random((n,n),dtype=np.float32)
y,x=np.mgrid[0:n,0:n].astype(np.float32)/n
coarse=(np.sin(x*24+np.sin(y*13))*np.sin(y*19)+np.sin(x*53+y*37)*.3)*.5+.5
asphalt=np.zeros((n,n,4),np.float32)
asphalt[:,:,:3]=(.025+grain[:,:,None]*.022+coarse[:,:,None]*.012)*np.array([.65,.78,1.0])
asphalt[:,:,3]=1
image('WetRoad_Albedo',asphalt)
mask=np.ones_like(asphalt); mask[:,:,:3]=.06
mask[:,:,3]=np.clip(.65+coarse*.05+grain*.01,0,1)
image('WetRoad_MetalSmooth',mask,False)
height=grain*.1+np.sin(x*125+y*37)*.004
gx=np.roll(height,1,axis=1)-height; gy=np.roll(height,1,axis=0)-height
norm=np.empty_like(asphalt); norm[:,:,0]=.5+gx*.8;norm[:,:,1]=.5+gy*.8;norm[:,:,2]=1;norm[:,:,3]=1
image('WetRoad_Normal',norm,False)
albedo=np.ones_like(asphalt);albedo[:,:,:3]=np.array([1,.63,.025])*(.975+grain[:,:,None]*.025)
paint_image=image('VelaPaint_Albedo',albedo)
bsdf=next(n for n in paint.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
node=paint.node_tree.nodes.new('ShaderNodeTexImage');node.image=paint_image
paint.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])

# Original sunset graphic for billboards; no downloaded artwork.
sun=((x-.52)**2+(y-.63)**2)<.21**2
poster=np.ones_like(asphalt)
poster[:,:,0]=.11+.48*y;poster[:,:,1]=.025+.05*y;poster[:,:,2]=.21+.28*y
poster[sun,0]=1;poster[sun,1]=.34+.25*y[sun];poster[sun,2]=.22
for row in range(700,1250,75):poster[row:row+10,:,0:3]*=.65
skyline=(y<(.17+.075*np.sin(np.floor(x*35)*31)**2))
poster[skyline,:3]=(.008,.015,.045)
poster[:,:,3]=1
poster_image=image('NightRun_Poster',poster)
poster_mat=mat('Neon_Poster',(1,1,1),.1,.4,1)
bsdf=next(n for n in poster_mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
node=poster_mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=poster_image
poster_mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
poster_mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Emission Color'])

def mesh_obj(name,verts,faces,material,parent=None,smooth=False):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);collection.objects.link(o);o.parent=parent
    if material:me.materials.append(material)
    for p in me.polygons:p.use_smooth=smooth
    uv=me.uv_layers.new(name='UVMap')
    for loop in me.loops:
        p=me.vertices[loop.vertex_index].co
        uv.data[loop.index].uv=((p.x+1.2)/2.4,(p.y+2.6)/5.2)
    return o

def box(name,loc,size,material,parent=None,bevel=.025):
    sx,sy,sz=(v*.5 for v in size)
    verts=[(-sx,-sy,-sz),(-sx,-sy,sz),(-sx,sy,-sz),(-sx,sy,sz),(sx,-sy,-sz),(sx,-sy,sz),(sx,sy,-sz),(sx,sy,sz)]
    faces=[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
    o=mesh_obj(name,verts,[tuple(reversed(f)) for f in faces],material,parent);o.location=loc
    if bevel:
        m=o.modifiers.new('Edge highlights','BEVEL');m.width=bevel;m.segments=2
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o

def beam(name,a,b,width,material,parent):
    delta=Vector(b)-Vector(a);o=box(name,(Vector(a)+Vector(b))*.5,(width,width,delta.length),material,parent,width*.2)
    o.rotation_mode='QUATERNION';o.rotation_quaternion=delta.to_track_quat('Z','Y');return o

def cylinder(name,loc,radius,depth,material,parent,axis='X',vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth)
    o=link(bpy.context.object);o.name=name;o.parent=parent;o.location=loc
    o.rotation_euler=(0,math.pi/2,0) if axis=='X' else (math.pi/2,0,0) if axis=='Y' else (0,0,0)
    o.data.materials.append(material)
    for p in o.data.polygons:p.use_smooth=len(p.vertices)==4
    if vertices > 8:
        bevel=o.modifiers.new('Machined edge','BEVEL');bevel.width=.006;bevel.segments=1
    return o

car=empty('VelaCoupe')
body=empty('Body',car)
ys=np.linspace(-2.32,2.32,113)
def widths(y):return float(np.interp(y,[-2.32,-2.12,-1.45,-.5,.7,1.45,2.12,2.32],[.81,.93,.95,.89,.90,.95,.90,.80]))
def hood(y):return float(np.interp(y,[-2.32,-2.08,-1.2,-.4,.7,1.15,2.05,2.32],[.70,.84,.88,.87,.88,.84,.77,.70]))
verts=[];rings=[]
for yv in ys:
    yv=float(yv);w=widths(yv);h=hood(yv)
    dy=min(abs(yv-1.43),abs(yv+1.43))
    arch=.4+math.sqrt(max(0,.51**2-dy**2)) if dy<.51 else .23
    low=max(.23,arch);shoulder=max(h+.045,low+.105)
    ring=[(0,.22),(.60,.22),(.78,low),(.97,low+.01),(1,shoulder-.055),(.94,shoulder),(.76,h+.025),(.38,h+.032),(0,h+.035),(-.38,h+.032),(-.76,h+.025),(-.94,shoulder),(-1,shoulder-.055),(-.97,low+.01),(-.78,low),(-.60,.22)]
    rings.append(ring)
    verts.extend([(xx*w,yv,zz) for xx,zz in ring])
faces=[]
for i in range(len(ys)-1):
    for j in range(16):faces.append((i*16+j,i*16+(j+1)%16,(i+1)*16+(j+1)%16,(i+1)*16+j))
faces.extend([tuple(reversed(range(16))),tuple((len(ys)-1)*16+j for j in range(16))])
shell=mesh_obj('Sculpted body and open wheel arches',verts,[tuple(reversed(f)) for f in faces],paint,body,True)
sub=shell.modifiers.new('Continuous body curvature','SUBSURF');sub.levels=1;sub.render_levels=1

# Coupe greenhouse: individual windows, roof, pillars and original stripe layout.
lf=(-.72,.91,.91);rf=(.72,.91,.91);lr=(-.72,-1.13,.91);rr=(.72,-1.13,.91)
tl=(-.56,.27,1.30);tr=(.56,.27,1.30);bl=(-.54,-.65,1.29);br=(.54,-.65,1.29)
mesh_obj('Windshield',[lf,rf,tr,tl],[(3,2,1,0)],glass,body)
mesh_obj('Rear glass',[lr,bl,br,rr],[(3,2,1,0)],glass,body)
mesh_obj('Left side window',[lf,tl,bl,lr],[(3,2,1,0)],glass,body)
mesh_obj('Right side window',[rf,rr,br,tr],[(3,2,1,0)],glass,body)
roof=mesh_obj('Low coupe roof',[tl,tr,br,bl],[(3,2,1,0)],paint,body,True)
solid=roof.modifiers.new('Roof thickness','SOLIDIFY');solid.thickness=.035
for a,b in [(lf,tl),(rf,tr),(lr,bl),(rr,br),(tl,bl),(tr,br),(tl,tr),(bl,br)]:beam('Window frame',a,b,.048,paint,body)
for s in [-1,1]:
    beam('B pillar',(s*.71,-.22,.92),(s*.55,-.25,1.30),.033,trim,body)
    box('Side sill',(s*.86,-.05,.26),(.12,2.22,.10),trim,body,.025)
    box('Door handle',(s*.902,-.18,.77),(.025,.18,.026),metal,body,.01)
    mirror=box('Mirror housing',(s*1.01,.66,.94),(.20,.21,.10),paint,body,.04)
    box('Mirror lens',(s*1.025,.545,.94),(.14,.014,.065),glass,body,.008)
    # Slim door seam, sitting just outside the side surface.
    for a,b in [((s*.905,.65,.72),(s*.905,.62,.39)),((s*.905,.62,.39),(s*.905,-.65,.39)),((s*.905,-.65,.39),(s*.905,-.75,.72))]:beam('Door shut line',a,b,.007,trim,body)
    stripe=[]
    for yv in np.linspace(.96,2.30,30):
        for xx in [s*.18-.065,s*.18+.065]:stripe.append((xx,float(yv),hood(float(yv))+.037))
    mesh_obj('Hood racing stripe',stripe,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(29)],trim,body)
    box('Roof stripe',(s*.18,-.19,1.312),(.13,.87,.008),trim,body,.001)
    stripe=[]
    for yv in np.linspace(-2.28,-1.17,25):
        for xx in [s*.18-.065,s*.18+.065]:stripe.append((xx,float(yv),hood(float(yv))+.037))
    mesh_obj('Trunk racing stripe',stripe,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(24)],trim,body)

box('Front grille surround',(0,2.329,.45),(1.46,.055,.25),trim,body,.035)
for z in [.365,.415,.465,.515]:box('Grille slat',(0,2.362,z),(1.22,.023,.014),metal,body,.005)
box('Front splitter',(0,2.34,.265),(1.74,.17,.055),trim,body,.018)
box('Rear diffuser',(0,-2.345,.35),(1.65,.07,.24),trim,body,.025)
for xval in [-.48,-.24,0,.24,.48]:box('Diffuser fin',(xval,-2.39,.25),(.025,.20,.14),trim,body,.006)
for xval in [-.67,-.40,.40,.67]:
    box('Tail dark housing',(xval,-2.33,.695),(.25,.07,.125),trim,body,.026)
    for dx,dz,w,h in [(0,.041,.18,.016),(0,-.041,.18,.016),(-.093,0,.016,.074),(.093,0,.016,.074)]:
        box('Tail LED',(xval+dx,-2.373,.695+dz),(w,.012,h),red,body,.006)
for s in [-1,1]:
    box('Headlight housing',(s*.59,2.30,.72),(.43,.08,.105),trim,body,.028)
    box('Headlight LED',(s*.59,2.347,.73),(.35,.018,.03),white,body,.012)
    box('Running light',(s*.73,2.321,.40),(.05,.04,.13),white,body,.015)
    box('Spoiler pedestal',(s*.55,-1.99,.99),(.06,.09,.24),trim,body,.01)
    for ex in [.54,.70]:
        cylinder('Exhaust tip',(s*ex,-2.39,.29),.056,.15,metal,body,'Y',32)
        cylinder('Exhaust opening',(s*ex,-2.471,.29),.041,.007,trim,body,'Y',32)
box('Rear wing',(0,-2.00,1.12),(1.88,.29,.055),trim,body,.022)
for s in [-1,1]:box('Wing end plate',(s*.93,-2.0,1.135),(.025,.32,.12),trim,body,.01)

for axle, yv in [('F',1.43),('R',-1.43)]:
    for side,s in [('L',-1),('R',1)]:
        wheel=empty('Wheel_'+axle+side,car,(s*.87,yv,.40))
        bpy.ops.mesh.primitive_torus_add(major_segments=48,minor_segments=16,major_radius=.31,minor_radius=.08)
        tyre=link(bpy.context.object);tyre.name='Performance tyre';tyre.parent=wheel;tyre.location=(0,0,0);tyre.rotation_euler=(0,math.pi/2,0);tyre.data.materials.append(rubber)
        for p in tyre.data.polygons:p.use_smooth=True
        cylinder('Rim barrel',(s*.005,0,0),.285,.18,trim,wheel)
        cylinder('Brake disc',(s*.085,0,0),.216,.015,metal,wheel)
        cylinder('Hub',(s*.143,0,0),.066,.035,metal,wheel)
        box('Brake caliper',(s*.10,.15,.035),(.06,.08,.17),red,wheel,.015)
        for i in range(5):
            angle=i*math.tau/5
            a=(s*.14,math.sin(angle)*.06,math.cos(angle)*.06)
            b=(s*.14,math.sin(angle+.14)*.258,math.cos(angle+.14)*.258)
            beam('Forged spoke',a,b,.045,metal,wheel)
        for i in range(16):
            angle=i*math.tau/16
            cylinder('Rotor drilling',(s*.097,math.sin(angle)*.173,math.cos(angle)*.173),.009,.003,trim,wheel,'X',8)
for s,name in [(-1,'L'),(1,'R')]:
    empty('Exhaust_'+name,car,(s*.62,-2.49,.29))
    empty('RearContact_'+name,car,(s*.87,-1.43,.01))

# Environment kit, in a separate collection and exported as individual roots.
collection=bpy.data.collections.new('Neon Harbor - Modular Kit');scene.collection.children.link(collection)
modules=[]
rail=empty('Module_Rail');modules.append(rail)
box('Rail body',(0,0,.65),(.65,3.5,1.3),wall,rail,.06)
box('Rail cyan cap',(0,0,1.34),(.68,3.5,.055),cyan,rail,.01)
box('Rail lower pink',(.34,0,.18),(.025,3.2,.065),pink,rail,.01)
gate=empty('Module_Gate');modules.append(gate)
for s in [-1,1]:
    box('Portal pillar',(s*10.7,0,4),(.65,.8,8),wall,gate,.08)
    box('Portal light',(s*10.30,-.42,4),(.065,.025,7.6),pink if s<0 else cyan,gate,.01)
box('Portal crossbeam',(0,0,8.0),(22,.8,.55),wall,gate,.06)
box('Portal top LED',(0,-.42,7.8),(20.7,.04,.065),pink,gate,.008)
pier=empty('Module_Pier');modules.append(pier)
box('Pier shaft',(0,0,-6.5),(2.2,2.2,13),wall,pier,.18)
box('Pier capital',(0,0,-.6),(12,2.7,1.2),wall,pier,.16)
for typ in ['A','B']:
    tower=empty('Module_Tower'+typ);modules.append(tower)
    h=42 if typ=='A' else 66;w=11 if typ=='A' else 13
    box('Tower structure',(0,0,h/2),(w,12,h),building,tower,.16)
    box('Crown',(0,0,h+.5),(w*.82,10,1),wall,tower,.1)
    for s in [-1,1]:
        box('Facade seam',(s*(w/2+.02),0,h/2),(.035,12.05,h),wall,tower,.005)
        box('Vertical neon',(s*(w/2+.08),-6.02,h*.62),(.09,.05,h*.55),cyan if typ=='A' else pink,tower,.01)
    # One mesh per colour across all windows, shared materials for instancing.
    for idx,material in enumerate([cyan,pink]):
        vv=[];ff=[]
        for z in np.arange(2,h-1,2.3):
            for xval in np.arange(-w/2+1,w/2-.5,1.6):
                if rng.random()>.38 or int((z+xval)*2)%2!=idx:continue
                for face in [-1,1]:
                    base=len(vv);yy=face*6.035
                    vv.extend([(xval,yy,z),(xval+.64,yy,z),(xval+.64,yy,z+.34),(xval,yy,z+.34)])
                    ff.append((base,base+1,base+2,base+3) if face<0 else (base+3,base+2,base+1,base))
        mesh_obj('Window array',vv,ff,material,tower)
bill=empty('Module_Billboard');modules.append(bill)
box('Ad frame',(0,0,0),(7,.35,10),wall,bill,.08)
poster_o=mesh_obj('Night Run screen',[(-3.2,-.19,-4.7),(3.2,-.19,-4.7),(3.2,-.19,4.7),(-3.2,-.19,4.7)],[(0,1,2,3)],poster_mat,bill)
uv=poster_o.data.uv_layers.active
for i,co in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[i].uv=co
for s in [-1,1]:box('Ad edge',(s*3.45,-.21,0),(.04,.04,9.9),pink,bill,.01)

# Export only authored assets; originals from the user's default scene are retained.
def export(root,path):
    bpy.ops.object.select_all(action='DESELECT')
    roots=[root]+list(root.children_recursive)
    for o in roots:o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH','EMPTY'},
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        bake_anim=False,mesh_smooth_type='FACE',use_mesh_modifiers=True,add_leaf_bones=False)

export(car,ART+'/Models/VelaCoupe.fbx')
for module in modules:export(module,ART+'/Models/'+module.name+'.fbx')

# Keep architecture staged off to the side in the .blend, leaving the car studio clear.
for i,module in enumerate(modules):module.location=(18+i*25,0,0)
collection=bpy.data.collections.new('Studio Cameras and Lights');scene.collection.children.link(collection)
floor=box('Studio floor',(0,0,-.055),(200,200,.1),mat('Studio_Floor',(.035,.045,.065),.3,.25),None,.01)
def area(name,pos,power,color,size):
    light=bpy.data.lights.new(name,'AREA');light.energy=power;light.color=color;light.shape='DISK';light.size=size
    ob=bpy.data.objects.new(name,light);collection.objects.link(ob);ob.location=pos
    ob.rotation_euler=(Vector((0,0,.6))-ob.location).to_track_quat('-Z','Y').to_euler()
area('Key softbox',(3,4,7),1600,(.78,.88,1),5)
area('Warm edge',(-4,-3,4),1300,(1,.55,.25),4)
area('Cool rim',(3,-4,3),1100,(.15,.55,1),3)
world=bpy.data.worlds.new('Studio World');scene.world=world;world.use_nodes=True
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.06,.08,.13,1)
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
camera=bpy.data.cameras.new('Studio Camera');camera.type='ORTHO';camera.ortho_scale=6.2
cam=bpy.data.objects.new('Studio Camera',camera);collection.objects.link(cam);scene.camera=cam
cam.location=(6,-8,4);cam.rotation_euler=(Vector((0,0,.65))-cam.location).to_track_quat('-Z','Y').to_euler()
deps=bpy.context.evaluated_depsgraph_get();triangles=0
for ob in [car]+list(car.children_recursive):
    if ob.type=='MESH':
        evaluated=ob.evaluated_get(deps);me=evaluated.to_mesh();me.calc_loop_triangles();triangles+=len(me.loop_triangles);evaluated.to_mesh_clear()
report={'name':'Loong Vela','triangles':triangles,'meshes':sum(o.type=='MESH' for o in car.children_recursive),
        'source_axes':'X right / Y forward / Z up','modules':[m.name for m in modules],
        'source':SOURCE+'/LoongVela_NeonHarbor.blend','export':ART+'/Models/VelaCoupe.fbx'}
with open(SOURCE+'/asset-report.json','w',encoding='utf8') as f:json.dump(report,f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=SOURCE+'/LoongVela_NeonHarbor.blend',check_existing=False)
print(json.dumps(report))
