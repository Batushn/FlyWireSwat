#!/usr/bin/env python3
"""Download CC0 assets from Poly Haven (https://polyhaven.com) into Assets/ThirdParty/PolyHaven and write CREDITS.md."""
import json, os, sys, urllib.request
opener = urllib.request.build_opener(); opener.addheaders = [("User-Agent", "FlyWireSwat/1.0 (asset fetch)")]; urllib.request.install_opener(opener)

ROOT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'ThirdParty', 'PolyHaven')
MODELS = ['dining_table', 'dining_chair_02', 'wooden_cutting_board', 'wooden_bowl_02', 'food_apple_01', 'lemon', 'bananas',
          'croissant', 'hamburger_buns', 'carrot_cake', 'tea_set_01', 'metal_jug', 'wine_bottles_01', 'pot_enamel_01',
          'electric_stove', 'modern_ceiling_lamp_01', 'rollershutter_window_01', 'all_purpose_cleaner', 'wooden_spoon',
          'wicker_basket_01', 'yellow_onion', 'food_pears_asian_01', 'modern_wooden_cabinet', 'desk_lamp_arm_01', 'wooden_crate_01']
TEXTURES = ['wood_floor_worn', 'plastered_wall_02', 'kitchen_wood', 'floor_tiles_06', 'white_plaster_rough_02']
HDRIS = ['kloofendal_48d_partly_cloudy_puresky']

def api(url):
    with urllib.request.urlopen(url, timeout=60) as r: return json.load(r)

def dl(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0: return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    print('  ', os.path.basename(path)); urllib.request.urlretrieve(url, path)

credits = ['# Third-party assets', '', 'All assets below are from [Poly Haven](https://polyhaven.com), licensed **CC0 1.0** (public domain). Credit is given anyway - thank you!', '']
meta = api('https://api.polyhaven.com/assets')

for m in MODELS:
    print('model', m)
    f = api(f'https://api.polyhaven.com/files/{m}')
    d = os.path.join(ROOT, 'Models', m)
    dl(f['fbx']['1k']['fbx']['url'], os.path.join(d, f'{m}.fbx'))
    for key in ('Diffuse', 'nor_gl', 'arm'):
        if key in f and '1k' in f[key]:
            fmt = 'jpg' if 'jpg' in f[key]['1k'] else list(f[key]['1k'].keys())[0]
            dl(f[key]['1k'][fmt]['url'], os.path.join(d, f'{m}_{key.lower()}.{fmt}'))
    a = meta.get(m, {}); credits.append(f"- Model `{m}` by {', '.join(a.get('authors', {}).keys()) or 'Poly Haven'} - https://polyhaven.com/a/{m}")

for t in TEXTURES:
    print('texture', t)
    f = api(f'https://api.polyhaven.com/files/{t}')
    d = os.path.join(ROOT, 'Textures', t)
    for key in ('Diffuse', 'nor_gl', 'arm', 'Rough', 'AO'):
        if key in f and '2k' in f[key] and 'jpg' in f[key]['2k']:
            dl(f[key]['2k']['jpg']['url'], os.path.join(d, f'{t}_{key.lower()}.jpg'))
    a = meta.get(t, {}); credits.append(f"- Texture `{t}` by {', '.join(a.get('authors', {}).keys()) or 'Poly Haven'} - https://polyhaven.com/a/{t}")

for h in HDRIS:
    print('hdri', h)
    f = api(f'https://api.polyhaven.com/files/{h}')
    dl(f['hdri']['2k']['hdr']['url'], os.path.join(ROOT, 'HDRIs', f'{h}_2k.hdr'))
    a = meta.get(h, {}); credits.append(f"- HDRI `{h}` by {', '.join(a.get('authors', {}).keys()) or 'Poly Haven'} - https://polyhaven.com/a/{h}")

os.makedirs(ROOT, exist_ok=True)
open(os.path.join(ROOT, 'CREDITS.md'), 'w').write('\n'.join(credits) + '\n')
print('done')
