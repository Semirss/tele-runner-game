"""Generate the source artwork for the Transit main menu (1080 x 1920)."""

from pathlib import Path
from math import cos, sin, pi
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "Resources" / "TransitMenu"
OUT.mkdir(parents=True, exist_ok=True)

W, H = 1080, 1920
NAVY = (2, 24, 77)
COBALT = (5, 52, 149)
WHITE = (250, 251, 253)
YELLOW = (255, 205, 20)
GREEN = (30, 189, 84)


def font(size, heavy=False, italic=False):
    base = Path("C:/Windows/Fonts")
    options = (["ariblk.ttf", "arialbd.ttf"] if heavy else
               ["arialbi.ttf", "arialbd.ttf"] if italic else ["arial.ttf"])
    for name in options:
        path = base / name
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default(size)


def centered(draw, xy, text, face, fill, **kwargs):
    box = draw.textbbox((0, 0), text, font=face)
    draw.text((xy[0] - (box[2] - box[0]) / 2,
               xy[1] - (box[3] - box[1]) / 2 - box[1]),
              text, font=face, fill=fill, **kwargs)


def line(draw, points, fill=WHITE, width=4):
    draw.line(points, fill=fill, width=width, joint="curve")


# The background is a separate world-space plate. No character or controls are baked in.
im = Image.new("RGB", (W, H))
px = im.load()
for y in range(H):
    t = min(y / 1510, 1)
    color = tuple(round(COBALT[c] * (1 - t) + NAVY[c] * t * 0.52 + COBALT[c] * t * 0.48)
                  for c in range(3))
    for x in range(W):
        px[x, y] = color
d = ImageDraw.Draw(im)

# Header: custom wordmark, player column and modest partner lockup.
for i, color in enumerate(((25, 163, 239), GREEN, YELLOW)):
    y = 68 + i * 21
    d.polygon([(72, y), (168, y), (156, y + 14), (59, y + 14)], fill=color)
d.text((175, 53), "TELE", font=font(73, True), fill=WHITE, stroke_width=0)
d.text((175, 121), "RUNNER", font=font(65, True), fill=WHITE)
line(d, [(522, 61), (522, 179)], width=3)
line(d, [(819, 61), (819, 179)], width=3)
d.text((855, 65), "Partner:", font=font(29, italic=True), fill=WHITE)
for i in range(12):
    angle = 2 * pi * i / 12
    cx, cy = 879 + 30 * cos(angle), 132 + 30 * sin(angle)
    d.ellipse((cx - 7, cy - 7, cx + 7, cy + 7), fill=YELLOW)
d.ellipse((867, 120, 891, 144), fill=YELLOW)
d.text((922, 103), "Sun", font=font(37, True), fill=WHITE)
d.text((922, 142), "Chips", font=font(37, True), fill=WHITE)
line(d, [(44, 219), (1037, 219)], width=4)
d.ellipse((122, 207, 146, 231), fill=GREEN)
d.ellipse((157, 207, 181, 231), fill=YELLOW)

# Oversized print typography sits behind the live 3D runner. Both words start
# inside the artboard so the first letter stays intact on every device.
d.text((52, 260), "TELE", font=font(205, True), fill=WHITE,
       stroke_width=1, stroke_fill=WHITE)
d.text((52, 489), "RUNNER", font=font(160, True), fill=WHITE,
       stroke_width=1, stroke_fill=WHITE)
d.polygon([(0, 765), (218, 765), (188, 817), (0, 817)], fill=YELLOW)

# The route has four stops, matching the transit concept.
route = [(875, 330), (871, 410), (970, 436), (1006, 488),
         (974, 588), (1012, 680), (1004, 741), (850, 799), (820, 859)]
def smooth_path(points, steps=12):
    result = []
    extended = [points[0]] + points + [points[-1]]
    for i in range(1, len(extended) - 2):
        p0, p1, p2, p3 = extended[i - 1:i + 3]
        for j in range(steps):
            t = j / steps
            t2, t3 = t * t, t * t * t
            result.append(tuple(0.5 * (2 * p1[k] + (-p0[k] + p2[k]) * t
                + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t2
                + (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t3)
                for k in (0, 1)))
    return result + [points[-1]]

line(d, smooth_path(route), width=5)
for x, y, n, color in [(875, 330, "01", GREEN), (1006, 488, "02", YELLOW),
                       (1004, 680, "03", COBALT), (820, 859, "04", GREEN)]:
    d.ellipse((x - 16, y - 16, x + 16, y + 16), fill=WHITE)
    d.ellipse((x - 11, y - 11, x + 11, y + 11), fill=color)
    d.text((x + 31, y - 20), n, font=font(34, italic=True), fill=WHITE)

# Addis transit sign and city line art.
d.rectangle((75, 876, 87, 1318), fill=(218, 223, 232))
d.rectangle((71, 875, 91, 891), fill=WHITE)
d.rounded_rectangle((93, 890, 310, 1064), radius=12, fill=WHITE)
d.rounded_rectangle((93, 890, 310, 959), radius=12, fill=YELLOW)
d.rectangle((93, 943, 310, 959), fill=YELLOW)
d.rounded_rectangle((125, 907, 169, 943), radius=6, fill=NAVY)
d.rectangle((131, 916, 163, 930), fill=YELLOW)
d.ellipse((130, 935, 137, 942), fill=YELLOW)
d.ellipse((157, 935, 164, 942), fill=YELLOW)
d.text((207, 899), "→", font=font(63, True), fill=NAVY)
centered(d, (201, 1009), "Addis Ababa", font(29, True), NAVY)

base_y = 1275
buildings = [(180, 1180, 225, base_y), (236, 1168, 278, base_y),
             (289, 1200, 315, base_y), (337, 1138, 383, base_y),
             (391, 1178, 430, base_y), (750, 1204, 795, base_y),
             (804, 1172, 846, base_y), (954, 1160, 998, base_y)]
for x0, y0, x1, y1 in buildings:
    d.rectangle((x0, y0, x1, y1), outline=WHITE, width=3)
    for wy in range(y0 + 19, y1 - 7, 26):
        for wx in range(x0 + 10, x1 - 6, 17):
            d.rectangle((wx, wy, wx + 6, wy + 8), fill=WHITE)
for x in range(35, W, 45):
    top = 1230 + (x * 37 % 41)
    d.arc((x - 22, top - 13, x + 22, top + 20), 180, 360, fill=WHITE, width=3)
line(d, [(0, base_y), (W, base_y)], width=4)
for x in (38, 590, 1050):
    line(d, [(x, 1268), (x, 1219)], width=3)
    d.ellipse((x - 16, 1202, x + 16, 1233), outline=WHITE, width=3)

# Monument silhouette at the right.
line(d, [(872, 1272), (910, 1248), (931, 1199), (939, 1109),
         (953, 1189), (976, 1249), (1013, 1272)], width=6)
d.ellipse((926, 1094, 952, 1120), fill=WHITE)

# A real curb under the animated character, with tactile yellow strip.
d.polygon([(0, 1326), (W, 1326), (W, 1445), (0, 1445)],
          fill=(184, 190, 201))
for x in range(0, W + 1, 130):
    line(d, [(x, 1327), (x - 35, 1443)], fill=(125, 135, 151), width=2)
line(d, [(0, 1369), (W, 1369)], fill=(129, 138, 151), width=2)
d.rectangle((0, 1410, W, 1441), fill=YELLOW)
for x in range(10, W, 24):
    for y in (1418, 1430):
        d.ellipse((x, y, x + 9, y + 5), fill=(219, 156, 13))

# Clear space for the real Run control and footer controls.
d.rectangle((0, 1690, W, H), fill=NAVY)
line(d, [(0, 1690), (W, 1690)], fill=(9, 65, 157), width=4)
for x in (270, 540, 810):
    line(d, [(x, 1741), (x, 1877)], fill=(107, 129, 173), width=2)
im.save(OUT / "TransitBackground.png", optimize=True)

# The Run button is a separate sprite and remains a real Unity Button.
button = Image.new("RGBA", (960, 188), (0, 0, 0, 0))
b = ImageDraw.Draw(button)
b.polygon([(45, 20), (960, 20), (930, 168), (0, 168)], fill=WHITE)
b.polygon([(45, 20), (111, 20), (80, 168), (0, 168)], fill=YELLOW)
b.line([(791, 94), (892, 94)], fill=NAVY, width=13)
b.line([(859, 61), (892, 94), (859, 127)], fill=NAVY, width=13, joint="curve")
button.save(OUT / "RunButton.png", optimize=True)


def make_icon(name, paint):
    icon = Image.new("RGBA", (120, 120), (0, 0, 0, 0))
    paint(ImageDraw.Draw(icon))
    icon.save(OUT / f"{name}.png", optimize=True)


def trophy(p):
    p.arc((26, 20, 94, 88), 0, 180, fill=WHITE, width=6)
    p.line([(34, 20), (86, 20), (82, 64), (70, 79), (50, 79), (38, 64), (34, 20)], fill=WHITE, width=6, joint="curve")
    p.arc((11, 28, 48, 69), 75, 265, fill=WHITE, width=6)
    p.arc((72, 28, 109, 69), -85, 105, fill=WHITE, width=6)
    p.line([(60, 79), (60, 98), (38, 98), (82, 98)], fill=WHITE, width=6)


def cart(p):
    p.line([(15, 21), (31, 21), (43, 79), (91, 79), (101, 39), (36, 39)], fill=WHITE, width=6, joint="curve")
    p.ellipse((41, 91, 53, 103), outline=WHITE, width=5)
    p.ellipse((83, 91, 95, 103), outline=WHITE, width=5)


def missions(p):
    p.rounded_rectangle((29, 15, 90, 105), radius=8, outline=WHITE, width=6)
    for y in (40, 61, 82):
        p.line([(43, y), (75, y)], fill=WHITE, width=5)


def gear(p):
    cx, cy = 60, 60
    points = []
    for i in range(24):
        a = 2 * pi * i / 24
        radius = 49 if i % 3 in (0, 1) else 39
        points.append((cx + cos(a) * radius, cy + sin(a) * radius))
    p.line(points + [points[0]], fill=WHITE, width=6, joint="curve")
    p.ellipse((45, 45, 75, 75), outline=WHITE, width=6)


for name, fn in (("Leaderboard", trophy), ("Shop", cart),
                 ("Missions", missions), ("Settings", gear)):
    make_icon(name, fn)

print("Generated Transit menu assets in", OUT)
