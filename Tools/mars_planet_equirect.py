#!/usr/bin/env python3
"""Reamostra a textura do modelo 3D NASA "Planet Mars" (esfera de cubo, planificação em cruz) para um mapa equirretangular.

Uso: python3 Tools/mars_planet_equirect.py <Mars_Planet_24881.glb> <saida.jpg> [largura=2048]

Entrada: ArtSource/Mars/R-MAR-05/original/Mars_Planet_24881.glb (NASA/JPL-Caltech, ver SOURCE.md).
Saída: mapa 2:1 com longitude 0–360°L da esquerda para a direita e latitude +90° no topo, compatível com
MerLandingGeometry.Sphere e MarsOriginChoreography.Direction. Só a textura difusa é usada (sem normais).

Calibração (conferida a olho contra Hellas, Syrtis Major, Valles Marineris e Olympus Mons; erro de poucos graus):
eixo norte = normalize(0,153; 0,988; 0) no espaço do modelo (média das duas calotas) e deslocamento de longitude de −86°.
Não use o mapa para medir posições: é ilustrativo.
"""
import io, json, struct, sys
import numpy as np
from PIL import Image

NORTH = np.array([0.153, 0.988, 0.0]); NORTH /= np.linalg.norm(NORTH)
LON_OFFSET_DEGREES = -86.0


def read_glb(path):
    d = open(path, "rb").read()
    off, g, binary = 12, None, None
    while off < len(d):
        length, kind = struct.unpack("<II", d[off:off + 8])
        if kind == 0x4E4F534A: g = json.loads(d[off + 8:off + 8 + length])
        elif kind == 0x004E4942: binary = d[off + 8:off + 8 + length]
        off += 8 + length
    return g, binary


def accessor(g, binary, index):
    a = g["accessors"][index]; v = g["bufferViews"][a["bufferView"]]
    comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}[a["type"]]
    dtype = {5126: np.float32, 5123: np.uint16, 5125: np.uint32}[a["componentType"]]
    arr = np.frombuffer(binary, dtype=dtype, count=a["count"] * comps, offset=v.get("byteOffset", 0) + a.get("byteOffset", 0))
    return arr.reshape(a["count"], comps) if comps > 1 else arr


def main(glb, out, width=2048):
    g, binary = read_glb(glb)
    prim = g["meshes"][0]["primitives"][0]
    pos = accessor(g, binary, prim["attributes"]["POSITION"]).astype(np.float64)
    uv = accessor(g, binary, prim["attributes"]["TEXCOORD_0"]).astype(np.float64)
    idx = accessor(g, binary, prim["indices"]).astype(np.int64).reshape(-1, 3)
    texture = g["materials"][prim["material"]]["pbrMetallicRoughness"]["baseColorTexture"]["index"]
    view = g["bufferViews"][g["images"][g["textures"][texture]["source"]]["bufferView"]]
    img = np.asarray(Image.open(io.BytesIO(binary[view.get("byteOffset", 0):view.get("byteOffset", 0) + view["byteLength"]])).convert("RGB")).astype(np.float64)
    h, w, _ = img.shape
    v0, v1, v2 = pos[idx[:, 0]], pos[idx[:, 1]], pos[idx[:, 2]]
    cent = (v0 + v1 + v2) / 3; cent /= np.linalg.norm(cent, axis=1)[:, None]
    nrm = np.cross(v1 - v0, v2 - v0); nrm /= np.linalg.norm(nrm, axis=1)[:, None]
    u0, u1, u2 = uv[idx[:, 0]], uv[idx[:, 1]], uv[idx[:, 2]]
    ref = np.array([0, 0, 1.0]); r = ref - NORTH * (ref @ NORTH); r /= np.linalg.norm(r); e = np.cross(NORTH, r)
    oh = width // 2
    lon = np.radians((np.arange(width) + 0.5) / width * 360 + LON_OFFSET_DEGREES)
    lat = np.radians(90 - (np.arange(oh) + 0.5) / oh * 180)
    LON, LAT = np.meshgrid(lon, lat)
    D = np.cos(LAT.ravel())[:, None] * (np.cos(LON.ravel())[:, None] * r + np.sin(LON.ravel())[:, None] * e) + np.sin(LAT.ravel())[:, None] * NORTH
    out_px = np.zeros((len(D), 3))
    for s in range(0, len(D), 8000):
        d = D[s:s + 8000]; t = np.argmax(d @ cent.T, axis=1)
        a = v0[t]; e1 = v1[t] - a; e2 = v2[t] - a
        k = (nrm[t] * a).sum(1) / np.maximum((nrm[t] * d).sum(1), 1e-9); p = d * k[:, None] - a
        d11, d12, d22 = (e1 * e1).sum(1), (e1 * e2).sum(1), (e2 * e2).sum(1); p1, p2 = (p * e1).sum(1), (p * e2).sum(1)
        den = d11 * d22 - d12 * d12; b = (d22 * p1 - d12 * p2) / den; c = (d11 * p2 - d12 * p1) / den
        t_uv = (1 - b - c)[:, None] * u0[t] + b[:, None] * u1[t] + c[:, None] * u2[t]
        x = np.clip(t_uv[:, 0] * w, 0, w - 1.001); y = np.clip(t_uv[:, 1] * h, 0, h - 1.001)
        xi, yi = x.astype(int), y.astype(int); fx, fy = (x - xi)[:, None], (y - yi)[:, None]
        xn, yn = np.minimum(xi + 1, w - 1), np.minimum(yi + 1, h - 1)
        out_px[s:s + 8000] = img[yi, xi] * (1 - fx) * (1 - fy) + img[yi, xn] * fx * (1 - fy) + img[yn, xi] * (1 - fx) * fy + img[yn, xn] * fx * fy
    Image.fromarray(out_px.reshape(oh, width, 3).clip(0, 255).astype(np.uint8)).save(out, quality=90)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 2048)
