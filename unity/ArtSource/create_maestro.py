"""Create Maestro's original included mesh, rig, and six reusable animations.

Run with Blender 4.5 LTS: blender --background --python this_file.py -- <repo>
All geometry is authored here; no downloaded model or motion assets are used.
The user-supplied cartoon reference supplies palette, costume, and visual cues.
Deliberately simplified geometry, painted eyes, and broad shapes avoid realistic skin.
"""
import bpy
import json
import math
import random
import sys
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree

ROOT = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
OUTPUT = ROOT / 'unity/MaestroQuest/Assets/Maestro/Resources/Avatars'
EVIDENCE = ROOT / '.quest-evidence/art'
OUTPUT.mkdir(parents=True, exist_ok=True)
EVIDENCE.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(731)
parts = []

def material(name, color):
    m = bpy.data.materials.new(name)
    # Store linear colors so Unity's imported material has the intended pigment.
    rgb = tuple(((int(color[i:i+2], 16) / 255 + .055) / 1.055) ** 2.4 for i in (0, 2, 4))
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = .92
    bsdf.inputs['Specular IOR Level'].default_value = .18
    return m

skin = material('Pigment skin warm ochre', 'E7AC7B')
skin_light = material('Pigment skin highlight', 'EBB384')
skin_shadow = material('Detail freckles and smile', 'AF714C')
hair = material('Pigment graphite hair', '302D37')
hair_line = material('Pigment braid strokes', '46414F')
violet = material('Pigment violet cloth', '65477D')
blue = material('Detail indigo thread', '3A65B5')
teal = material('Detail blue thread', '43A9E0')
ink = material('Detail graphite marks', '342D2B')
gold = material('Pigment ochre jewelry', 'CEB15D')
ivory = material('Detail paper white', 'FFF0D2')
iris = material('Detail blue iris', '4B8DB8')
trousers = material('Pigment purple cargo trousers', '705282')
boots = material('Pigment purple sneakers', '634A78')
lips = material('Detail soft smile', 'C47E5F')

def finish(obj, name, mat, bone):
    obj.name = name
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if obj.type == 'MESH':
        for face in obj.data.polygons:
            face.use_smooth = True
        obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    obj['rig_bone'] = bone
    parts.append(obj)
    return obj

def ellipsoid(name, center, scale, mat, bone, segments=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    obj = bpy.context.object
    obj.scale = scale
    return finish(obj, name, mat, bone)

def capsule(name, start, end, width, depth, mat, bone):
    a, b = Vector(start), Vector(end)
    obj = ellipsoid(name, (a+b)*.5, (width, depth, (b-a).length*.5 + width*.35), mat, bone)
    obj.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    return obj

def stroke(name, points, radius, mat, bone, cyclic=False):
    bpy.ops.object.select_all(action='DESELECT')
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    curve.use_fill_caps = True
    spline = curve.splines.new('POLY')
    spline.points.add(len(points)-1)
    for point, co in zip(spline.points, points):
        point.co = (*co, 1)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    finish(obj, name, mat, bone)
    obj.select_set(False)
    return obj

def ring_surface(name, rings, mat, bone):
    vertices, faces = [], []
    steps = 24
    for z, width, depth, y in rings:
        for i in range(steps):
            angle = i / steps * math.tau
            vertices.append((math.cos(angle)*width, y+math.sin(angle)*depth, z))
    for row in range(len(rings)-1):
        for i in range(steps):
            j = row*steps+i
            next_j = row*steps+(i+1)%steps
            faces.append((j, next_j, next_j+steps, j+steps))
    faces.extend([tuple(range(steps-1, -1, -1)), tuple((len(rings)-1)*steps+i for i in range(steps))])
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    finish(obj, name, mat, bone)
    obj.select_set(False)
    return obj

def sleeve(name, start, end, widths, mat, bone):
    a, b = Vector(start), Vector(end)
    direction = (b-a).normalized()
    right = direction.cross(Vector((0,1,0))).normalized()
    front = right.cross(direction).normalized()
    vertices, faces = [], []
    steps = 16
    for row, width in enumerate(widths):
        center = a.lerp(b, row/(len(widths)-1))
        for step in range(steps):
            angle = step/steps*math.tau
            vertices.append(tuple(center + width*(math.cos(angle)*right + math.sin(angle)*front)))
    for row in range(len(widths)-1):
        for step in range(steps):
            j=row*steps+step; k=row*steps+(step+1)%steps
            faces.append((j,j+steps,k+steps,k))
    faces.extend([tuple(range(steps)), tuple((len(widths)-1)*steps+i for i in range(steps-1,-1,-1))])
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    finish(obj,name,mat,bone); obj.select_set(False)
    return obj

# About five heads tall: an illustrated adult, with a broad, readable silhouette.
ring_surface('Cargo waist', [(.77,.158,.094,0),(.88,.175,.103,0),(1.00,.131,.085,0)], trousers, 'Hips')
ring_surface('Simple midriff', [(.98,.126,.082,0),(1.07,.12,.082,0),(1.13,.14,.089,0)], skin, 'Spine')
ring_surface('Purple cropped top', [(1.085,.142,.097,0),(1.17,.172,.104,0),(1.25,.186,.086,0),(1.28,.145,.07,0)], violet, 'Spine')
ellipsoid('Left shoulder', (.175,0,1.292), (.056,.063,.065), skin, 'Chest')
ellipsoid('Right shoulder', (-.175,0,1.292), (.056,.063,.065), skin, 'Chest')
ellipsoid('Neck', (0,0,1.36), (.047,.046,.07), skin, 'Neck')
ring_surface('Halter neckline', [(1.23,.164,.092,0),(1.27,.137,.081,0),(1.315,.055,.049,0)], violet, 'Chest')
stroke('Halter collar', [(.056*math.cos(a),.05*math.sin(a),1.315) for a in [i*math.tau/32 for i in range(32)]], .006, violet, 'Chest', True)
for sign in (-1,1):
    stroke('Halter strap',[(sign*.15,-.014,1.282),(sign*.094,-.043,1.318),(sign*.054,-.025,1.319)],.010,violet,'Chest')
stroke('Crop hem pencil seam', [(.143*math.cos(a),.098*math.sin(a),1.089) for a in [i*math.tau/32 for i in range(32)]], .0014, ink, 'Spine', True)
stroke('Waist seam', [(.132*math.cos(a),.086*math.sin(a),.992) for a in [i*math.tau/32 for i in range(32)]], .0015, ink, 'Hips', True)
ellipsoid('Waist button',(0,-.087,.976),(.012,.003,.012),gold,'Hips',12,8)

bones = [('Root',(0,0,0),(0,0,.12),None), ('Hips',(0,0,.86),(0,0,1.02),'Root'), ('Spine',(0,0,1.02),(0,0,1.21),'Hips'), ('Chest',(0,0,1.21),(0,0,1.31),'Spine'), ('Neck',(0,0,1.31),(0,0,1.40),'Chest'), ('Head',(0,0,1.40),(0,0,1.61),'Neck')]
for sign, side in ((-1,'Right'),(1,'Left')):
    shoulder, elbow, wrist = (sign*.198,0,1.29), (sign*.293,-.004,1.073), (sign*.329,-.035,.871)
    hip, knee, ankle = (sign*.088,0,.86), (sign*.093,-.007,.48), (sign*.096,.001,.135)
    bones.extend([(side+'UpperArm',shoulder,elbow,'Chest'),(side+'LowerArm',elbow,wrist,side+'UpperArm'),(side+'Hand',wrist,(sign*.344,-.037,.778),side+'LowerArm'),(side+'UpperLeg',hip,knee,'Hips'),(side+'LowerLeg',knee,ankle,side+'UpperLeg'),(side+'Foot',ankle,(sign*.096,-.13,.065),side+'LowerLeg')])
    capsule(side+' upper arm',shoulder,elbow,.044,.046,skin,side+'UpperArm')
    capsule(side+' forearm',elbow,wrist,.033,.035,skin,side+'LowerArm')
    ellipsoid(side+' elbow',elbow,(.034,.034,.037),skin,side+'LowerArm')
    sleeve(side+' upper puff sleeve',(sign*.237,-.001,1.205),(sign*.295,-.007,1.051),[.05,.065,.067,.058],violet,side+'UpperArm')
    sleeve(side+' lower puff sleeve',(sign*.288,-.002,1.095),wrist,[.058,.065,.068,.043,.033],violet,side+'LowerArm')
    capsule(side+' purple cuff',(sign*.326,-.033,.892),(sign*.329,-.035,.87),.036,.038,violet,side+'LowerArm')
    ellipsoid(side+' palm',(sign*.338,-.035,.823),(.033,.021,.052),skin,side+'Hand')
    for finger in range(4):
        x = sign*(.317 + finger*.014)
        capsule(side+' finger '+str(finger),(x,-.038,.81),(x+sign*.007,-.04,.768+abs(finger-1)*.009),.007,.009,skin,side+'Hand')
    capsule(side+' thumb',(sign*.31,-.042,.837),(sign*.296,-.056,.796),.011,.012,skin,side+'Hand')
    sleeve(side+' baggy trouser thigh',hip,knee,[.092,.10,.098,.087],trousers,side+'UpperLeg')
    sleeve(side+' baggy trouser leg',knee,(sign*.096,0,.085),[.087,.09,.086,.09,.082],trousers,side+'LowerLeg')
    ellipsoid(side+' knee',knee,(.086,.087,.087),trousers,side+'LowerLeg')
    # Raised cargo pockets and a few long seam strokes give the trousers their identity.
    pocket=ellipsoid(side+' cargo pocket',(sign*.172,-.038,.675),(.026,.048,.085),violet,side+'UpperLeg',12,8)
    stroke(side+' pocket flap',[(sign*.19,-.072,.731),(sign*.196,-.045,.729),(sign*.191,-.002,.728)],.0017,ink,side+'UpperLeg')
    for h in range(3):
        z=.23+h*.065
        stroke(side+' cloth fold',[(sign*.137,-.072,z),(sign*.11,-.084,z-.015),(sign*.075,-.08,z-.014)],.0012,ink,side+'LowerLeg')
    ellipsoid(side+' sneaker',(sign*.096,-.060,.065),(.074,.13,.057),boots,side+'Foot')
    ellipsoid(side+' rubber sole',(sign*.096,-.060,.025),(.078,.134,.025),ivory,side+'Foot')
    ellipsoid(side+' shoe toe panel',(sign*.096,-.126,.089),(.062,.055,.018),ivory,side+'Foot')
    for lace in range(3):
        stroke(side+' lace '+str(lace),[(sign*.096-.039,-.10+lace*.023,.112),(sign*.096+.039,-.09+lace*.023,.112)],.003,ivory,side+'Foot')

# Shaped cheeks/chin, eyes and relief strokes are actual geometry.
ellipsoid('Face',(0,-.013,1.485),(.122,.102,.16),skin,'Head',32,20)
# A single rounded head avoids a separate protruding chin or realistic cheek anatomy.
for sign in (-1,1):
    ellipsoid('Ear',(sign*.122,.0,1.493),(.023,.026,.039),skin,'Head')
    ellipsoid('Ear inset',(sign*.139,-.014,1.492),(.004,.012,.022),skin_shadow,'Head',12,8)
    # White eye bounds and blue irises lie on a gently curved face.
    ellipsoid('Eye white',(sign*.050,-.106,1.516),(.031,.010,.017),ivory,'Head')
    ellipsoid('Iris',(sign*.050,-.116,1.516),(.013,.0035,.014),iris,'Head',20,12)
    ellipsoid('Pupil',(sign*.050,-.119,1.516),(.0068,.0018,.0093),ink,'Head',16,10)
    ellipsoid('Eye glint',(sign*.046,-.121,1.522),(.0032,.0011,.0038),ivory,'Head',12,8)
    stroke('Upper eyelid',[(sign*(.021+i*.006),-.115,1.516+.015*math.sin(i/10*math.pi)) for i in range(11)],.0025,ink,'Head')
    stroke('Indigo brow',[(sign*(.025+i*.007),-.107,1.553+.006*math.sin(i/7*math.pi)) for i in range(8)],.0045,blue,'Head')
    for i in range(7):
        x = sign*(.035+random.random()*.05)
        z = 1.47 + random.random()*.022
        y = -.013-.103*math.sqrt(max(.03,1-(x/.126)**2-((z-1.485)/.167)**2))-.0015
        ellipsoid('Freckle',(x,y,z),(.0012+random.random()*.0008,.001,.0012),skin_shadow,'Head',8,6)
ellipsoid('Simple nose',(0,-.115,1.481),(.016,.015,.015),skin,'Head')
def face_surface(x,z):
    return -.013-.102*math.sqrt(max(.05,1-(x/.122)**2-((z-1.485)/.16)**2))-.004
stroke('Smile',[(x,face_surface(x,1.433+9*x*x),1.433+9*x*x) for x in [i*.005 for i in range(-9,10)]],.0022,skin_shadow,'Head')
stroke('Lower lip',[(x,face_surface(x,1.426+7*x*x)-.001,1.426+7*x*x) for x in [i*.004 for i in range(-8,9)]],.0035,lips,'Head')
stroke('Nose ring',[(.015+.008*math.cos(a),-.137,1.476+.009*math.sin(a)) for a in [i*math.tau/20 for i in range(20)]],.0017,gold,'Head',True)
stroke('Earring hoop',[(-.138+.011*math.cos(a),-.022,1.46+.016*math.sin(a)) for a in [i*math.tau/24 for i in range(24)]],.0023,gold,'Head',True)
star=[]
for i in range(10):
    a=math.pi*.5+i*math.pi/5
    r=.011 if i%2==0 else .005
    star.append((-.138+r*math.cos(a),-.022,1.425+r*math.sin(a)))
stroke('Star charm',star,.0017,gold,'Head',True)

# Swept crown and individual plaits: no transparent hair cards or texture atlases.
ellipsoid('Hair crown',(0,.019,1.58),(.13,.096,.09),hair,'Head',28,16)
ellipsoid('Hair back',(0,.060,1.482),(.119,.065,.155),hair,'Head')
for braid_index in range(10):
    t=braid_index/9
    points=[]
    for step in range(17):
        p=step/16
        x=(-.107+.198*t)*(1-p)+.118*p
        y=-.059+.080*p
        z=(1.571+.035*math.sin(t*math.pi))*(1-p)+1.527*p+.068*math.sin(p*math.pi)
        points.append((x,y,z))
    stroke('Swept crown plait',points,.012,hair,'Head')
    stroke('Crown pencil highlight',[(x-.003,y-.002,z+.006) for x,y,z in points],.0015,hair_line,'Head')
for braid_index in range(12):
    column=braid_index%6; layer=braid_index//6
    t=column/5
    points=[]
    for step in range(33):
        p=step/32
        x=.105+t*.075 + p*(.035+t*.035)+.010*math.sin(p*math.tau+column*.7)
        y=.012+layer*.038-.065*p
        z=1.61-column*.012-p*(.72+.10*math.sin(column*.8+layer))
        points.append((x,y,z))
    # Three broad color zones read as blue-tipped plaits at headset distance.
    stroke('Dark braid root '+str(braid_index),points[:15],.0105,hair,'Head')
    stroke('Indigo braid '+str(braid_index),points[13:26],.0095,blue,'Head')
    stroke('Blue braid tip '+str(braid_index),points[24:],.007,teal,'Head')
    for strand in range(2):
        twisted=[]
        for step,(x,y,z) in enumerate(points):
            angle=step*1.3+strand*math.pi
            radius=.0105 if step<15 else .0095 if step<26 else .0065
            twisted.append((x+radius*math.cos(angle),y-radius*math.sin(angle),z))
        stroke('Braided graphite rhythm',twisted,.0012,ink,'Head')

# Join by pigment for low draw counts, retaining each part's bone vertex groups.
for obj in parts:
    if obj['rig_bone']=='Head':
        # Scale the entire drawn face consistently, including eyes and jewelry.
        for vertex in obj.data.vertices:
            world=obj.matrix_world@vertex.co
            world.x*=1.15; world.y*=1.02; world.z=1.40+(world.z-1.40)*1.10
            vertex.co=obj.matrix_world.inverted()@world
bpy.ops.object.select_all(action='DESELECT')
joined=[]
material_groups = [(mat, [o for o in parts if o.data.materials[0] == mat]) for mat in list(bpy.data.materials)]
for mat, group in material_groups:
    if not group: continue
    for obj in group: obj.select_set(True)
    bpy.context.view_layer.objects.active=group[0]
    if len(group)>1: bpy.ops.object.join()
    merged=bpy.context.object
    merged.name=mat.name
    # Fuse overlapping body pieces into a continuous surface, carrying original
    # nearby bone weights across the remesh. Facial pigments remain separate.
    if mat in (skin, trousers, boots, violet):
        original_weights=[{g.group:g.weight for g in v.groups} for v in merged.data.vertices]
        tree=KDTree(len(merged.data.vertices))
        for index,vertex in enumerate(merged.data.vertices): tree.insert(vertex.co,index)
        tree.balance()
        remesh=merged.modifiers.new('Continuous sculpt','REMESH')
        remesh.mode='VOXEL'; remesh.voxel_size=.005
        bpy.ops.object.modifier_apply(modifier=remesh.name)
        smooth=merged.modifiers.new('Soft clay surface','SMOOTH')
        smooth.factor=.55; smooth.iterations=3
        bpy.ops.object.modifier_apply(modifier=smooth.name)
        decimate=merged.modifiers.new('Quest mesh budget','DECIMATE')
        decimate.ratio=.12
        bpy.ops.object.modifier_apply(modifier=decimate.name)
        for vertex in merged.data.vertices:
            weights={}
            for _,index,distance in tree.find_n(vertex.co,4):
                for group_id,weight in original_weights[index].items():
                    weights[group_id]=weights.get(group_id,0)+weight/max(.0005,distance)
            best=sorted(weights.items(),key=lambda item:item[1],reverse=True)[:4]
            total=sum(weight for _,weight in best)
            for group_id,weight in best: merged.vertex_groups[group_id].add([vertex.index],weight/total,'REPLACE')
        for face in merged.data.polygons: face.use_smooth=True
    joined.append(merged)
    bpy.ops.object.select_all(action='DESELECT')

armature=bpy.data.armatures.new('Maestro humanoid skeleton')
rig=bpy.data.objects.new('Maestro',armature)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,head,tail,parent in bones:
    bone=armature.edit_bones.new(name)
    bone.head=head; bone.tail=tail
    if parent: bone.parent=armature.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
for obj in joined:
    obj.parent=rig
    modifier=obj.modifiers.new('Maestro skeleton','ARMATURE')
    modifier.object=rig

scene=bpy.context.scene
scene.render.fps=30
rig.animation_data_create()
durations={'Idle':120,'Listening':90,'Speaking':90,'Greeting':75,'Pointing':90,'Walk':31}
for name,last in durations.items():
    action=bpy.data.actions.new(name)
    action.use_fake_user=True
    rig.animation_data.action=action
    for frame in range(1,last+1,3):
        p=(frame-1)/(last-1)
        pulse=math.sin(p*math.tau)
        for pb in rig.pose.bones:
            pb.rotation_mode='XYZ'; pb.rotation_euler=(0,0,0)
        rig.pose.bones['Spine'].rotation_euler.y=.012*pulse
        rig.pose.bones['Chest'].rotation_euler.x=.009*pulse
        rig.pose.bones['Head'].rotation_euler.y=.015*pulse
        if name=='Listening':
            rig.pose.bones['Head'].rotation_euler.x=.045+.023*math.sin(p*math.tau*2)
            rig.pose.bones['Head'].rotation_euler.z=.04
        if name=='Speaking':
            rig.pose.bones['Head'].rotation_euler.x=.025*math.sin(p*math.tau*3)
            rig.pose.bones['LeftUpperArm'].rotation_euler.x=-.18-.07*pulse
            rig.pose.bones['LeftLowerArm'].rotation_euler.x=-.48-.1*pulse
            rig.pose.bones['RightLowerArm'].rotation_euler.x=-.20+.07*pulse
        if name=='Greeting':
            envelope=min(1,p*7,(1-p)*7)
            rig.pose.bones['RightUpperArm'].rotation_euler.x=-1.85*envelope
            rig.pose.bones['RightUpperArm'].rotation_euler.z=-.65*envelope
            rig.pose.bones['RightLowerArm'].rotation_euler.x=-.62*envelope
            rig.pose.bones['RightHand'].rotation_euler.z=.20*math.sin(p*math.tau*4)*envelope
        if name=='Pointing':
            rig.pose.bones['LeftUpperArm'].rotation_euler.x=-.67
            rig.pose.bones['LeftLowerArm'].rotation_euler.x=-.35
            rig.pose.bones['Head'].rotation_euler.z=-.12
        if name=='Walk':
            for side,phase in [('Left',0),('Right',math.pi)]:
                swing=math.sin(p*math.tau+phase)
                rig.pose.bones[side+'UpperLeg'].rotation_euler.x=.30*swing
                rig.pose.bones[side+'LowerLeg'].rotation_euler.x=-.45*max(0,-swing)
                rig.pose.bones[side+'Foot'].rotation_euler.x=-.15*swing
                rig.pose.bones[side+'UpperArm'].rotation_euler.x=-.20*swing
                rig.pose.bones[side+'LowerArm'].rotation_euler.x=-.16
        for pb in rig.pose.bones:
            pb.keyframe_insert(data_path='rotation_euler',frame=frame,group=pb.name)
    # Close looping gestures exactly; greeting also returns to a neutral rest.
    for pb in rig.pose.bones:
        if name=='Greeting': pb.rotation_euler=(0,0,0)
        else:
            scene.frame_set(1)
        pb.keyframe_insert(data_path='rotation_euler',frame=last,group=pb.name)
rig.animation_data.action=bpy.data.actions['Idle']
scene.frame_set(1)

bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
for obj in joined: obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'DefaultMaestro.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,mesh_smooth_type='FACE',path_mode='STRIP')

# A clean, neutral studio render is review evidence, not a substitute for XR QA.
scene.world.color=(.6,.6,.6)
floor_mat=material('Studio paper','F1E8D5')
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003))
bpy.context.object.data.materials.append(floor_mat)
for location,energy,size in [((-2,-4,5),500,5),((3,-1,3),250,4),((0,3,4),350,3)]:
    bpy.ops.object.light_add(type='AREA',location=location)
    lamp=bpy.context.object; lamp.data.energy=energy; lamp.data.shape='DISK'; lamp.data.size=size
    lamp.rotation_euler=(Vector((0,0,1))-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2,-5,2.3))
camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.88))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'; camera.data.ortho_scale=2.05
scene.camera=camera
scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.render.resolution_x=800; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(EVIDENCE/'maestro-full-body.png')
bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE/'MaestroSource.blend'))
bpy.ops.render.render(write_still=True)
camera.location=(0,-4,1.5)
camera.rotation_euler=(Vector((0,-.02,1.47))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=.58
scene.render.resolution_x=800; scene.render.resolution_y=800
scene.render.filepath=str(EVIDENCE/'maestro-portrait.png')
bpy.ops.render.render(write_still=True)
triangles=sum(sum(len(poly.vertices)-2 for poly in obj.data.polygons) for obj in joined)
height=max((obj.matrix_world@Vector(corner)).z for obj in joined for corner in obj.bound_box)
metadata={'generator':'Blender 4.5.9 / create_maestro.py','heightMeters':round(height,3),'triangles':triangles,'materials':len(joined),'bones':len(bones),'animations':durations,'provenance':'Original procedural geometry and animation; visual reference is the user-supplied cartoon Maestro reference (2026-09-25).','artDirection':'Simplified cartoon proportions, blue-tipped braids, purple cropped top and cargo trousers, sneakers, matte pencil and watercolor.'}
(OUTPUT/'DefaultMaestro.provenance.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
print('MAESTRO_ART_COMPLETE '+json.dumps(metadata))
