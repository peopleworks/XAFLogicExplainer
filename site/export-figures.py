#!/usr/bin/env python3
"""Export the site's inline figures as standalone SVGs for the README.

The figures live inside site/index.html, where they inherit the page's design tokens and its
light/dark switch. A file on its own inherits nothing, and GitHub renders an SVG through an
<img>, which cannot see the theme of the page framing it -- so a copied figure would arrive
colourless.

This writes each figure twice, with the tokens resolved for each theme, and README wires them
with <picture> so GitHub picks the right one. Generated rather than hand-copied so the README
figures cannot drift from the site.

    python site/export-figures.py
"""

from __future__ import annotations

import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "site" / "index.html"
OUT = ROOT / "docs" / "assets"

# Only the tokens the figures actually reference. Kept in step with :root in index.html by hand,
# which is a small surface: the figure palette is deliberately short.
THEMES = {
    "dark": {
        "--fig-bg": "#0a0d12",
        "--fig-panel": "#10151d",
        "--fig-line": "#232b36",
        "--fig-graphite": "#46505e",
        "--fig-graphite-ink": "#737f8f",
        "--fig-ink": "#e8edf3",
        "--fig-mute": "#8794a4",
        "--fig-yours": "#ff8a3d",
        "--fig-yours-soft": "rgba(255,138,61,.14)",
        "--fig-ok": "#4ade80",
    },
    "light": {
        "--fig-bg": "#ffffff",
        "--fig-panel": "#f4f6f9",
        "--fig-line": "#d8dee6",
        "--fig-graphite": "#b3bcc7",
        "--fig-graphite-ink": "#6b7684",
        "--fig-ink": "#12171d",
        "--fig-mute": "#5b6673",
        "--fig-yours": "#c2410c",
        "--fig-yours-soft": "rgba(194,65,12,.10)",
        "--fig-ok": "#15803d",
    },
}

# The class rules the figures use, inlined so the file stands alone.
CLASS_RULES = """
    text { font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
    .t-mono { font-family: ui-monospace, "Cascadia Mono", Menlo, Consolas, monospace; }
    .t-cap { font-family: ui-monospace, "Cascadia Mono", Menlo, Consolas, monospace;
             font-size: 11px; letter-spacing: .12em; text-transform: uppercase; fill: var(--fig-mute); }
    .t-title { font-size: 15px; font-weight: 640; fill: var(--fig-ink); }
    .t-body { font-size: 12.5px; fill: var(--fig-mute); }
    .t-yours { fill: var(--fig-yours); font-weight: 640; }
    .t-grey { fill: var(--fig-graphite-ink); }
"""

FIGURES = {
    "fig-gap": "how-it-fits",
    "fig-context": "two-tier-context",
    "fig-pipeline": "extraction-pipeline",
}


def extract(html: str, figure_id: str) -> str:
    """Pull one <svg>…</svg> out of the figure element carrying the given id."""
    anchor = html.index(f'id="{figure_id}"')
    start = html.index("<svg", anchor)
    end = html.index("</svg>", start) + len("</svg>")
    return html[start:end]


def standalone(svg: str, theme: str) -> str:
    """Give a figure its own tokens so it renders correctly with nothing around it."""
    tokens = "\n".join(f"      {name}: {value};" for name, value in THEMES[theme].items())
    style = f"<style>\n    svg {{\n{tokens}\n    }}\n{CLASS_RULES}  </style>\n  "

    # The animation classes do nothing without the page's keyframes; strip the stroke-dasharray
    # they rely on so every connector is simply drawn.
    svg = svg.replace('class="draw draw--2"', "").replace('class="draw draw--3"', "")
    svg = svg.replace('class="draw"', "")

    # Inline SVG in an HTML document needs no namespace; a standalone file will not render
    # without one, and an <img> with no intrinsic size is at the mercy of whatever frames it.
    width, height = svg.split('viewBox="0 0 ', 1)[1].split('"', 1)[0].split()
    svg = svg.replace(
        "<svg ",
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" ',
        1,
    )

    marker = ">"
    head_end = svg.index(marker) + 1
    return svg[:head_end] + "\n  " + style + svg[head_end:] + "\n"


def main() -> None:
    html = SOURCE.read_text(encoding="utf-8")
    OUT.mkdir(parents=True, exist_ok=True)

    for figure_id, name in FIGURES.items():
        svg = extract(html, figure_id)
        for theme in THEMES:
            path = OUT / f"{name}-{theme}.svg"
            path.write_text(standalone(svg, theme), encoding="utf-8")
            print(f"{path.relative_to(ROOT)}  {len(path.read_text(encoding='utf-8')):,} bytes")


if __name__ == "__main__":
    main()
