"""Procedural, seamless PBR textures for the MER rover (base color, normal, ORM).

Atlas: 2048 px, 4x4 tiles of 512 px. Solar: separate tileable 1024 px set.
Everything is generated with numpy (periodic FFT noise -> seamless tiles).
Arrays are bottom-up (row 0 = v 0), matching Blender image pixels.
Normals use the OpenGL convention (+Y green up), as glTF and Unity expect.
"""
import numpy as np

RNG = np.random.default_rng(1977)


def fnoise(n, beta=2.0, seed=None, aniso=(1.0, 1.0), fmin=1.0):
    """Periodic Gaussian random field with 1/f^beta spectrum, normalized 0..1."""
    rng = np.random.default_rng(seed) if seed is not None else RNG
    fx = np.fft.fftfreq(n)[None, :] * n * aniso[0]
    fy = np.fft.fftfreq(n)[:, None] * n * aniso[1]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0
    amp = 1.0 / np.maximum(f, fmin) ** (beta / 2.0)
    amp[0, 0] = 0.0
    ph = rng.normal(size=(n, n)) + 1j * rng.normal(size=(n, n))
    img = np.real(np.fft.ifft2(ph * amp))
    img -= img.min()
    img /= max(img.max(), 1e-9)
    return img


def band(n, lo, hi, seed=None, aniso=(1.0, 1.0)):
    """Band-limited periodic noise (frequencies lo..hi cycles per tile)."""
    rng = np.random.default_rng(seed) if seed is not None else RNG
    fx = np.fft.fftfreq(n)[None, :] * n * aniso[0]
    fy = np.fft.fftfreq(n)[:, None] * n * aniso[1]
    f = np.sqrt(fx * fx + fy * fy)
    amp = ((f >= lo) & (f <= hi)).astype(float)
    ph = rng.normal(size=(n, n)) + 1j * rng.normal(size=(n, n))
    img = np.real(np.fft.ifft2(ph * amp))
    img -= img.min()
    img /= max(img.max(), 1e-9)
    return img


def ridged(n, lo, hi, seed=None):
    b = band(n, lo, hi, seed)
    return 1.0 - np.abs(2.0 * b - 1.0)


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    nx = -dx * strength
    ny = -dy * strength
    nz = np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / ln * 0.5 + 0.5, ny / ln * 0.5 + 0.5, nz / ln * 0.5 + 0.5], axis=-1)


def col(c):
    return np.array(c, dtype=float)[None, None, :]


def lerp(a, b, t):
    return a + (b - a) * t[..., None]


def grid_lines(n, count, width, offset=0.0):
    """Periodic line mask (1 on lines) in both axes; count lines per tile."""
    x = (np.arange(n) / n * count + offset) % 1.0
    d = np.minimum(x, 1.0 - x) * n / count
    m = np.clip(1.0 - d / max(width, 1e-6), 0.0, 1.0)
    return m


def dust(n, seed, amount):
    """Mars dust mask: low-frequency patches + fine grain."""
    lo = fnoise(n, 3.0, seed)
    fine = fnoise(n, 1.2, seed + 1)
    m = np.clip((lo - (1.0 - amount)) * 2.5 + fine * 0.25 * amount, 0.0, 1.0)
    return m


DUST = (0.56, 0.38, 0.24)


class Tile:
    def __init__(self, n):
        self.n = n
        self.albedo = np.zeros((n, n, 3))
        self.height = np.zeros((n, n))
        self.rough = np.full((n, n), 0.5)
        self.metal = np.zeros((n, n))
        self.ao = np.ones((n, n))
        self.nstrength = 4.0


def apply_dust(t, seed, amount, color=DUST):
    m = dust(t.n, seed, amount)
    t.albedo = lerp(t.albedo, col(color) * (0.85 + 0.3 * fnoise(t.n, 1.0, seed + 5))[..., None], m * 0.85)
    t.rough = t.rough * (1 - m) + 0.85 * m
    t.metal = t.metal * (1 - m * 0.9)
    t.height = t.height + m * 0.15 * fnoise(t.n, 0.8, seed + 7)


# ---------------------------------------------------------------- atlas tiles
def t_gold(n):
    t = Tile(n)
    cr = ridged(n, 10, 32, 11) * 0.45 + ridged(n, 28, 80, 12) * 0.55
    big = fnoise(n, 3.0, 13)
    base = lerp(col((0.36, 0.24, 0.10)), col((0.62, 0.46, 0.24)), cr * 0.45 + big * 0.55)
    seams = np.maximum(grid_lines(n, 3, 3.0, 0.17)[None, :], grid_lines(n, 2, 3.0, 0.41)[:, None])
    tape = np.maximum(grid_lines(n, 3, 9.0, 0.17)[None, :], grid_lines(n, 2, 9.0, 0.41)[:, None])
    t.albedo = lerp(base, col((0.70, 0.52, 0.20)), tape * 0.6)
    t.albedo = lerp(t.albedo, col((0.20, 0.13, 0.05)), seams * 0.8)
    t.height = cr * 0.6 + big * 0.4 - seams * 0.5 + tape * 0.15
    t.rough = 0.22 + 0.25 * (1 - cr) + tape * 0.15
    t.metal = np.full((n, n), 1.0) - seams * 0.5
    t.nstrength = 4.5
    apply_dust(t, 14, 0.45)
    return t


def t_kapton(n):
    t = Tile(n)
    w = ridged(n, 4, 16, 21)
    t.albedo = lerp(col((0.035, 0.03, 0.025)), col((0.10, 0.08, 0.06)), w)
    seams = grid_lines(n, 2, 2.0, 0.3)
    t.albedo = lerp(t.albedo, col((0.25, 0.20, 0.12)), seams * 0.7)
    t.height = w * 0.8 - seams * 0.3
    t.rough = 0.35 + 0.2 * w
    t.metal = np.full((n, n), 0.15)
    t.nstrength = 3.5
    apply_dust(t, 22, 0.40)
    return t


def t_white(n):
    t = Tile(n)
    sp = fnoise(n, 0.6, 31)
    big = fnoise(n, 3.0, 32)
    t.albedo = lerp(col((0.70, 0.68, 0.63)), col((0.86, 0.84, 0.80)), big * 0.6 + sp * 0.4)
    scr = (band(n, 40, 120, 33, aniso=(1.0, 0.15)) > 0.82).astype(float)
    t.albedo = lerp(t.albedo, col((0.55, 0.52, 0.47)), scr * 0.5)
    t.height = sp * 0.2 - scr * 0.3
    t.rough = 0.55 + 0.1 * sp
    t.nstrength = 3.0
    apply_dust(t, 34, 0.50)
    return t


def t_alu(n):
    t = Tile(n)
    br = fnoise(n, 1.0, 41, aniso=(0.02, 1.0))
    t.albedo = lerp(col((0.50, 0.50, 0.49)), col((0.72, 0.72, 0.70)), br)
    t.height = br * 0.3
    t.rough = 0.28 + 0.15 * br
    t.metal = np.full((n, n), 1.0)
    t.nstrength = 2.0
    apply_dust(t, 42, 0.35)
    return t


def t_struct(n):
    t = Tile(n)
    big = fnoise(n, 2.6, 51)
    t.albedo = lerp(col((0.30, 0.20, 0.12)), col((0.48, 0.34, 0.21)), big)
    edge = band(n, 30, 90, 52) > 0.78
    t.albedo = lerp(t.albedo, col((0.62, 0.55, 0.45)), edge.astype(float) * 0.35)
    t.height = big * 0.3 + edge * 0.1
    t.rough = 0.5
    t.metal = np.full((n, n), 0.45)
    apply_dust(t, 53, 0.55)
    return t


def t_wheel(n):
    t = Tile(n)
    big = fnoise(n, 2.4, 61)
    t.albedo = lerp(col((0.24, 0.13, 0.08)), col((0.40, 0.24, 0.15)), big)
    t.height = fnoise(n, 1.0, 62) * 0.3
    t.rough = 0.55
    t.metal = np.full((n, n), 0.55)
    apply_dust(t, 63, 0.6, color=(0.46, 0.30, 0.20))
    return t


def t_copper(n):
    t = Tile(n)
    v = np.arange(n)[:, None] / n
    rib = (np.sin(v * np.pi * 2 * 24) * 0.5 + 0.5) * np.ones((1, n))
    big = fnoise(n, 2.5, 71)
    t.albedo = lerp(col((0.55, 0.24, 0.08)), col((0.85, 0.48, 0.22)), rib * 0.5 + big * 0.5)
    t.height = rib
    t.rough = 0.30 + 0.15 * big
    t.metal = np.full((n, n), 1.0)
    t.nstrength = 6.0
    apply_dust(t, 72, 0.30)
    return t


def t_darkmetal(n):
    t = Tile(n)
    big = fnoise(n, 2.0, 81)
    t.albedo = lerp(col((0.035, 0.035, 0.04)), col((0.11, 0.105, 0.10)), big)
    t.height = fnoise(n, 0.9, 82) * 0.2
    t.rough = 0.38 + 0.2 * big
    t.metal = np.full((n, n), 0.6)
    apply_dust(t, 83, 0.30)
    return t


def t_honeycomb(n):
    t = Tile(n)
    # hexagonal cells, periodic: 16 x ~9.24 cells per tile
    y, x = np.mgrid[0:n, 0:n] / n
    cells = 16
    qx = x * cells
    qy = y * cells * 2 / np.sqrt(3)
    qy = qy * (np.round(cells * 2 / np.sqrt(3)) / (cells * 2 / np.sqrt(3)))
    row = np.floor(qy)
    qx2 = qx + (row % 2) * 0.5
    fx = qx2 - np.floor(qx2) - 0.5
    fy = qy - row - 0.5
    d = np.maximum(np.abs(fx), (np.abs(fx) * 0.5 + np.abs(fy) * 0.866))
    wall = (d > 0.42).astype(float)
    t.albedo = lerp(col((0.22, 0.21, 0.19)), col((0.46, 0.44, 0.40)), wall)
    t.height = wall * 0.8
    t.rough = 0.6
    t.metal = wall * 0.6
    t.nstrength = 5.0
    apply_dust(t, 91, 0.35)
    return t


def t_beige(n):
    t = Tile(n)
    big = fnoise(n, 2.4, 101)
    t.albedo = lerp(col((0.58, 0.54, 0.46)), col((0.78, 0.74, 0.66)), big)
    t.height = fnoise(n, 0.8, 102) * 0.2
    t.rough = 0.5
    t.metal = np.full((n, n), 0.2)
    apply_dust(t, 103, 0.40)
    return t


def t_lens(n):
    t = Tile(n)
    big = fnoise(n, 3.0, 111)
    t.albedo = lerp(col((0.008, 0.01, 0.014)), col((0.03, 0.035, 0.05)), big)
    t.rough = 0.06 + 0.04 * big
    t.metal = np.zeros((n, n))
    return t


def t_caltarget(n):
    """Pancam calibration target face (sundial style): colour chips + grey rings."""
    t = Tile(n)
    y, x = (np.mgrid[0:n, 0:n] + 0.5) / n
    cx, cy = x - 0.5, y - 0.5
    r = np.sqrt(cx * cx + cy * cy)
    a = np.full((n, n, 3), 0.18)
    rings = [(0.40, 0.88), (0.30, 0.45), (0.20, 0.08)]
    for rr, g in rings:
        a[r < rr] = g
    chips = [((0.1, 0.1), (0.75, 0.15, 0.10)), ((0.9, 0.1), (0.15, 0.55, 0.20)),
             ((0.1, 0.9), (0.15, 0.25, 0.70)), ((0.9, 0.9), (0.85, 0.75, 0.15))]
    for (px, py), c in chips:
        m = (np.abs(x - px) < 0.085) & (np.abs(y - py) < 0.085)
        a[m] = c
    t.albedo = a
    t.height = (r < 0.40) * 0.3
    t.rough = 0.6
    apply_dust(t, 121, 0.20)
    return t


def t_cable(n):
    t = Tile(n)
    v = np.arange(n)[:, None] / n * np.ones((1, n))
    u = np.ones((n, 1)) * np.arange(n)[None, :] / n
    braid = (np.sin((u + v) * np.pi * 2 * 40) * np.sin((u - v) * np.pi * 2 * 40)) * 0.5 + 0.5
    t.albedo = lerp(col((0.06, 0.06, 0.06)), col((0.20, 0.19, 0.17)), braid)
    tie = grid_lines(n, 4, 6.0)[:, None] * np.ones((1, n))
    t.albedo = lerp(t.albedo, col((0.80, 0.78, 0.72)), tie)
    t.height = braid * 0.5 + tie * 0.5
    t.rough = 0.6
    t.nstrength = 5.0
    apply_dust(t, 131, 0.35)
    return t


def t_deckframe(n):
    t = Tile(n)
    big = fnoise(n, 2.6, 141)
    t.albedo = lerp(col((0.09, 0.085, 0.08)), col((0.20, 0.18, 0.16)), big)
    y, x = (np.mgrid[0:n, 0:n] + 0.5) / n
    k = 16
    fx = (x * k) % 1.0 - 0.5
    fy = (y * k) % 1.0 - 0.5
    rv = np.sqrt(fx * fx + fy * fy)
    rivet = np.clip(1 - rv / 0.16, 0, 1) * ((np.floor(y * k) % 4) == 0)
    t.albedo = lerp(t.albedo, col((0.45, 0.43, 0.40)), (rivet > 0).astype(float) * 0.8)
    t.height = big * 0.2 + rivet
    t.rough = 0.55
    t.metal = (rivet > 0) * 0.8 + 0.1
    t.nstrength = 6.0
    apply_dust(t, 142, 0.45)
    return t


def t_dish(n):
    t = Tile(n)
    big = fnoise(n, 2.4, 151)
    t.albedo = lerp(col((0.03, 0.03, 0.03)), col((0.09, 0.085, 0.08)), big)
    t.height = fnoise(n, 1.0, 152) * 0.2
    t.rough = 0.65
    apply_dust(t, 153, 0.35)
    return t


def t_stripe(n):
    """Brown/orange thermal tape used on the mast."""
    t = Tile(n)
    big = fnoise(n, 2.4, 161)
    t.albedo = lerp(col((0.30, 0.16, 0.06)), col((0.55, 0.32, 0.12)), big)
    t.height = ridged(n, 10, 40, 162) * 0.4
    t.rough = 0.35
    t.metal = np.full((n, n), 0.7)
    apply_dust(t, 163, 0.25)
    return t


def t_rust(n):
    """Heavily dusted surface (bottom plates, wheel-well areas)."""
    t = Tile(n)
    t.albedo = col(DUST) * (0.7 + 0.5 * fnoise(n, 1.6, 171))[..., None] * np.ones((n, n, 3))
    t.height = fnoise(n, 1.2, 172)
    t.rough = 0.9
    t.nstrength = 3.0
    return t


TILES = [
    ("gold", t_gold), ("kapton", t_kapton), ("white", t_white), ("alu", t_alu),
    ("struct", t_struct), ("wheel", t_wheel), ("copper", t_copper), ("darkmetal", t_darkmetal),
    ("honeycomb", t_honeycomb), ("beige", t_beige), ("lens", t_lens), ("caltarget", t_caltarget),
    ("cable", t_cable), ("deckframe", t_deckframe), ("dish", t_dish), ("stripe", t_stripe),
]
TILE_INDEX = {name: i for i, (name, _) in enumerate(TILES)}
GRID = 4


def build_atlas(tile_px=512):
    size = tile_px * GRID
    albedo = np.zeros((size, size, 3))
    normal = np.zeros((size, size, 3))
    orm = np.zeros((size, size, 3))
    for i, (name, fn) in enumerate(TILES):
        t = fn(tile_px)
        cx, cy = i % GRID, i // GRID
        sl = (slice(cy * tile_px, (cy + 1) * tile_px), slice(cx * tile_px, (cx + 1) * tile_px))
        albedo[sl] = np.clip(t.albedo, 0, 1)
        normal[sl] = normal_from_height(t.height, t.nstrength)
        orm[sl] = np.stack([t.ao, np.clip(t.rough, 0.02, 1), np.clip(t.metal, 0, 1)], axis=-1)
    return albedo, normal, orm


def build_solar(n=1024):
    """Tileable solar array: one tile = 0.5 m. GaAs cells ~ 4 x 6.4 cm."""
    y, x = (np.mgrid[0:n, 0:n] + 0.5) / n
    cu, cv = 12, 8                       # cells per tile (0.5 m / 12 = 4.2 cm; 0.5/8 = 6.3 cm)
    fx = (x * cu) % 1.0
    fy = (y * cv) % 1.0
    gap_u = np.minimum(fx, 1 - fx) * n / cu
    gap_v = np.minimum(fy, 1 - fy) * n / cv
    gap = np.clip(1.0 - np.minimum(gap_u, gap_v) / 2.5, 0, 1)
    finger = (np.abs(((x * cu * 14) % 1.0) - 0.5) > 0.47).astype(float) * (1 - gap)
    bus = (np.abs(fy - 0.12) < 0.012).astype(float) + (np.abs(fy - 0.88) < 0.012).astype(float)
    cellid = (np.floor(x * cu) * 7 + np.floor(y * cv) * 13) % 5
    tint = 0.85 + cellid / 5 * 0.3
    base = col((0.035, 0.040, 0.060)) * tint[..., None]
    a = base * np.ones((n, n, 3))
    a = lerp(a, col((0.33, 0.30, 0.27)), gap)
    a = lerp(a, col((0.28, 0.28, 0.30)), finger * 0.35)
    a = lerp(a, col((0.62, 0.60, 0.56)), np.clip(bus, 0, 1) * 0.8)
    h = (1 - gap) * 0.6 + bus * 0.3 + finger * 0.1
    rough = 0.12 + gap * 0.5 + bus * 0.2
    metal = bus * 0.9 + gap * 0.3
    m = 0.18 + 0.22 * fnoise(n, 2.2, 191) + 0.10 * fnoise(n, 0.9, 192)
    a = lerp(a, col((0.45, 0.32, 0.22)), m)
    rough = rough * (1 - m) + 0.8 * m
    metal = metal * (1 - m)
    nrm = normal_from_height(h, 5.0)
    orm = np.stack([np.ones((n, n)), np.clip(rough, 0.02, 1), np.clip(metal, 0, 1)], axis=-1)
    return np.clip(a, 0, 1), nrm, orm


def tile_rect(name, pad_px=10, tile_px=512):
    """UV rectangle (u0, v0, u1, v1) for a tile, inset by pad_px."""
    i = TILE_INDEX[name]
    cx, cy = i % GRID, i // GRID
    size = tile_px * GRID
    p = pad_px / size
    return (cx / GRID + p, cy / GRID + p, (cx + 1) / GRID - p, (cy + 1) / GRID - p)
