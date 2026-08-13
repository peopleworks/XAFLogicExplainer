#!/usr/bin/env python3
"""Build the video scenes named by guion.video.{en,es}.json.

The scripts have always referenced `scenes/<lang>/<id>.html`; nothing wrote them. This does,
from the same palette as the site, so a frame of the video and a figure in the README cannot
end up two different oranges.

    python docs/Blog/video/build-scenes.py            # HTML + one poster PNG per scene
    python docs/Blog/video/build-scenes.py --video    # also render every scene to mp4
    python docs/Blog/video/build-scenes.py --video 01-gancho 07-pantallas

Two kinds of scene, and the difference is the whole point:

  * **Argument scenes** are drawn here, because there is nothing real to film -- a claim about
    three tools composing is not a screenshot of anything.
  * **Evidence scenes** are the tool's own output, captured from a real run against the
    synthetic PharmacyDemo fixture. A hand-built HTML mock-up of the report would look better
    and mean less, and a video full of mock-ups is exactly what the article complains about.

Frames are captured by setting `currentTime` on every animation rather than by recording in
real time, so a slow machine produces the same file as a fast one and a re-render is diffable.

Needs `pip install playwright pillow`, `python -m playwright install chromium`, and ffmpeg on
PATH for --video. Generating the evidence scenes also needs the .NET SDK, to run the tool.
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import shutil
import subprocess
import sys
from xml.sax.saxutils import escape

from PIL import Image
from playwright.sync_api import sync_playwright

ROOT = pathlib.Path(__file__).resolve().parents[3]
VIDEO = ROOT / "docs" / "Blog" / "video"
SCENES = VIDEO / "scenes"
STILLS = VIDEO / "stills"        # real output, captured once and panned over
POSTERS = VIDEO / "posters"      # one frame per scene, to judge a design without rendering
RENDER = VIDEO / "render"
FIXTURE = ROOT / "tests" / "XafLogicExplainer.Tests" / "Fixtures" / "DemoSolution" / "PharmacyDemo.Module"
REPORT = VIDEO / ".report.html"

W, H = 1280, 720
FPS = 30

# What a narration voice can actually deliver. Below `TIGHT` is comfortable, above `IMPOSSIBLE`
# is not a pacing choice, it is an unusable take -- and finding that out after recording costs a
# recording session.
WPM_TIGHT = 190
WPM_IMPOSSIBLE = 225

# The pace to plan a scene around, and the breath after the last word. Only an estimate: the
# scene length that actually matters is the length of the recorded audio, so once a take exists
# in audio/<lang>/<scene>.<ext> its measured duration wins and this constant stops being used.
WPM_NATURAL = 165
TAIL_SECONDS = 0.9
AUDIO = VIDEO / "audio"
AUDIO_EXTS = (".mp3", ".wav", ".m4a", ".ogg")

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

BASE_CSS = """
* { margin: 0; padding: 0; box-sizing: border-box; }
html, body { width: %(W)dpx; height: %(H)dpx; overflow: hidden; }
body {
  background: %(bg)s;
  color: %(ink)s;
  font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  -webkit-font-smoothing: antialiased;
}
/* The same barely-there grid the covers use, so a still lifted from the video sits beside
   them without looking like it came from somewhere else. */
body::before {
  content: ""; position: fixed; inset: 0; pointer-events: none;
  background-image: radial-gradient(circle at 1px 1px, %(ink)s 1px, transparent 0);
  background-size: 26px 26px; opacity: .035;
}
/* Column rather than absolute placement: the content block centres itself in whatever room is
   left between the eyebrow and the closing line, so a scene with two rows and one with three
   do not both leave a hole in the middle of the frame. */
.stage {
  position: relative; width: 100%%; height: 100%%; padding: 52px 72px 62px;
  display: flex; flex-direction: column;
}
.main { flex: 1; display: flex; flex-direction: column; justify-content: center; }
.mono { font-family: ui-monospace, "Cascadia Mono", "JetBrains Mono", Menlo, Consolas, monospace; }
.cap {
  font-family: ui-monospace, "Cascadia Mono", Menlo, Consolas, monospace;
  font-size: 13px; letter-spacing: .14em; text-transform: uppercase; color: %(faint)s;
}
.head { font-size: 44px; font-weight: 640; letter-spacing: -.015em; line-height: 1.18; }
.yours { color: %(yours)s; }
.grey { color: %(graphite_ink)s; }
.mut { color: %(mute)s; }

.pill {
  display: inline-block; padding: 7px 15px; border-radius: 999px;
  border: 1px solid %(line)s; color: %(mute)s; font-size: 15px; margin-right: 10px;
}
.pill--accent { border-color: %(yours)s; color: %(yours)s; }

/* Every animation is `both` and starts at zero, because frames are captured by setting
   currentTime -- a negative delay would place a frame before the timeline and capture nothing. */
[class*="anim-"] { animation-duration: .5s; animation-fill-mode: both; animation-timing-function:
  cubic-bezier(.2,.7,.3,1); }
@keyframes rise  { from { opacity: 0; transform: translateY(18px); } to { opacity: 1; transform: none; } }
@keyframes fade  { from { opacity: 0; } to { opacity: 1; } }
@keyframes dim   { from { opacity: 1; } to { opacity: .3; } }
@keyframes out   { from { opacity: 1; transform: none; } to { opacity: 0; transform: translateY(-12px); } }
@keyframes wipe  { from { clip-path: inset(0 100%% 0 0); } to { clip-path: inset(0 0 0 0); } }
@keyframes grow  { from { transform: scaleX(0); } to { transform: scaleX(1); } }
.anim-rise { animation-name: rise; }
.anim-fade { animation-name: fade; }
.anim-dim  { animation-name: dim; }
.anim-wipe { animation-name: wipe; }
.anim-grow { animation-name: grow; transform-origin: left center; }
""" % {**PALETTE, "W": W, "H": H}


# ---------------------------------------------------------------------------------------------
# Copy. Deliberately not the narration: on-screen text that repeats a voice-over word for word
# makes a viewer read instead of listen. It has to agree with it, not duplicate it.
# ---------------------------------------------------------------------------------------------

COPY = {
    "en": {
        "intro": {
            "name": "XAF LOGIC EXPLAINER",
            "line": "Teach your coding agent what your application does",
            "pills": ["Free", "MIT", ".NET 10"],
        },
        "01-gancho": {
            "cap": "YOU ASK YOUR CODING AGENT",
            "prompt": "Add a validation rule to Invoice",
            "code": [
                '[RuleCriteria("Invoice must balance",',
                "    DefaultContexts.Save,",
                '    "TotalAmount = Sum(Lines.Amount)")]',
            ],
            "flag": "your class has",
            "flag_code": "Total",
            "line1": "It isn't hallucinating XAF.",
            "line2a": "It knows XAF. It has never seen ",
            "line2b": "yours.",
        },
        "02-la-brecha": {
            "cap": "THREE THINGS AN AGENT NEEDS TO KNOW",
            "rows": [
                ("How XAF works", "DevExpress agent-skills"),
                ("What the documentation says", "DevExpress Docs MCP"),
            ],
            "row3": ("What YOUR application does", "XAF Logic Explainer"),
            "close": "They compose. None of them replaces the others.",
        },
        "03-dos-minutos": {
            "cap": "TWO MINUTES, START TO FINISH",
            "cmds": [
                "dotnet tool install -g XafLogicExplainer.Cli",
                "xaflogic agents --project MyApp.Module",
            ],
            "files": ["AGENTS.md", "CLAUDE.md", ".github/copilot-instructions.md"],
            "close": "No account. No API key. Nothing uploaded.",
        },
        "04-oculto": {
            "cap": "THE ENTITIES ARE THE EASY PART",
            "lead": "None of this is in the business class:",
            "chips": ["Model Editor (.xafml)", "Custom editors, and their JavaScript",
                      "Version-gated migrations", "Which controllers load onto a screen"],
        },
        "05-editores": {
            "cap": "A STRING THAT RENDERS AS A SCANNER",
            "note1": "In the platform project, beside the module. Nobody reading the business objects meets it.",
            "note2": "Behaviour in neither C# nor XML — and your agent reads neither of these.",
        },
        "06-migraciones": {
            "cap": "AT MOST ONCE PER DATABASE",
            "stamp": "RAN ONCE",
            "note": "Today's code cannot recover what it did. Ask why a column holds that value and the agent invents a reason.",
        },
        "07-pantallas": {
            "chip": "What runs when you open this screen",
            "callout1": "XAF generates the screens. They are in no file.",
            "callout2": "Which controllers load is four conditions, decided at run time.",
            "foot": "Real output — PharmacyDemo, the repository's synthetic fixture",
        },
        "08-roslyn": {
            "cap": "READ AS SYNTAX, NEVER COMPILED",
            "facts": ["Works on a branch that does not build",
                      "No DevExpress assembly is ever referenced",
                      "{tests} tests run free on a public runner"],
        },
        "10-mapa": {
            "chip": "The same extraction, for a person",
            "callout1": "A map of your domain model that most teams have never seen.",
            "callout2": "It lives in one person's head — which is what leaves when they do.",
            "foot": "Real output — PharmacyDemo, the repository's synthetic fixture",
        },
        "09-niveles": {
            "cap": "TWO TIERS, ON PURPOSE",
            "bar1": ("AGENTS.md — read on every single request", "11 KB", 16),
            "bar2": ("opened only when the agent needs it", "70 KB", 100),
            "close": "If it isn't listed, it does not exist.",
        },
        "11-cierre": {
            "line1": "Your agent already knows XAF.",
            "line2a": "Let's teach it ",
            "line2b": "your application.",
            "cmd": "dotnet tool install -g XafLogicExplainer.Cli",
            "repo": "github.com/peopleworks/XAFLogicExplainer",
            "pills": ["Free", "MIT", ".NET 10"],
        },
    },
    "es": {
        "intro": {
            "name": "XAF LOGIC EXPLAINER",
            "line": "Enséñale a tu agente qué hace tu aplicación",
            "pills": ["Gratis", "MIT", ".NET 10"],
        },
        "01-gancho": {
            "cap": "LE PIDES ALGO A TU AGENTE",
            "prompt": "Añade una regla de validación a Invoice",
            "code": [
                '[RuleCriteria("La factura debe cuadrar",',
                "    DefaultContexts.Save,",
                '    "TotalAmount = Sum(Lines.Amount)")]',
            ],
            "flag": "tu clase tiene",
            "flag_code": "Total",
            "line1": "No está alucinando XAF.",
            "line2a": "Sabe XAF. Nunca ha visto ",
            "line2b": "el tuyo.",
        },
        "02-la-brecha": {
            "cap": "TRES COSAS QUE UN AGENTE NECESITA SABER",
            "rows": [
                ("Cómo funciona XAF", "agent-skills de DevExpress"),
                ("Qué dice la documentación", "MCP de documentación"),
            ],
            "row3": ("Qué hace TU aplicación", "XAF Logic Explainer"),
            "close": "Se complementan. Ninguna sustituye a las otras.",
        },
        "03-dos-minutos": {
            "cap": "DOS MINUTOS, DE PRINCIPIO A FIN",
            "cmds": [
                "dotnet tool install -g XafLogicExplainer.Cli",
                "xaflogic agents --project MiApp.Module",
            ],
            "files": ["AGENTS.md", "CLAUDE.md", ".github/copilot-instructions.md"],
            "close": "Sin cuenta. Sin API key. Sin subir nada.",
        },
        "04-oculto": {
            "cap": "LAS ENTIDADES SON LA PARTE FÁCIL",
            "lead": "Nada de esto está en la clase de negocio:",
            "chips": ["Model Editor (.xafml)", "Editores propios, y su JavaScript",
                      "Migraciones con versión", "Qué controladores carga una pantalla"],
        },
        "05-editores": {
            "cap": "UN STRING QUE SE DIBUJA COMO UN LECTOR",
            "note1": "Vive en el proyecto de plataforma, al lado del módulo. Quien lee los objetos de negocio no lo ve nunca.",
            "note2": "Comportamiento que no está ni en C# ni en XML — y tu agente no lee ninguno de los dos.",
        },
        "06-migraciones": {
            "cap": "COMO MUCHO UNA VEZ POR BASE DE DATOS",
            "stamp": "SE EJECUTÓ UNA VEZ",
            "note": "El código de hoy no puede recuperar lo que hizo. Pregunta por qué una columna vale eso y el agente se inventa una razón.",
        },
        "07-pantallas": {
            "chip": "Qué se ejecuta cuando abres una pantalla",
            "callout1": "Las pantallas las genera XAF. No están en ningún fichero.",
            "callout2": "Qué controladores se cargan son cuatro condiciones, en ejecución.",
            "foot": "Salida real — PharmacyDemo, el fixture sintético del repositorio",
        },
        "08-roslyn": {
            "cap": "SE LEE COMO SINTAXIS, NUNCA SE COMPILA",
            "facts": ["Funciona en una rama que no compila",
                      "Nunca referencia un ensamblado de DevExpress",
                      "{tests} pruebas corren gratis en un runner público"],
        },
        "10-mapa": {
            "chip": "La misma extracción, para una persona",
            "callout1": "Un mapa de tu modelo de dominio que casi ningún equipo ha visto.",
            "callout2": "Vive en la cabeza de una persona — que es lo que se va cuando ella se va.",
            "foot": "Salida real — PharmacyDemo, el fixture sintético del repositorio",
        },
        "09-niveles": {
            "cap": "DOS NIVELES, A PROPÓSITO",
            "bar1": ("AGENTS.md — se lee en cada petición", "11 KB", 16),
            "bar2": ("se abre solo cuando el agente lo necesita", "70 KB", 100),
            "close": "Si no está en la lista, no existe.",
        },
        "11-cierre": {
            "line1": "Tu agente ya sabe XAF.",
            "line2a": "Vamos a enseñarle ",
            "line2b": "tu aplicación.",
            "cmd": "dotnet tool install -g XafLogicExplainer.Cli",
            "repo": "github.com/peopleworks/XAFLogicExplainer",
            "pills": ["Gratis", "MIT", ".NET 10"],
        },
    },
}


FIXTURE_ROOT = ROOT / "tests" / "XafLogicExplainer.Tests" / "Fixtures" / "DemoSolution"


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
            # BarcodeScannerPropertyEditor and BlazorPropertyEditorBase, which reads as a
            # rendering fault rather than as emphasis.
            text = re.sub(rf"\b{re.escape(escape(mark))}\b",
                          f'<span class="hit">{escape(mark)}</span>', text)
        body.append(f'<div class="ln" {a("wipe", delay + 0.5 + i * step, 0.4)}>{text or "&nbsp;"}</div>')
    return (f'<div class="src">'
            f'<div class="src__path mono" {a("fade", delay, 0.5)}>{escape(rel)}</div>'
            f'<div class="src__code mono" style="font-size:{size}px">{"".join(body)}</div></div>')


CODE_CSS = """
/* min-width:0, or a grid column sizes itself to the longest unwrappable code line and pushes
   the whole scene off the right of the frame. */
.src { border:1px solid %(line)s; background:%(panel)s; border-radius:13px; overflow:hidden;
       min-width:0; }
.src__path {
  padding:11px 18px; font-size:14px; color:%(faint)s;
  border-bottom:1px solid %(line)s; background:%(bg)s;
}
.src__code { padding:18px 20px; line-height:1.66; color:%(graphite_ink)s; position:relative; }
.src__code .ln { white-space:pre; }
/* Real code has real long lines -- the RuleCriteria on Prescription is one of them. A hard cut
   mid-token reads as a broken render; a fade reads as "this line continues", which is true. */
.src__code::after {
  content:""; position:absolute; top:0; right:0; bottom:0; width:58px; pointer-events:none;
  background:linear-gradient(to right, rgba(16,21,29,0), %(panel)s 82%%);
}
.src__code .hit { color:%(yours)s; font-weight:600; }
""" % PALETTE


def a(name: str, delay: float, dur: float = 0.5) -> str:
    """The style attribute for one animation. Delays stagger; nothing is ever negative."""
    return f'style="animation:{dur}s cubic-bezier(.2,.7,.3,1) {delay}s both {name}"'


def a2(first: tuple[str, float, float], second: tuple[str, float, float]) -> str:
    """Two animations on one element -- how a line leaves so the next one can have the space."""
    parts = ", ".join(f"{dur}s cubic-bezier(.2,.7,.3,1) {delay}s both {name}"
                      for name, delay, dur in (first, second))
    return f'style="animation:{parts}"'


# ---------------------------------------------------------------------------------------------
# Argument scenes
# ---------------------------------------------------------------------------------------------

def scene_01(c: dict, _: dict) -> tuple[str, str]:
    css = """
.prompt { display:flex; align-items:center; gap:14px; margin-top:20px; }
.prompt .bubble {
  border:1px solid %(line)s; background:%(panel)s; border-radius:12px;
  padding:14px 20px; font-size:21px; color:%(ink)s;
}
.code { margin-top:34px; font-size:24px; line-height:1.62; color:%(graphite_ink)s; }
/* pre on the code lines only, not the block: the indentation is the only thing saying these
   three lines are one attribute, but a `pre` container also renders the newlines between the
   tags -- which doubled the leading and pushed the punchline off the bottom of the frame. */
.code .ln { white-space:pre; }
.code .hit { position:relative; color:%(ink)s; }
.code .hit::after {
  content:""; position:absolute; left:0; right:0; bottom:-6px; height:2px; background:%(yours)s;
}
.flagbox { margin-top:18px; margin-left:38px; display:flex; align-items:center; gap:12px; }
.flagbox .lab { font-size:16px; color:%(mute)s; }
.flagbox .val { font-size:24px; color:%(yours)s; font-weight:600; }
.punch { margin-top:44px; }
""" % PALETTE

    def code_line(line: str, i: int) -> str:
        marked = escape(line).replace("TotalAmount", '<span class="hit">TotalAmount</span>')
        return f'<div class="mono ln" {a("wipe", 1.6 + i * 0.45, 0.55)}>{marked}</div>'

    code = "".join(code_line(line, i) for i, line in enumerate(c["code"]))

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.15)}>{escape(c["cap"])}</div>
  <div class="main">
    <div>
      <div class="prompt" {a("rise", 0.4)}>
        <span class="cap" style="color:{PALETTE['yours']}">&gt;</span>
        <span class="bubble">{escape(c["prompt"])}</span>
      </div>

      <div class="code" {a("dim", 7.6, 0.7)}>{code}
        <div class="flagbox" {a("rise", 4.9)}>
          <span class="lab">{escape(c["flag"])}</span>
          <span class="mono val">{escape(c["flag_code"])}</span>
        </div>
      </div>

      <div class="punch">
        <div class="head grey" {a("rise", 8.0, 0.6)}>{escape(c["line1"])}</div>
        <div class="head" {a("rise", 9.6, 0.6)}>{escape(c["line2a"])}<span
          class="yours">{escape(c["line2b"])}</span></div>
      </div>
    </div>
  </div>
  <div {a("fade", 11.4, 0.6)}>
    <span class="pill pill--accent">MIT</span>
    <span class="pill">github.com/peopleworks/XAFLogicExplainer</span>
  </div>
</div>"""
    return css, body


def scene_02(c: dict, _: dict) -> tuple[str, str]:
    css = """
.rows { display:flex; flex-direction:column; gap:20px; }
.row {
  display:flex; align-items:center; gap:26px; padding:24px 28px;
  border:1px solid %(line)s; background:%(panel)s; border-radius:14px;
}
.row .what { flex:1; font-size:25px; color:%(graphite_ink)s; }
.row .tool { font-size:20px; color:%(mute)s; }
.row .tick { font-size:22px; color:%(graphite)s; }
.row--yours { border-color:%(yours)s; background:%(yours_soft)s; }
.row--yours .what { color:%(ink)s; font-weight:640; }
.row--yours .tool { color:%(yours)s; font-weight:640; }
.close { font-size:24px; color:%(mute)s; }
""" % PALETTE

    rows = "\n".join(
        f"""<div class="row" {a("rise", 0.7 + i * 1.1)}>
              <div class="what">{escape(what)}</div>
              <div class="tool">{escape(tool)}</div>
              <div class="tick">&#10003;</div>
            </div>"""
        for i, (what, tool) in enumerate(c["rows"])
    )
    what3, tool3 = c["row3"]

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.15)}>{escape(c["cap"])}</div>
  <div class="main">
    <div class="rows">
      {rows}
      <div class="row row--yours" {a("rise", 3.6, 0.6)}>
        <div class="what">{escape(what3)}</div>
        <div class="tool" {a("fade", 5.6, 0.6)}>{escape(tool3)}</div>
      </div>
    </div>
  </div>
  <div class="close" {a("rise", 8.6, 0.6)}>{escape(c["close"])}</div>
</div>"""
    return css, body


def scene_03(c: dict, _: dict) -> tuple[str, str]:
    css = """
.term {
  border:1px solid %(line)s; background:%(panel)s; border-radius:14px;
  padding:30px 32px; font-size:21px; line-height:2.05;
}
.term .p { color:%(yours)s; }
.term .c { color:%(ink)s; }
.files { margin-top:34px; display:flex; gap:16px; }
.file {
  border:1px solid %(yours)s; background:%(yours_soft)s; border-radius:10px;
  padding:15px 20px; font-size:19px; color:%(yours)s;
}
.close { font-size:25px; color:%(mute)s; }
""" % PALETTE

    cmds = "\n".join(
        f'<div {a("wipe", 0.5 + i * 2.4, 0.9)}><span class="p">$</span> '
        f'<span class="c">{escape(cmd)}</span></div>'
        for i, cmd in enumerate(c["cmds"])
    )
    files = "\n".join(
        f'<div class="file mono" {a("rise", 6.0 + i * 0.5)}>{escape(f)}</div>'
        for i, f in enumerate(c["files"])
    )

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.15)}>{escape(c["cap"])}</div>
  <div class="main">
    <div>
      <div class="term mono">{cmds}</div>
      <div class="files">{files}</div>
    </div>
  </div>
  <div class="close" {a("rise", 8.6, 0.6)}>{escape(c["close"])}</div>
</div>"""
    return css, body


def scene_09(c: dict, _: dict) -> tuple[str, str]:
    css = """
.bars { display:flex; flex-direction:column; gap:54px; }
.bar .lab { font-size:22px; color:%(mute)s; margin-bottom:16px; }
.bar .track { display:flex; align-items:center; gap:22px; }
.bar .fill { height:46px; border-radius:9px; transform-origin:left center; }
.bar .val { font-size:26px; font-weight:640; }
.bar--a .fill { background:%(yours)s; }
.bar--a .val  { color:%(yours)s; }
.bar--a .lab  { color:%(ink)s; }
.bar--b .fill { background:%(graphite)s; }
.bar--b .val  { color:%(graphite_ink)s; }
.close { font-size:30px; font-weight:640; }
""" % PALETTE

    def bar(kind: str, spec: tuple[str, str, int], delay: float) -> str:
        label, value, pct = spec
        return f"""
  <div class="bar bar--{kind}">
    <div class="lab" {a("fade", delay)}>{escape(label)}</div>
    <div class="track">
      <div class="fill" style="width:{pct * 9}px;
           animation:1.1s cubic-bezier(.2,.7,.3,1) {delay + 0.25}s both grow"></div>
      <div class="val mono" {a("fade", delay + 1.1)}>{escape(value)}</div>
    </div>
  </div>"""

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.15)}>{escape(c["cap"])}</div>
  <div class="main">
    <div class="bars">
      {bar("a", c["bar1"], 0.7)}
      {bar("b", c["bar2"], 3.4)}
    </div>
  </div>
  <div class="close" {a("rise", 9.2, 0.6)}>{escape(c["close"])}</div>
</div>"""
    return css, body


def scene_11(c: dict, _: dict) -> tuple[str, str]:
    css = """
.head { font-size:52px; }
.cmd {
  margin-top:52px; display:inline-block; border:1px solid %(line)s; background:%(panel)s;
  border-radius:12px; padding:17px 24px; font-size:21px; color:%(mute)s;
}
.repo { margin-top:26px; font-size:20px; color:%(yours)s; }
""" % PALETTE

    pills = "\n".join(
        f'<span class="pill" {a("fade", 9.6 + i * 0.3, 0.5)}>{escape(p)}</span>'
        for i, p in enumerate(c["pills"])
    )

    body = f"""
<div class="stage">
  <div class="main">
    <div>
      <div class="head grey" {a("rise", 0.4, 0.6)}>{escape(c["line1"])}</div>
      <div class="head" {a("rise", 1.9, 0.6)}>{escape(c["line2a"])}<span
        class="yours">{escape(c["line2b"])}</span></div>
      <div class="cmd mono" {a("rise", 4.6, 0.6)}>{escape(c["cmd"])}</div>
      <div class="repo mono" {a("fade", 6.6, 0.6)}>{escape(c["repo"])}</div>
    </div>
  </div>
  <div>{pills}</div>
</div>"""
    return css, body


def scene_intro(c: dict, _: dict) -> tuple[str, str]:
    """Three and a half seconds. Long enough to name the thing, too short for a second idea."""
    css = """
.stage { justify-content:center; align-items:center; text-align:center; }
.name {
  font-family: ui-monospace, "Cascadia Mono", Menlo, Consolas, monospace;
  font-size:46px; font-weight:640; letter-spacing:.10em; color:%(ink)s;
}
.rule { width:150px; height:2px; background:%(yours)s; margin:26px auto 24px; }
.line { font-size:22px; color:%(mute)s; }
/* Symmetric margins: the shared .pill has a right margin only, which leaves a centred group
   sitting half a gap to the left of centre. */
.pills { margin-top:34px; }
.pills .pill { margin:0 5px; }
""" % PALETTE

    pills = "".join(f'<span class="pill" {a("fade", 2.0 + i * 0.18, 0.5)}>{escape(p)}</span>'
                    for i, p in enumerate(c["pills"]))

    body = f"""
<div class="stage">
  <div>
    <div class="name" {a("rise", 0.15, 0.7)}>{escape(c["name"])}</div>
    <div class="rule" style="animation:.7s cubic-bezier(.2,.7,.3,1) .75s both grow;
         transform-origin:center"></div>
    <div class="line" {a("fade", 1.15, 0.6)}>{escape(c["line"])}</div>
    <div class="pills">{pills}</div>
  </div>
</div>"""
    return css, body


def scene_04(c: dict, _: dict) -> tuple[str, str]:
    """The business class, real, beside the four things that are not in it."""
    css = CODE_CSS + """
.split { display:grid; grid-template-columns: 1.15fr .85fr; gap:34px; align-items:start; }
.lead { font-size:20px; color:%(mute)s; margin-bottom:20px; }
.chip4 {
  display:flex; align-items:center; gap:13px; padding:14px 17px; margin-bottom:12px;
  border:1px solid %(yours)s; background:%(yours_soft)s; border-radius:10px;
  font-size:18px; color:%(ink)s;
}
.chip4 .x { color:%(yours)s; font-size:15px; }
""" % PALETTE

    code = snippet("PharmacyDemo.Module/BusinessObjects/Prescription.cs",
                   "public class Prescription", lines=11, before=5)
    chips = "".join(
        f'<div class="chip4" {a("rise", 3.4 + i * 0.85)}>'
        f'<span class="x mono">not here</span>{escape(chip)}</div>'
        for i, chip in enumerate(c["chips"])
    )

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.15)}>{escape(c["cap"])}</div>
  <div class="main">
    <div class="split">
      {code_block("PharmacyDemo.Module/BusinessObjects/Prescription.cs", code, 0.5, size=15)}
      <div>
        <div class="lead" {a("fade", 3.0, 0.6)}>{escape(c["lead"])}</div>
        {chips}
      </div>
    </div>
  </div>
</div>"""
    return css, body


def scene_05(c: dict, _: dict) -> tuple[str, str]:
    """The custom editor and the script it cannot work without, both real files."""
    css = CODE_CSS + """
/* Stacked, not side by side. The path is the argument here -- the editor lives in the platform
   project, not the module -- so it cannot be shortened to fit a half-width column. */
.note { font-size:18px; color:%(mute)s; margin-top:13px; line-height:1.45; }
.stack { display:flex; flex-direction:column; gap:26px; }
""" % PALETTE

    editor = snippet("PharmacyDemo.Blazor.Server/Editors/BarcodeScannerPropertyEditor.cs",
                     "[PropertyEditor(typeof(string)", lines=7)
    js = snippet("PharmacyDemo.Blazor.Server/wwwroot/js/barcode-scanner.js",
                 "export function start", lines=2, before=1)

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.15)}>{escape(c["cap"])}</div>
  <div class="main">
    <div class="stack">
      <div>
        {code_block("PharmacyDemo.Blazor.Server/Editors/BarcodeScannerPropertyEditor.cs",
                    editor, 0.4, marks=("PropertyEditor", "BlazorPropertyEditorBase"), size=15)}
        <div class="note" {a("rise", 4.6, 0.6)}>{escape(c["note1"])}</div>
      </div>
      <div>
        {code_block("PharmacyDemo.Blazor.Server/wwwroot/js/barcode-scanner.js", js, 6.2, size=15)}
        <div class="note" {a("rise", 8.4, 0.6)}>{escape(c["note2"])}</div>
      </div>
    </div>
  </div>
</div>"""
    return css, body


def scene_06(c: dict, _: dict) -> tuple[str, str]:
    """The guarded block, with the comment that is the only surviving record of why."""
    css = CODE_CSS + """
.wrap { position:relative; }
/* Bottom right, over the closing brace: anywhere higher and the stamp covers the second half
   of the condition, which is the line the scene exists to show.
   Its own keyframe, because `rise` ends at `transform: none` and flattened the tilt out. */
@keyframes stampdown {
  from { opacity:0; transform:rotate(-9deg) scale(1.55); }
  to   { opacity:1; transform:rotate(-9deg) scale(1); }
}
.stamp {
  position:absolute; right:38px; bottom:20px; transform:rotate(-9deg);
  border:3px solid %(yours)s; color:%(yours)s; border-radius:9px;
  padding:11px 22px; font-size:27px; font-weight:700; letter-spacing:.06em;
}
.note { margin-top:26px; font-size:21px; color:%(mute)s; line-height:1.45; }
""" % PALETTE

    code = snippet("PharmacyDemo.Module/DatabaseUpdate/PharmacyUpdater.cs",
                   "if (CurrentDBVersion < new Version(\"1.1.0.0\")", lines=6, before=2)

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.15)}>{escape(c["cap"])}</div>
  <div class="main">
    <div>
      <div class="wrap">
        {code_block("PharmacyDemo.Module/DatabaseUpdate/PharmacyUpdater.cs", code, 0.4,
                    marks=("CurrentDBVersion",), size=18)}
        <div class="stamp mono" {a("stampdown", 5.4, 0.45)}>{escape(c["stamp"])}</div>
      </div>
      <div class="note" {a("rise", 7.6, 0.6)}>{escape(c["note"])}</div>
    </div>
  </div>
</div>"""
    return css, body


def scene_08(c: dict, ctx: dict) -> tuple[str, str]:
    css = CODE_CSS + """
.split { display:grid; grid-template-columns: 1.05fr .95fr; gap:38px; align-items:center; }
.fact { display:flex; gap:15px; align-items:flex-start; margin-bottom:24px; font-size:21px; }
.fact .n { color:%(yours)s; font-size:16px; padding-top:4px; }
.arrow { text-align:center; font-size:15px; color:%(faint)s; margin:14px 0 4px; }
""" % PALETTE

    code = snippet("PharmacyDemo.Module/BusinessObjects/Prescription.cs",
                   "[RuleCriteria(\"Prescription_NotExpired\"", lines=9, before=4)

    facts = "".join(
        f'<div class="fact" {a("rise", 3.4 + i * 1.35)}><span class="n mono">&#9679;</span>'
        f'<span>{escape(fact.format(tests=ctx["tests"]))}</span></div>'
        for i, fact in enumerate(c["facts"])
    )

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.15)}>{escape(c["cap"])}</div>
  <div class="main">
    <div class="split">
      {code_block("PharmacyDemo.Module/BusinessObjects/Prescription.cs", code, 0.4,
                  marks=("RuleCriteria",), size=16)}
      <div>{facts}</div>
    </div>
  </div>
</div>"""
    return css, body


# ---------------------------------------------------------------------------------------------
# Evidence scenes: the tool's own report, panned over. Not a mock-up of it.
# ---------------------------------------------------------------------------------------------

def evidence(still_key: str, mode: str = "pan"):
    """An evidence scene reads one captured section of the real report. Same frame every time,
    different section -- so a new one is a capture and a caption, not another layout.

    `pan` scrolls a capture taller than the frame: right for a list nobody could fit on screen.
    `fit` shows the whole capture and drifts in: right for a diagram, where scrolling past half
    a graph defeats the reason for showing it.
    """
    def build(c: dict, ctx: dict) -> tuple[str, str]:
        return _evidence(c, ctx, ctx["stills"][still_key], mode)
    return build


def _evidence(c: dict, ctx: dict, still: dict, mode: str) -> tuple[str, str]:
    seconds = ctx["seconds"]

    # Pan the full height of the capture past the window, ending a beat before the scene does so
    # the last line can be read rather than glimpsed.
    shown_w = W - 144
    scale = shown_w / still["width"]
    scaled_h = still["height"] * scale
    travel = max(0, scaled_h - (H - 210))

    if mode == "fit":
        motion = """
.window { display:flex; align-items:center; justify-content:center; }
.window .roll { height:100%%; display:flex; align-items:center; }
.window .roll img { width:auto; height:100%%; }
@keyframes drift { from { transform:scale(1); } to { transform:scale(1.07); } }
.window .roll { animation: drift %(pan)ss linear 1.2s both; }
""" % {"pan": round(seconds - 2.0, 2)}
    else:
        motion = """
/* The capture is taller than the window, so the scene reads it rather than showing it. */
@keyframes pan { from { transform: translateY(0); } to { transform: translateY(-%(travel)spx); } }
.window .roll { animation: pan %(pan)ss linear 1.2s both; }
""" % {"travel": round(travel, 1), "pan": round(seconds - 2.6, 2)}

    css = """
.chip {
  position:absolute; left:72px; top:38px; z-index:3;
  border:1px solid %(yours)s; background:%(bg)s; border-radius:999px;
  padding:11px 20px; font-size:19px; color:%(yours)s;
}
/* Top right, out of the callouts' way: it is a provenance note, not a line anyone reads aloud. */
.foot { position:absolute; right:72px; top:48px; font-size:13px; color:%(faint)s; z-index:3; }
.window {
  position:absolute; left:72px; right:72px; top:98px; bottom:112px;
  border:1px solid %(line)s; border-radius:14px; overflow:hidden; background:%(panel)s;
}
.window img { display:block; width:100%%; }
__MOTION__
.window::after {
  content:""; position:absolute; inset:0; pointer-events:none;
  background: linear-gradient(%(bg)s 0%%, transparent 9%%, transparent 91%%, %(bg)s 100%%);
}
/* Both callouts occupy the same line. The first has to leave before the second arrives --
   stacking them was how the first render put two sentences on top of each other. */
.callout {
  position:absolute; left:72px; right:72px; bottom:52px; font-size:25px; color:%(ink)s;
}
""" % PALETTE
    css = css.replace("__MOTION__", motion)

    hand_over = seconds * 0.52

    body = f"""
<div class="stage" style="padding:0">
  <div class="chip" {a("rise", 0.2, 0.6)}>{escape(c["chip"])}</div>
  <div class="foot mono" {a("fade", 1.4, 0.8)}>{escape(c["foot"])}</div>
  <div class="window" {a("fade", 0.5, 0.7)}>
    <div class="roll"><img src="../../{still["rel"]}" alt=""></div>
  </div>
  <div class="callout" {a2(("rise", 2.2, 0.6), ("out", hand_over, 0.5))}>{escape(c["callout1"])}</div>
  <div class="callout" {a("rise", hand_over + 0.5, 0.6)}>{escape(c["callout2"])}</div>
</div>"""
    return css, body


BUILDERS = {
    "intro": scene_intro,
    "01-gancho": scene_01,
    "02-la-brecha": scene_02,
    "03-dos-minutos": scene_03,
    "04-oculto": scene_04,
    "05-editores": scene_05,
    "06-migraciones": scene_06,
    "07-pantallas": evidence("screens"),
    "08-roslyn": scene_08,
    "09-niveles": scene_09,
    "10-mapa": evidence("map", mode="fit"),
    "11-cierre": scene_11,
}

# When to grab the poster: the moment the scene has said its whole piece. A fraction of the
# scene rather than a second count, so retiming a scene does not leave its poster showing a
# half-drawn frame.
POSTER_AT = {
    "intro": .90, "01-gancho": .94, "02-la-brecha": .75, "03-dos-minutos": .80, "04-oculto": .78,
    "05-editores": .82, "06-migraciones": .80, "07-pantallas": .62, "08-roslyn": .84,
    "09-niveles": .74, "10-mapa": .62, "11-cierre": .82,
}

# Section of the real report each evidence scene reads, and whether to unfold its detail rows.
STILLS_WANTED = [
    ("screens", "#screens-Prescription", True),
    ("map", "#map", False),
]


# ---------------------------------------------------------------------------------------------

def page(css: str, body: str) -> str:
    return (f"<!doctype html>\n<html><head><meta charset=\"utf-8\">\n<style>\n{BASE_CSS}\n{css}\n"
            f"</style></head>\n<body>{body}\n</body></html>\n")


def recorded_seconds(lang: str, scene_id: str) -> float | None:
    """How long the take actually is, if one has been recorded."""
    for ext in AUDIO_EXTS:
        take = AUDIO / lang / f"{scene_id}{ext}"
        if take.exists():
            out = subprocess.run(
                ["ffprobe", "-v", "error", "-show_entries", "format=duration",
                 "-of", "csv=p=0", str(take)], capture_output=True, text=True)
            if out.returncode == 0 and out.stdout.strip():
                return float(out.stdout.strip())
    return None


def wanted_seconds(lang: str, scene: dict) -> tuple[float, str]:
    """What a scene should last, and where that number came from."""
    recorded = recorded_seconds(lang, scene["id"])
    if recorded is not None:
        return round(recorded + TAIL_SECONDS, 1), "recorded"
    words = len(scene["narration"].split())
    return round(words / WPM_NATURAL * 60 + TAIL_SECONDS, 1), "estimated"


def check_pacing(guion: dict, lang: str) -> list[str]:
    """Narration that cannot be spoken in the time the scene lasts."""
    problems = []
    for scene in guion["scenes"]:
        words = len(scene["narration"].split())
        wpm = words / scene["minSeconds"] * 60
        need, source = wanted_seconds(lang, scene)
        if wpm >= WPM_IMPOSSIBLE:
            problems.append(f"{lang}/{scene['id']}: {wpm:.0f} wpm over {scene['minSeconds']}s "
                            f"({words} words) — needs {need:.0f}s ({source}). "
                            f"Run --retime to apply.")
        elif wpm >= WPM_TIGHT:
            print(f"  ! {lang}/{scene['id']}: {wpm:.0f} wpm — tight, {need:.0f}s would be easier")
    return problems


def retime(guiones: dict) -> None:
    """Rewrite every scene's length from the recorded take, or from the words until one exists.

    The scene animation cannot be shorter than the voice over it, and the voice is whatever
    length it is -- so the script is the thing that has to move, not the delivery.
    """
    for lang, guion in guiones.items():
        total = guion["intro"]["seconds"]
        chapters = []
        for scene in guion["scenes"]:
            was = scene["minSeconds"]
            scene["minSeconds"], source = wanted_seconds(lang, scene)
            # YouTube ignores a chapter list whose first entry is not 00:00, so the opening
            # chapter swallows the intro rather than starting after it.
            chapters.append((0 if not chapters else total, scene["id"]))
            total += scene["minSeconds"]
            flag = "" if was == scene["minSeconds"] else f"   was {was}s"
            print(f"  {lang}/{scene['id']:<16} {scene['minSeconds']:>5}s  {source}{flag}")

        path = VIDEO / f"guion.video.{lang}.json"
        path.write_text(json.dumps(guion, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"  -> {path.name}   total {int(total // 60)}:{int(total % 60):02d}\n")
        print(f"  chapters for the {lang} description")
        for at, scene_id in chapters:
            print(f"  {int(at // 60):02d}:{int(at % 60):02d}  {scene_id}")
        print()


def test_count() -> str:
    """Read the suite size off the README, which has a test of its own keeping it honest.

    Typing it here would put a number on screen that nothing forces to stay true -- the exact
    failure the video is about.
    """
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    hits = set(re.findall(r"\*\*(\d[\d,]*) tests\*\*", readme))
    if len(hits) != 1:
        sys.exit(f"README.md: expected one '**N tests**' claim, found {sorted(hits) or 'none'}")
    return hits.pop()


def ensure_stills(page_obj) -> dict:
    """Capture the real report sections the evidence scenes pan over."""
    STILLS.mkdir(parents=True, exist_ok=True)
    wanted = {key: STILLS / f"{key}.png" for key, _, _ in STILLS_WANTED}

    if any(not path.exists() for path in wanted.values()):
        if not REPORT.exists():
            print(f"  generating {REPORT.name} from {FIXTURE.name}")
            result = subprocess.run(
                ["dotnet", "run", "--project", str(ROOT / "src" / "XafLogicExplainer.Cli"),
                 "-c", "Release", "--no-launch-profile", "--",
                 "explain", "--project", str(FIXTURE), "--output", str(REPORT)],
                capture_output=True, text=True, cwd=ROOT)
            if result.returncode != 0 or not REPORT.exists():
                sys.exit(f"could not generate the report:\n{result.stdout}\n{result.stderr}")

        page_obj.set_viewport_size({"width": 1180, "height": 1400})
        page_obj.goto(REPORT.as_uri())
        page_obj.evaluate("document.documentElement.dataset.theme = 'dark'")

        for key, selector, expand in STILLS_WANTED:
            # Expanded, unlike the README figure: a long list of inherited framework controllers
            # is a bad still and a good pan.
            page_obj.evaluate(
                "([sel, open]) => document.querySelectorAll('details')"
                ".forEach(d => d.open = open && d.closest(sel) !== null)", [selector, expand])
            page_obj.wait_for_timeout(300)

            element = page_obj.query_selector(selector)
            if element is None:
                sys.exit(f"the report has no {selector} — the evidence scene would show nothing")
            box = element.bounding_box()
            page_obj.screenshot(path=str(wanted[key]), full_page=True, clip={
                "x": box["x"] - 13, "y": box["y"] - 13,
                "width": box["width"] + 26, "height": box["height"] + 26})

        REPORT.unlink(missing_ok=True)

    stills = {}
    for key, path in wanted.items():
        with Image.open(path) as image:
            stills[key] = {"width": image.width, "height": image.height,
                           "rel": path.relative_to(VIDEO).as_posix()}
    return stills


def overflowing(page_obj) -> str:
    """How far the scene's content runs past 1280x720, if it does.

    A frame silently crops whatever does not fit, so a line of copy one word longer just goes
    missing rather than looking wrong -- and it goes missing in a video, where nobody scrolls
    back. The stage is a flex column, so the overflow lands on the stage, not on the body.
    """
    over = page_obj.evaluate(
        """([w, h]) => {
             const stage = document.querySelector('.stage');
             // The evidence scene pans a capture that is deliberately taller than its window.
             // Anything inside a clipping box is the design, not a spill, so skip those
             // subtrees -- stopping the walk at the stage, since body itself hides overflow.
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
           }""", [W, H])
    dx, dy = over
    bits = [f"{dx}px right"] if dx > 1 else []
    bits += [f"{dy}px below"] if dy > 1 else []
    return " and ".join(bits)


def freeze(page_obj, seconds: float) -> None:
    """Put every animation at exactly `seconds`, so a frame never depends on wall-clock time."""
    page_obj.evaluate(
        "t => document.getAnimations().forEach(a => { a.pause(); a.currentTime = t * 1000; })",
        seconds)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--video", nargs="*", default=None, metavar="SCENE",
                        help="render mp4s; with no ids, every scene")
    parser.add_argument("--assemble", action="store_true",
                        help="join the rendered scenes into one film per language, carrying "
                             "each scene's narration if audio/<lang>/<scene>.mp3 exists")
    parser.add_argument("--retime", action="store_true",
                        help="rewrite each scene's length from its recorded take, or from its "
                             "word count until one exists, and print the chapter list")
    args = parser.parse_args()

    guiones = {lang: json.loads((VIDEO / f"guion.video.{lang}.json").read_text(encoding="utf-8"))
               for lang in COPY}

    if args.retime:
        print("retiming")
        retime(guiones)

    print("pacing")
    pacing = [p for lang, g in guiones.items() for p in check_pacing(g, lang)]

    tests = test_count()
    for d in (SCENES, POSTERS, RENDER):
        d.mkdir(parents=True, exist_ok=True)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch()
        helper = browser.new_page()
        stills = ensure_stills(helper)
        helper.close()
        print()
        for still in stills.values():
            print(f"still  {still['rel']}  {still['width']}x{still['height']}")

        page_obj = browser.new_page(viewport={"width": W, "height": H},
                                    device_scale_factor=1, color_scheme="dark")

        for lang, g in guiones.items():
            (SCENES / lang).mkdir(parents=True, exist_ok=True)
            (POSTERS / lang).mkdir(parents=True, exist_ok=True)
            seconds = {"intro": g["intro"]["seconds"]}
            seconds.update({s["id"]: s["minSeconds"] for s in g["scenes"]})

            print(f"\n{lang}")
            for scene_id, build in BUILDERS.items():
                ctx = {"seconds": seconds[scene_id], "stills": stills, "tests": tests}
                css, body = build(COPY[lang][scene_id], ctx)
                html = SCENES / lang / f"{scene_id}.html"
                html.write_text(page(css, body), encoding="utf-8")

                page_obj.goto(html.as_uri())
                page_obj.wait_for_timeout(120)
                freeze(page_obj, POSTER_AT[scene_id] * seconds[scene_id])
                poster = POSTERS / lang / f"{scene_id}.png"
                page_obj.screenshot(path=str(poster))

                spill = overflowing(page_obj)
                mark = f"  SPILLS {spill}" if spill else ""
                print(f"  {scene_id:<16} {seconds[scene_id]:>3}s  "
                      f"{poster.relative_to(VIDEO)}{mark}")
                if spill:
                    pacing.append(f"{lang}/{scene_id}: content runs {spill} past the frame")

                wanted = args.video is not None and (not args.video or scene_id in args.video)
                if wanted:
                    render(page_obj, html, RENDER / lang / f"{scene_id}.mp4", seconds[scene_id])

        browser.close()

    if args.assemble:
        print("\nassembling")
        for lang, g in guiones.items():
            assemble(lang, ["intro"] + [s["id"] for s in g["scenes"]])

    if pacing:
        sys.exit("\nnarration that does not fit its scene:\n  " + "\n  ".join(pacing))


def audio_take(lang: str, scene_id: str) -> pathlib.Path | None:
    for ext in AUDIO_EXTS:
        take = AUDIO / lang / f"{scene_id}{ext}"
        if take.exists():
            return take
    return None


def assemble(lang: str, order: list[str]) -> None:
    """Join the scene mp4s into one film, carrying each scene's narration if it is recorded.

    Every segment gets an audio stream -- the real take, or silence -- because concat with
    `-c copy` needs the streams to match, and a video that is silent until scene four would
    otherwise concat into a file whose audio starts halfway through.
    """
    parts = RENDER / lang
    missing = [s for s in order if not (parts / f"{s}.mp4").exists()]
    if missing:
        print(f"  {lang}: cannot assemble, not rendered yet: {', '.join(missing)}")
        return

    work = parts / ".segments"
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True)

    voiced = 0
    for scene_id in order:
        clip, segment = parts / f"{scene_id}.mp4", work / f"{scene_id}.mp4"
        take = audio_take(lang, scene_id)
        if take is None:
            command = ["ffmpeg", "-y", "-loglevel", "error", "-i", str(clip),
                       "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
                       "-map", "0:v", "-map", "1:a", "-c:v", "copy", "-c:a", "aac",
                       "-b:a", "160k", "-shortest", str(segment)]
        else:
            voiced += 1
            # apad with -shortest, so the take ends exactly where the picture does instead of
            # leaving a stream shorter than its scene for concat to trip over.
            command = ["ffmpeg", "-y", "-loglevel", "error", "-i", str(clip), "-i", str(take),
                       "-map", "0:v", "-map", "1:a", "-c:v", "copy", "-c:a", "aac",
                       "-b:a", "160k", "-af", "apad", "-shortest", str(segment)]
        subprocess.run(command, check=True)

    listing = work / "concat.txt"
    listing.write_text("".join(f"file '{work / f'{s}.mp4'}'\n".replace("\\", "/")
                               for s in order), encoding="utf-8")

    out = RENDER / f"xaflogic-explainer-{lang}.mp4"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0",
                    "-i", str(listing), "-c", "copy", str(out)], check=True)
    shutil.rmtree(work)

    length = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration",
                             "-of", "csv=p=0", str(out)], capture_output=True, text=True)
    total = float(length.stdout.strip() or 0)
    narration = f"{voiced}/{len(order)} scenes voiced" if voiced else "silent — no takes yet"
    print(f"  {out.relative_to(VIDEO)}  {int(total // 60)}:{total % 60:04.1f}  "
          f"{out.stat().st_size:,} bytes  ({narration})")


def render(page_obj, html: pathlib.Path, out: pathlib.Path, seconds: float) -> None:
    """Frame by frame, then ffmpeg. Slow on purpose: deterministic beats fast for an artefact
    that gets re-rendered every time the copy changes."""
    if shutil.which("ffmpeg") is None:
        sys.exit("--video needs ffmpeg on PATH")

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


if __name__ == "__main__":
    main()
