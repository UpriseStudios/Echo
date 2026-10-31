"""
Google Play feature graphic (1024 x 500, 24-bit PNG, no alpha) for Echo.

Built from the game's own generated pixel art (Assets/Echo/Content/Art, written by Echo/Generate Art), composed
at the game's pixel scale (256 x 125) and scaled up 4x with no smoothing, so it looks exactly like the game.
As on the app icon, the figures are drawn at 2x so they read at the small sizes Play shows the graphic at:
the runner at the top of a jump, and the same jump replayed behind it by the ghosts of earlier runs, the
nearest one the horned Nemesis.
Run from the repo root:  python StoreListing/make_feature_graphic.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, 'Assets', 'Echo', 'Content', 'Art')
FONTS = os.path.join(ROOT, 'Assets', 'Echo', 'Content', 'Fonts')
OUT = os.path.join(ROOT, 'StoreListing')

W, H, SCALE = 256, 125, 4
GROUND = 104            # y of the ground's top edge, from the top

TOP = (30, 92, 108)     # the app icon's dusk: teal overhead, violet at the horizon
HORIZON = (78, 60, 124)
CYAN = (115, 242, 255)
RED = (255, 84, 84)
WHITE = (255, 255, 255)
INK = (10, 18, 34)


def load(name):
    return Image.open(os.path.join(ART, name)).convert('RGBA')


def char(name, scale=2):
    img = load(os.path.join('Characters', name))
    return img.resize((img.width * scale, img.height * scale), Image.NEAREST)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def shade(rgb, k):
    return tuple(round(v * k) for v in rgb)


def tint(img, rgb, alpha=1.0):
    """Greyscale art times a colour, as the game's sprite tint does."""
    out = Image.new('RGBA', img.size)
    src, dst = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = src[x, y]
            dst[x, y] = (r * rgb[0] // 255, g * rgb[1] // 255, b * rgb[2] // 255, round(a * alpha))
    return out


def paste(canvas, img, x, y):
    canvas.alpha_composite(img, (int(x), int(y)))


def tile_row(canvas, img, y, offset=0):
    x = -offset
    while x < W:
        paste(canvas, img, x, y)
        x += img.width


def sky(c):
    """Banded dusk with a checkerboard dither between bands, like the icon's sky."""
    bands = 8
    px = c.load()
    for y in range(H):
        t = min(1.0, y / GROUND) * (bands - 1)
        lo, frac = int(t), t - int(t)
        a = lerp(TOP, HORIZON, lo / (bands - 1))
        b = lerp(TOP, HORIZON, min(bands - 1, lo + 1) / (bands - 1))
        for x in range(W):
            use_b = frac > 0.5 and (x + y) % 2 == 0 or frac > 0.8
            px[x, y] = (b if use_b else a) + (255,)


def text(canvas, s, font, x, y, fill, shadow=None, offset=1):
    layer = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.fontmode = '1'   # hard pixels, no anti-aliasing
    if shadow:
        d.text((x + offset, y + offset), s, font=font, fill=shadow)
    d.text((x, y), s, font=font, fill=fill)
    canvas.alpha_composite(layer)


def main():
    c = Image.new('RGBA', (W, H))
    sky(c)

    # The moon behind the jump, a soft glow round it, and a few stars.
    glow = tint(load('fx_glow.png'), (200, 240, 250), 0.3).resize((112, 112), Image.NEAREST)
    moon = load('fx_full_moon.png')
    paste(c, glow, 170 - 56, 22 - 56)
    paste(c, moon, 170 - moon.width // 2, 22 - moon.height // 2)
    star = tint(load('fx_star.png'), WHITE, 0.8)
    for (sx, sy) in [(118, 8), (204, 8), (104, 40), (140, 30), (236, 14), (8, 6), (66, 4), (250, 34)]:
        paste(c, star, sx, sy)

    # Parallax: far peaks a little lighter than the sky, the graveyard hill darker (as BackgroundView).
    paste(c, tint(load('bg_mountains.png'), lerp(HORIZON, WHITE, 0.1)), -150, 44)
    near = tint(load('bg_graveyard.png'), shade(HORIZON, 0.62))
    paste(c, near, -60, GROUND - near.height + 8)

    # The ground: top tiles and fill, darker again.
    tile_row(c, tint(load('ground_top.png'), shade(HORIZON, 0.5)), GROUND - 2, 7)
    fill = tint(load('ground_fill.png'), shade(HORIZON, 0.36))
    for y in range(GROUND + 8, H, fill.height):
        tile_row(c, fill, y, 7)

    # Graves of the runs the ghosts came from.
    grave = tint(load('grave.png'), shade(HORIZON, 0.75))
    for gx in (90, 108):
        paste(c, grave, gx, GROUND - grave.height + 2)

    # One jump, played four times over: the ghosts of earlier runs follow its arc, oldest and faintest first.
    def foot(x):
        # The arc's height above the ground at the figure's left edge (the runner is at the top, x = 204).
        return max(0, round(14 - 14 * ((x - 204) / 60) ** 2))

    ghosts = [
        ('char_wizard_ghost_jump.png', 132, CYAN, 0.45),
        ('char_cat_ghost_jump.png', 156, CYAN, 0.75),
        ('char_knight_ghost_jump.png', 180, RED, 1.0),    # the Nemesis
    ]
    for name, gx, col, a in ghosts:
        g = tint(char(name), col, a)
        gy = GROUND - g.height - foot(gx)
        paste(c, g, gx, gy)
        if col == RED:
            horns = load('nemesis_horns.png')
            horns = tint(horns.resize((horns.width * 2, horns.height * 2), Image.NEAREST), RED, a)
            left, top, right, _ = g.getbbox()
            paste(c, horns, gx + (left + right - horns.width) // 2, gy + top - horns.height + 2)
    runner = char('char_hooded_jump.png')
    paste(c, runner, 204, GROUND - runner.height - foot(202))

    # The logo, echoing itself as on the title screen.
    logo = ImageFont.truetype(os.path.join(FONTS, 'PressStart2P-Regular.ttf'), 32)
    for e, a in ((3, 0.18), (2, 0.35), (1, 0.6)):
        text(c, 'ECHO', logo, 10 - 2 * e, 22 - 2 * e, CYAN + (round(255 * a),))
    text(c, 'ECHO', logo, 10, 22, WHITE + (255,), INK + (255,), 2)

    # The tagline at full resolution: Pixelify Sans only sits on its pixel grid at larger sizes.
    out = c.resize((W * SCALE, H * SCALE), Image.NEAREST)
    tag = ImageFont.truetype(os.path.join(FONTS, 'PixelifySans-Regular.ttf'), 40)
    text(out, 'Your past is', tag, 40, 252, (236, 250, 252, 255), INK + (255,), 4)
    text(out, 'trying to kill you.', tag, 40, 300, (236, 250, 252, 255), INK + (255,), 4)
    out = out.convert('RGB')
    assert out.size == (1024, 500)
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, 'feature_graphic_1024x500.png')
    out.save(path, optimize=True)
    print(path)


if __name__ == '__main__':
    main()
