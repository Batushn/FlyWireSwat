#!/usr/bin/env python3
"""
Extract the Drosophila looming-escape circuit from the FlyWire FAFB v783 connectome.

Seed populations (from the literature, Ache et al. 2019 / von Reyn et al. 2014 / Shiu et al. 2024):
  visual projection neurons  : LC4, LPLC2, LPLC1, LC6, LC16, LC22, LC15
  descending / escape neurons: DNp01 (= Giant Fiber, GF), DNp02, DNp04, DNp06, DNp11
We then add every neuron that projects to a descending seed with >= MIN_SYN synapses
(1 hop upstream) and keep all connections among the resulting set.

Output: Assets/Data/escape_circuit.json  (small, loads in Unity via Newtonsoft)
"""
import csv, gzip, json, os, sys, collections

RAW = os.path.join(os.path.dirname(__file__), '..', 'Data', 'raw')
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'Connectome', 'escape_circuit.json')
MIN_SYN_UPSTREAM = 10     # min synapses for a non-seed neuron to be pulled in via a DN
MIN_SYN_EDGE = 3          # min synapses for an edge to be kept

VPN_TYPES = {'LC4', 'LPLC2', 'LPLC1', 'LC6', 'LC16', 'LC22', 'LC15'}
DN_TYPES = {'DNp01', 'DNp02', 'DNp04', 'DNp06', 'DNp11'}
SEED_TYPES = VPN_TYPES | DN_TYPES

def rd(name):
    with gzip.open(os.path.join(RAW, name), 'rt') as f:
        yield from csv.DictReader(f)

print('reading cell types...')
ctype = {}
for r in rd('consolidated_cell_types.csv.gz'):
    ctype[r['root_id']] = r['primary_type']

print('reading classification...')
cls = {r['root_id']: r for r in rd('classification.csv.gz')}
print('reading neurons (nt)...')
nt = {r['root_id']: (r['nt_type'], float(r['nt_type_score'] or 0)) for r in rd('neurons.csv.gz')}

seeds = {rid for rid, t in ctype.items() if t in SEED_TYPES}
dns = {rid for rid, t in ctype.items() if t in DN_TYPES}
print(f'seed neurons: {len(seeds)}  (descending: {len(dns)})')

print('scanning connections (3.7M rows)...')
rows = []
upstream = collections.Counter()
for r in rd('connections.csv.gz'):
    pre, post, syn = r['pre_root_id'], r['post_root_id'], int(r['syn_count'])
    if post in dns and syn >= MIN_SYN_UPSTREAM:
        upstream[pre] += syn
    if pre in seeds or post in seeds:
        rows.append((pre, post, syn, r['nt_type'], r['neuropil']))

keep = set(seeds) | set(upstream)
print(f'kept neuron set: {len(keep)}')

# second pass restricted to kept set (need edges among upstream partners too)
edges = collections.defaultdict(lambda: [0, collections.Counter()])
for r in rd('connections.csv.gz'):
    pre, post = r['pre_root_id'], r['post_root_id']
    if pre in keep and post in keep:
        syn = int(r['syn_count'])
        e = edges[(pre, post)]
        e[0] += syn
        e[1][r['nt_type']] += syn

ids = sorted(keep, key=lambda i: (0 if ctype.get(i) in DN_TYPES else 1 if ctype.get(i) in VPN_TYPES else 2, ctype.get(i, ''), i))
idx = {rid: i for i, rid in enumerate(ids)}

SIGN = {'ACH': 1, 'GABA': -1, 'GLUT': -1, 'DA': 0.3, 'SER': 0.3, 'OCT': 0.3}
neurons = []
for rid in ids:
    c = cls.get(rid, {})
    n = nt.get(rid, ('', 0))
    neurons.append({
        'id': rid,
        'type': ctype.get(rid, ''),
        'nt': n[0], 'ntScore': n[1],
        'side': c.get('side', ''),
        'superClass': c.get('super_class', ''),
        'cls': c.get('class', ''),
        'isSeed': rid in seeds,
        'role': 'DN' if rid in dns else ('VPN' if ctype.get(rid) in VPN_TYPES else 'other'),
    })

out_edges = []
for (pre, post), (syn, nts) in edges.items():
    if syn < MIN_SYN_EDGE: continue
    nt_dom = nts.most_common(1)[0][0]
    out_edges.append({'pre': idx[pre], 'post': idx[post], 'syn': syn, 'nt': nt_dom, 'sign': SIGN.get(nt_dom, 0)})
out_edges.sort(key=lambda e: (e['post'], e['pre']))

typecount = collections.Counter(n['type'] for n in neurons)
summary = {
    'source': 'FlyWire FAFB v783 (codex.flywire.ai), CC-BY 4.0',
    'neuronCount': len(neurons), 'edgeCount': len(out_edges),
    'totalSynapses': sum(e['syn'] for e in out_edges),
    'typeCounts': dict(typecount.most_common(40)),
}
os.makedirs(os.path.dirname(OUT), exist_ok=True)
json.dump({'summary': summary, 'neurons': neurons, 'edges': out_edges}, open(OUT, 'w'))
print(json.dumps(summary, indent=1))
print('wrote', OUT, os.path.getsize(OUT)//1024, 'KB')

# quick GF input report
for gf in [i for i, n in enumerate(neurons) if n['type'] == 'DNp01']:
    inp = collections.Counter()
    for e in out_edges:
        if e['post'] == gf: inp[neurons[e['pre']]['type'] or '?'] += e['syn']
    print('GF', neurons[gf]['side'], 'inputs (syn by type):', inp.most_common(12))
