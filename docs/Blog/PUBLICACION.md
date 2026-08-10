# Paquete de publicación — XAF Logic Explainer

Artículos en `Docs/Blog/*.md` (fuente) y `*.html` (para WordPress/Blogger, generado con
`node build-article.mjs <fichero>.md`). Guiones de vídeo en `video/guion.video.{en,es}.json`.

Enlaces fijos:
**Sitio** https://peopleworks.github.io/XAFLogicExplainer/ ·
**Repo** https://github.com/peopleworks/XAFLogicExplainer ·
**NuGet** https://www.nuget.org/packages/XafLogicExplainer.Cli

> **Nota sobre el público.** Esto no es un lanzamiento de consumo como SignsOfAI. El lector es un
> desarrollador .NET con licencia de DevExpress y una aplicación XAF en producción, probablemente
> heredada. Es un público pequeño, técnico y muy concreto: el gancho es el dolor (el agente inventa)
> y no la novedad (otra herramienta de IA). Nada de exageraciones — este público las detecta.

---

## 1. ARTÍCULO — Inglés (`xaflogic-hidden-behaviour-story.en.md`)

**Título:** Your coding agent knows XAF. It has never seen your application.

**Dónde publicar, por orden de valor:**

1. **DevExpress Support Center / blogs de la comunidad XAF** — es donde está el público exacto.
   Publicar como aporte de la comunidad, dejando claro desde la primera línea que el proyecto
   *complementa* el tooling oficial de DevExpress y no compite con él. La tabla de tres filas hace
   ese trabajo sola.
2. **dev.to** con las etiquetas `dotnet`, `devexpress`, `ai`, `showdev`.
3. **LinkedIn** (artículo completo, no enlace: LinkedIn penaliza los enlaces salientes).
4. **Blog propio**, como `canonical_url`. Poner el canónico en dev.to y LinkedIn para no competir
   consigo mismo en buscadores.

**Resumen para redes (280 caracteres):**
```
Your AI assistant can recite the XAF docs and still invent a property your class doesn't have.
It knows XAF. It's never seen YOUR XAF. So I built the missing piece — free, MIT, no DevExpress
licence needed to run it.
```

---

## 2. ARTÍCULO — Español (`xaflogic-hidden-behaviour-story.es.md`)

**Título:** Tu agente de código sabe XAF. Nunca ha visto tu aplicación.

Escrito en español, no traducido. Los dos artículos comparten estructura y argumentos, pero
ninguna frase es una traducción de la otra — igual que el pack de español de SignsOfAI.

**Dónde publicar:** blog propio, LinkedIn en español, y los grupos hispanohablantes de
DevExpress/XAF. En España y Latinoamérica hay muchísimo XAF en producción y prácticamente ningún
contenido técnico sobre XAF en español: es la ventaja competitiva más barata que tenemos.

**Resumen para redes (280 caracteres):**
```
Tu asistente de IA te recita la documentación de XAF y aun así referencia una propiedad que tu
clase no tiene. Sabe XAF. No ha visto TU XAF. Construí la pieza que falta: gratis, MIT, y para
usarla no hace falta licencia de DevExpress.
```

---

## 3. VÍDEO GRANDE — Inglés (`xaflogic-explainer-en.mp4` · ~2:20 · 1280×720 · voz Rachel)

**Título:**
```
Your AI agent knows XAF but has never seen YOUR app | XAF Logic Explainer — free & open source
```

**Descripción (pegar tal cual):**
```
Ask your AI assistant to add a rule to Invoice and it writes perfect XAF — then references a property your class doesn't have. It isn't hallucinating XAF. It knows XAF. It has never seen yours.

DevExpress already ships agent skills for how the framework works, and a docs MCP server for the reference. Neither has read a line of your codebase. XAF Logic Explainer is the third piece: it reads YOUR application with Roslyn and hands the result to whatever agent you code with.

It never compiles your project and never references a DevExpress assembly — so it works on a branch that doesn't build, and needs no DevExpress licence to run.

It also extracts the parts that aren't in your business classes at all: Model Editor (.xafml) customizations, custom property editors and the JavaScript they depend on, built-in editors reconfigured at run time, and the version-gated migrations that ran once on a production database.

Free and open source (MIT). Built with .NET 10.

▶ How it works: https://peopleworks.github.io/XAFLogicExplainer/
⭐ Code: https://github.com/peopleworks/XAFLogicExplainer
📦 dotnet tool install -g XafLogicExplainer.Cli

CHAPTERS
00:00  It knows XAF, not your XAF
00:16  DevExpress closed part of the gap
00:29  Two minutes: AGENTS.md, CLAUDE.md, Copilot
00:41  Where behaviour actually hides
00:55  Custom editors, and their JavaScript
01:10  Migrations that ran once, three years ago
01:26  Roslyn: no compile, no licence
01:40  Why most docs are NOT in AGENTS.md
01:54  The domain map nobody has seen
02:09  Try it on your application

#DevExpress #XAF #dotnet #AI #OpenSource
```

**Etiquetas / Tags (bloque para YouTube):**
```
xaf, devexpress, devexpress xaf, expressapp framework, xpo, ef core, dotnet, .net 10, roslyn, mcp, model context protocol, ai coding agent, claude code, github copilot, agents.md, legacy code, code documentation, xafml, model editor, open source
```

---

## 4. VÍDEO GRANDE — Español (`xaflogic-explainer-es.mp4` · ~2:20 · 1280×720 · voz Marcela)

**Título:**
```
Tu agente de IA sabe XAF pero no conoce TU app | XAF Logic Explainer — gratis y open source
```

**Descripción (pegar tal cual):**
```
Le pides a tu asistente de IA que añada una regla a Invoice y escribe XAF impecable — y entonces referencia una propiedad que tu clase no tiene. No está alucinando XAF. Sabe XAF. Lo que nunca ha visto es el tuyo.

DevExpress ya publica skills para enseñar cómo funciona el framework, y un servidor MCP con la documentación. Ninguno ha leído una línea de tu código. XAF Logic Explainer es la tercera pieza: lee TU aplicación con Roslyn y le entrega el resultado al agente con el que programes.

Nunca compila tu proyecto y nunca referencia un ensamblado de DevExpress — así que funciona en una rama que no compila, y para usarlo no hace falta licencia de DevExpress.

Además extrae lo que no está en las clases de negocio: personalizaciones del Model Editor (.xafml), editores de propiedad propios y el JavaScript del que dependen, editores integrados reconfigurados en tiempo de ejecución, y las migraciones con versión que se ejecutaron una sola vez sobre una base de datos en producción.

Gratis y de código abierto (MIT). Hecho con .NET 10.

▶ Cómo funciona: https://peopleworks.github.io/XAFLogicExplainer/
⭐ Código: https://github.com/peopleworks/XAFLogicExplainer
📦 dotnet tool install -g XafLogicExplainer.Cli

CAPÍTULOS
00:00  Sabe XAF, pero no tu XAF
00:16  DevExpress cerró parte de la brecha
00:29  Dos minutos: AGENTS.md, CLAUDE.md, Copilot
00:41  Dónde se esconde el comportamiento
00:55  Editores propios, y su JavaScript
01:10  Migraciones que corrieron una vez, hace tres años
01:26  Roslyn: sin compilar, sin licencia
01:40  Por qué la mayoría de la documentación NO está en AGENTS.md
01:54  El mapa de dominio que nadie ha visto
02:09  Pruébalo en tu aplicación

#DevExpress #XAF #dotnet #IA #OpenSource
```

**Etiquetas / Tags:**
```
xaf, devexpress, devexpress xaf, xpo, ef core, dotnet, .net 10, roslyn, mcp, agentes de ia, claude code, github copilot, documentacion de codigo, codigo heredado, xafml, model editor, open source, programacion en español
```

---

## 5. SHORTS (1080×1920) — guiones

Mismo patrón que SignsOfAI: HTML animado con CSS, sin narración salvo donde se indique, texto
grande y una sola idea por short. Cada uno debe entenderse **sin sonido**.

### Short 1 — «Inventa una propiedad» (el gancho) · ~12 s
```
[0.0s]  Prompt en pantalla:  "Add a validation rule to Invoice"
[1.5s]  Aparece código XAF, impecable, línea a línea
[4.0s]  Se resalta en rojo:  Invoice.TotalAmount
[5.5s]  Debajo, en verde:    tu clase tiene → Total
[7.5s]  Texto grande:        No está alucinando XAF.
[9.0s]                       Sabe XAF. No ha visto EL TUYO.
[11.0s] Pills: Gratis · MIT · Sin licencia DX
```
*El más importante. Es el único short que debería promocionarse pagando, si se paga alguno.*

### Short 2 — «Las tres piezas» · ~12 s
```
[0.0s]  Tres filas apareciendo una a una, las dos primeras en gris:
        Cómo funciona XAF        → DevExpress agent-skills   ✓
        Qué dice la documentación → DevExpress Docs MCP      ✓
[4.5s]  La tercera entra en naranja, con retardo:
        Qué hace TU aplicación   → ¿?
[7.0s]  Se rellena:              XAF Logic Explainer
[9.0s]  Texto:                   Se complementan. No compiten.
```

### Short 3 — «La migración fantasma» · ~14 s
```
[0.0s]  Bloque de código:  if (CurrentDBVersion < new Version("1.1.0.0"))
[2.0s]  Sello girado encima: SE EJECUTÓ UNA VEZ
[4.0s]  Texto: En la base de datos de producción de alguien. Hace tres años.
[6.5s]  Pregunta: "¿por qué esta columna tiene ese valor?"
[8.5s]  Respuesta del agente, tachada: se lo inventa
[10.5s] La ficha real de la herramienta, con el comentario del bloque resaltado
[12.5s] Texto: El comentario suele ser el único registro del porqué.
```

### Short 4 — «El editor que no está donde miras» · ~12 s
```
[0.0s]  Árbol de la solución:
          MiApp.Module/BusinessObjects/Product.cs      ← estás leyendo aquí
          MiApp.Blazor.Server/Editors/Barcode...cs     ← el comportamiento está aquí
[4.0s]   Flecha entre los dos, en naranja
[6.0s]   Texto: Una propiedad string que se pinta como un escáner.
[8.0s]   Texto: Y el JavaScript sin el que no funciona.
[10.0s]  Texto: Ni en C#. Ni en XML.
```

### Short 5 — «El mapa que nadie ha visto» · ~13 s
```
[0.0s]  El GIF del mapa de dominio, a pantalla completa (docs/assets/domain-map.gif)
[7.0s]  Texto sobreimpreso: La mayoría de los equipos nunca ha visto el suyo.
[9.5s]  Texto: Vive en la cabeza de una persona.
[11.0s] Texto, en naranja: Justo el conocimiento que se va cuando esa persona se va.
```
*El más compartible. El movimiento del mapa retiene sin necesidad de leer nada.*

### Short 6 — «Sin compilar, sin licencia» · ~11 s
```
[0.0s]  Terminal: git checkout feature/rota
[1.5s]  Salida de build en rojo: 47 errors
[3.5s]  Terminal: xaflogic agents --project ...
[5.0s]  Salida en verde: ✓ AGENTS.md · CLAUDE.md · copilot-instructions.md
[7.0s]  Texto: Nunca compila tu proyecto.
[9.0s]  Texto: Roslyn lee el código como texto.
```

### Short 7 — «El impuesto del AGENTS.md» · ~12 s
```
[0.0s]  Barra de contexto llenándose de gris hasta el 90%: "documentación volcada"
[3.0s]  Texto: Se lee en CADA petición. Para siempre.
[5.0s]  La barra se vacía a un 11% naranja: "índice"
[7.0s]  Debajo, en gris claro: "70 KB, solo si hacen falta"
[9.0s]  Texto: Lo más pequeño es lo que más pesa: si no está en la lista, no existe.
```

---

## 6. ORDEN DE PUBLICACIÓN SUGERIDO

1. **Artículo en inglés** en el blog propio (fija el `canonical_url`).
2. **Short 1** en LinkedIn/X/YouTube Shorts, enlazando al artículo.
3. **Artículo en español**, 2–3 días después, para no solapar audiencias.
4. **Vídeo grande EN**, enlazado desde el README y desde los dos artículos.
5. **Shorts 3 y 5** repartidos en la semana siguiente. El 5 es el más compartible.
6. **Vídeo grande ES**.
7. **Hilo en el Support Center de DevExpress** al final, cuando ya haya artículo y vídeo que
   enlazar. Ahí el tono cambia: menos lanzamiento, más «esto es lo que faltaba y aquí lo tenéis».

## 7. LO QUE NO HAY QUE DECIR

- **No** «reemplaza a las herramientas de DevExpress». Las complementa, y decirlo mal quema el
  único canal de distribución que de verdad importa.
- **No** «entiende tu aplicación». *Extrae* lo que tu aplicación declara. La diferencia es la
  honestidad entera del proyecto.
- **No** presumir de 1.0. Está en 0.x a propósito, y el artículo lo dice: el 1.0 se gana cuando
  haya leído código que no escribimos nosotros.
- **No** usar nombres de clientes en capturas ni en ejemplos. Todo el material visual sale de la
  aplicación demo sintética del repositorio.
