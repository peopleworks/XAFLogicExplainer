#!/usr/bin/env python3
"""Build the article cover images, in both languages and both aspect ratios.

The cover carries the article's whole argument in one glance, so it is generated rather than
drawn: the palette is the site's, the claim on it is the article's first paragraph, and both
stay in step because one file produces every variant.

    python site/build-covers.py

Two ratios, because the places this gets published crop differently:

  * 1200x630 -- Open Graph, LinkedIn article cover, the blog's own header.
  * 1000x420 -- dev.to, which crops anything taller and would eat the install line.

Emits SVG (for the blog, which can serve vectors) and PNG (for dev.to and LinkedIn, which
cannot). The PNG pass also measures every text element and fails if one runs past the safe
margin -- SVG has no text layout, so a translation that grew by three words would silently
print off the edge, and nobody proof-reads an image they did not see fail.

Needs `pip install playwright pillow` and `python -m playwright install chromium`.
"""

from __future__ import annotations

import pathlib
import sys
from xml.sax.saxutils import escape

from PIL import Image
from playwright.sync_api import sync_playwright

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "docs" / "assets"

# Retina. A cover is the one image a reader sees at full width on a phone.
SCALE = 2

# The site's dark tokens, resolved. A cover is a raster on somebody else's page: it cannot
# follow a theme, so it commits to the dark one, which is the tool's own surface anyway.
PALETTE = {
    "bg": "#07090c",
    "panel": "#10151d",
    "line": "#232b36",
    "graphite": "#46505e",
    "graphite_ink": "#737f8f",
    "ink": "#f2f5f8",
    "mute": "#8794a4",
    "faint": "#64707e",
    "yours": "#ff8a3d",
    "yours_soft": "rgba(255,138,61,.10)",
}

# The copy. Line 2 of the headline splits so the last clause can carry the accent: the whole
# thesis is that "your application" is the part nothing else knows, and the colour says it.
COPY = {
    "en": {
        "eyebrow": "XAF LOGIC EXPLAINER  ·  MIT  ·  OPEN SOURCE",
        "head_1": "Your agent knows XAF.",
        "head_2a": "It has never seen ",
        "head_2b": "your application.",
        "left_cap": "WHAT THE AGENT WRITES",
        "left_code": "Invoice.TotalAmount",
        "left_note": "Fluent XAF. No such member.",
        "right_cap": "WHAT YOUR CLASS HAS",
        "right_code": "Invoice.Total",
        "right_note": "Read from your source, with Roslyn.",
        "install": "$ dotnet tool install -g XafLogicExplainer.Cli",
    },
    "es": {
        "eyebrow": "XAF LOGIC EXPLAINER  ·  MIT  ·  CÓDIGO ABIERTO",
        "head_1": "Tu agente sabe XAF.",
        "head_2a": "Nunca ha visto ",
        "head_2b": "tu aplicación.",
        "left_cap": "LO QUE ESCRIBE EL AGENTE",
        "left_code": "Invoice.TotalAmount",
        "left_note": "XAF impecable. Miembro inexistente.",
        "right_cap": "LO QUE TIENE TU CLASE",
        "right_code": "Invoice.Total",
        "right_note": "Leído de tu código, con Roslyn.",
        "install": "$ dotnet tool install -g XafLogicExplainer.Cli",
    },
}

REPO = "github.com/peopleworks/XAFLogicExplainer"

# Geometry per ratio. Every number is a baseline or an edge; nothing is computed from text,
# because SVG cannot measure text -- which is what the overflow check exists to catch.
SIZES = {
    "": dict(  # 1200x630
        w=1200, h=630, m=72,
        eyebrow_y=78, eyebrow_size=13,
        head_y1=196, head_y2=258, head_size=52,
        panel_y=322, panel_h=160, panel_w=508, panel_gap=40, panel_pad=26,
        cap_dy=38, cap_size=12, code_dy=92, code_size=34, note_dy=132, note_size=13.5,
        foot_y=560, foot_size=17, repo_size=13,
        dot_gap=26,
    ),
    "-wide": dict(  # 1000x420
        w=1000, h=420, m=56,
        eyebrow_y=54, eyebrow_size=11.5,
        head_y1=132, head_y2=178, head_size=40,
        panel_y=208, panel_h=130, panel_w=424, panel_gap=40, panel_pad=22,
        cap_dy=31, cap_size=11, code_dy=76, code_size=27, note_dy=108, note_size=12,
        foot_y=384, foot_size=14, repo_size=11,
        dot_gap=22,
    ),
}


def build_svg(lang: str, g: dict) -> str:
    """One cover, as SVG source."""
    t = {key: escape(value) for key, value in COPY[lang].items()}
    p = PALETTE

    right_x = g["m"] + g["panel_w"] + g["panel_gap"]
    edge = g["w"] - g["m"]
    panel_y = g["panel_y"]

    def panel(x: float, colour: str, cap: str, code: str, note: str, accent: bool) -> str:
        fill = p["yours_soft"] if accent else p["panel"]
        stroke = p["yours"] if accent else p["line"]
        inner = x + g["panel_pad"]
        return f"""
  <g>
    <rect x="{x}" y="{panel_y}" width="{g['panel_w']}" height="{g['panel_h']}" rx="12"
          fill="{fill}" stroke="{stroke}" stroke-width="{1.5 if accent else 1}"/>
    <text class="cap" x="{inner}" y="{panel_y + g['cap_dy']}"
          style="fill:{colour};font-size:{g['cap_size']}px">{cap}</text>
    <text class="mono" x="{inner}" y="{panel_y + g['code_dy']}"
          style="fill:{colour};font-size:{g['code_size']}px;font-weight:600">{code}</text>
    <text class="body" x="{inner}" y="{panel_y + g['note_dy']}"
          style="font-size:{g['note_size']}px">{note}</text>
  </g>"""

    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="{g['w']}" height="{g['h']}"
     viewBox="0 0 {g['w']} {g['h']}" role="img" aria-labelledby="cover-t cover-d">
  <title id="cover-t">{t['head_1']} {t['head_2a']}{t['head_2b']}</title>
  <desc id="cover-d">{t['left_cap']}: {t['left_code']}. {t['right_cap']}: {t['right_code']}.</desc>

  <style>
    text {{ font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }}
    .mono {{ font-family: ui-monospace, "Cascadia Mono", "JetBrains Mono", Menlo, Consolas, monospace; }}
    .cap  {{ font-family: ui-monospace, "Cascadia Mono", Menlo, Consolas, monospace;
             letter-spacing: .13em; }}
    .head {{ font-size: {g['head_size']}px; font-weight: 640; letter-spacing: -.015em; }}
    .body {{ fill: {p['mute']}; }}
  </style>

  <defs>
    <!-- A flat rectangle at 1200px reads as an empty slide. The grid is barely visible and
         only has to keep the surface from looking like a placeholder. -->
    <pattern id="grid" width="{g['dot_gap']}" height="{g['dot_gap']}" patternUnits="userSpaceOnUse">
      <circle cx="1" cy="1" r="1" fill="{p['ink']}" opacity=".035"/>
    </pattern>
  </defs>

  <rect width="{g['w']}" height="{g['h']}" fill="{p['bg']}"/>
  <rect width="{g['w']}" height="{g['h']}" fill="url(#grid)"/>

  <text class="cap" x="{g['m']}" y="{g['eyebrow_y']}"
        style="fill:{p['faint']};font-size:{g['eyebrow_size']}px">{t['eyebrow']}</text>

  <!-- Grey line, then colour: framework knowledge is the plentiful half, yours is the scarce one. -->
  <text class="head" x="{g['m']}" y="{g['head_y1']}" style="fill:{p['graphite_ink']}">{t['head_1']}</text>
  <text class="head" x="{g['m']}" y="{g['head_y2']}" style="fill:{p['ink']}">{t['head_2a']}<tspan
        style="fill:{p['yours']}">{t['head_2b']}</tspan></text>
{panel(g['m'], p['graphite_ink'], t['left_cap'], t['left_code'], t['left_note'], False)}
{panel(right_x, p['yours'], t['right_cap'], t['right_code'], t['right_note'], True)}

  <text class="mono" x="{g['m']}" y="{g['foot_y']}"
        style="fill:{p['mute']};font-size:{g['foot_size']}px">{t['install']}</text>
  <text class="mono" x="{edge}" y="{g['foot_y']}" text-anchor="end"
        style="fill:{p['faint']};font-size:{g['repo_size']}px">{REPO}</text>
</svg>
"""


def render(page, svg_path: pathlib.Path, png_path: pathlib.Path, g: dict) -> list[str]:
    """Rasterise one cover and report any text that ran past the safe margin."""
    page.set_viewport_size({"width": g["w"], "height": g["h"]})
    page.goto(svg_path.as_uri())
    page.wait_for_timeout(200)

    # A few pixels of slack: getBBox reports ink, and a glyph like "Y" or a trailing "r" leans
    # a pixel or two past its own origin. A line that genuinely does not fit misses by tens.
    overflow = page.evaluate(
        """([left, right, slack]) => [...document.querySelectorAll('text')]
             .map(t => [t.textContent.trim(), t.getBBox()])
             .filter(([, b]) => b.x < left - slack || b.x + b.width > right + slack)
             .map(([text, b]) => `${text}  (${Math.round(b.x)}px to ${Math.round(b.x + b.width)}px)`)""",
        [g["m"], g["w"] - g["m"], 3],
    )

    page.screenshot(path=str(png_path))
    return overflow


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    problems: list[str] = []

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch()
        page = browser.new_page(
            viewport={"width": 1200, "height": 630},
            device_scale_factor=SCALE,
            color_scheme="dark",
        )

        for lang in COPY:
            for suffix, g in SIZES.items():
                stem = f"cover-{lang}{suffix}"
                svg_path = OUT / f"{stem}.svg"
                png_path = OUT / f"{stem}.png"

                svg_path.write_text(build_svg(lang, g), encoding="utf-8")
                overflow = render(page, svg_path, png_path, g)

                with Image.open(png_path) as image:
                    print(f"  {stem}  {image.width}x{image.height}  "
                          f"{png_path.stat().st_size:,} bytes")

                for line in overflow:
                    problems.append(f"{stem}: {line}")

        browser.close()

    if problems:
        sys.exit("text past the safe margin:\n  " + "\n  ".join(problems))


if __name__ == "__main__":
    main()
