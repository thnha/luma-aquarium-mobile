#!/usr/bin/env python3
"""Cuts fake Crowntail spikes and a Double Tail slit into the Betta fin alpha.

Reads Art/Betta/Betta.fbx (binary FBX: mesh, UVs, skin weights, bind pose) and
Art/Betta/Textures/BETTA_BaseColor_Alpha.png, and writes next to it:
  BETTA_BaseColor_Crowntail.png   caudal, dorsal and anal edges cut into spikes
  BETTA_BaseColor_DoubleTail.png  caudal split by a slit over the middle Tail_* ray
Only alpha changes; RGB, mesh and rig stay the same. A missing .meta is copied from
the source texture (same import settings, new GUID).

How: every fin texel is mapped back to its bind-pose position on the side plane
(the medial fins lie in x = 0). Around each fin's pivot (Tail_Base, or the midpoint of
the Dorsal/Anal root bones) the painted alpha gives the fin edge radius per angle,
so depth = radius / edge radius runs 0 at the base to 1 at the painted edge.
  Crowntail: spikes every `spacing` metres of edge arc; between spikes the web stops
             at depth `web`, spikes taper to a point at the edge.
  Double Tail: a V slit centred on the Tail_ ray, opening from depth 0.12 to the edge.
The result is rougher than hand-painted; an artist can paint over the output.

Usage: python3 Tools/betta_fin_cuts.py   (needs numpy and Pillow)
"""
import os, re, struct, uuid, zlib
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets/_Project/Art/Betta")
FBX = os.path.join(ART, "Betta.fbx")
SRC = os.path.join(ART, "Textures/BETTA_BaseColor_Alpha.png")
BODY_GEOMETRY = "Plane.001"  # geometry of the BODY mesh

CROWN = {  # fin: (spike spacing along the edge in metres, web depth)
    "caudal": (0.016, 0.58),
    "dorsal": (0.013, 0.62),
    "anal": (0.013, 0.62),
}
SLIT_START = 0.12      # depth where the double-tail split begins
SLIT_UP = 0.2          # share of the angle Tail_ -> Tail_UpMid the slit takes at the edge
SLIT_DOWN = 0.5        # share of the angle Tail_ -> Tail_LowMid
EDGE_BINS = 720


# --- binary FBX -------------------------------------------------------------------

def read_fbx(path):
    data = open(path, "rb").read()
    wide = struct.unpack_from("<I", data, 23)[0] >= 7500

    def node(off):
        if wide:
            end, nprops, _ = struct.unpack_from("<QQQ", data, off); off += 24
        else:
            end, nprops, _ = struct.unpack_from("<III", data, off); off += 12
        nl = data[off]; off += 1
        name = data[off:off + nl].decode(); off += nl
        if end == 0:
            return None, off
        props = []
        for _ in range(nprops):
            t = chr(data[off]); off += 1
            if t in "YCILFD":
                fmt = {"Y": "<h", "C": "<?", "I": "<i", "L": "<q", "F": "<f", "D": "<d"}[t]
                props.append(struct.unpack_from(fmt, data, off)[0]); off += struct.calcsize(fmt)
            elif t in "fdlib":
                n, enc, clen = struct.unpack_from("<III", data, off); off += 12
                raw = data[off:off + clen]; off += clen
                if enc:
                    raw = zlib.decompress(raw)
                dt = {"f": "<f4", "d": "<f8", "l": "<i8", "i": "<i4", "b": "?"}[t]
                props.append(np.frombuffer(raw, dtype=dt, count=n))
            elif t in "SR":
                n = struct.unpack_from("<I", data, off)[0]; off += 4
                b = data[off:off + n]; off += n
                props.append(b.decode("utf-8", "replace") if t == "S" else b)
        children = []
        while off < end:
            c, off = node(off)
            if c is None:
                break
            children.append(c)
        return (name, props, children), end

    off, top = 27, {}
    while off < len(data) - 200:
        c, off = node(off)
        if c is None:
            break
        top[c[0]] = c
    return top


def child(n, key):
    return next(c for c in n[2] if c[0] == key)


def obj_name(o):
    return o[1][1].split("\x00")[0]


def load_rig():
    top = read_fbx(FBX)
    objs = top["Objects"][2]
    byid = {o[1][0]: o for o in objs}
    geo = next(o for o in objs if o[0] == "Geometry" and obj_name(o) == BODY_GEOMETRY)
    verts = child(geo, "Vertices")[1][0].reshape(-1, 3)
    pvi = child(geo, "PolygonVertexIndex")[1][0]
    uvl = child(geo, "LayerElementUV")
    uv = child(uvl, "UV")[1][0].reshape(-1, 2)
    uvi = child(uvl, "UVIndex")[1][0]  # ByPolygonVertex / IndexToDirect
    corner_vert = np.where(pvi < 0, ~pvi, pvi)
    tris, poly = [], []
    for k, i in enumerate(pvi):
        poly.append(k)
        if i < 0:
            tris += [(poly[0], poly[j], poly[j + 1]) for j in range(1, len(poly) - 1)]
            poly = []
    tris = np.array(tris)  # corner indices

    # Skin: Cluster deformers, each linked to its bone Model.
    bone_of = {}
    for c in top["Connections"][2]:
        if c[1][0] == "OO" and c[1][1] in byid and byid[c[1][1]][0] == "Model":
            bone_of.setdefault(c[1][2], obj_name(byid[c[1][1]]))
    bones, weights = [], []
    for o in objs:
        if o[0] == "Deformer" and o[1][2] == "Cluster":
            d = {c[0]: c[1] for c in o[2]}
            if "Indexes" not in d:
                continue
            w = np.zeros(len(verts))
            w[d["Indexes"][0]] = d["Weights"][0]
            bones.append(bone_of[o[1][0]]); weights.append(w)
    lead = np.array(bones)[np.array(weights).argmax(0)]

    pose = next(o for o in objs if o[0] == "Pose" and obj_name(o) == BODY_GEOMETRY)
    bind = {}
    for pn in pose[2]:
        if pn[0] == "PoseNode":
            d = {c[0]: c[1] for c in pn[2]}
            p = d["Matrix"][0].reshape(4, 4)[3, :3]
            bind[obj_name(byid[d["Node"][0]])] = np.array([p[2], p[1]])  # side plane (z back, y up)
    return verts, uv, uvi, corner_vert, tris, lead, bind


# --- fin texels -------------------------------------------------------------------

def fin_group(bone):
    if bone.startswith("Tail_"): return "caudal"
    if bone.startswith("Dorsal_"): return "dorsal"
    if bone.startswith("Anal_"): return "anal"
    return "other" if bone.startswith(("Pelvic_", "Pectoral_")) and not bone.endswith("_Base") else "body"


def rasterize_fins(size, verts, uv, uvi, corner_vert, tris, lead):
    """Per texel of the caudal/dorsal/anal UV islands: fin name and side-plane position."""
    group = np.array([fin_group(b) for b in lead])
    parent = np.arange(len(uv))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]; i = parent[i]
        return i
    for t in tris:
        a, b, c = uvi[t]
        parent[find(b)] = find(a); parent[find(c)] = find(a)
    island = np.array([find(uvi[t[0]]) for t in tris])
    # Island = fin when under 15% of it follows body bones (same rule as BettaMaskBaker).
    island_group = {}
    for k in np.unique(island):
        g = group[corner_vert[tris[island == k]].ravel()]
        names, counts = np.unique(g, return_counts=True)
        island_group[k] = "body" if (g == "body").mean() > 0.15 else names[counts.argmax()]

    pos = np.full((size, size, 2), np.nan, np.float32)
    fin = np.full((size, size), "", dtype=object)
    for t, k in zip(tris, island):
        g = island_group[k]
        if g not in CROWN:
            continue
        a, b, c = uv[uvi[t]] * size
        a, b, c = [np.array([p[0], size - p[1]]) for p in (a, b, c)]  # PNG rows run top-down
        p3 = verts[corner_vert[t]][:, [2, 1]]
        x0, y0 = np.maximum(np.floor(np.minimum(np.minimum(a, b), c)).astype(int), 0)
        x1, y1 = np.minimum(np.ceil(np.maximum(np.maximum(a, b), c)).astype(int), size - 1)
        den = (b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])
        if abs(den) < 1e-9 or x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        u = ((xs - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (ys - a[1])) / den
        v = ((b[0] - a[0]) * (ys - a[1]) - (xs - a[0]) * (b[1] - a[1])) / den
        w = 1 - u - v
        m = (u >= -0.01) & (v >= -0.01) & (w >= -0.01)
        q = w[..., None] * p3[0] + u[..., None] * p3[1] + v[..., None] * p3[2]
        rows, cols = (ys[m] - 0.5).astype(int), (xs[m] - 0.5).astype(int)
        pos[rows, cols] = q[m]
        fin[rows, cols] = g
    return pos, fin


def polar(pos, mask, pivot, alpha):
    """Angle, edge-arc bin and depth (0 base .. 1 painted edge) of the fin texels in mask."""
    d = pos[mask] - pivot
    r = np.hypot(d[:, 0], d[:, 1])
    theta = np.arctan2(d[:, 1], d[:, 0])
    bins = ((theta + np.pi) / (2 * np.pi) * EDGE_BINS).astype(int) % EDGE_BINS
    edge = np.zeros(EDGE_BINS)
    visible = alpha[mask] > 0.5
    np.maximum.at(edge, bins[visible], r[visible])
    # The painted edge is ragged: close small gaps, then smooth.
    edge = np.maximum.reduce([np.roll(edge, k) for k in range(-3, 4)])
    edge = np.mean([np.roll(edge, k) for k in range(-4, 5)], axis=0)
    return theta, bins, edge, r / np.maximum(edge[bins], 1e-4)


def ray_angle(bind, chain):
    d = bind[chain + "05"] - bind["Tail_Base"]
    return np.arctan2(d[1], d[0])


def coverage(margin):
    """Soft 0..1 from a margin in depth units (> 0 keeps), so alpha clip gets a clean edge."""
    return np.clip(0.5 + margin / 0.02, 0, 1)


def crowntail(pos, fin, alpha, bind):
    keep = np.ones_like(alpha)
    pivots = {
        "caudal": bind["Tail_Base"],
        "dorsal": (bind["Dorsal_A_01"] + bind["Dorsal_B_01"]) / 2,
        "anal": (bind["Anal_A_01"] + bind["Anal_B_01"]) / 2,
    }
    for i, (name, (spacing, web)) in enumerate(CROWN.items()):
        m = fin == name
        _, bins, edge, depth = polar(pos, m, pivots[name], alpha)
        arc = np.cumsum(edge * (2 * np.pi / EDGE_BINS))  # metres along the edge
        s = arc[bins] / spacing
        spike = np.round(s)
        e = np.abs(s - spike) * 2  # 0 on a spike, 1 halfway to the next
        length = 0.88 + 0.12 * ((np.sin((spike + 100 * i) * 12.9898 + 78.233) * 43758.5453) % 1.0)
        limit = web + (length - web) * (1 - e) ** 2.2
        keep[m] = coverage(limit - depth)
    return keep


def doubletail(pos, fin, alpha, bind):
    keep = np.ones_like(alpha)
    m = fin == "caudal"
    theta, bins, edge, depth = polar(pos, m, bind["Tail_Base"], alpha)
    centre = ray_angle(bind, "Tail_")
    up = (ray_angle(bind, "Tail_UpMid_") - centre) * SLIT_UP
    down = (centre - ray_angle(bind, "Tail_LowMid_")) * SLIT_DOWN
    opening = np.sqrt(np.clip((depth - SLIT_START) / (1 - SLIT_START), 0, 1))  # V that rounds out
    off = theta - centre
    inside = np.where(off > 0, up * opening - off, down * opening + off)  # radians, > 0 in the slit
    keep[m] = coverage(-inside * depth)  # radians -> roughly depth units at this radius
    return keep


def write(rgba, keep, name):
    out = rgba.copy()
    out[..., 3] = np.round(rgba[..., 3] * keep).astype(np.uint8)
    path = os.path.join(os.path.dirname(SRC), name)
    Image.fromarray(out).save(path, optimize=True)
    meta = path + ".meta"
    if not os.path.exists(meta):
        text = open(SRC + ".meta").read()
        open(meta, "w").write(re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, text, count=1))
    print("wrote", os.path.relpath(path, ROOT))


def main():
    rgba = np.array(Image.open(SRC).convert("RGBA"))
    alpha = rgba[..., 3].astype(np.float32) / 255
    verts, uv, uvi, corner_vert, tris, lead, bind = load_rig()
    pos, fin = rasterize_fins(rgba.shape[0], verts, uv, uvi, corner_vert, tris, lead)
    write(rgba, crowntail(pos, fin, alpha, bind), "BETTA_BaseColor_Crowntail.png")
    write(rgba, doubletail(pos, fin, alpha, bind), "BETTA_BaseColor_DoubleTail.png")


if __name__ == "__main__":
    main()
