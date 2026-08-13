#!/usr/bin/env python3
"""Build the eight vertical shorts scripted in PUBLICACION.md.

    python docs/Blog/video/build-shorts.py             # HTML + one poster PNG each
    python docs/Blog/video/build-shorts.py --video     # also render every short to mp4
    python docs/Blog/video/build-shorts.py --video s1 s5

1080x1920, and **no narration**: every one has to land with the sound off, which is how these
are actually watched. So the durations are fixed by the script rather than by a voice, and the
copy carries the whole idea -- one idea per short, nothing that needs a second viewing.

Palette, animation machinery and the code blocks come from scene_kit, shared with the long
video. Two generators each holding their own #ff8a3d is two oranges waiting to drift, and a
short that does not match the film it promotes looks like somebody else made it.

The safe band matters here in a way it does not in landscape: TikTok, Reels and Shorts all put
their own interface over the top and bottom of the frame, so nothing that has to be read goes
outside the middle. The overflow check measures against that band, not against the frame.

Needs `pip install playwright pillow`, `python -m playwright install chromium`, and ffmpeg on
PATH for --video. Run build-scenes.py first: the two shorts that show real output pan over the
same captures it makes.
"""

from __future__ import annotations

import argparse
import re
import sys

from playwright.sync_api import sync_playwright

from scene_kit import (
    CODE_CSS, PALETTE, VIDEO, a, base_css, build_report, code_block, freeze, overflowing,
    page, render, snippet,
)

W, H = 1080, 1920
SHORTS = VIDEO / "shorts"
POSTERS = VIDEO / "shorts-posters"
RENDER = VIDEO / "render" / "shorts"
STILLS = VIDEO / "stills"
REPORT = VIDEO / ".report.html"

# What the platforms cover with their own interface. Everything readable lives between these.
SAFE_TOP, SAFE_BOTTOM = 300, 400

FRAME_CSS = base_css(W, H) + """
.stage {
  position:relative; width:100%%; height:100%%; padding:%(top)dpx 60px %(bottom)dpx;
  display:flex; flex-direction:column; justify-content:center;
}
.cap {
  font-family: ui-monospace, "Cascadia Mono", Menlo, Consolas, monospace;
  font-size:22px; letter-spacing:.14em; text-transform:uppercase; color:%(faint)s;
  margin-bottom:38px;
}
.big { font-size:62px; font-weight:640; letter-spacing:-.02em; line-height:1.14; }
.mid { font-size:40px; font-weight:600; line-height:1.28; }
.body { font-size:33px; color:%(mute)s; line-height:1.4; }
.pill {
  display:inline-block; padding:13px 24px; border-radius:999px;
  border:1px solid %(line)s; color:%(mute)s; font-size:27px; margin:0 8px 12px 0;
}
.pill--accent { border-color:%(yours)s; color:%(yours)s; }
.gap { height:44px; }
""" % {**PALETTE, "top": SAFE_TOP, "bottom": SAFE_BOTTOM}


COPY = {
    "en": {
        "s1": {
            "cap": "YOU ASK YOUR AGENT",
            "prompt": "Add a validation rule to Invoice",
            "code": ['[RuleCriteria("Invoice must balance",',
                     "    DefaultContexts.Save,",
                     '    "TotalAmount = Sum(Lines.Amount)")]'],
            "src_label": "Invoice.cs — what the agent wrote",
            "flag": "your class has",
            "flag_code": "Total",
            "line1": "It isn't hallucinating XAF.",
            "line2a": "It knows XAF. It has never seen ",
            "line2b": "YOURS.",
            "pills": ["Free", "MIT", "No DevExpress licence"],
        },
        "s2": {
            "cap": "THREE THINGS AN AGENT NEEDS",
            "rows": [("How XAF works", "DevExpress agent-skills"),
                     ("What the docs say", "DevExpress Docs MCP")],
            "row3": ("What YOUR application does", "XAF Logic Explainer"),
            "close": "They compose. None of them replaces the others.",
        },
        "s3": {
            "cap": "THE GHOST MIGRATION",
            "stamp": "RAN ONCE",
            "when": "On somebody's production database. Three years ago.",
            "question": "why does this column hold that value?",
            "answer": "the agent invents a reason",
            "close": "The comment is usually the only surviving record of why.",
        },
        "s4": {
            "cap": "NOT WHERE YOU ARE LOOKING",
            "here": "you are reading here",
            "there": "the behaviour is here",
            "note1": "A string property that renders as a scanner.",
            "note2": "And the JavaScript it cannot work without.",
            "close": "Not in C#. Not in XML.",
        },
        "s5": {
            "line1": "Most teams have never seen their own.",
            "line2": "It lives in one person's head.",
            "line3": "Exactly the knowledge that leaves when they do.",
            "foot": "Real output — PharmacyDemo, the repository's synthetic fixture",
        },
        "s6": {
            "cap": "NO COMPILE, NO LICENCE",
            "broken": ["$ git checkout feature/half-finished",
                       "$ dotnet build",
                       "error CS0246: type or namespace not found",
                       "Build FAILED."],
            "works": ["$ xaflogic agents --project PharmacyDemo.Module",
                      "AGENTS.md · CLAUDE.md · copilot-instructions.md"],
            "note1": "It never compiles your project.",
            "note2": "Roslyn reads the code as syntax.",
        },
        "s7": {
            "cap": "THE AGENTS.MD TAX",
            "bad_label": "everything, dumped in",
            "bad_note": "Read on EVERY request. Forever.",
            "good_label": "an index — 11 KB",
            "good_note": "the other 70 KB open only when the agent needs them",
            "close": "The smallest part matters most: if it isn't listed, it does not exist.",
        },
        "s8": {
            "cap": "IN NO FILE AT ALL",
            "grep": '$ grep -r "Patient_Prescriptions_ListView" .',
            "nothing": "(no results)",
            "line": "And your users open it every day.",
            "counter": "{entities} classes  →  {screens} screens",
            "note": "none of them written down anywhere",
            "close": "Now you know which controllers load onto each one.",
        },
    },
    "es": {
        "s1": {
            "cap": "LE PIDES A TU AGENTE",
            "prompt": "Añade una regla de validación a Invoice",
            "code": ['[RuleCriteria("La factura debe cuadrar",',
                     "    DefaultContexts.Save,",
                     '    "TotalAmount = Sum(Lines.Amount)")]'],
            "src_label": "Invoice.cs — lo que escribió el agente",
            "flag": "tu clase tiene",
            "flag_code": "Total",
            "line1": "No está alucinando XAF.",
            "line2a": "Sabe XAF. No ha visto ",
            "line2b": "EL TUYO.",
            "pills": ["Gratis", "MIT", "Sin licencia DevExpress"],
        },
        "s2": {
            "cap": "TRES COSAS QUE UN AGENTE NECESITA",
            "rows": [("Cómo funciona XAF", "agent-skills de DevExpress"),
                     ("Qué dice la documentación", "MCP de documentación")],
            "row3": ("Qué hace TU aplicación", "XAF Logic Explainer"),
            "close": "Se complementan. Ninguna sustituye a las otras.",
        },
        "s3": {
            "cap": "LA MIGRACIÓN FANTASMA",
            "stamp": "SE EJECUTÓ UNA VEZ",
            "when": "En la base de datos de producción de alguien. Hace tres años.",
            "question": "¿por qué esta columna tiene ese valor?",
            "answer": "el agente se lo inventa",
            "close": "El comentario suele ser el único registro del porqué.",
        },
        "s4": {
            "cap": "NO ESTÁ DONDE MIRAS",
            "here": "estás leyendo aquí",
            "there": "el comportamiento está aquí",
            "note1": "Una propiedad string que se pinta como un escáner.",
            "note2": "Y el JavaScript sin el que no funciona.",
            "close": "Ni en C#. Ni en XML.",
        },
        "s5": {
            "line1": "La mayoría de los equipos nunca ha visto el suyo.",
            "line2": "Vive en la cabeza de una persona.",
            "line3": "Justo el conocimiento que se va cuando esa persona se va.",
            "foot": "Salida real — PharmacyDemo, el fixture sintético del repositorio",
        },
        "s6": {
            "cap": "SIN COMPILAR, SIN LICENCIA",
            "broken": ["$ git checkout feature/a-medias",
                       "$ dotnet build",
                       "error CS0246: no se encuentra el tipo o el espacio de nombres",
                       "Compilación FALLIDA."],
            "works": ["$ xaflogic agents --project PharmacyDemo.Module",
                      "AGENTS.md · CLAUDE.md · copilot-instructions.md"],
            "note1": "Nunca compila tu proyecto.",
            "note2": "Roslyn lee el código como sintaxis.",
        },
        "s7": {
            "cap": "EL IMPUESTO DEL AGENTS.MD",
            "bad_label": "todo, volcado dentro",
            "bad_note": "Se lee en CADA petición. Para siempre.",
            "good_label": "un índice — 11 KB",
            "good_note": "los otros 70 KB se abren solo si hacen falta",
            "close": "Lo más pequeño es lo que más pesa: si no está en la lista, no existe.",
        },
        "s8": {
            "cap": "EN NINGÚN ARCHIVO",
            "grep": '$ grep -r "Patient_Prescriptions_ListView" .',
            "nothing": "(sin resultados)",
            "line": "Y tus usuarios la abren todos los días.",
            "counter": "{entities} clases  →  {screens} pantallas",
            "note": "ninguna escrita en ningún archivo",
            "close": "Ahora sabes qué controladores carga cada una.",
        },
    },
}

SECONDS = {"s1": 12, "s2": 12, "s3": 14, "s4": 12, "s5": 13, "s6": 11, "s7": 12, "s8": 15}
POSTER_AT = {"s1": .93, "s2": .88, "s3": .95, "s4": .92, "s5": .95, "s6": .92, "s7": .93,
             "s8": .93}


# ---------------------------------------------------------------------------------------------

def short_1(c: dict, _: dict) -> tuple[str, str]:
    css = CODE_CSS + """
.bubble {
  border:1px solid %(line)s; background:%(panel)s; border-radius:16px;
  padding:24px 28px; font-size:34px; color:%(ink)s; margin-bottom:44px;
}
.flagbox { margin-top:26px; display:flex; align-items:baseline; gap:18px; }
.flagbox .lab { font-size:27px; color:%(mute)s; }
.flagbox .val { font-size:40px; color:%(yours)s; font-weight:700; }
""" % PALETTE

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.2)}>{c["cap"]}</div>
  <div class="bubble" {a("rise", 0.5)}>{c["prompt"]}</div>
  {code_block(c["src_label"], c["code"], 1.4, marks=("TotalAmount",),
              step=0.5, size=25)}
  <div class="flagbox" {a("rise", 4.4)}>
    <span class="lab">{c["flag"]}</span>
    <span class="mono val">{c["flag_code"]}</span>
  </div>
  <div class="gap"></div>
  <div class="big grey" {a("rise", 6.6, 0.6)}>{c["line1"]}</div>
  <div class="big" {a("rise", 8.2, 0.6)}>{c["line2a"]}<span class="yours">{c["line2b"]}</span></div>
  <div class="gap"></div>
  <div {a("fade", 10.4, 0.6)}>
    {"".join(f'<span class="pill">{p}</span>' for p in c["pills"][:1])}
    {"".join(f'<span class="pill pill--accent">{p}</span>' for p in c["pills"][1:2])}
    {"".join(f'<span class="pill">{p}</span>' for p in c["pills"][2:])}
  </div>
</div>"""
    return css, body


def short_2(c: dict, _: dict) -> tuple[str, str]:
    css = """
.row {
  padding:34px 32px; margin-bottom:26px; border:1px solid %(line)s;
  background:%(panel)s; border-radius:18px;
}
.row .what { font-size:36px; color:%(graphite_ink)s; margin-bottom:12px; }
.row .tool { font-size:29px; color:%(mute)s; }
.row--yours { border-color:%(yours)s; background:%(yours_soft)s; }
.row--yours .what { color:%(ink)s; font-weight:640; }
.row--yours .tool { color:%(yours)s; font-weight:640; }
""" % PALETTE

    rows = "".join(
        f'<div class="row" {a("rise", 0.6 + i * 1.3)}><div class="what">{what}</div>'
        f'<div class="tool">{tool} &#10003;</div></div>'
        for i, (what, tool) in enumerate(c["rows"]))
    what3, tool3 = c["row3"]

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.2)}>{c["cap"]}</div>
  {rows}
  <div class="row row--yours" {a("rise", 4.2, 0.6)}>
    <div class="what">{what3}</div>
    <div class="tool" {a("fade", 6.6, 0.6)}>{tool3}</div>
  </div>
  <div class="gap"></div>
  <div class="mid" {a("rise", 8.8, 0.6)}>{c["close"]}</div>
</div>"""
    return css, body


def short_3(c: dict, _: dict) -> tuple[str, str]:
    css = CODE_CSS + """
.wrap { position:relative; }
@keyframes stampdown {
  from { opacity:0; transform:rotate(-9deg) scale(1.6); }
  to   { opacity:1; transform:rotate(-9deg) scale(1); }
}
.stamp {
  position:absolute; right:-8px; bottom:-26px; transform:rotate(-9deg);
  border:4px solid %(yours)s; color:%(yours)s; border-radius:12px;
  padding:14px 26px; font-size:32px; font-weight:700; letter-spacing:.05em;
}
.qa { margin-top:64px; }
.q { font-size:34px; color:%(ink)s; margin-bottom:20px; }
.aline { font-size:34px; color:%(graphite_ink)s; text-decoration:line-through; }
""" % PALETTE

    code = snippet("PharmacyDemo.Module/DatabaseUpdate/PharmacyUpdater.cs",
                   'if (CurrentDBVersion < new Version("1.1.0.0")', lines=6, before=2)

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.2)}>{c["cap"]}</div>
  <div class="wrap">
    {code_block("PharmacyUpdater.cs", code, 0.4, marks=("CurrentDBVersion",), step=0.34, size=22)}
    <div class="stamp mono" {a("stampdown", 3.6, 0.45)}>{c["stamp"]}</div>
  </div>
  <div class="gap"></div>
  <div class="body" {a("rise", 5.2, 0.6)}>{c["when"]}</div>
  <div class="qa">
    <div class="q" {a("rise", 7.0, 0.6)}>&ldquo;{c["question"]}&rdquo;</div>
    <div class="aline" {a("rise", 8.8, 0.6)}>{c["answer"]}</div>
  </div>
  <div class="gap"></div>
  <div class="mid yours" {a("rise", 11.0, 0.6)}>{c["close"]}</div>
</div>"""
    return css, body


def short_4(c: dict, _: dict) -> tuple[str, str]:
    css = """
.path {
  border:1px solid %(line)s; background:%(panel)s; border-radius:14px;
  padding:26px 28px; font-size:24px; color:%(graphite_ink)s;
}
.path--there { border-color:%(yours)s; background:%(yours_soft)s; color:%(yours)s; }
.tag { display:block; margin-top:14px; font-size:24px; color:%(mute)s; }
.path--there .tag { color:%(yours)s; }
.arrow { text-align:center; font-size:46px; color:%(yours)s; margin:22px 0; }
""" % PALETTE

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.2)}>{c["cap"]}</div>
  <div class="path mono" {a("rise", 0.5)}>PharmacyDemo.Module/BusinessObjects/Product.cs
    <span class="tag">&#8592; {c["here"]}</span></div>
  <div class="arrow" {a("fade", 2.6, 0.6)}>&#8595;</div>
  <div class="path path--there mono" {a("rise", 3.4, 0.6)}>PharmacyDemo.Blazor.Server/Editors/BarcodeScannerPropertyEditor.cs
    <span class="tag">&#8592; {c["there"]}</span></div>
  <div class="gap"></div>
  <div class="body" {a("rise", 5.8, 0.6)}>{c["note1"]}</div>
  <div class="body" {a("rise", 7.4, 0.6)}>{c["note2"]}</div>
  <div class="gap"></div>
  <div class="big yours" {a("rise", 9.4, 0.6)}>{c["close"]}</div>
</div>"""
    return css, body


def short_5(c: dict, ctx: dict) -> tuple[str, str]:
    """The map, filling the frame. Movement holds a viewer who reads nothing."""
    still = ctx["stills"]["map"]
    css = """
.stage { padding:0; justify-content:flex-start; }
.window {
  position:absolute; left:0; right:0; top:%(top)dpx; height:%(mh)dpx; overflow:hidden;
}
.window img { display:block; width:190%%; max-width:none; }
@keyframes drift { from { transform:translate(-24%%, -4%%) scale(1); }
                   to   { transform:translate(-26%%, -8%%) scale(1.14); } }
.window .roll { animation: drift %(dur)ss linear 0s both; }
.window::after {
  content:""; position:absolute; inset:0; pointer-events:none;
  background:linear-gradient(%(bg)s 0%%, transparent 14%%, transparent 74%%, %(bg)s 99%%);
}
.lines { position:absolute; left:60px; right:60px; bottom:%(bottom)dpx; }
.lines > div { margin-bottom:26px; }
""" % {**PALETTE, "top": SAFE_TOP - 190, "mh": 980, "dur": SECONDS["s5"],
       "bottom": SAFE_BOTTOM}

    body = f"""
<div class="stage">
  <div class="window" {a("fade", 0.0, 0.8)}>
    <div class="roll"><img src="../../{still["rel"]}" alt=""></div>
  </div>
  <div class="lines">
    <div class="big" {a("rise", 6.6, 0.6)}>{c["line1"]}</div>
    <div class="mid grey" {a("rise", 8.8, 0.6)}>{c["line2"]}</div>
    <div class="mid yours" {a("rise", 10.6, 0.6)}>{c["line3"]}</div>
  </div>
</div>"""
    return css, body


def short_6(c: dict, _: dict) -> tuple[str, str]:
    css = """
.term {
  border:1px solid %(line)s; background:%(panel)s; border-radius:16px;
  padding:30px 30px; font-size:23px; line-height:1.95; margin-bottom:34px;
}
.term div { white-space:pre-wrap; }
.term .cmd { color:%(ink)s; }
.term .err { color:%(graphite_ink)s; }
.term--ok { border-color:%(yours)s; background:%(yours_soft)s; }
.term--ok .out { color:%(yours)s; }
""" % PALETTE

    broken = "".join(
        f'<div class="{"cmd" if line.startswith("$") else "err"}" '
        f'{a("wipe", 0.4 + i * 0.7, 0.5)}>{line}</div>'
        for i, line in enumerate(c["broken"]))
    works = "".join(
        f'<div class="{"cmd" if line.startswith("$") else "out"}" '
        f'{a("wipe", 4.4 + i * 0.8, 0.5)}>{line}</div>'
        for i, line in enumerate(c["works"]))

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.2)}>{c["cap"]}</div>
  <div class="term mono">{broken}</div>
  <div class="term term--ok mono" {a("fade", 4.2, 0.5)}>{works}</div>
  <div class="gap"></div>
  <div class="big grey" {a("rise", 7.0, 0.6)}>{c["note1"]}</div>
  <div class="big" {a("rise", 8.8, 0.6)}>{c["note2"]}</div>
</div>"""
    return css, body


def short_7(c: dict, _: dict) -> tuple[str, str]:
    css = """
.bar { height:96px; border-radius:14px; background:%(panel)s; border:1px solid %(line)s;
       overflow:hidden; margin-bottom:18px; }
.bar span { display:block; height:100%%; transform-origin:left center; }
.bar--bad span { width:90%%; background:%(graphite)s; }
.bar--good span { width:11%%; background:%(yours)s; }
.lab { font-size:31px; margin-bottom:16px; }
.note { font-size:28px; color:%(mute)s; margin-bottom:58px; }
.note--bad { color:%(graphite_ink)s; }
""" % PALETTE

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.2)}>{c["cap"]}</div>
  <div class="lab grey" {a("fade", 0.5)}>{c["bad_label"]}</div>
  <div class="bar bar--bad"><span style="animation:1.5s linear .8s both grow"></span></div>
  <div class="note note--bad" {a("rise", 2.6, 0.6)}>{c["bad_note"]}</div>

  <div class="lab" {a("fade", 4.6, 0.6)}>{c["good_label"]}</div>
  <div class="bar bar--good"><span style="animation:.7s linear 4.9s both grow"></span></div>
  <div class="note" {a("rise", 6.0, 0.6)}>{c["good_note"]}</div>

  <div class="mid yours" {a("rise", 8.4, 0.6)}>{c["close"]}</div>
</div>"""
    return css, body


def short_8(c: dict, ctx: dict) -> tuple[str, str]:
    still = ctx["stills"]["screens"]
    css = """
.term {
  border:1px solid %(line)s; background:%(panel)s; border-radius:16px;
  padding:28px; font-size:21px; line-height:1.9; margin-bottom:40px;
}
.term .cmd { color:%(ink)s; white-space:pre-wrap; }
.term .none { color:%(graphite_ink)s; }
.count { font-size:46px; font-weight:700; color:%(yours)s; margin:34px 0 14px; }
.window {
  height:520px; border:1px solid %(line)s; border-radius:16px; overflow:hidden;
  background:%(panel)s; margin-top:34px; position:relative;
}
.window img { display:block; width:100%%; }
@keyframes pan8 { from { transform:translateY(0); } to { transform:translateY(-%(travel)spx); } }
.window .roll { animation: pan8 5s linear 9.6s both; }
""" % {**PALETTE, "travel": round(still["height"] * ((W - 120) / still["width"]) - 520, 1)}

    body = f"""
<div class="stage">
  <div class="cap" {a("fade", 0.2)}>{c["cap"]}</div>
  <div class="term mono">
    <div class="cmd" {a("wipe", 0.4, 0.9)}>{c["grep"]}</div>
    <div class="none" {a("fade", 1.8, 0.5)}>{c["nothing"]}</div>
  </div>
  <div class="big" {a("rise", 3.2, 0.6)}>{c["line"]}</div>
  <div class="count mono" {a("rise", 5.6, 0.6)}>{c["counter"].format(**ctx["counts"])}</div>
  <div class="body" {a("fade", 7.2, 0.6)}>{c["note"]}</div>
  <div class="window" {a("fade", 9.2, 0.6)}>
    <div class="roll"><img src="../../{still["rel"]}" alt=""></div>
  </div>
  <div class="mid yours" style="margin-top:30px;{a("rise", 12.6, 0.6)[7:-1]}">{c["close"]}</div>
</div>"""
    return css, body


BUILDERS = {"s1": short_1, "s2": short_2, "s3": short_3, "s4": short_4,
            "s5": short_5, "s6": short_6, "s7": short_7, "s8": short_8}


# ---------------------------------------------------------------------------------------------

def counts() -> dict:
    """Entities and screens, counted in the report rather than typed in.

    The short puts both numbers on screen. Typed here they would be a claim nothing keeps true
    the next time the fixture grows -- which is the failure the whole project is about.
    """
    if not REPORT.exists():
        build_report(REPORT)
    html = REPORT.read_text(encoding="utf-8")

    entities = len(set(re.findall(r'id="entity-([A-Za-z0-9_]+)"', html)))
    screens = len(set(re.findall(r"\b([A-Za-z0-9_]+_(?:List|Detail|LookupList)View)\b", html)))
    if not entities or not screens:
        sys.exit("could not count entities or screens in the report — the short would lie")
    return {"entities": entities, "screens": screens}


def stills() -> dict:
    found = {}
    for key in ("map", "screens"):
        path = STILLS / f"{key}.png"
        if not path.exists():
            sys.exit(f"{path.relative_to(VIDEO)} is missing — run build-scenes.py first")
        from PIL import Image
        with Image.open(path) as image:
            found[key] = {"width": image.width, "height": image.height,
                          "rel": path.relative_to(VIDEO).as_posix()}
    return found


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--video", nargs="*", default=None, metavar="SHORT",
                        help="render mp4s; with no ids, every short")
    args = parser.parse_args()

    ctx = {"stills": stills(), "counts": counts()}
    print(f"counted in the report: {ctx['counts']['entities']} entities, "
          f"{ctx['counts']['screens']} screens")

    problems = []
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch()
        page_obj = browser.new_page(viewport={"width": W, "height": H},
                                    device_scale_factor=1, color_scheme="dark")

        for lang in COPY:
            (SHORTS / lang).mkdir(parents=True, exist_ok=True)
            (POSTERS / lang).mkdir(parents=True, exist_ok=True)
            print(f"\n{lang}")

            for short_id, build in BUILDERS.items():
                css, body = build(COPY[lang][short_id], ctx)
                html = SHORTS / lang / f"{short_id}.html"
                html.write_text(page(FRAME_CSS, css, body), encoding="utf-8")

                page_obj.goto(html.as_uri())
                page_obj.wait_for_timeout(120)
                freeze(page_obj, POSTER_AT[short_id] * SECONDS[short_id])
                poster = POSTERS / lang / f"{short_id}.png"
                page_obj.screenshot(path=str(poster))

                # Against the safe band, not the frame: a line the platform's own interface
                # covers is as lost as one that fell off the bottom.
                spill = overflowing(page_obj, W, H - SAFE_BOTTOM)
                mark = f"  SPILLS {spill}" if spill else ""
                print(f"  {short_id}  {SECONDS[short_id]:>3}s  "
                      f"{poster.relative_to(VIDEO)}{mark}")
                if spill:
                    problems.append(f"{lang}/{short_id}: {spill} past the safe band")

                if args.video is not None and (not args.video or short_id in args.video):
                    render(page_obj, html, RENDER / lang / f"{short_id}.mp4", SECONDS[short_id])

        browser.close()

    REPORT.unlink(missing_ok=True)
    if problems:
        sys.exit("\ncontent outside the safe band:\n  " + "\n  ".join(problems))


if __name__ == "__main__":
    main()
