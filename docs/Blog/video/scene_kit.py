"""What the landscape scenes and the vertical shorts both need.

Shared rather than copied for one reason: the palette. Two generators each holding their own
`#ff8a3d` is two oranges waiting to drift apart, and a short that does not match the film it
promotes looks like somebody else made it.

Everything here is format-agnostic. Anything that assumes 1280x720 or 1080x1920 belongs in the
generator that owns that shape.
"""

from __future__ import annotations

import pathlib
import re
import shutil
import subprocess
import sys
from xml.sax.saxutils import escape

ROOT = pathlib.Path(__file__).resolve().parents[3]
VIDEO = ROOT / "docs" / "Blog" / "video"
FIXTURE_ROOT = ROOT / "tests" / "XafLogicExplainer.Tests" / "Fixtures" / "DemoSolution"
FIXTURE = FIXTURE_ROOT / "PharmacyDemo.Module"

FPS = 30

# The site's dark tokens, resolved once. A frame of video and a figure in the README have to be
# the same colours or the whole set stops looking like one project.
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
    "ok": "#4ade80",
}


def base_css(w: int, h: int) -> str:
    """The frame itself: size, ground, grid, and the keyframes every scene animates with."""
    return """
* { margin: 0; padding: 0; box-sizing: border-box; }
html, body { width: %(W)dpx; height: %(H)dpx; overflow: hidden; }
body {
  background: %(bg)s;
  color: %(ink)s;
  font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  -webkit-font-smoothing: antialiased;
}
/* The same barely-there grid the covers use, so a still lifted from a frame sits beside them
   without looking like it came from somewhere else. */
body::before {
  content: ""; position: fixed; inset: 0; pointer-events: none;
  background-image: radial-gradient(circle at 1px 1px, %(ink)s 1px, transparent 0);
  background-size: 26px 26px; opacity: .035;
}
.main { flex: 1; display: flex; flex-direction: column; justify-content: center; }
.mono { font-family: ui-monospace, "Cascadia Mono", "JetBrains Mono", Menlo, Consolas, monospace; }
.yours { color: %(yours)s; }
.grey { color: %(graphite_ink)s; }
.mut { color: %(mute)s; }

/* Every animation is `both` and starts at zero, because frames are captured by setting
   currentTime -- a negative delay would place a frame before the timeline and capture nothing. */
@keyframes rise  { from { opacity: 0; transform: translateY(18px); } to { opacity: 1; transform: none; } }
@keyframes fade  { from { opacity: 0; } to { opacity: 1; } }
@keyframes dim   { from { opacity: 1; } to { opacity: .3; } }
@keyframes out   { from { opacity: 1; transform: none; } to { opacity: 0; transform: translateY(-12px); } }
@keyframes wipe  { from { clip-path: inset(0 100%% 0 0); } to { clip-path: inset(0 0 0 0); } }
@keyframes grow  { from { transform: scaleX(0); } to { transform: scaleX(1); } }
""" % {**PALETTE, "W": w, "H": h}


CODE_CSS = """
/* min-width:0, or a grid column sizes itself to the longest unwrappable code line and pushes
   the whole scene off the side of the frame. */
.src { border:1px solid %(line)s; background:%(panel)s; border-radius:13px; overflow:hidden;
       min-width:0; }
.src__path {
  padding:11px 18px; font-size:14px; color:%(faint)s;
  border-bottom:1px solid %(line)s; background:%(bg)s;
}
.src__code { padding:18px 20px; line-height:1.66; color:%(graphite_ink)s; position:relative; }
.src__code .ln { white-space:pre; }
.src__code .hit { color:%(yours)s; font-weight:600; }
/* Real code has real long lines. A hard cut mid-token reads as a broken render; a fade reads
   as "this line continues", which is true. */
.src__code::after {
  content:""; position:absolute; top:0; right:0; bottom:0; width:58px; pointer-events:none;
  background:linear-gradient(to right, rgba(16,21,29,0), %(panel)s 82%%);
}
""" % PALETTE


def a(name: str, delay: float, dur: float = 0.5) -> str:
    """The style attribute for one animation. Delays stagger; nothing is ever negative."""
    return f'style="animation:{dur}s cubic-bezier(.2,.7,.3,1) {delay}s both {name}"'


def a2(first: tuple[str, float, float], second: tuple[str, float, float]) -> str:
    """Two animations on one element -- how a line leaves so the next one can have the space."""
    parts = ", ".join(f"{dur}s cubic-bezier(.2,.7,.3,1) {delay}s both {name}"
                      for name, delay, dur in (first, second))
    return f'style="animation:{parts}"'


def snippet(rel: str, anchor: str, lines: int, before: int = 0) -> list[str]:
    """Lift real lines out of a fixture file, anchored on their text rather than their number.

    Developers watching a technical video want to read actual code, and code invented for a
    slide is the one thing on screen nothing keeps true. Anchoring on the text means a fixture
    edit either still matches or fails the build here -- never silently shows the wrong lines.
    """
    source = (FIXTURE_ROOT / rel).read_text(encoding="utf-8").splitlines()
    hits = [i for i, line in enumerate(source) if anchor in line]
    if len(hits) != 1:
        sys.exit(f"{rel}: {'no line' if not hits else f'{len(hits)} lines'} containing {anchor!r}")

    start = max(0, hits[0] - before)
    picked = source[start:start + lines]

    # Dedent as a block, so relative indentation -- the only thing saying what is nested in
    # what -- survives being taken out of its file.
    pad = min((len(l) - len(l.lstrip()) for l in picked if l.strip()), default=0)
    return [l[pad:] if l.strip() else "" for l in picked]


def code_block(rel: str, code: list[str], delay: float, marks: tuple[str, ...] = (),
               step: float = 0.28, size: int = 17) -> str:
    """A file chip over its own lines, wiping in one at a time. `marks` get the accent."""
    body = []
    for i, line in enumerate(code):
        text = escape(line)
        for mark in marks:
            # Whole tokens only. A bare replace lit up the "PropertyEditor" inside
            # BarcodeScannerPropertyEditor, which reads as a rendering fault, not emphasis.
            text = re.sub(rf"\b{re.escape(escape(mark))}\b",
                          f'<span class="hit">{escape(mark)}</span>', text)
        body.append(f'<div class="ln" {a("wipe", delay + 0.5 + i * step, 0.4)}>{text or "&nbsp;"}</div>')
    return (f'<div class="src">'
            f'<div class="src__path mono" {a("fade", delay, 0.5)}>{escape(rel)}</div>'
            f'<div class="src__code mono" style="font-size:{size}px">{"".join(body)}</div></div>')


def test_count() -> str:
    """Read the suite size off the README, which has a test of its own keeping it honest.

    Typing it into a slide would put a number on screen that nothing forces to stay true --
    the exact failure this material is about.
    """
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    hits = set(re.findall(r"\*\*(\d[\d,]*) tests\*\*", readme))
    if len(hits) != 1:
        sys.exit(f"README.md: expected one '**N tests**' claim, found {sorted(hits) or 'none'}")
    return hits.pop()


def build_report(out: pathlib.Path) -> None:
    """Run the tool, so anything filmed is output it actually produces."""
    result = subprocess.run(
        ["dotnet", "run", "--project", str(ROOT / "src" / "XafLogicExplainer.Cli"),
         "-c", "Release", "--no-launch-profile", "--",
         "explain", "--project", str(FIXTURE), "--output", str(out)],
        capture_output=True, text=True, cwd=ROOT)
    if result.returncode != 0 or not out.exists():
        sys.exit(f"could not generate the report:\n{result.stdout}\n{result.stderr}")


def page(base: str, css: str, body: str) -> str:
    return (f"<!doctype html>\n<html><head><meta charset=\"utf-8\">\n<style>\n{base}\n{css}\n"
            f"</style></head>\n<body>{body}\n</body></html>\n")


def freeze(page_obj, seconds: float) -> None:
    """Put every animation at exactly `seconds`, so a frame never depends on wall-clock time."""
    page_obj.evaluate(
        "t => document.getAnimations().forEach(a => { a.pause(); a.currentTime = t * 1000; })",
        seconds)


def overflowing(page_obj, w: int, h: int, root: str = ".stage") -> str:
    """How far the scene's content runs past the frame, if it does.

    A frame silently crops whatever does not fit, so a line of copy one word longer just goes
    missing rather than looking wrong -- and it goes missing in a video, where nobody scrolls
    back. A few pixels of slack, because getBBox reports ink and a glyph leans past its origin.
    """
    dx, dy = page_obj.evaluate(
        """([sel, w, h]) => {
             const stage = document.querySelector(sel);
             // Anything inside a clipping box is the design -- a capture panned inside its
             // window -- so skip those subtrees, stopping at the stage: body hides overflow.
             const clipped = e => {
               for (let p = e.parentElement; p && p !== stage.parentElement; p = p.parentElement) {
                 const o = getComputedStyle(p);
                 if (o.overflow !== 'visible' || o.overflowY !== 'visible') return true;
               }
               return false;
             };
             let dx = 0, dy = 0;
             for (const e of stage.querySelectorAll('*')) {
               if (clipped(e)) continue;
               const r = e.getBoundingClientRect();
               dx = Math.max(dx, r.right - w);
               dy = Math.max(dy, r.bottom - h);
             }
             return [Math.round(Math.max(0, dx)), Math.round(Math.max(0, dy))];
           }""", [root, w, h])
    bits = [f"{dx}px right"] if dx > 3 else []
    bits += [f"{dy}px below"] if dy > 3 else []
    return " and ".join(bits)


def render(page_obj, html: pathlib.Path, out: pathlib.Path, seconds: float) -> None:
    """Frame by frame, then ffmpeg. Slow on purpose: deterministic beats fast for an artefact
    that gets re-rendered every time the copy changes."""
    if shutil.which("ffmpeg") is None:
        sys.exit("rendering needs ffmpeg on PATH")

    out.parent.mkdir(parents=True, exist_ok=True)
    frames = out.parent / f".{out.stem}-frames"
    if frames.exists():
        shutil.rmtree(frames)
    frames.mkdir()

    page_obj.goto(html.as_uri())
    page_obj.wait_for_timeout(120)
    total = int(seconds * FPS)
    for i in range(total):
        freeze(page_obj, i / FPS)
        page_obj.screenshot(path=str(frames / f"{i:05d}.png"))

    subprocess.run(
        ["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS),
         "-i", str(frames / "%05d.png"), "-c:v", "libx264", "-pix_fmt", "yuv420p",
         "-crf", "17", "-preset", "slow", str(out)],
        check=True)
    shutil.rmtree(frames)
    print(f"    -> {out.relative_to(VIDEO)}  {out.stat().st_size:,} bytes  ({total} frames)")
