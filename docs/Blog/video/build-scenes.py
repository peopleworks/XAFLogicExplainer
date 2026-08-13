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
import os
import pathlib
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
from xml.sax.saxutils import escape

from PIL import Image
from playwright.sync_api import sync_playwright

from scene_kit import (
    FIXTURE, FPS, PALETTE, ROOT, VIDEO, a, a2, base_css, build_report, code_block, CODE_CSS,
    freeze, overflowing, page, render, snippet, test_count,
)

SCENES = VIDEO / "scenes"
STILLS = VIDEO / "stills"        # real output, captured once and panned over
POSTERS = VIDEO / "posters"      # one frame per scene, to judge a design without rendering
RENDER = VIDEO / "render"
REPORT = VIDEO / ".report.html"

W, H = 1280, 720

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

BASE_CSS = base_css(W, H) + """
/* Column rather than absolute placement: the content block centres itself in whatever room is
   left between the eyebrow and the closing line, so a scene with two rows and one with three
   do not both leave a hole in the middle of the frame. */
.stage {
  position: relative; width: 100%%; height: 100%%; padding: 52px 72px 62px;
  display: flex; flex-direction: column;
}
.cap {
  font-family: ui-monospace, "Cascadia Mono", Menlo, Consolas, monospace;
  font-size: 13px; letter-spacing: .14em; text-transform: uppercase; color: %(faint)s;
}
.head { font-size: 44px; font-weight: 640; letter-spacing: -.015em; line-height: 1.18; }
.pill {
  display: inline-block; padding: 7px 15px; border-radius: 999px;
  border: 1px solid %(line)s; color: %(mute)s; font-size: 15px; margin-right: 10px;
}
.pill--accent { border-color: %(yours)s; color: %(yours)s; }
""" % PALETTE


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


def eleven_key() -> str:
    """The key, from the environment or a gitignored file beside this script.

    Same convention as the other video tools in this workshop, so nobody has to re-export a
    variable every shell session -- and so the key never has a reason to be in a tracked file.
    """
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if key:
        return key
    local = VIDEO / ".elevenlabs.key"
    if local.exists():
        for line in local.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line and not line.startswith("#"):
                return line
    sys.exit("no ElevenLabs key: set ELEVENLABS_API_KEY, or put it in "
             f"{local.relative_to(ROOT)} (gitignored)")


# Premade voices still synthesise by id but no longer come back from /v1/voices against an
# account, so a name lookup alone cannot find Rachel -- who is the English voice this workshop
# has used from the start. Pinned rather than dropped, so the guion can keep naming her.
PREMADE_VOICE_IDS = {"rachel": "21m00Tcm4TlvDq8ikWAM"}


def resolve_voice(name: str, key: str) -> str:
    """Look the voice up by name rather than pinning an id, so the guion stays readable."""
    want = name.strip().lower()
    request = urllib.request.Request("https://api.elevenlabs.io/v1/voices?show_legacy=true",
                                     headers={"xi-api-key": key})
    with urllib.request.urlopen(request, timeout=30) as response:
        voices = json.load(response).get("voices", [])

    hit = (next((v for v in voices if v.get("name", "").lower() == want), None)
           or next((v for v in voices if want in v.get("name", "").lower()), None))
    if hit is not None:
        return hit["voice_id"]
    if want in PREMADE_VOICE_IDS:
        return PREMADE_VOICE_IDS[want]

    sys.exit(f"no voice called {name!r}. Available: "
             f"{', '.join(sorted(v.get('name', '?') for v in voices))}")


def narrate(guiones: dict) -> None:
    """Record every scene that has no take yet.

    Skips what already exists: the call is billed per character, and a re-run to fix one line
    should not pay for the other twenty-one.
    """
    key = eleven_key()
    for lang, guion in guiones.items():
        voice_name = guion.get("voice") or "Rachel"
        voice_id = resolve_voice(voice_name, key)
        print(f"  {lang}: voice {voice_name}")
        (AUDIO / lang).mkdir(parents=True, exist_ok=True)

        for scene in guion["scenes"]:
            out = AUDIO / lang / f"{scene['id']}.mp3"
            if audio_take(lang, scene["id"]) is not None:
                print(f"    {scene['id']:<16} already recorded — skipped")
                continue

            body = json.dumps({
                "text": scene["narration"],
                "model_id": "eleven_multilingual_v2",
                # Between the two settings already in use here: steadier than the twenty-second
                # ads (style .25 / stability .42), livelier than the how-to series (.12 / .55).
                # This is a technical explainer that still has to hold a cold viewer.
                "voice_settings": {"stability": 0.50, "similarity_boost": 0.80,
                                   "style": 0.18, "use_speaker_boost": True},
            }).encode("utf-8")
            request = urllib.request.Request(
                f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}", data=body,
                headers={"xi-api-key": key, "Content-Type": "application/json",
                         "Accept": "audio/mpeg"})
            try:
                with urllib.request.urlopen(request, timeout=120) as response:
                    out.write_bytes(response.read())
            except urllib.error.HTTPError as error:
                sys.exit(f"ElevenLabs {error.code} on {lang}/{scene['id']}: "
                         f"{error.read()[:200].decode('utf-8', 'replace')}")

            seconds = recorded_seconds(lang, scene["id"]) or 0
            print(f"    {scene['id']:<16} {seconds:>5.1f}s  {out.stat().st_size:,} bytes")
    print()


def write_scripts(guiones: dict) -> None:
    """One sheet per language: what to read, and the exact name to save each take under.

    The pipeline finds a take by filename and nothing else, so a session that gets the names
    wrong silently produces a film that is still silent. Generated from the guion, so the sheet
    cannot drift from the narration the scenes were timed against.
    """
    for lang, guion in guiones.items():
        voice = guion.get("voice", "?")
        lines = [
            f"# Recording sheet — {lang}   (voice: {voice})",
            "",
            f"Save each take as `audio/{lang}/<id>.mp3` — the id is the heading, exactly as",
            "written. `.wav`, `.m4a` and `.ogg` work too; the extension is not the part that",
            "matters, the stem is.",
            "",
            "The scene lengths in the guion are an estimate from the word count until a take",
            "exists. Once these files are here:",
            "",
            "```",
            "python docs/Blog/video/build-scenes.py --retime    # lengths from the real audio",
            "python docs/Blog/video/build-scenes.py --video     # re-render at the new lengths",
            "python docs/Blog/video/build-scenes.py --assemble  # one film, with the voice on it",
            "```",
            "",
            "`--retime` prints the chapter list to paste into PUBLICACION.md. Nothing here is",
            "read aloud except the quoted line: the heading is a filename, not a cue.",
            "",
            "---",
            "",
            "## intro",
            "",
            f"*No narration — {guion['intro']['seconds']}s of title card. Skip it.*",
            "",
        ]
        for scene in guion["scenes"]:
            words = len(scene["narration"].split())
            lines += [
                f"## {scene['id']}",
                "",
                f"*{words} words · about {words / WPM_NATURAL * 60:.0f}s at a normal pace*",
                "",
                scene["narration"],
                "",
            ]

        out = AUDIO / lang / "SCRIPT.md"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text("\n".join(lines), encoding="utf-8")
        print(f"  {out.relative_to(VIDEO)}   {len(guion['scenes'])} takes")
    print()


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


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--video", nargs="*", default=None, metavar="SCENE",
                        help="render mp4s; with no ids, every scene")
    parser.add_argument("--narrate", action="store_true",
                        help="record any scene with no take yet, with the voice the guion "
                             "names, into audio/<lang>/. Billed per character; skips what "
                             "already exists")
    parser.add_argument("--script", action="store_true",
                        help="write the recording sheets: every line to read, under the exact "
                             "filename --assemble will look for")
    parser.add_argument("--assemble", action="store_true",
                        help="join the rendered scenes into one film per language, carrying "
                             "each scene's narration if audio/<lang>/<scene>.mp3 exists")
    parser.add_argument("--retime", action="store_true",
                        help="rewrite each scene's length from its recorded take, or from its "
                             "word count until one exists, and print the chapter list")
    args = parser.parse_args()

    guiones = {lang: json.loads((VIDEO / f"guion.video.{lang}.json").read_text(encoding="utf-8"))
               for lang in COPY}

    if args.narrate:
        print("narrating")
        narrate(guiones)

    if args.script:
        print("recording sheets")
        write_scripts(guiones)

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
                html.write_text(page(BASE_CSS, css, body), encoding="utf-8")

                page_obj.goto(html.as_uri())
                page_obj.wait_for_timeout(120)
                freeze(page_obj, POSTER_AT[scene_id] * seconds[scene_id])
                poster = POSTERS / lang / f"{scene_id}.png"
                page_obj.screenshot(path=str(poster))

                spill = overflowing(page_obj, W, H)
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
            lengths = {"intro": g["intro"]["seconds"]}
            lengths.update({s["id"]: s["minSeconds"] for s in g["scenes"]})
            assemble(lang, ["intro"] + [s["id"] for s in g["scenes"]], lengths)

    if pacing:
        sys.exit("\nnarration that does not fit its scene:\n  " + "\n  ".join(pacing))


def audio_take(lang: str, scene_id: str) -> pathlib.Path | None:
    for ext in AUDIO_EXTS:
        take = AUDIO / lang / f"{scene_id}{ext}"
        if take.exists():
            return take
    return None


def concat_list(work: pathlib.Path, name: str, files: list[pathlib.Path]) -> pathlib.Path:
    listing = work / name
    listing.write_text("".join(f"file '{f.as_posix()}'\n" for f in files), encoding="utf-8")
    return listing


def assemble(lang: str, order: list[str], seconds: dict) -> None:
    """Join the scene mp4s into one film, carrying each scene's narration if it is recorded.

    Picture and sound are built separately and married at the end, which is not the obvious
    way round. The obvious way -- mux each scene with its own take, then concat the results --
    loses about a second and a half per scene: `apad` fills the gap between a take and its
    scene with silence, and concatenating the AAC tracks with `-c copy` trims exactly that
    trailing padding back off. Eleven scenes in, the voice finished seventeen seconds before
    the picture did, and every individual segment measured correct.

    So the sound is assembled once, as PCM, where there is no encoder priming or edit list for
    a concat to trim. One encode at the end, against a video track that was only ever copied.
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
    tracks = []
    for scene_id in order:
        track = work / f"{scene_id}.wav"
        take = audio_take(lang, scene_id)
        length = str(seconds[scene_id])
        if take is None:
            command = ["ffmpeg", "-y", "-loglevel", "error",
                       "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo", "-t", length]
        else:
            voiced += 1
            # apad then -t: the take is shorter than its scene by design, and the difference
            # has to be real silence in the track, not padding a later step can reclaim.
            command = ["ffmpeg", "-y", "-loglevel", "error", "-i", str(take),
                       "-af", "apad", "-t", length]
        subprocess.run(command + ["-ac", "2", "-ar", "48000", "-c:a", "pcm_s16le",
                                  str(track)], check=True)
        tracks.append(track)

    sound = work / "sound.wav"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0",
                    "-i", str(concat_list(work, "sound.txt", tracks)), "-c", "copy",
                    str(sound)], check=True)

    picture = work / "picture.mp4"
    clips = [parts / f"{s}.mp4" for s in order]
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0",
                    "-i", str(concat_list(work, "picture.txt", clips)), "-c", "copy",
                    str(picture)], check=True)

    out = RENDER / f"xaflogic-explainer-{lang}.mp4"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(picture), "-i", str(sound),
                    "-map", "0:v", "-map", "1:a", "-c:v", "copy", "-c:a", "aac", "-b:a", "160k",
                    str(out)], check=True)
    shutil.rmtree(work)

    # The failure this replaced was invisible in every per-segment check, so the assembled
    # file is measured against itself: sound has to reach the end of the picture.
    streams = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "stream=codec_type,duration",
         "-of", "csv=p=0", str(out)], capture_output=True, text=True).stdout
    lengths = {kind: float(value) for kind, value in
               (line.split(",") for line in streams.strip().splitlines())}
    drift = abs(lengths.get("video", 0) - lengths.get("audio", 0))
    if drift > 0.5:
        sys.exit(f"{out.name}: sound runs {drift:.1f}s short of the picture")

    length = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration",
                             "-of", "csv=p=0", str(out)], capture_output=True, text=True)
    total = float(length.stdout.strip() or 0)
    narration = f"{voiced}/{len(order)} scenes voiced" if voiced else "silent — no takes yet"
    print(f"  {out.relative_to(VIDEO)}  {int(total // 60)}:{total % 60:04.1f}  "
          f"{out.stat().st_size:,} bytes  ({narration})")


if __name__ == "__main__":
    main()
