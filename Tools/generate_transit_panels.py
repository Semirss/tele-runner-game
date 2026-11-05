"""Draw the transit menu's reusable popup panels."""
from pathlib import Path
from PIL import Image, ImageDraw
from math import cos, sin, pi

OUT = Path(__file__).resolve().parents[1] / "Assets/Resources/TransitMenu"
OUT.mkdir(parents=True, exist_ok=True)
NAVY = "#062567"
BLUE = "#0b368f"
YELLOW = "#ffd41b"
GREEN = "#19b94d"
PAPER = "#f7f9fc"
LINE = "#cbd8ef"


def panel(name, width, height, inset, header):
    im = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    r = int(width * 0.028)
    d.rounded_rectangle((inset + 8, inset + 12, width - inset + 8,
                         height - inset + 12), radius=r, fill="#03184399")
    d.rounded_rectangle((inset, inset, width - inset, height - inset),
                        radius=r, fill=PAPER, outline=LINE, width=5)
    d.rounded_rectangle((inset + 4, inset + 4, width - inset - 4,
                         inset + header), radius=r - 4, fill=NAVY)
    d.rectangle((inset + 4, inset + header - r, width - inset - 4,
                 inset + header), fill=NAVY)
    d.polygon([(inset + 18, inset + header + 12),
               (inset + 125, inset + header + 12),
               (inset + 112, inset + header + 27),
               (inset + 7, inset + header + 27)], fill=YELLOW)
    # A small sunflower cue ties secondary screens back to the partner mark.
    sx, sy = width - inset - 70, inset + header // 2
    for step in range(10):
        angle = step * 2 * pi / 10
        px, py = sx + 29 * cos(angle), sy + 29 * sin(angle)
        d.ellipse((px - 7, py - 7, px + 7, py + 7), fill=YELLOW)
    d.ellipse((sx - 15, sy - 15, sx + 15, sy + 15), fill=YELLOW)
    d.line((inset + 25, height - inset - 62,
            width - inset - 25, height - inset - 62), fill=LINE, width=3)
    for i, color in enumerate((GREEN, YELLOW, BLUE)):
        x = inset + 40 + i * 36
        y = height - inset - 62
        d.ellipse((x - 9, y - 9, x + 9, y + 9), fill=color,
                  outline="white", width=3)
    im.save(OUT / name)


panel("MissionPanel.png", 1080, 1920, 14, 148)
panel("SettingsPanel.png", 900, 1050, 13, 155)
panel("StorePanel.png", 1080, 1920, 12, 190)
