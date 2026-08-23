# XAF Logic Explainer

[![CI](https://github.com/peopleworks/XAFLogicExplainer/actions/workflows/ci.yml/badge.svg)](https://github.com/peopleworks/XAFLogicExplainer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/peopleworks/XAFLogicExplainer?color=blue)](LICENSE)
[![NuGet CLI](https://img.shields.io/nuget/v/XafLogicExplainer.Cli?logo=nuget&label=CLI)](https://www.nuget.org/packages/XafLogicExplainer.Cli)
[![NuGet Core](https://img.shields.io/nuget/v/XafLogicExplainer.Core?logo=nuget&label=Core)](https://www.nuget.org/packages/XafLogicExplainer.Core)
[![NuGet MCP](https://img.shields.io/nuget/v/XafLogicExplainer.Mcp?logo=nuget&label=MCP%20server)](https://www.nuget.org/packages/XafLogicExplainer.Mcp)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![MCP registry](https://img.shields.io/badge/MCP_registry-io.github.peopleworks%2Fxaf--logic--explainer-000000?logo=modelcontextprotocol&logoColor=white)](https://registry.modelcontextprotocol.io/v0/servers?search=xaf-logic-explainer)
[![Available on CodeGuilds](https://img.shields.io/badge/Available_on-CodeGuilds-6366f1)](https://codeguilds.dev/packages/xaf-logic-explainer)
[![Listed on Glama](https://img.shields.io/badge/Listed_on-Glama-a855f7)](https://glama.ai/mcp/servers/tnzvgbukeb)
[![XAF](https://img.shields.io/badge/DevExpress-XAF-FF7200?logo=devexpress&logoColor=white)](https://www.devexpress.com/products/net/application_framework/)
[![GitHub stars](https://img.shields.io/github/stars/peopleworks/XAFLogicExplainer?style=social)](https://github.com/peopleworks/XAFLogicExplainer/stargazers)

**Teach your AI coding agent what *your* XAF application actually does.**

**[See how it works &rarr;](https://peopleworks.github.io/XAFLogicExplainer/)**

Point it at an XAF module. It reads your entities, controllers, actions, business rules,
navigation and Model Editor customizations straight from source — and hands the result to
whatever agent you code with.

---

## Why this exists

DevExpress has done excellent work making AI agents fluent in XAF. Two pieces already exist,
and this is the third:

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/how-it-fits-dark.svg">
  <img alt="Three kinds of knowledge an agent needs about an XAF codebase. Two are already solved by DevExpress tooling; the third — what your own application does — is the gap this project fills." src="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/how-it-fits-light.svg">
</picture>

| Teaches the agent… | Tool |
| --- | --- |
| How XAF works in general | [DevExpress `agent-skills`](https://github.com/DevExpress/agent-skills) |
| What the official documentation says | [DevExpress Docs MCP Server](https://docs.devexpress.com/) |
| **What YOUR application does** | **XAF Logic Explainer** ← *you are here* |

An agent that has read every page of the XAF documentation still does not know that your `Invoice`
total is calculated from its lines, that `ApproveController` refuses to run when the period is
closed, or that three columns were hidden in the Model Editor and appear in no C# file at all. It
will confidently invent all three.

That gap is not solvable by better prompting. It is solvable by extraction.

**These tools compose.** Install the DevExpress skills for framework knowledge, use the Docs MCP
for the official reference, and use this for your own codebase. None of them replaces the others.

## What it extracts

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/extraction-pipeline-dark.svg">
  <img alt="Source files are parsed as syntax by Roslyn, never compiled, producing a model rendered to agent files, an MCP server, or Markdown and JSON." src="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/extraction-pipeline-light.svg">
</picture>

Everything below is read as **syntax**, using Roslyn. Your project never has to compile, and this
tool never links against DevExpress assemblies:

- **Entities** — properties, types, associations, and the XAF attributes that give them meaning
  (`[Association]`, `[Aggregated]`, `[RuleRequiredField]`, `[Appearance]`, `[ModelDefault]`, …).
  **XPO and EF Core**, auto-detected from your `using` statements.
- **Controllers and actions** — `SimpleAction`, `PopupWindowShowAction`, `SingleChoiceAction`,
  their target criteria, and the handler code that runs when they fire.
- **Business rules** — validation attributes and code rules, with the conditions attached.
- **Module setup** — `ModuleUpdater` seed data and what gets created on first run.
- **Navigation** — the groups and items your users actually see.
- **Model Editor (`.xafml`)** — the customizations that exist *only* in XML and are invisible to
  anyone reading your C#. Module and platform files are merged the way XAF merges them.
- **Custom property and list editors** — including the JavaScript they cannot work without, and
  built-in editors reconfigured at run time through `View.CustomizeViewItemControl<T>()`. These
  live in the platform project *beside* the module, so nobody reading the business objects meets
  them.
- **Version-gated migrations** — the `CurrentDBVersion < new Version(…)` blocks in your updater.
  Each runs at most once for any database, and is the only explanation for data the current code
  cannot account for.
- **Every screen, and what loads onto it** — see below.

These are the reason an agent that has read every business class can still be confidently wrong
about the application:

<img alt="The custom editors section of a generated explainer: a barcode scanner property editor with what it renders, the alias XAF matches on, its base type and the JavaScript file it depends on, followed by built-in editors a controller reconfigures at run time." src="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/explainer-editors.png">

## What runs when you open this screen

Nothing in an XAF repository answers that, and both halves are missing for different reasons.

**The screens themselves are in no file.** XAF generates a list, a detail and a lookup view for
every business class, plus a list view for every collection, and the Model Editor stores only the
ones somebody changed. Grepping your source for `Patient_Prescriptions_ListView` finds nothing —
and that is not evidence it is missing.

**Which controllers run there is decided at run time**, by four conditions XAF ANDs together:
nesting, view type, object type and view id. Each is unrestricted when unset, so a controller that
sets none of them loads onto *every* screen you have.

This reads all four the way `ViewController.IsFitToView` evaluates them, against a view inventory
built from the framework's own id generators — and records **why** each one matched, so the answer
can be checked rather than trusted:

<img alt="The screens section of a generated explainer, showing the five views XAF generates for one business class. Each names the controllers that activate on it and the condition that made each one match; the framework's own controllers are folded away behind a single line." src="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/explainer-screens.png">

Two layers, kept apart. What your team wrote gets the full treatment; what XAF provides is folded
away behind one line, because there is a great deal of it and it is not yours to change. With a
[ground-truth catalog](#optional-tell-your-code-apart-from-devexpresss) it is named too — scoped to
the modules you actually register, so a WinForms controller never appears on a Blazor screen.

What it will not claim: a controller listed here can still switch itself off through
`Active["reason"]`, which depends on the data and the user. This is what XAF **loads** onto a
screen, not what will necessarily do something — and anything it could not read from the source is
listed apart, with the reason, instead of being quietly treated as "runs everywhere".

## Quick start

```bash
dotnet tool install -g XafLogicExplainer.Cli

xaflogic agents --project "C:\MySolution\MyApp.Module"
```

That writes `AGENTS.md`, `CLAUDE.md` and `.github/copilot-instructions.md` at your solution root.
No account, no API key, no server. Your agent understands the application on its next question.

### What it writes, and why it is split in two

`AGENTS.md` is prepended to *every* request an agent makes in the repository, so its cost is paid
forever. Dumping 70 KB of entity detail there would crowd out the actual question. So the output is
tiered:

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/two-tier-context-dark.svg">
  <img alt="What a full documentation dump costs an agent's context on every request, against the tiered output that leaves that room free." src="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/two-tier-context-light.svg">
</picture>


| | | |
| --- | --- | --- |
| `AGENTS.md` | ~11 KB | Always loaded: ground rules, complete inventories, conventions, recipes |
| `.xaflogic/*.md` | ~70 KB | Opened on demand: full properties, handler code, rule messages, `.xafml` |

The most valuable part is the smallest. `AGENTS.md` opens with **ground rules** — that this
application uses XPO and never EF Core, that the inventories are *complete* so anything absent
genuinely does not exist, and that some behavior lives in the Model Editor rather than in C#. Those
few paragraphs stop most of the confident invention agents produce about unfamiliar XAF codebases.

Existing files are never clobbered: generated text lives between markers, anything you wrote by
hand is preserved, and regenerating is byte-identical when nothing changed.

## Or let the agent ask questions directly

Generated files are a snapshot. The MCP server is a live connection — the agent queries your
application while you work on it, and cannot go stale.

```
/plugin marketplace add peopleworks/XAFLogicExplainer
/plugin install xaf-logic-explainer@peopleworks-xaf
```

That installs a skill and an MCP server in one step. For any other MCP client, either run it
straight from NuGet with no install:

```json
{
  "mcpServers": {
    "xaf": { "command": "dnx", "args": ["XafLogicExplainer.Mcp", "--yes"] }
  }
}
```

…or point at the CLI if you already have it:

```json
{ "mcpServers": { "xaf": { "command": "xaflogic", "args": ["mcp"] } } }
```

Started from a solution directory it finds the XAF module by itself, so neither form needs a path.

| Tool | Answers |
| --- | --- |
| `xaf_overview` | What this application is, and the complete list of everything in it |
| `xaf_search` | Where a field, concept or business term is defined |
| `xaf_entity` | Every property, relationship, rule and calculation on one entity |
| `xaf_controller` | What an action does — including the C# that runs when it fires |
| `xaf_rules` | What the application validates, computes, hides and disables |
| `xaf_model` | Model Editor customizations, which exist in no C# file |
| `xaf_editors` | Custom editors, the JavaScript they need, and built-in editors changed at run time |
| `xaf_migrations` | What ran once against a live database, and the comment explaining why |
| `xaf_view` | Everything loaded onto one screen — which controllers activate, and why |
| `xaf_walkthrough` | **How one process works end to end** — what runs, in what order, and what it could not follow |
| `xaf_refresh` | Re-read the source (changes are detected automatically) |

Ask for something that isn't there and the answer is the useful one:

> There is no entity called 'PurchaseOrder' in this application.
> This is the complete list of 19 entities, extracted from the whole source tree: …
> If the user expects 'PurchaseOrder' to exist, it has not been created yet.

**Pair it with the official DevExpress skills.** `/plugin install dx-xaf@DevExpress-agent-skills`
teaches how XAF works; this teaches what your application does. An agent with only the first will
write correct XAF against entities you do not have.

## The same knowledge, for a person

An agent reads `AGENTS.md` or queries the MCP server. Someone who has just inherited a ten-year-old
XAF application needs the same facts arranged very differently:

```bash
xaflogic explain --project "C:\MySolution\MyApp.Module" --open
```

One HTML file. No server, no build step, no request to the network — it opens from an email
attachment on a machine with no internet, which is how handovers actually happen.

It draws **a map of your domain model** from the association attributes scattered across your
codebase. Most teams have never seen theirs: it exists in one person's head, which is exactly the
knowledge that leaves when they do.

![The domain model of a sample XAF application. Hovering an entity dims everything it does not touch, leaving only its own relationships lit — purple where deleting the parent deletes the child.](https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/domain-map.gif)

<sub>Real output, from the sample application in this repository. Hover an entity and everything it
does not touch fades; purple means deleting the parent deletes the child.</sub>

Alongside it: every entity and what each property is, every action with the code it runs,
validation with the message the user will actually see, and the Model Editor settings that appear
in no C# file.

<img alt="An entity card from a generated explainer: every property with its type, the calculated ones marked with the expression behind each, and the relationships whose parent owns the child marked as owned." src="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/explainer-entity.png">

And **an index of every criteria expression in the application** — a dialect that is neither SQL
nor C#, gathered from attributes spread across the source and otherwise collected nowhere:

<img alt="The criteria index of a generated explainer: appearance rules, validation and lookup filters in one table, each with the entity and attribute it came from." src="https://raw.githubusercontent.com/peopleworks/XAFLogicExplainer/main/docs/assets/explainer-criteria.png">

Try it on the sample without touching your own code:

```bash
xaflogic explain --project tests/XafLogicExplainer.Tests/Fixtures/DemoSolution/PharmacyDemo.Module --open
```

## Optional: tell your code apart from DevExpress's

Extraction reads your source without knowing anything about the framework it is written against,
which leaves one question unanswerable: is `DeleteObjectsViewController` something your team wrote,
or something DevExpress ships? Without an answer, generated documentation presents framework
behavior and your own logic as the same thing.

If you have a DevExpress licence:

```bash
xaflogic catalog build
```

That reads **your own installation** and records what XAF itself provides — attributes, controllers,
model interfaces and modules, with the official summaries and documentation links DevExpress ships.
On DevExpress 26.1 that is around 850 framework types.

If you also installed the DevExpress **source code** component, it records *where each framework
controller activates* — the four conditions XAF checks before running it. That cannot be read from
the assemblies: four out of five built-in controllers set their target inside a constructor. Pass
`--dx-sources <Components/Sources>` if they are not beside your assemblies.

Extraction then picks it up automatically and can say things it otherwise could not:

- *"`ArchiveController` extends the built-in `DeleteObjectsViewController`"* — you are changing how
  deletion works application-wide, not adding a feature beside it.
- *"`[AuditedByFinance]` is not an XAF or .NET attribute"* — your team invented it, so its meaning
  lives in this codebase and in no documentation anywhere.
- *"32 framework controllers also load onto this screen"* — named, with what each one does, and
  scoped to the modules your application actually registers, so a WinForms controller never appears
  on a Blazor screen.

The catalog is written to `~/.xaflogic/catalog/`, **never into your repository**: it is derived from
licensed software. Everything works without it — it only sharpens the output. See
[NOTICE.md](NOTICE.md).

### Commands

| Command | What it does |
| --- | --- |
| `agents` | **Write `AGENTS.md` / `CLAUDE.md` / Copilot instructions for your agent** |
| `mcp` | **Run as an MCP server so agents can query the app live** |
| `explain` | **Write a self-contained HTML page explaining the app to a person** |
| `catalog` | Build the DevExpress ground-truth catalog (`build`, `status`) |
| `extract` | Read the project, write Markdown + JSON locally |
| `walkthrough` | **Trace one business process** — what runs, in what order, and what governs it |
| `diff` | Compare against the previous extraction and report what changed |
| `status` | Show the change-detection hash and whether a re-extract is needed |
| `watch` | Re-extract on file change, with debounce |
| `sync` | Extract and publish to a remote target |
| `chat` | Ask questions about the extracted project |
| `config` | Set defaults in `~/.xaflogic/config.json` |
| `projects` | Manage several XAF projects; most commands accept `--all` |

Documentation is generated in **English or Spanish** (`--lang en|es`).

Useful flags: `--orm auto\|xpo\|efcore`, `--lang en\|es`, `--enrich` (AI-generated business-logic
summaries per controller and action), `--force`, `--all`.

### Tracing one process

```bash
xaflogic walkthrough --from ApproveOrder            # to the screen, or > process.md
xaflogic walkthrough --from ApproveOrder --depth 4 --out docs/approval.md
```

What runs, in what order, which entities it touches and which rules govern them — every step citing
`file:line`, with a Mermaid diagram **emitted from the trace itself, never drawn by a model.** Calls
the trace could not follow are listed rather than skipped, so an empty list means the path really is
complete. Add `--narrate` for prose over the steps; a paragraph that cannot name a real step is
dropped before you see it.

`--enrich` and `--narrate` need a model, and **any of these is enough** — a key on the command line
wins, then the environment, then a PeopleWorks Copilot account if you happen to have one:

```bash
xaflogic extract --enrich --api-key sk-...              # or any OpenAI-compatible endpoint:
xaflogic extract --enrich --api-key ... --ai-base-url http://localhost:11434/v1 --ai-model qwen2.5-coder

export OPENAI_API_KEY=sk-...        # picked up with no configuration at all
export ANTHROPIC_API_KEY=sk-ant-...
```

Everything else in this tool runs with no key, no account and no network — the walkthrough
included, minus its prose.

Extraction is **incremental** — a SHA-256 over your `.cs` and `.xafml` files means an unchanged
project is a no-op. There is an MSBuild `.targets` file if you want it to run on build.

## Status

**v0.14.0.** The extraction engine is the mature part: it runs in production against real XAF
applications. The agent-facing surface is what is landing now, in the open.

| | |
| --- | --- |
| ✅ | Roslyn extraction — entities, controllers, rules, updater, navigation, `.xafml` |
| ✅ | XPO and EF Core, auto-detected |
| ✅ | **Custom property and list editors**, their client assets, and built-in editors reconfigured at run time |
| ✅ | **Version-gated data migrations** — what happened to databases that were not fresh |
| ✅ | Incremental change detection, diff reports, multi-project, watch mode |
| ✅ | AI enrichment of controllers and actions (`--enrich`) |
| ✅ | Blazor in-app help panel |
| ✅ | **`AGENTS.md` / `CLAUDE.md` / Copilot instructions** — zero infrastructure, works for everyone |
| ✅ | **`xaflogic explain`** — one self-contained HTML page, for a person rather than an agent |
| ✅ | Pluggable publishing targets (`IDocumentationSink`) |
| ✅ | **MCP server** — 11 tools, live against your source |
| ✅ | **Installable Claude Code plugin** with skill and MCP server |
| ✅ | **382 tests** over synthetic XPO and EF Core fixtures — no DevExpress needed |
| ✅ | **DevExpress ground-truth catalog**, generated locally by licensees |

PeopleWorks Copilot, where this tool grew up, is now one sink among several rather than the
destination everything was built around. The outputs that matter most need no server at all.

## The long version

Why a third of an XAF application's behaviour lives outside its business classes, the four places
it hides, and what the extracted output actually looks like:

- **[Your coding agent knows XAF. It has never seen your application.](https://peopleworksgpt.com/your-coding-agent-knows-xaf-it-has-never-seen-your-application/)**
- **[Tu agente de código sabe XAF. Nunca ha visto tu aplicación.](https://peopleworks.com.do/2026/08/13/tu-agente-de-codigo-sabe-xaf-nunca-ha-visto-tu-aplicacion/)** — *en español*

Each is written in its own language rather than translated from the other. Sources in
[`docs/Blog/`](docs/Blog/).

## Repository layout

```
src/
  XafLogicExplainer.Core                 Roslyn extraction engine — no DevExpress reference
  XafLogicExplainer.Mcp                  MCP server (ModelContextProtocol 2.1)
  XafLogicExplainer.Cli                  the `xaflogic` command
  XafLogicExplainer.CopilotSync          PeopleWorks Copilot target + AI enrichment
  XafLogicExplainer.DescriptionAnnotator generates missing [Description] attributes
  XafLogicExplainer.Blazor               in-app help panel for XAF Blazor apps
plugins/
  xaf-logic-explainer                    the installable Claude Code plugin
```

Built on **.NET 10**.

Only `XafLogicExplainer.Blazor` references DevExpress packages; it needs the DevExpress NuGet feed
and a license to build. Everything else builds anywhere, which is why CI can verify it for free.

## Contributing

The single most valuable contribution is telling us **what the extractor missed**. XAF is enormous,
every codebase uses a different slice of it, and no single project exercises the whole framework.
There is an [extraction-gap issue template](.github/ISSUE_TEMPLATE/extraction-gap.yml) for exactly
this: show the XAF pattern your project uses and what the tool failed to see.

See [CONTRIBUTING.md](CONTRIBUTING.md). Bug reports, docs and translations are equally welcome.

## License

[MIT](LICENSE). See [NOTICE.md](NOTICE.md) for the relationship to DevExpress.

An independent community project — not affiliated with, endorsed by, or supported by
Developer Express Inc. It contains no DevExpress source code and needs no DevExpress license to
build or run. *DevExpress*, *XAF* and *eXpressApp Framework* are trademarks of Developer Express Inc.

Built by [Pedro Hernández](https://github.com/peopleworks) (PeopleWorks),
[Microsoft MVP for .NET](https://mvp.microsoft.com/en-US/mvp/profile/24060a02-dbc6-44ec-bca5-c213ff9835c5) —
for the DevExpress and XAF community.
