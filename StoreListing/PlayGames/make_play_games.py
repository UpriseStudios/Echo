"""
Play Games Services setup files for Play Console, made from the game's own catalog and art.

  achievements.zip      Play Console > Play Games Services > Achievements > Import: AchievementsMetadata.csv,
                        AchievementsIconsMappings.csv and a 512 px icon for each achievement (no text, centred
                        for Android's circular crop). Read from Core/Achievements/AchievementCatalog.cs, so the
                        names here are the game's titles; Echo/Play Games/Import Resources matches them back.
  game_stats_events.csv Game Stats > Events (PlayerGameEvent.csv): the events the game sends.
  game_stats.zip        Game Stats > Import: RepetitiveStatsConfig.csv, ProgressionStatConfig.csv and icons.
  preview.png           Every icon on one sheet, for review.

Run from the repo root:  python StoreListing/PlayGames/make_play_games.py
"""
import csv
import io
import os
import re
import zipfile
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(ROOT, 'Assets', 'Echo', 'Content', 'Art')
CATALOG = os.path.join(ROOT, 'Assets', 'Echo', 'Core', 'Achievements', 'AchievementCatalog.cs')
OUT = os.path.dirname(os.path.abspath(__file__))

G = 64          # icons are drawn on a 64 px grid and scaled 8x, like the game's pixels
SIZE = 512

CYAN = (115, 242, 255)
RED = (255, 84, 84)
GOLD = (255, 201, 74)
WHITE = (255, 255, 255)

# Points (multiples of 5, at most 1,000 in all) by how hard each is, and Play's hidden flag from the catalog.
POINTS = {
    'hello_again': 5, 'that_was_me': 10, 'deja_vu': 10, 'seriously': 15, 'sworn_enemy': 25,
    'crowded_house': 10, 'echo_chamber': 20, 'haunted': 30, 'theyre_everywhere': 40, 'army_of_me': 60, 'costume_party': 20,
    'close_one': 5, 'thread_the_needle': 15, 'how_did_i_survive': 30, 'no_fear': 40,
    'fresh_start': 15, 'exorcist': 30, 'mass_exorcism': 50, 'so_close': 10, 'smashed_it': 25, 'perfect_echo': 10,
    'graveyard_shift': 15, 'unfinished_business': 10,
    'getting_started': 5, 'going_somewhere': 25, 'long_way_from_home': 50, 'untouchable': 30,
    'bad_decisions': 20, 'i_can_fix_this': 15, 'just_one_more': 20,
    'haunted_by_strangers': 25, 'passing_through': 15, 'top_ten': 40, 'daily_grind': 20,
    'performance_enhanced': 30,
    'time_traveller': 10, 'year_3026': 15, 'extinct': 10, 'fossil_record': 10, 'paradox': 5,
    'twelve_days': 30, 'lead_the_way': 30, 'snowed_under': 30, 'first_footer': 15, 'trick_or_treat': 15,
    'its_complicated': 15, 'egg_hunt': 20,
}


def catalog():
    """(id, title, description, target, hidden) for every achievement, in the catalog's order."""
    src = open(CATALOG, encoding='utf-8').read()
    ids = dict(re.findall(r'public const string (\w+) = "([^"]+)"',
                          open(os.path.join(os.path.dirname(CATALOG), 'AchievementIds.cs'), encoding='utf-8').read()))
    out = []
    for m in re.finditer(r'new AchievementDef\((\w+), "((?:[^"\\]|\\.)*)", "((?:[^"\\]|\\.)*)"(.*?)\),\n', src):
        const, title, desc, rest = m.groups()
        target = re.match(r', (\d+)', rest)
        out.append((ids[const], title, desc, int(target.group(1)) if target else 1, 'hidden: true' in rest))
    return out


def load(name):
    return Image.open(os.path.join(ART, name)).convert('RGBA')


def tint(img, rgb, alpha=1.0):
    out = Image.new('RGBA', img.size)
    src, dst = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = src[x, y]
            dst[x, y] = (r * rgb[0] // 255, g * rgb[1] // 255, b * rgb[2] // 255, round(a * alpha))
    return out


def scaled(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def shade(rgb, k):
    return tuple(max(0, min(255, round(v * k))) for v in rgb)


class Icon:
    """A 64 px badge: a dithered disc in the group's colour, then sprites placed by their centre and foot."""

    def __init__(self, colour):
        self.c = Image.new('RGBA', (G, G), shade(colour, 0.35) + (255,))
        px = self.c.load()
        for y in range(G):
            for x in range(G):
                d = ((x - 31.5) ** 2 + (y - 31.5) ** 2) ** 0.5
                if d < 25 or (d < 28 and (x + y) % 2 == 0):
                    px[x, y] = shade(colour, 0.75 if d < 25 else 0.55) + (255,)
                if d < 18 or (d < 21 and (x + y) % 2 == 0):
                    px[x, y] = colour + (255,)

    def put(self, img, cx, foot):
        self.c.alpha_composite(img, (int(cx - img.width // 2), int(foot - img.height)))
        return self

    def char(self, name, cx=32, foot=50, frame='jump', k=1):
        return self.put(scaled(load(f'Characters/{name}_{frame}.png'), k), cx, foot)

    def ghost(self, name, cx=32, foot=50, colour=CYAN, alpha=1.0, frame='jump', horns=False):
        g = tint(load(f'Characters/{name}_ghost_{frame}.png'), colour, alpha)
        self.put(g, cx, foot)
        if horns:
            left, top, right, _ = g.getbbox()
            h = tint(load('nemesis_horns.png'), colour, alpha)
            self.put(h, cx - g.width // 2 + (left + right) // 2, foot - g.height + top + 2)
        return self

    def sprite(self, name, cx, foot, colour=None, alpha=1.0, k=1):
        img = scaled(load(name), k)
        if colour:
            img = tint(img, colour, alpha)
        return self.put(img, cx, foot)

    def crowd(self, n, colour=CYAN):
        """n small ghosts in rows, newest brightest."""
        cols = 6 if n > 15 else 5 if n > 9 else 3 if n > 4 else n
        rows = (n + cols - 1) // cols
        icon = load('ui_ghost_icon.png')
        step = {6: 7, 5: 8}.get(cols, 10)
        rise = 7 if rows > 3 else 9
        for i in range(n):
            r, c = divmod(i, cols)
            x = 32 + (c - (cols - 1) / 2) * step
            y = 32 + (r - (rows - 1) / 2) * rise + 8
            self.put(tint(icon, colour, 0.45 + 0.55 * (i + 1) / n), x, y)
        return self

    def image(self):
        return self.c.resize((SIZE, SIZE), Image.NEAREST).convert('RGB')


def achievement_icons():
    teal, wine, gold, green, slate, blue, ember, violet = (
        (40, 128, 140), (130, 46, 84), (196, 150, 44), (52, 140, 84), (84, 96, 120), (52, 92, 170), (190, 96, 40), (104, 64, 160))
    return {
        'hello_again': Icon(teal).ghost('char_hooded'),
        'that_was_me': Icon(wine).ghost('char_hooded', 25, 48, CYAN, 0.8).char('char_hooded', 39, 52),
        'deja_vu': Icon(wine).ghost('char_knight', 24, 48, CYAN, 0.6).ghost('char_knight', 40, 52, RED),
        'seriously': Icon(wine).ghost('char_cat', 20, 46, CYAN, 0.45).ghost('char_cat', 32, 49, CYAN, 0.7).ghost('char_cat', 44, 52, RED),
        'sworn_enemy': Icon(wine).ghost('char_skeleton', 32, 54, RED, horns=True),
        'crowded_house': Icon(teal).crowd(5),
        'echo_chamber': Icon(teal).crowd(10),
        'haunted': Icon(teal).crowd(15),
        'theyre_everywhere': Icon(teal).crowd(20),
        'army_of_me': Icon(shade(teal, 1.25)).crowd(30, WHITE),
        'costume_party': Icon(violet).ghost('char_wizard', 18, 44, CYAN, 0.7).ghost('char_princess', 46, 44, CYAN, 0.7)
            .ghost('char_robot', 24, 54).ghost('char_frog', 40, 54).ghost('char_ninja', 32, 49, CYAN, 0.85),
        'close_one': Icon(gold).ghost('char_hooded', 22, 52, CYAN, 0.7).char('char_hooded', 40, 44)
            .sprite('fx_sparkle.png', 30, 30, WHITE, k=2),
        'thread_the_needle': Icon(gold).ghost('char_knight', 16, 54, CYAN, 0.7).ghost('char_cat', 48, 54, CYAN, 0.7).char('char_fox', 32, 42),
        'how_did_i_survive': Icon(gold).ghost('char_knight', 14, 56, CYAN, 0.6).ghost('char_cat', 50, 56, CYAN, 0.6)
            .ghost('char_wizard', 32, 60, CYAN, 0.6).char('char_ninja', 32, 38),
        'no_fear': Icon(ember).sprite('spike.png', 44, 54, (236, 240, 248), k=1).char('char_barbarian', 26, 52),
        'fresh_start': Icon(green).sprite('fx_glow.png', 32, 52, (255, 240, 180), 0.6, 1).char('char_hooded', 32, 52, frame='0'),
        'exorcist': Icon(green).sprite('fx_beam.png', 32, 30, (230, 255, 240), 0.8).ghost('char_zombie', 32, 54, CYAN, 0.4),
        'mass_exorcism': Icon(shade(green, 1.2)).sprite('fx_glow.png', 32, 50, WHITE, 0.7, 1).crowd(10, (230, 255, 240)),
        'so_close': Icon(slate).sprite('grave.png', 22, 52, (200, 208, 220), k=2).char('char_hooded', 44, 52),
        'smashed_it': Icon(gold).char('char_golden', 32, 52).sprite('fx_sparkle.png', 16, 22, WHITE, k=2).sprite('fx_sparkle.png', 49, 30, WHITE, k=2),
        'perfect_echo': Icon(slate).sprite('grave.png', 22, 52, (200, 208, 220), k=2).ghost('char_hooded', 22, 34, CYAN, 0.5).char('char_hooded', 44, 46),
        'graveyard_shift': Icon(slate).sprite('grave.png', 14, 46, (190, 198, 214)).sprite('grave.png', 26, 50, (210, 216, 228))
            .sprite('grave.png', 38, 46, (190, 198, 214)).sprite('grave.png', 50, 50, (210, 216, 228)).sprite('fx_full_moon.png', 32, 30, None),
        'unfinished_business': Icon(slate).sprite('ui_headstone.png', 32, 56),
        'getting_started': Icon(blue).char('char_hooded', 32, 52, frame='1'),
        'going_somewhere': Icon(blue).char('char_knight', 32, 52, frame='2'),
        'long_way_from_home': Icon((28, 36, 84)).sprite('fx_planet.png', 40, 30).char('char_astronaut', 24, 56),
        'untouchable': Icon(violet).sprite('ui_power_shield.png', 32, 34, None, k=2).char('char_shadow', 32, 58),
        'bad_decisions': Icon(ember).char('char_skeleton', 32, 52, frame='0'),
        'i_can_fix_this': Icon(ember).char('char_robot', 32, 52).sprite('fx_bolt.png', 50, 40, GOLD),
        'just_one_more': Icon(ember).char('char_arcade', 32, 52),
        'haunted_by_strangers': Icon(violet).ghost('char_alien', 20, 50, (180, 140, 255)).ghost('char_vampire', 44, 50, (255, 160, 200))
            .ghost('char_marine', 32, 56, CYAN),
        'passing_through': Icon(violet).ghost('char_princess', 22, 50, (255, 230, 140), 0.8).char('char_hooded', 42, 46),
        'top_ten': Icon(gold).char('char_king', 32, 52).sprite('fx_star.png', 48, 22, WHITE, k=2).sprite('fx_star.png', 16, 28, WHITE, k=2),
        'daily_grind': Icon(violet).sprite('fx_full_moon.png', 32, 40).char('char_gravekeeper', 32, 58),
        'performance_enhanced': Icon((40, 128, 140)).sprite('ui_power_shield.png', 16, 30).sprite('ui_power_phase.png', 32, 26)
            .sprite('ui_power_lantern.png', 48, 30).char('char_hooded', 32, 58, frame='1'),
        'time_traveller': Icon((190, 110, 50)).sprite('Seasons/timerun_dino_spike.png', 48, 54, (236, 228, 204)).char('char_caveman', 28, 52),
        'year_3026': Icon((24, 34, 80)).sprite('fx_glow.png', 32, 40, CYAN, 0.6, 1).char('char_chrono', 32, 52),
        'extinct': Icon((150, 70, 40)).sprite('Seasons/grave_fossil.png', 32, 52, k=2),
        'fossil_record': Icon((190, 110, 50)).crowd(10, (237, 228, 204)),
        'paradox': Icon((24, 34, 80)).ghost('char_hooded', 22, 50, (74, 240, 255), 0.9).char('char_hooded', 42, 52),
        'twelve_days': Icon((150, 36, 48)).char('char_santa', 32, 52),
        'lead_the_way': Icon((40, 96, 64)).char('char_rudolph', 32, 52),
        'snowed_under': Icon((60, 110, 160)).sprite('Seasons/christmas_grave.png', 32, 52, k=2),
        'first_footer': Icon((30, 30, 70)).sprite('Seasons/fx_spark.png', 20, 22, GOLD, k=3).sprite('Seasons/fx_spark.png', 44, 18, (255, 120, 200), k=3)
            .sprite('Seasons/fx_spark.png', 32, 30, CYAN, k=3).char('char_hooded', 32, 58),
        'trick_or_treat': Icon((190, 96, 30)).char('char_pumpkin', 32, 52),
        'its_complicated': Icon((170, 50, 90)).sprite('Seasons/grave_heart.png', 32, 52, k=2),
        'egg_hunt': Icon((120, 170, 110)).char('char_easter_chick', 26, 52).sprite('Seasons/fx_egg.png', 46, 52, k=2),
    }


def stat_icons():
    teal, gold, blue, green, violet = (40, 128, 140), (196, 150, 44), (52, 92, 170), (52, 140, 84), (104, 64, 160)
    return {
        'runs.png': Icon(blue).char('char_hooded', 32, 52, frame='1'),
        'distance.png': Icon(teal).char('char_fox', 32, 52, frame='2'),
        'ghosts_passed.png': Icon(violet).ghost('char_knight', 22, 50, CYAN, 0.6).char('char_ninja', 42, 46),
        'near_misses.png': Icon(gold).ghost('char_cat', 22, 52, CYAN, 0.7).char('char_wizard', 42, 44).sprite('fx_sparkle.png', 31, 30, WHITE, k=2),
        'best_score.png': Icon(shade(gold, 1.15)).char('char_golden', 32, 52),
        'best_distance.png': Icon((28, 36, 84)).sprite('fx_planet.png', 40, 30).char('char_hooded', 26, 56),
    }


def csv_text(rows):
    buf = io.StringIO()
    csv.writer(buf, lineterminator='\n').writerows(rows)
    return buf.getvalue()


def no_commas(s):
    # Play's import takes no commas in names or descriptions.
    return s.replace('1,000', '1000').replace(', ', ' ').replace(',', '')


def main():
    defs = catalog()
    assert set(d[0] for d in defs) == set(POINTS), set(d[0] for d in defs) ^ set(POINTS)
    total = sum(POINTS.values())
    assert total <= 1000 and all(p % 5 == 0 and 5 <= p <= 200 for p in POINTS.values()), total

    icons = achievement_icons()
    meta, mapping = [], []
    with zipfile.ZipFile(os.path.join(OUT, 'achievements.zip'), 'w', zipfile.ZIP_DEFLATED) as z:
        for order, (aid, title, desc, target, hidden) in enumerate(defs, 1):
            name = no_commas(title)
            incremental = target > 1
            meta.append([name, no_commas(desc), 'True' if incremental else 'False', target if incremental else '',
                         'Hidden' if hidden else 'Revealed', POINTS[aid], order])
            png = aid + '.png'
            mapping.append([name, png])
            buf = io.BytesIO()
            icons[aid].image().save(buf, 'PNG', optimize=True)
            z.writestr(png, buf.getvalue())
        z.writestr('AchievementsMetadata.csv', csv_text(meta))
        z.writestr('AchievementsIconsMappings.csv', csv_text(mapping))

    single = os.path.join(OUT, 'performance_enhanced.png')
    icons['performance_enhanced'].image().save(single, optimize=True)

    # Game Stats: one event a finished run (main game, Daily Haunt and modes), and the progression stat.
    events = [['Event Name', 'Property Name', 'Property Type'],
              ['runEnded', 'distance', 'INT64'], ['runEnded', 'score', 'INT64'],
              ['runEnded', 'ghostsPassed', 'INT64'], ['runEnded', 'nearMisses', 'INT64'],
              ['progressUpdate', 'currentProgress', 'INT64']]
    open(os.path.join(OUT, 'game_stats_events.csv'), 'w', encoding='utf-8', newline='').write(csv_text(events))
    repetitive = [['Stat Id', 'Event Name', 'Event Property Name', 'Aggregation Type', 'Stat Display Name', 'Stat Description',
                   'Filter Property', 'Filter Operator', 'Filter Value', 'Is Competitive', 'Min limit', 'Max limit',
                   'Icon File Name', 'Good value direction', 'Unit'],
                  ['runs_played', 'runEnded', 'distance', 'COUNT', 'Runs', 'Runs finished in any mode',
                   '', '', '', 'false', '', '', 'runs.png', 'INCREASING', 'UNITLESS'],
                  ['metres_run', 'runEnded', 'distance', 'SUM', 'Metres Run', 'Metres run across every run',
                   '', '', '', 'false', '', '', 'distance.png', 'INCREASING', 'UNITLESS'],
                  ['ghosts_passed', 'runEnded', 'ghostsPassed', 'SUM', 'Ghosts Passed', 'Ghosts of your old runs overtaken',
                   '', '', '', 'false', '', '', 'ghosts_passed.png', 'INCREASING', 'UNITLESS'],
                  ['near_misses', 'runEnded', 'nearMisses', 'SUM', 'Near Misses', 'Ghosts passed within a hair',
                   '', '', '', 'false', '', '', 'near_misses.png', 'INCREASING', 'UNITLESS'],
                  ['best_score', 'runEnded', 'score', 'MAX', 'Best Score', 'The highest score of a single run',
                   '', '', '', 'true', '0', '1000000', 'best_score.png', 'INCREASING', 'UNITLESS']]
    progression = [['Stat Id', 'Event Property Name', 'Stat Display Name', 'Stat Description', 'Icon File Name',
                    'Good value direction', 'Unit'],
                   ['best_distance', 'currentProgress', 'Best Distance', 'Your furthest run in metres',
                    'best_distance.png', 'INCREASING', 'UNITLESS']]
    stats = stat_icons()
    with zipfile.ZipFile(os.path.join(OUT, 'game_stats.zip'), 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('RepetitiveStatsConfig.csv', csv_text(repetitive))
        z.writestr('ProgressionStatConfig.csv', csv_text(progression))
        for png, icon in stats.items():
            buf = io.BytesIO()
            icon.image().save(buf, 'PNG', optimize=True)
            z.writestr(png, buf.getvalue())

    # A review sheet: every icon at 128 px.
    all_icons = [icons[d[0]] for d in defs] + list(stats.values())
    cols = 8
    sheet = Image.new('RGB', (cols * 136, ((len(all_icons) + cols - 1) // cols) * 136), (16, 16, 24))
    for i, icon in enumerate(all_icons):
        sheet.paste(icon.image().resize((128, 128), Image.NEAREST), ((i % cols) * 136 + 4, (i // cols) * 136 + 4))
    sheet.save(os.path.join(OUT, 'preview.png'))
    print(f'{len(defs)} achievements, {total} points; {len(stats)} stats')


if __name__ == '__main__':
    main()
