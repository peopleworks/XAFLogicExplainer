#!/usr/bin/env python3
"""Capture the README and landing-page screenshots from a real explainer.

Every raster figure on the site is a crop of a report this tool actually produced, from the
synthetic PharmacyDemo fixture. Nothing is mocked up, which is the point: the landing page cannot
promise something the tool does not do.

    python site/capture-screenshots.py

It regenerates the report first, so the shots can never show a version of the output that no
longer exists. That has already gone wrong once -- the shots were taken by an uncommitted script
in a scratch directory, and three commits later the sections they showed had different headings.
A figure nothing can regenerate is a claim nothing keeps true.

Needs `pip install playwright pillow` and `python -m playwright install chromium`.
"""

from __future__ import annotations

import pathlib
import subprocess
import sys

from PIL import Image
from playwright.sync_api import sync_playwright

ROOT = pathlib.Path(__file__).resolve().parent.parent
FIXTURE = ROOT / "tests" / "XafLogicExplainer.Tests" / "Fixtures" / "DemoSolution" / "PharmacyDemo.Module"
REPORT = ROOT / "site" / ".report.html"
OUT = ROOT / "docs" / "assets"

# Retina, so the figures stay sharp on the displays most readers have.
SCALE = 2
WIDTH = 1180

# A crop that stops exactly on a card's border catches a sliver of its neighbour, which reads as a
# rendering fault rather than as a deliberate edge.
PAD_EDGE = 13

# Dark in both themes, on purpose. A screenshot is a raster: it cannot follow the page that frames
# it, and a light shot on a dark README glares. Dark reads as a terminal, which is where this tool
# lives anyway.
THEME = "dark"

# selector -> output name. Each is a section of the report, cropped to its own bounds.
# selector, output name, and whether to expand the <details> blocks inside it.
#
# The screens card is deliberately left folded. The two layers are the design -- the team's own
# controllers in full, the framework's behind one line -- and expanding thirty inherited
# controllers across five views produces a figure sixteen thousand pixels tall that says less.
SHOTS = [
    # Prescription, because its detail view is the one with something to show: the team's own
    # controller, the action it contributes, and the framework layer underneath it.
    ("#screens-Prescription", "explainer-screens.png", False),
    ("#entity-Sale", "explainer-entity.png", True),
    ("#criteria", "explainer-criteria.png", True),
    # Whole sections, not the first card: the README describes the built-in editors a controller
    # reconfigures and *both* migration blocks, and a figure has to show what its alt text says.
    ("#editors", "explainer-editors.png", True),
    ("#migrations", "explainer-migrations.png", True),
]


def build_report() -> None:
    """Regenerate the explainer, so the shots match the code in this working copy."""
    print(f"generating {REPORT.relative_to(ROOT)} from {FIXTURE.name}")

    result = subprocess.run(
        [
            "dotnet", "run", "--project", str(ROOT / "src" / "XafLogicExplainer.Cli"),
            "-c", "Release", "--no-launch-profile", "--",
            "explain", "--project", str(FIXTURE), "--output", str(REPORT),
        ],
        capture_output=True,
        text=True,
        cwd=ROOT,
    )

    if result.returncode != 0 or not REPORT.exists():
        sys.exit(f"could not generate the report:\n{result.stdout}\n{result.stderr}")


def capture() -> None:
    OUT.mkdir(parents=True, exist_ok=True)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch()
        page = browser.new_page(
            viewport={"width": WIDTH, "height": 1400},
            device_scale_factor=SCALE,
            color_scheme=THEME,
        )
        page.goto(REPORT.as_uri())

        # The report stores a theme choice; set it explicitly rather than trusting the default.
        page.evaluate(f"document.documentElement.dataset.theme = '{THEME}'")

        page.wait_for_timeout(400)

        for selector, name, expand in SHOTS:
            # Per shot, because a collapsed row hides the point of some figures and *is* the point
            # of others.
            page.evaluate(
                "([selector, expand]) => document.querySelectorAll('details')"
                ".forEach(d => d.open = expand && d.closest(selector) !== null)",
                [selector, expand],
            )
            page.wait_for_timeout(150)

            element = page.query_selector(selector)
            box = element.bounding_box() if element else None

            if box is None:
                print(f"  ! {selector} not found — skipped")
                continue

            # full_page, because a clip is in page coordinates and everything below the fold is
            # outside the viewport image otherwise.
            page.screenshot(
                path=str(OUT / name),
                full_page=True,
                clip={
                    "x": box["x"] - PAD_EDGE,
                    "y": box["y"] - PAD_EDGE,
                    "width": box["width"] + PAD_EDGE * 2,
                    "height": box["height"] + PAD_EDGE * 2,
                },
            )

            with Image.open(OUT / name) as image:
                print(f"  {name}  {image.width}x{image.height}  {(OUT / name).stat().st_size:,} bytes")

        browser.close()

    REPORT.unlink(missing_ok=True)


if __name__ == "__main__":
    build_report()
    capture()
