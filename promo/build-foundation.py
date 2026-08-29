"""Generates the blueprint elevation infographic. Geometry is computed, never eyeballed."""
import io
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "foundation", "infographic.html")

# ---------------------------------------------------------------- measured data
# Measured 2026-08-28 by `xaflogic wiki` over six real XAF module directories.
APPS = [
    ("pwLegalOffice", 231, 78, 66),
    ("PeopleWorksCopilotBackOffice", 56, 6, 9),
    ("pwControlVisita", 54, 6, 6),
    ("PWPresupuesto", 35, 14, 11),
    ("ComisionSOS", 19, 7, 7),
    ("XafAiExtensionsDataAnalysis", 10, 1, 0),
]
TOTAL_E = sum(a[1] for a in APPS)          # 405
TOTAL_C = sum(a[2] for a in APPS)          # 112
TOTAL_A = sum(a[3] for a in APPS)          # 99
SHARED_CLASSES = 16
SHARED_PROPS = 60
SHARED_BASES = 0

# ---------------------------------------------------------------- drawing geometry
VB_W, VB_H = 968, 810
GROUND = 690
MAX_H = 520.0
MAX_V = max(a[1] for a in APPS)            # 231
SCALE = MAX_H / MAX_V                      # px per entity
SLOT, BW = 148, 104
X0 = 62
LEFT = X0                                  # 62
RIGHT = X0 + (len(APPS) - 1) * SLOT + BW   # 906
MID = (LEFT + RIGHT) / 2                   # 484


def buildings():
    """Elevations. Height is data; windows and parapets are drafting detail."""
    out = []
    cols = [12.5, 43.0, 73.5]      # three window columns, 18px wide, inside a 104px facade
    for i, (name, entities, _c, _a) in enumerate(APPS):
        h = entities * SCALE
        x = X0 + i * SLOT
        y = GROUND - h
        out.append(f'      <!-- {name}: {entities} entities x {SCALE:.5f} px = {h:.2f}px -->')
        # Every elevation rises for the same 0.9s; only the delay differs, so every
        # mid-flight frame is a truthful scaled copy of the finished drawing.
        out.append(f'      <g class="bldg-g" style="--d:{0.80 + i * 0.12:.2f}s">')
        out.append(f'      <rect class="bldg" x="{x}" y="{y:.2f}" width="{BW}" height="{h:.2f}"/>')
        # Parapet: a capping band that makes the block read as a roof, not a bar end.
        out.append(f'      <rect class="parapet" x="{x - 5}" y="{y - 7:.2f}" '
                   f'width="{BW + 10}" height="7"/>')
        # Windows, from the roof down; only the rows that fit above the ground.
        row = y + 20
        while row + 14 < GROUND - 10:
            for cx in cols:
                out.append(f'      <rect class="win" x="{x + cx:.1f}" y="{row:.2f}" '
                           f'width="18" height="14"/>')
            row += 26
        # A door, so the smallest sheds still read as buildings.
        dh = min(22.0, max(9.0, h - 5))
        out.append(f'      <rect class="door" x="{x + BW / 2 - 11:.1f}" '
                   f'y="{GROUND - dh:.2f}" width="22" height="{dh:.2f}"/>')
        out.append('      </g>')
        # The count arrives after its building has settled.
        out.append(f'      <g class="anno" style="--d:{2.45 + i * 0.09:.2f}s">')
        out.append(f'      <line class="tick" x1="{x + BW / 2}" y1="{y - 22:.2f}" '
                   f'x2="{x + BW / 2}" y2="{y - 14:.2f}"/>')
        out.append(f'      <text class="count" x="{x + BW / 2}" y="{y - 30:.2f}">{entities}</text>')
        out.append('      </g>')
        # The drop that ties this roof to the shared-vocabulary dimensions above.
        out.append(f'      <g class="drop" style="--d:{3.35 + i * 0.07:.2f}s">')
        out.append(f'      <line class="ext-drop" x1="{x + BW / 2}" y1="118" '
                   f'x2="{x + BW / 2}" y2="{y - 30:.2f}"/>')
        out.append('      </g>')
    return chr(10).join(out)


def footings():
    """Each building gets its own slab, sunk in earth, with earth between them."""
    out = []
    for i, (name, _e, _c, _a) in enumerate(APPS):
        x = X0 + i * SLOT - 4
        out.append(f'      <g class="foot-g" style="--d:{4.35 + i * 0.10:.2f}s">')
        out.append(f'      <rect class="footing" x="{x}" y="{GROUND}" width="{BW + 8}" height="38"/>')
        out.append(f'      <text class="foot-lbl" x="{x + (BW + 8) / 2:.1f}" y="{GROUND + 25}">'
                   f'F{i + 1}</text>')
        out.append('      </g>')
    return chr(10).join(out)


def earth_ticks():
    """The drafting symbol for undisturbed earth: short 45-degree strokes under the ground line.

    Two courses. The upper one is only ever seen in the gaps between the slabs, which is the
    whole point of the drawing: there is ground between these foundations, not shared concrete.
    """
    out = []
    for top in (GROUND + 4, GROUND + 44):
        x = 34
        while x < 940:
            out.append(f'      <line class="earth" x1="{x}" y1="{top}" '
                       f'x2="{x - 12}" y2="{top + 12}"/>')
            x += 15
    return chr(10).join(out)


def scale_figure(x, base):
    """A person, 20px tall, so the elevations read as buildings at human scale."""
    return (f'      <g class="figure">'
            f'<circle cx="{x}" cy="{base - 17}" r="3.4"/>'
            f'<line x1="{x}" y1="{base - 13.5}" x2="{x}" y2="{base - 6}"/>'
            f'<line x1="{x - 5}" y1="{base - 11}" x2="{x + 5}" y2="{base - 11}"/>'
            f'<line x1="{x}" y1="{base - 6}" x2="{x - 4}" y2="{base}"/>'
            f'<line x1="{x}" y1="{base - 6}" x2="{x + 4}" y2="{base}"/></g>')


def knockout(cx, y, text, size=14):
    """A dimension label with a background knockout, so the line breaks around it."""
    w = len(text) * size * 0.6 + 16
    return (f'      <rect class="knock" x="{cx - w / 2:.1f}" y="{y - size * 0.85:.1f}" '
            f'width="{w:.1f}" height="{size * 1.7:.1f}"/>\n'
            f'      <text class="dim" x="{cx}" y="{y + size * 0.36:.1f}" '
            f'font-size="{size}">{text}</text>')


def dimension_line(y, label):
    return (f'      <line class="ext" x1="{LEFT}" y1="{y - 14}" x2="{LEFT}" y2="{y + 14}"/>\n'
            f'      <line class="ext" x1="{RIGHT}" y1="{y - 14}" x2="{RIGHT}" y2="{y + 14}"/>\n'
            f'      <line class="dimline" x1="{LEFT}" y1="{y}" x2="{RIGHT}" y2="{y}" '
            f'marker-start="url(#arrowL)" marker-end="url(#arrowR)"/>\n'
            + knockout(MID, y, label))


def scallop(x, y, w, h, r=13):
    """A revision cloud: a rectangle perimeter drawn as outward semicircular bumps."""
    pts = []
    def run(length, dx, dy, sx, sy):
        n = max(2, round(length / (2 * r)))
        step = length / n
        for _ in range(n):
            sx += dx * step
            sy += dy * step
            pts.append(f'A {step / 2:.2f} {step / 2:.2f} 0 0 1 {sx:.2f} {sy:.2f}')
        return sx, sy
    cx, cy = x, y
    pts.append(f'M {cx:.2f} {cy:.2f}')
    cx, cy = run(w, 1, 0, cx, cy)
    cx, cy = run(h, 0, 1, cx, cy)
    cx, cy = run(w, -1, 0, cx, cy)
    cx, cy = run(h, 0, -1, cx, cy)
    return " ".join(pts) + " Z"


CLOUD = scallop(150, 18, 780, 244)

HTML = f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>The foundation nobody shared</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Saira+Condensed:wght@600;700&family=IBM+Plex+Mono:ital,wght@0,400;0,500;0,600;1,400&display=swap" rel="stylesheet">
<style>
/* ===================================================================
   COMPOSITION: The Big Object.
   The reader is standing over an engineer's drafting sheet: six
   applications drawn in elevation, dimensioned, on separate footings.
   The base class is the foundation, and the sheet measures its absence.
   =================================================================== */
*, *::before, *::after {{ box-sizing: border-box; margin: 0; padding: 0; }}
svg {{ display: block; max-width: 100%; }}

:root {{
  --canvas-w: 1080px; --canvas-h: 1920px;
  --bg:#123A66; --bg-2:#0D3054;
  --line:#D8E8F8; --line-soft:rgb(216 232 248 / 0.45);
  --ink:#EAF2FB; --ink-muted:#9FB8D4;
  --chart-1:#3E9BD6; --chart-2:#B8860B; --chart-3:#C75B9B; --chart-4:#6F9436;
  --de-emphasis:#2A537F;
  --font-display:'Saira Condensed',sans-serif; --font-body:'IBM Plex Mono',monospace;
  --space-1:8px; --space-2:16px; --space-3:24px; --space-4:40px; --space-5:56px;
  --radius:0;
}}

html, body {{ background: var(--bg); }}
body {{ width: var(--canvas-w); font-family: var(--font-body); color: var(--ink); }}

.canvas {{
  position: relative;
  width: var(--canvas-w); height: var(--canvas-h);
  overflow: hidden;
  padding: var(--space-5);
  display: flex; flex-direction: column;
  background-image:
    linear-gradient(var(--line-soft) 1px, transparent 1px),
    linear-gradient(90deg, var(--line-soft) 1px, transparent 1px);
  background-size: 40px 40px;
}}
/* The sheet is not a flat field: a faint vignette settles the corners. */
.canvas::after {{
  content: ""; position: absolute; inset: 0; pointer-events: none;
  background: radial-gradient(ellipse at 50% 38%, transparent 45%, rgb(9 30 54 / .55) 100%);
}}
.frame {{ position: absolute; inset: 12px; border: 2px solid var(--line); }}
.frame::before {{ content: ""; position: absolute; inset: 6px; border: 1px solid var(--line-soft); }}
.reg {{ position: absolute; width: 22px; height: 22px; }}
.reg line {{ stroke: var(--line-soft); stroke-width: 1; }}

.sheet {{ position: relative; z-index: 1; display: flex; flex-direction: column; height: 100%; }}

/* ---------------------------------------------------- header */
.kicker {{
  font: 500 15px/1.4 var(--font-body); color: var(--ink-muted);
  letter-spacing: .18em; text-transform: uppercase;
}}
h1 {{
  font-family: var(--font-display); font-weight: 700; text-transform: uppercase;
  letter-spacing: .04em; line-height: .93; font-size: 76px; color: var(--ink);
  margin-top: var(--space-2);
}}
h1 em {{ font-style: normal; color: var(--chart-2); }}
.standfirst {{
  font: 400 16px/1.6 var(--font-body); color: var(--ink-muted);
  margin-top: var(--space-3); max-width: 760px;
}}

/* ---------------------------------------------------- the drawing */
.drawing {{ margin-top: var(--space-2); }}
.bldg {{ fill: url(#hatch); stroke: var(--line); stroke-width: 2.5; }}
.parapet {{ fill: var(--de-emphasis); stroke: var(--line); stroke-width: 2; }}
.win {{ fill: var(--bg); stroke: var(--line-soft); stroke-width: 1; }}
.door {{ fill: var(--bg-2); stroke: var(--line); stroke-width: 1.25; }}
.ext-drop {{ stroke: var(--line-soft); stroke-width: 1; stroke-dasharray: 3 5; }}
.earth {{ stroke: var(--line-soft); stroke-width: 1; }}
.foot-lbl {{
  font-family: var(--font-body); font-size: 12px; font-weight: 500;
  fill: var(--ink-muted); text-anchor: middle; letter-spacing: .1em;
}}
.figure line, .figure circle {{ stroke: var(--line); stroke-width: 1.4; fill: none; }}
.tick {{ stroke: var(--line); stroke-width: 1.25; }}
.count {{
  font-family: var(--font-display); font-weight: 600; font-size: 26px;
  fill: var(--ink); text-anchor: middle;
}}
.ground {{ stroke: var(--line); stroke-width: 2.5; }}
.footing {{ fill: var(--de-emphasis); stroke: var(--line); stroke-width: 1.5; }}
.dimline, .ext {{ stroke: var(--line); stroke-width: 1; }}
.knock {{ fill: var(--bg); }}
.dim {{
  font-family: var(--font-body); font-weight: 500; fill: var(--ink);
  text-anchor: middle; letter-spacing: .08em;
}}
.gap-note {{
  font-family: var(--font-body); font-size: 13px; fill: var(--ink-muted);
  text-anchor: middle; letter-spacing: .1em;
}}

/* ---------------------------------------------------- the hero */
.hero {{ position: relative; margin-top: var(--space-2); height: 280px; }}
.cloud {{ position: absolute; inset: 0; }}
.cloud path {{ fill: none; stroke: var(--chart-2); stroke-width: 2; }}
.rev {{
  position: absolute; left: 150px; top: -13px;
  background: var(--bg); padding: 0 8px;
  font: 600 13px/1 var(--font-body); color: var(--chart-2); letter-spacing: .16em;
}}
.hero-inner {{
  position: absolute; inset: 0;
  display: flex; align-items: center; justify-content: center; gap: 40px;
}}
.zero {{
  font-family: var(--font-display); font-weight: 700; font-size: 208px; line-height: .8;
  color: var(--ink);
}}
.zero-label {{ max-width: 430px; }}
.zero-label .big {{
  font-family: var(--font-display); font-weight: 700; text-transform: uppercase;
  letter-spacing: .04em; font-size: 40px; line-height: 1; color: var(--ink);
}}
.zero-label .small {{
  font: 400 15px/1.6 var(--font-body); color: var(--ink-muted); margin-top: 12px;
}}

/* ---------------------------------------------------- stat row */
.stats {{ display: flex; gap: var(--space-4); margin-top: var(--space-4); }}
.stat {{ flex: 1; border-top: 2px solid var(--line); padding-top: 12px; }}
.stat b {{
  display: block; font-family: var(--font-display); font-weight: 700;
  font-size: 54px; line-height: 1; color: var(--ink);
}}
.stat span {{
  display: block; font: 500 13px/1.4 var(--font-body); color: var(--ink-muted);
  letter-spacing: .14em; text-transform: uppercase; margin-top: 6px;
}}

/* ====================================================================
   MOTION — a layer on the approved still. The final frame IS the still:
   every entrance is a from-only keyframe with `backwards`, so nothing is
   left compositing on the finished frame. Blueprint never bounces.
   Build order: the sheet is already there -> ground -> elevations rise ->
   counts -> what they share -> the six pours -> the hero -> the footer.
   ==================================================================== */
@keyframes m-fade   {{ from {{ opacity: 0; }} }}
@keyframes m-rise   {{ from {{ transform: scaleY(0); }} }}
@keyframes m-draw   {{ from {{ stroke-dashoffset: 1; }} }}
@keyframes m-land   {{ from {{ opacity: 0; transform: scale(.86); }} }}
@keyframes m-lift   {{ from {{ opacity: 0; transform: translateY(14px); }} }}

/* The sheet arrives already titled. Frame 0 is the poster frame of a short,
   and an empty grid is a wasted one -- the place, and what it is a drawing
   OF, are both there before anything is built. */
.m-head3 {{ animation: m-fade .6s ease-out .45s backwards; }}

/* structure: the ground is surveyed before anything is built on it */
.ground {{ stroke-dasharray: 1; animation: m-draw .9s cubic-bezier(.2,.8,.2,1) .30s both; }}
.earth-g {{ animation: m-fade .7s ease-out 1.05s backwards; }}

/* data: every elevation grows from its own footing line, same duration for all */
.bldg-g {{
  transform-box: view-box; transform-origin: 0px {GROUND}px;
  animation: m-rise .9s cubic-bezier(.2,.8,.2,1) backwards;
  animation-delay: var(--d);
}}
.anno {{ animation: m-fade .5s ease-out backwards; animation-delay: var(--d); }}
.drop {{ animation: m-fade .5s ease-out backwards; animation-delay: var(--d); }}
.dims-g {{ animation: m-fade .8s ease-out 3.05s backwards; }}
.foot-g {{ animation: m-fade .55s ease-out backwards; animation-delay: var(--d); }}
.motion-note {{ animation: m-fade .6s ease-out 5.00s backwards; }}

/* the hero lands alone: nothing else moves while the cloud closes on it */
.m-cloud  {{ stroke-dasharray: 1; animation: m-draw 1.1s cubic-bezier(.2,.8,.2,1) 5.35s both; }}
.m-rev    {{ animation: m-fade .4s ease-out 6.25s backwards; }}
.m-zero   {{ animation: m-land .8s cubic-bezier(.2,.8,.2,1) 5.95s backwards; }}
.m-zlabel {{ animation: m-lift .7s cubic-bezier(.2,.8,.2,1) 6.20s backwards; }}

/* the footer arrives almost unnoticed */
.m-stats {{ animation: m-fade .6s ease-out 6.55s backwards; }}
.m-block {{ animation: m-fade .6s ease-out 6.75s backwards; }}

/* ---------------------------------------------------- title block */
.spacer {{ flex: 1; min-height: 8px; }}
.titleblock {{
  align-self: flex-end; width: 640px;
  border: 2px solid var(--line); background: var(--bg-2);
}}
.titleblock .row {{
  display: flex; border-bottom: 1px solid var(--line-soft);
}}
.titleblock .row:last-child {{ border-bottom: 0; }}
.titleblock .k {{
  width: 118px; padding: 9px 12px; border-right: 1px solid var(--line-soft);
  font: 500 12px/1.3 var(--font-body); color: var(--ink-muted); letter-spacing: .12em;
}}
.titleblock .v {{
  flex: 1; padding: 9px 12px;
  font: 500 13px/1.35 var(--font-body); color: var(--ink);
}}
</style>
</head>
<body>
<div class="canvas">
  <div class="frame"></div>
  <svg class="reg" style="left:30px;  top:30px"    viewBox="0 0 22 22"><line x1="11" y1="0" x2="11" y2="22"/><line x1="0" y1="11" x2="22" y2="11"/></svg>
  <svg class="reg" style="right:30px; top:30px"    viewBox="0 0 22 22"><line x1="11" y1="0" x2="11" y2="22"/><line x1="0" y1="11" x2="22" y2="11"/></svg>
  <svg class="reg" style="left:30px;  bottom:30px" viewBox="0 0 22 22"><line x1="11" y1="0" x2="11" y2="22"/><line x1="0" y1="11" x2="22" y2="11"/></svg>

  <div class="sheet">

    <div class="kicker m-head">Six XAF applications &middot; one author &middot; read from source</div>
    <h1 class="m-head2">The foundation<br>nobody <em>shared</em></h1>
    <div class="standfirst m-head3">
      The same developer modelled the same ideas six times over. The words repeat.
      The structure never does.
    </div>

    <svg class="drawing" viewBox="0 0 {VB_W} {VB_H}" role="img"
         aria-label="Six application elevations of proportional height, each on its own separate footing">
      <defs>
        <pattern id="hatch" width="8" height="8" patternTransform="rotate(45)" patternUnits="userSpaceOnUse">
          <rect width="8" height="8" fill="var(--bg)"/>
          <line x1="0" y1="0" x2="0" y2="8" stroke="var(--chart-1)" stroke-width="2.4"/>
        </pattern>
        <marker id="arrowL" markerWidth="9" markerHeight="9" refX="8" refY="4.5" orient="auto">
          <path d="M9 0 L0 4.5 L9 9 Z" fill="var(--line)"/>
        </marker>
        <marker id="arrowR" markerWidth="9" markerHeight="9" refX="1" refY="4.5" orient="auto">
          <path d="M0 0 L9 4.5 L0 9 Z" fill="var(--line)"/>
        </marker>
      </defs>

      <!-- what the six DO share, dimensioned across the whole row -->
      <g class="dims-g">
{dimension_line(56, f"{SHARED_CLASSES} CLASS NAMES SHARED")}
{dimension_line(104, f"{SHARED_PROPS} PROPERTY NAMES SHARED")}
      </g>

      <!-- elevations: height is proportional to entity count, {SCALE:.5f} px per entity -->
{buildings()}

      <!-- ground line, then undisturbed earth beneath it -->
      <line class="ground" x1="30" y1="{GROUND}" x2="938" y2="{GROUND}" pathLength="1"/>
      <g class="earth-g">
{earth_ticks()}
{scale_figure(188, GROUND)}
      </g>

      <!-- and what they do not share: six slabs, poured six times, earth between them -->
{footings()}
      <text class="gap-note motion-note" x="{MID}" y="{GROUND + 76}">SIX SEPARATE FOUNDATIONS &middot; NOT ONE OF THEM THE SAME POUR</text>
    </svg>

    <div class="hero" data-hero data-overlap-ok>
      <svg class="cloud" viewBox="0 0 1080 286" preserveAspectRatio="none">
        <path class="m-cloud" d="{CLOUD}" pathLength="1"/>
      </svg>
      <span class="rev m-rev">REV A</span>
      <div class="hero-inner">
        <div class="zero m-zero">{SHARED_BASES}</div>
        <div class="zero-label m-zlabel">
          <div class="big">Base classes<br>they share</div>
          <div class="small">
            Not one. Every application rebuilt its own persistence layer
            from XPO primitives, alone.
          </div>
        </div>
      </div>
    </div>

    <div class="stats m-stats">
      <div class="stat"><b>{TOTAL_E}</b><span>Entities</span></div>
      <div class="stat"><b>{TOTAL_C}</b><span>Controllers</span></div>
      <div class="stat"><b>{TOTAL_A}</b><span>Actions</span></div>
    </div>

    <div class="spacer"></div>

    <div class="titleblock m-block">
      <div class="row"><div class="k">TITLE</div><div class="v">The foundation nobody shared</div></div>
      <div class="row"><div class="k">DWG NO</div><div class="v">XLE-001 &middot; REV A</div></div>
      <div class="row"><div class="k">SCALE</div><div class="v">Elevation height &prop; entity count ({SCALE:.4f} px per entity)</div></div>
      <div class="row"><div class="k">DATE</div><div class="v">2026-08-28</div></div>
      <div class="row"><div class="k">SOURCE</div><div class="v">xaflogic wiki, read from source. pwLegalOffice &middot; PeopleWorksCopilotBackOffice &middot; pwControlVisita &middot; PWPresupuesto &middot; ComisionSOS &middot; XafAiExtensionsDataAnalysis</div></div>
    </div>

  </div>
</div>
</body>
</html>
"""

os.makedirs(os.path.dirname(OUT), exist_ok=True)
io.open(OUT, "w", encoding="utf-8", newline="\n").write(HTML)
print("wrote", OUT)
print(f"scale {SCALE:.5f} px/entity | totals {TOTAL_E}/{TOTAL_C}/{TOTAL_A}")
