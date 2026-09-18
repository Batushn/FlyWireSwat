import bpy, sys, os, math, glob
src = sys.argv[sys.argv.index("--") + 1]; out = sys.argv[sys.argv.index("--") + 2]
os.makedirs(out, exist_ok=True)
files = sorted(glob.glob(os.path.join(src, "*.fbx")))
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [i.identifier for i in scene.render.bl_rna.properties['engine'].enum_items] else scene.render.engine
scene.render.resolution_x = 320; scene.render.resolution_y = 320
n = len(files); cols = 5; rows = math.ceil(n / cols)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
for i, f in enumerate(files):
    bpy.ops.import_scene.fbx(filepath=f)
    objs = [o for o in bpy.context.selected_objects]
    # normalise size and place on a grid
    from mathutils import Vector
    mn = Vector((1e9,)*3); mx = Vector((-1e9,)*3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    size = max(mx - mn); center = (mn + mx) / 2
    s = 1.0 / size
    for o in objs:
        o.location = (o.location - center) * s + Vector(((i % cols) * 1.4, 0, -(i // cols) * 1.4))
        o.scale = o.scale * s
bpy.ops.object.light_add(type='SUN', location=(3, -3, 6)); bpy.context.active_object.data.energy = 3
bpy.ops.object.light_add(type='AREA', location=(-3, -4, 4)); bpy.context.active_object.data.energy = 800
cam = bpy.data.cameras.new('c'); co = bpy.data.objects.new('c', cam); scene.collection.objects.link(co); scene.camera = co
cx = (cols - 1) * 0.7; cz = -(rows - 1) * 0.7
co.location = (cx, -9.5, cz + 4.5); co.rotation_euler = (math.radians(65), 0, 0)
cam.lens = 45
scene.render.resolution_x = 1400; scene.render.resolution_y = 900
scene.world = bpy.data.worlds.new('w'); scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (0.6, 0.6, 0.65, 1)
scene.render.filepath = os.path.join(out, 'props_contact_sheet.png')
bpy.ops.render.render(write_still=True)
print('rendered', scene.render.filepath)
