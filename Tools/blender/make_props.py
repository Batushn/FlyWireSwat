"""Headless Blender script: procedurally models weapons / repellent props and exports one FBX each.
Run: blender -b --python Tools/blender/make_props.py -- <out_dir>
Convention: +Y = "front" (business end), +Z = up, metres."""
import bpy, bmesh, math, sys, os
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "Assets/Models/Generated"
os.makedirs(OUT, exist_ok=True)

def clear():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)

def mat(name, rgb, rough=0.6, metal=0.0, alpha=1.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1.0)
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    if alpha < 1.0:
        bsdf.inputs['Alpha'].default_value = alpha
        m.blend_method = 'BLEND'
    m.diffuse_color = (*rgb, 1.0)
    return m

def add(prim, name, loc=(0,0,0), scale=(1,1,1), rot=(0,0,0), m=None, **kw):
    getattr(bpy.ops.mesh, 'primitive_' + prim + '_add')(location=loc, rotation=rot, **kw)
    o = bpy.context.active_object; o.name = name; o.scale = scale
    if m: o.data.materials.append(m)
    return o

def smooth(o, subdiv=2):
    bpy.context.view_layer.objects.active = o
    if subdiv > 0:
        md = o.modifiers.new('sub', 'SUBSURF'); md.levels = subdiv; md.render_levels = subdiv
    bpy.ops.object.shade_smooth()

def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object; o.name = name
    return o

def export(name):
    bpy.ops.object.select_all(action='SELECT')
    for o in bpy.context.selected_objects:
        bpy.context.view_layer.objects.active = o
        for md in list(o.modifiers):
            try: bpy.ops.object.modifier_apply(modifier=md.name)
            except Exception as e: print('modifier', md.name, e)
    bpy.ops.object.select_all(action='SELECT')
    path = os.path.join(OUT, name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', use_mesh_modifiers=True, path_mode='COPY', embed_textures=False)
    print('exported', path)

# ---------------- hand (cartoon, subdivided) ----------------
def hand():
    clear()
    skin = mat('Skin', (0.87, 0.62, 0.48), 0.55)
    nail = mat('Nail', (0.95, 0.85, 0.8), 0.3)
    palm = add('cube', 'Palm', (0, 0, 0), (0.045, 0.05, 0.012), m=skin)
    parts = [palm]
    # four fingers along +Y
    for i, (x, ln) in enumerate([(-0.033, 0.075), (-0.011, 0.085), (0.011, 0.08), (0.033, 0.065)]):
        y0 = 0.05
        for seg in range(3):
            l = ln / 3
            f = add('cube', f'F{i}_{seg}', (x, y0 + l * 0.5, 0.0 - seg * 0.002), (0.0095, l * 0.5, 0.009), m=skin)
            parts.append(f); y0 += l
        parts.append(add('uv_sphere', f'Nail{i}', (x, y0 - 0.004, 0.006), (0.006, 0.007, 0.002), m=nail, segments=12, ring_count=8))
    # thumb
    for seg in range(2):
        t = add('cube', f'T{seg}', (0.055 + seg * 0.028, 0.01 + seg * 0.02, 0.0), (0.012, 0.016, 0.01), rot=(0, 0, -0.6), m=skin)
        parts.append(t)
    # wrist
    parts.append(add('cube', 'Wrist', (0, -0.07, 0), (0.035, 0.03, 0.013), m=skin))
    o = join(parts, 'Hand')
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.remove_doubles(); bpy.ops.object.mode_set(mode='OBJECT')
    smooth(o, 2)
    export('hand')

# ---------------- fly swatter ----------------
def swatter():
    clear()
    plastic = mat('SwatterRed', (0.85, 0.15, 0.12), 0.45)
    handle_m = mat('SwatterHandle', (0.2, 0.2, 0.22), 0.5)
    parts = []
    # perforated head: grid of thin bars
    w, h, t = 0.11, 0.13, 0.0025
    cells = 9
    for i in range(cells + 1):
        x = -w / 2 + i * w / cells
        parts.append(add('cube', f'bx{i}', (x, 0.28, 0), (0.0018, h / 2, t), m=plastic))
    for j in range(int(cells * h / w) + 1):
        y = 0.28 - h / 2 + j * (h / int(cells * h / w))
        parts.append(add('cube', f'by{j}', (0, y, 0), (w / 2, 0.0018, t), m=plastic))
    frame = add('cube', 'frame', (0, 0.28, 0), (w / 2 + 0.006, h / 2 + 0.006, t * 1.4), m=plastic)
    bpy.context.view_layer.objects.active = frame
    md = frame.modifiers.new('bevel', 'BEVEL'); md.width = 0.004; md.segments = 3
    parts.append(frame)
    parts.append(add('cylinder', 'neck', (0, 0.17, 0), (0.007, 0.007, 0.05), rot=(math.pi / 2, 0, 0), m=handle_m, vertices=16))
    parts.append(add('cylinder', 'handle', (0, 0.03, 0), (0.011, 0.011, 0.11), rot=(math.pi / 2, 0, 0), m=handle_m, vertices=16))
    parts.append(add('uv_sphere', 'cap', (0, -0.08, 0), (0.012, 0.012, 0.012), m=handle_m, segments=16, ring_count=8))
    o = join(parts, 'Swatter')
    bpy.ops.object.shade_smooth()
    export('swatter')

# ---------------- electric racket ----------------
def racket():
    clear()
    yellow = mat('RacketYellow', (0.98, 0.8, 0.1), 0.4)
    dark = mat('RacketDark', (0.15, 0.15, 0.17), 0.5)
    wire = mat('RacketWire', (0.75, 0.75, 0.78), 0.3, 0.9)
    parts = []
    ring = add('torus', 'ring', (0, 0.3, 0), (1, 1, 1), rot=(math.pi / 2, 0, 0), m=yellow, major_radius=0.1, minor_radius=0.011, major_segments=48, minor_segments=12)
    parts.append(ring)
    for i in range(-8, 9):
        parts.append(add('cylinder', f'w{i}', (i * 0.011, 0.3, 0), (0.0006, 0.0006, math.sqrt(max(0.0001, 0.1 ** 2 - (i * 0.011) ** 2))), rot=(math.pi / 2, 0, 0), m=wire, vertices=6))
    for j in range(-8, 9):
        parts.append(add('cylinder', f'v{j}', (0, 0.3 + j * 0.011, 0), (0.0006, 0.0006, math.sqrt(max(0.0001, 0.1 ** 2 - (j * 0.011) ** 2))), rot=(0, math.pi / 2, 0), m=wire, vertices=6))
    parts.append(add('cube', 'neck', (0, 0.17, 0), (0.02, 0.035, 0.012), m=yellow))
    parts.append(add('cube', 'handle', (0, 0.06, 0), (0.017, 0.085, 0.014), m=dark))
    parts.append(add('cube', 'button', (0, 0.09, 0.015), (0.006, 0.012, 0.004), m=mat('Btn', (0.9, 0.2, 0.2))))
    o = join(parts, 'Racket')
    smooth(o, 1)
    export('racket')

# ---------------- rolled newspaper ----------------
def newspaper():
    clear()
    paper = mat('Paper', (0.86, 0.84, 0.76), 0.85)
    ink = mat('Ink', (0.35, 0.35, 0.35), 0.8)
    parts = [add('cylinder', 'roll', (0, 0, 0), (0.02, 0.02, 0.19), rot=(math.pi / 2, 0, 0), m=paper, vertices=24)]
    # spiral seam + text stripes
    for k in range(6):
        parts.append(add('cube', f'line{k}', (0.0205 * math.cos(k), -0.15 + k * 0.06, 0.0205 * math.sin(k)), (0.0005, 0.002, 0.006), rot=(0, -k, 0), m=ink))
    parts.append(add('torus', 'band', (0, 0.05, 0), (1, 1, 1), rot=(math.pi / 2, 0, 0), m=mat('Band', (0.8, 0.2, 0.2)), major_radius=0.0205, minor_radius=0.0015, major_segments=24, minor_segments=6))
    o = join(parts, 'Newspaper'); bpy.ops.object.shade_smooth()
    export('newspaper')

# ---------------- towel ----------------
def towel():
    clear()
    cloth = mat('Towel', (0.35, 0.6, 0.85), 0.9)
    bpy.ops.mesh.primitive_plane_add(size=1); o = bpy.context.active_object; o.name = 'Towel'
    o.scale = (0.06, 0.28, 1)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.subdivide(number_cuts=12); bpy.ops.object.mode_set(mode='OBJECT')
    bm = bmesh.new(); bm.from_mesh(o.data)
    for v in bm.verts:
        v.co.z = 0.012 * math.sin(v.co.y * 14) + 0.006 * math.sin(v.co.x * 40)
        v.co.x *= (1.0 + 0.5 * (v.co.y + 0.5))  # twisted: narrow at the tip
    bm.to_mesh(o.data); bm.free()
    md = o.modifiers.new('solid', 'SOLIDIFY'); md.thickness = 0.008
    o.data.materials.append(cloth); smooth(o, 1)
    export('towel')

# ---------------- vacuum nozzle ----------------
def vacuum():
    clear()
    grey = mat('VacGrey', (0.35, 0.36, 0.4), 0.5)
    black = mat('VacBlack', (0.1, 0.1, 0.11), 0.6)
    parts = [add('cylinder', 'tube', (0, 0, 0), (0.018, 0.018, 0.22), rot=(math.pi / 2, 0, 0), m=grey, vertices=24),
             add('cone', 'nozzle', (0, 0.25, 0), (1, 1, 1), rot=(-math.pi / 2, 0, 0), m=black, radius1=0.02, radius2=0.03, depth=0.06, vertices=24),
             add('cylinder', 'hose', (0, -0.3, -0.02), (0.02, 0.02, 0.1), rot=(math.pi / 2 - 0.4, 0, 0), m=black, vertices=16)]
    for k in range(8):
        parts.append(add('torus', f'rib{k}', (0, -0.24 - k * 0.022, -0.02 - k * 0.009), (1, 1, 1), rot=(math.pi / 2 - 0.4, 0, 0), m=black, major_radius=0.021, minor_radius=0.003, major_segments=16, minor_segments=6))
    o = join(parts, 'Vacuum'); bpy.ops.object.shade_smooth()
    export('vacuum')

# ---------------- cat (stylised) ----------------
def cat():
    clear()
    fur = mat('Fur', (0.42, 0.36, 0.33), 0.8)
    pink = mat('Pink', (0.9, 0.55, 0.6), 0.6)
    eye = mat('CatEye', (0.35, 0.8, 0.3), 0.2)
    parts = [add('uv_sphere', 'body', (0, -0.05, 0.12), (0.11, 0.16, 0.1), m=fur, segments=24, ring_count=12),
             add('uv_sphere', 'head', (0, 0.12, 0.2), (0.075, 0.07, 0.07), m=fur, segments=24, ring_count=12),
             add('cone', 'earL', (-0.045, 0.12, 0.26), (1, 1, 1), m=fur, radius1=0.02, radius2=0.0, depth=0.05, vertices=8),
             add('cone', 'earR', (0.045, 0.12, 0.26), (1, 1, 1), m=fur, radius1=0.02, radius2=0.0, depth=0.05, vertices=8),
             add('uv_sphere', 'eyeL', (-0.028, 0.18, 0.215), (0.012, 0.008, 0.012), m=eye, segments=12, ring_count=8),
             add('uv_sphere', 'eyeR', (0.028, 0.18, 0.215), (0.012, 0.008, 0.012), m=eye, segments=12, ring_count=8),
             add('uv_sphere', 'nose', (0, 0.19, 0.19), (0.008, 0.006, 0.006), m=pink, segments=8, ring_count=6),
             add('cylinder', 'tail', (0, -0.2, 0.14), (0.015, 0.015, 0.09), rot=(0.9, 0, 0), m=fur, vertices=12)]
    for x in (-0.06, 0.06):
        for y in (0.05, -0.13):
            parts.append(add('cylinder', 'leg', (x, y, 0.05), (0.022, 0.022, 0.05), m=fur, vertices=12))
    o = join(parts, 'Cat'); smooth(o, 1)
    export('cat')

# ---------------- repellent props ----------------
def water_bag():
    clear()
    water = mat('Water', (0.7, 0.85, 1.0), 0.05, 0.0, 0.35)
    parts = [add('uv_sphere', 'bag', (0, 0, 0.09), (0.07, 0.07, 0.09), m=water, segments=24, ring_count=12),
             add('cylinder', 'knot', (0, 0, 0.19), (0.012, 0.012, 0.02), m=mat('Knot', (0.9, 0.9, 0.95), 0.4), vertices=12),
             add('cylinder', 'string', (0, 0, 0.32), (0.002, 0.002, 0.12), m=mat('String', (0.6, 0.5, 0.3)), vertices=6)]
    for i in range(4):  # coins inside
        parts.append(add('cylinder', f'coin{i}', (0.02 * math.cos(i * 1.6), 0.02 * math.sin(i * 1.6), 0.03 + i * 0.004), (0.012, 0.012, 0.001), rot=(0.3, 0.2 * i, 0), m=mat('Coin', (0.85, 0.65, 0.2), 0.3, 1.0), vertices=16))
    o = join(parts, 'WaterBag'); smooth(o, 1)
    export('water_bag')

def coffee_bowl():
    clear()
    parts = [add('cylinder', 'dish', (0, 0, 0.008), (0.045, 0.045, 0.008), m=mat('Dish', (0.9, 0.9, 0.88), 0.3), vertices=32),
             add('uv_sphere', 'grounds', (0, 0, 0.018), (0.032, 0.032, 0.014), m=mat('Coffee', (0.25, 0.15, 0.08), 0.95), segments=24, ring_count=10),
             add('cone', 'ember', (0, 0, 0.032), (1, 1, 1), m=mat('Ember', (1.0, 0.35, 0.05), 0.5), radius1=0.006, radius2=0.0, depth=0.01, vertices=8)]
    o = join(parts, 'CoffeeBowl'); bpy.ops.object.shade_smooth()
    export('coffee_bowl')

def deet():
    clear()
    parts = [add('cylinder', 'can', (0, 0, 0.06), (0.026, 0.026, 0.06), m=mat('DeetGreen', (0.15, 0.45, 0.25), 0.35, 0.3), vertices=32),
             add('cylinder', 'cap', (0, 0, 0.135), (0.014, 0.014, 0.015), m=mat('DeetCap', (0.9, 0.9, 0.9), 0.4), vertices=24),
             add('cube', 'nozzle', (0, 0.012, 0.15), (0.005, 0.008, 0.004), m=mat('DeetNozzle', (0.2, 0.2, 0.2))),
             add('cube', 'label', (0, 0, 0.06), (0.0265, 0.0265, 0.03), m=mat('DeetLabel', (0.95, 0.9, 0.5), 0.6))]
    o = join(parts, 'Deet'); bpy.ops.object.shade_smooth()
    export('deet')

def lemon_cloves():
    clear()
    parts = [add('uv_sphere', 'lemon', (0, 0, 0.032), (0.035, 0.035, 0.03), m=mat('Lemon', (0.95, 0.85, 0.15), 0.5), segments=24, ring_count=12)]
    for k in range(14):
        a, b = k * 2.4, 0.4 + (k % 5) * 0.5
        d = Vector((math.cos(a) * math.sin(b), math.sin(a) * math.sin(b), math.cos(b)))
        p = Vector((0, 0, 0.032)) + Vector((d.x * 0.035, d.y * 0.035, d.z * 0.03))
        parts.append(add('cylinder', f'clove{k}', tuple(p), (0.002, 0.002, 0.006), rot=d.to_track_quat('Z', 'Y').to_euler(), m=mat('Clove', (0.3, 0.2, 0.12)), vertices=6))
    o = join(parts, 'LemonCloves'); bpy.ops.object.shade_smooth()
    export('lemon_cloves')

def ultrasonic():
    clear()
    parts = [add('cube', 'box', (0, 0, 0.035), (0.03, 0.02, 0.035), m=mat('Ultra', (0.92, 0.92, 0.94), 0.4)),
             add('cylinder', 'speaker', (0, -0.021, 0.045), (0.012, 0.012, 0.002), rot=(math.pi / 2, 0, 0), m=mat('Grille', (0.2, 0.2, 0.2)), vertices=24),
             add('uv_sphere', 'led', (0.018, -0.021, 0.02), (0.003, 0.003, 0.003), m=mat('Led', (0.2, 1.0, 0.3), 0.2), segments=8, ring_count=6),
             add('cube', 'plug', (0, 0.025, 0.02), (0.006, 0.006, 0.004), m=mat('Plug', (0.3, 0.3, 0.3)))]
    o = join(parts, 'Ultrasonic'); bpy.ops.object.shade_smooth()
    export('ultrasonic')

def fan():
    clear()
    m_ = mat('FanWhite', (0.9, 0.9, 0.9), 0.4); blade = mat('FanBlade', (0.5, 0.7, 0.9), 0.3, 0.0, 0.7)
    parts = [add('cylinder', 'base', (0, 0, 0.01), (0.09, 0.09, 0.01), m=m_, vertices=32),
             add('cylinder', 'pole', (0, 0, 0.09), (0.012, 0.012, 0.08), m=m_, vertices=16),
             add('cylinder', 'motor', (0, -0.01, 0.2), (0.03, 0.03, 0.025), rot=(math.pi / 2, 0, 0), m=m_, vertices=24),
             add('torus', 'cage', (0, -0.04, 0.2), (1, 1, 1), rot=(math.pi / 2, 0, 0), m=m_, major_radius=0.13, minor_radius=0.004, major_segments=48, minor_segments=8)]
    for k in range(3):
        parts.append(add('cube', f'blade{k}', (0, -0.035, 0.2), (0.11, 0.002, 0.04), rot=(0, k * 2.094 + 0.5, 0), m=blade))
    for k in range(12):
        a = k * math.pi / 6
        parts.append(add('cylinder', f'rib{k}', (0.065 * math.cos(a), -0.04, 0.2 + 0.065 * math.sin(a)), (0.002, 0.002, 0.065), rot=(0, math.pi / 2 - a, 0), m=m_, vertices=6))
    o = join(parts, 'Fan'); bpy.ops.object.shade_smooth()
    export('fan')

def uv_zapper():
    clear()
    parts = [add('cube', 'top', (0, 0, 0.3), (0.06, 0.06, 0.012), m=mat('ZapBlack', (0.1, 0.1, 0.12), 0.5)),
             add('cube', 'bottom', (0, 0, 0.02), (0.06, 0.06, 0.012), m=mat('ZapBlack2', (0.1, 0.1, 0.12), 0.5)),
             add('cylinder', 'tube', (0, 0, 0.16), (0.014, 0.014, 0.13), m=mat('UVTube', (0.7, 0.4, 1.0), 0.2), vertices=24),
             add('cylinder', 'hook', (0, 0, 0.33), (0.004, 0.004, 0.02), m=mat('Hook', (0.6, 0.6, 0.65), 0.3, 1.0), vertices=8)]
    for k in range(10):
        a = k * math.pi / 5
        parts.append(add('cylinder', f'bar{k}', (0.05 * math.cos(a), 0.05 * math.sin(a), 0.16), (0.0025, 0.0025, 0.13), m=mat('Grid', (0.5, 0.5, 0.55), 0.4, 0.8), vertices=6))
    o = join(parts, 'UVZapper'); bpy.ops.object.shade_smooth()
    export('uv_zapper')

def vinegar_trap():
    clear()
    glass = mat('Jar', (0.9, 0.95, 1.0), 0.05, 0.0, 0.3)
    parts = [add('cylinder', 'jar', (0, 0, 0.05), (0.04, 0.04, 0.05), m=glass, vertices=32),
             add('cylinder', 'liquid', (0, 0, 0.02), (0.038, 0.038, 0.02), m=mat('Vinegar', (0.75, 0.55, 0.2), 0.1, 0.0, 0.85), vertices=32),
             add('cylinder', 'wrap', (0, 0, 0.101), (0.042, 0.042, 0.001), m=mat('Wrap', (0.95, 0.95, 1.0), 0.2, 0.0, 0.5), vertices=32)]
    o = join(parts, 'VinegarTrap'); bpy.ops.object.shade_smooth()
    export('vinegar_trap')

for fn in (hand, swatter, racket, newspaper, towel, vacuum, cat, water_bag, coffee_bowl, deet, lemon_cloves, ultrasonic, fan, uv_zapper, vinegar_trap):
    try: fn()
    except Exception as e: print('FAILED', fn.__name__, e)
print('ALL DONE')
