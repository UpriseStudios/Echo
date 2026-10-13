"""
Google Play feature graphic (1024 x 500, 24-bit PNG, no alpha) for Echo.

Built from the game's own generated pixel art (Assets/Echo/Content/Art, written by Echo/Generate Art), composed
at the game's pixel scale (256 x 125) and scaled up 4x with no smoothing, so it looks exactly like the game.
Run from the repo root:  python StoreListing/make_feature_graphic.py
"""
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, 'Assets', 'Echo', 'Content', 'Art')
FONTS = os.path.join(ROOT, 'Assets', 'Echo', 'Content', 'Fonts')
OUT = os.path.join(ROOT, 'StoreListing')

W, H, SCALE = 256, 125, 4
GROUND = 96             # y of the ground's top edge, from the top

SKY = (36, 103, 115)
CYAN = (115, 242, 255)
RED = (255, 84, 84)
GOLD = (255, 201, 74)
WHITE = (255, 255, 255)


def load(name):
    return Image.open(os.path.join(ART, name)).convert('RGBA')


def char(name):
    return load(os.path.join('Characters', name))


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


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


def text(canvas, s, font, x, y, fill, shadow=None):
    layer = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.fontmode = '1'   # hard pixels, no anti-aliasing
    if shadow:
        d.text((x + 1, y + 1), s, font=font, fill=shadow)
    elif canvas.width > W:
        d.text((x + 4, y + 4), s, font=font, fill=(8, 30, 36, 255))
    d.text((x, y), s, font=font, fill=fill)
    canvas.alpha_composite(layer)


def main():
    c = Image.new('RGBA', (W, H), SKY + (255,))

    # A soft moon glow and the moon, high on the right.
    glow = tint(load('fx_glow.png'), (190, 240, 245), 0.35).resize((96, 96), Image.NEAREST)
    moon = load('fx_full_moon.png')
    paste(c, glow, 226 - 48, 34 - 48)
    paste(c, moon, 226 - moon.width // 2, 34 - moon.height // 2)
    for (sx, sy) in [(150, 10), (128, 26), (96, 6), (182, 40), (240, 52), (60, 14), (20, 30)]:
        paste(c, tint(load('fx_star.png'), WHITE, 0.7), sx, sy)

    # Parallax: the far peaks hazy and lighter than the sky, the graveyard a shade darker (as BackgroundView).
    mountains = tint(load('bg_mountains.png'), lerp(SKY, WHITE, 0.14))
    paste(c, mountains, -120, -14)
    near = tint(load('bg_graveyard.png'), tuple(round(v * 0.72) for v in SKY))
    paste(c, near, -40, GROUND - near.height + 6)

    # The ground: top tiles and fill, a shade darker again.
    ground_col = tuple(round(v * 0.5) for v in SKY)
    tile_row(c, tint(load('ground_top.png'), ground_col), GROUND - 2, 7)
    fill = tint(load('ground_fill.png'), tuple(round(v * 0.42) for v in SKY))
    for y in range(GROUND + 8, H, fill.height):
        tile_row(c, fill, y, 7)

    # Graves of earlier runs, each with its ghost on the way past.
    grave = tint(load('grave.png'), tuple(round(v * 0.62) for v in SKY))
    for gx in (100, 146, 176):
        paste(c, grave, gx, GROUND - grave.height + 2)

    # A spike to jump, and the runner over it with a pack of ghosts behind: every failed run comes back.
    spike = tint(load('spike.png') if os.path.exists(os.path.join(ART, 'spike.png')) else load('block.png'),
                 lerp(SKY, WHITE, 0.25))
    spike = spike.resize((12, 12), Image.NEAREST)
    paste(c, spike, 222, GROUND - 11)

    ghosts = [
        ('char_knight_ghost_1.png', 92, 0, CYAN, 0.4),
        ('char_wizard_ghost_2.png', 118, 0, CYAN, 0.55),
        ('char_cat_ghost_jump.png', 146, -10, CYAN, 0.7),
        ('char_princess_ghost_jump.png', 178, -22, RED, 0.9),   # the Nemesis
    ]
    for name, gx, lift, col, a in ghosts:
        g = tint(char(name), col, a)
        paste(c, g, gx, GROUND - g.height + 2 + lift)
        if col == RED:
            horns = tint(load('nemesis_horns.png'), RED, a)
            paste(c, horns, gx + 4, GROUND - g.height + 2 + lift - 5)
    runner = char('char_hooded_jump.png')
    paste(c, runner, 214, GROUND - runner.height - 28)

    # The logo, echoing itself as on the title screen, and the tagline.
    logo = ImageFont.truetype(os.path.join(FONTS, 'PressStart2P-Regular.ttf'), 24)
    for e, a in ((2, 0.2), (1, 0.45)):
        text(c, 'ECHO', logo, 12 - 3 * e, 22 - 3 * e, CYAN + (round(255 * a),))
    text(c, 'ECHO', logo, 12, 22, WHITE + (255,), (8, 30, 36, 255))

    # The tagline at full resolution: Pixelify Sans only sits on its pixel grid at larger sizes.
    out = c.resize((W * SCALE, H * SCALE), Image.NEAREST)
    tag = ImageFont.truetype(os.path.join(FONTS, 'PixelifySans-Regular.ttf'), 40)
    text(out, 'Your past is trying to kill you.', tag, 52, 200, (230, 250, 252, 255), None)
    out = out.convert('RGB')
    assert out.size == (1024, 500)
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, 'feature_graphic_1024x500.png')
    out.save(path, optimize=True)
    print(path)


if __name__ == '__main__':
    main()
