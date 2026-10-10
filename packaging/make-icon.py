"""
Plan 8.2 — the TrafficLab+ icon (Ron, 2026-10-09: "use the theme with the +. A streetlight with
sides making a plus is an idea. Use the same simple theme as the other applications").

The MARK — a traffic signal: the upright head with its three lamps, on a backplate whose sides
reach out to make the "+". White; the lamps are cut through to the green, the middle one lit amber
(the signal amber of the page). One simple white mark on the program's colour, the guide-sign
green of every TrafficLab+ page (#0F6B4A) — as Gamify+, CourseBuilder+ and Assessment+ do theirs.

    python packaging/make-icon.py

Writes:
    resources/images/trafficlab-icon-1024.png        the master: full-bleed green with the mark
    resources/images/trafficlab-icon-rounded-1024.png a rounded green square on transparency
    resources/images/icon-candidates.png              the chosen mark and two variants side by side
    resources/images/trafficlab-mark-2048.png, trafficlab-glyph-2048.png   for make-store-assets.ps1
    src/TrafficLabPlus.App/TrafficLabPlus.ico         the program's .exe and window icon (16-256)
The package images for the Store (tiles, splash) come from the master when packaging is built (8.3).
"""
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMAGES = os.path.join(ROOT, 'resources', 'images')
ICO = os.path.join(ROOT, 'src', 'TrafficLabPlus.App', 'TrafficLabPlus.ico')
os.makedirs(IMAGES, exist_ok=True)

GREEN = (15, 107, 74)
WHITE = (255, 255, 255)
LAMP = (190, 226, 208)      # a pale green lamp, unlit
AMBER = (226, 164, 0)       # the page's signal amber, lit
S = 4                       # supersampling


def plus_signal(d, size, lit=1, holes=True, body=0.15, tall=0.39, wing=0.075, span=0.34, lamp=0.088):
    """A signal head (the upright, three lamps) on a backplate whose sides make the "+" — the
    backplate a real signal wears, stretched into the arms of the plus. Fractions of `size`."""
    c = size / 2
    bw, bh = size * body, size * tall
    ww, wl = size * wing, size * span
    d.rounded_rectangle([c - wl, c - ww, c + wl, c + ww], radius=ww, fill=WHITE)          # the arms
    d.rounded_rectangle([c - bw, c - bh, c + bw, c + bh], radius=bw * 0.8, fill=WHITE)    # the head
    gap = (bh - bw * 0.95)
    for i, y in enumerate([c - gap, c, c + gap]):
        rr = size * lamp
        fill = AMBER if i == lit else (GREEN if holes else LAMP)
        d.ellipse([c - rr, y - rr, c + rr, y + rr], fill=fill)


def tile(size, rounded=False, **kw):
    big = size * S
    img = Image.new('RGBA', (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if rounded:
        d.rounded_rectangle([0, 0, big - 1, big - 1], radius=int(big * 0.18), fill=GREEN)
    else:
        d.rectangle([0, 0, big, big], fill=GREEN)
    plus_signal(d, big, **kw)
    return img.resize((size, size), Image.LANCZOS)


# the chosen mark
tile(1024).save(os.path.join(IMAGES, 'trafficlab-icon-1024.png'))
tile(1024, rounded=True).save(os.path.join(IMAGES, 'trafficlab-icon-rounded-1024.png'))

# for the Store package (packaging/make-store-assets.ps1): the mark at 2048 on green, and the white
# signal alone on transparency (its lamps green) to set beside the name on the wide tile and hero art
tile(2048).save(os.path.join(IMAGES, 'trafficlab-mark-2048.png'))
glyph = Image.new('RGBA', (2048 * 2, 2048 * 2), (0, 0, 0, 0))
plus_signal(ImageDraw.Draw(glyph), 2048 * 2)
glyph.resize((2048, 2048), Image.LANCZOS).save(os.path.join(IMAGES, 'trafficlab-glyph-2048.png'))

# the .ico: rounded square on transparency, each size drawn at its size (sharp at 16 px)
sizes = [16, 24, 32, 48, 64, 128, 256]
frames = [tile(s, rounded=True, lamp=0.095 if s <= 32 else 0.088) for s in sizes]
frames[-1].save(ICO, format='ICO', sizes=[(s, s) for s in sizes], append_images=frames[:-1])

# candidates: A (chosen) lamps cut through, the middle one amber; B pale lamps; C the top lamp lit, wider arms
sheet = Image.new('RGB', (3 * 300 + 4 * 20, 340), (238, 241, 239))
for i, img in enumerate([tile(300, rounded=True), tile(300, rounded=True, holes=False), tile(300, rounded=True, lit=0, wing=0.1, span=0.37)]):
    sheet.paste(img, (20 + i * 320, 20), img)
sheet.save(os.path.join(IMAGES, 'icon-candidates.png'))
print('wrote', ICO)
