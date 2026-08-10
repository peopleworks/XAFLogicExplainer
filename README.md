# XAF Logic Explainer

[![CI](https://github.com/peopleworks/XAFLogicExplainer/actions/workflows/ci.yml/badge.svg)](https://github.com/peopleworks/XAFLogicExplainer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/peopleworks/XAFLogicExplainer?color=blue)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![XAF](https://img.shields.io/badge/DevExpress-XAF-FF7200?logo=devexpress&logoColor=white)](https://www.devexpress.com/products/net/application_framework/)
[![GitHub stars](https://img.shields.io/github/stars/peopleworks/XAFLogicExplainer?style=social)](https://github.com/peopleworks/XAFLogicExplainer/stargazers)

**Teach your AI coding agent what *your* XAF application actually does.**

Point it at an XAF module. It reads your entities, controllers, actions, business rules,
navigation and Model Editor customizations straight from source — and hands the result to
whatever agent you code with.

---

## Why this exists

DevExpress has done excellent work making AI agents fluent in XAF. Two pieces already exist,
and this is the third:

| Teaches the agent… | Tool |
| --- | --- |
| How XAF works in general | [DevExpress `agent-skills`](https://github.com/DevExpress/agent-skills) |
| What the official documentation says | [DevExpress Docs MCP Server](https://docs.devexpress.com/) |
| **What YOUR application does** | **XAF Logic Explainer** ← *you are here* |

An agent that has read every page of the XAF documentation still does not know that your
`Comision` entity is calculated from `PorcentajeBase`, that your `ApproveController` blocks
approval when the period is closed, or that your `ListView` was customized in the Model Editor
to hide three columns. It will confidently invent all three.

That gap is not solvable by better prompting. It is solvable by extraction.

**These tools compose.** Install the DevExpress skills for framework knowledge, use the Docs MCP
for the official reference, and use this for your own codebase. None of them replaces the others.

## What it extracts

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

## Quick start

```bash
dotnet tool install -g XafLogicExplainer.Cli

# Extract, writing Markdown + JSON into .xaflogic-output/
xaflogic extract --project "C:\MySolution\MyApp.Module"
```

Documentation is generated in **English or Spanish** (`--lang en|es`).

### Commands

| Command | What it does |
| --- | --- |
| `extract` | Read the project, write Markdown + JSON locally |
| `diff` | Compare against the previous extraction and report what changed |
| `status` | Show the change-detection hash and whether a re-extract is needed |
| `watch` | Re-extract on file change, with debounce |
| `sync` | Extract and publish to a remote target |
| `chat` | Ask questions about the extracted project |
| `config` | Set defaults in `~/.xaflogic/config.json` |
| `projects` | Manage several XAF projects; most commands accept `--all` |

Useful flags: `--orm auto\|xpo\|efcore`, `--lang en\|es`, `--enrich` (AI-generated business-logic
summaries per controller and action), `--force`, `--all`.

Extraction is **incremental** — a SHA-256 over your `.cs` and `.xafml` files means an unchanged
project is a no-op. There is an MSBuild `.targets` file if you want it to run on build.

## Status

**v0.9.0.** The extraction engine is the mature part: it runs in production against real XAF
applications. The agent-facing surface is what is landing now, in the open.

| | |
| --- | --- |
| ✅ | Roslyn extraction — entities, controllers, rules, updater, navigation, `.xafml` |
| ✅ | XPO and EF Core, auto-detected |
| ✅ | Incremental change detection, diff reports, multi-project, watch mode |
| ✅ | AI enrichment of controllers and actions (`--enrich`) |
| ✅ | Blazor in-app help panel |
| 🚧 | **`AGENTS.md` / `CLAUDE.md` output** — zero infrastructure, works for everyone |
| 🚧 | **MCP server** — let any agent query your XAF app live |
| 🚧 | **Claude Code / Copilot / Cursor skill**, installable from this repo |
| 🚧 | Pluggable publishing targets (`IDocumentationSink`) |
| 🚧 | xUnit test suite over a synthetic XAF fixture |

Today the only remote publishing target is PeopleWorks Copilot, which is where this tool grew up.
That is being generalized: it becomes one sink among several, and the interesting outputs
(`AGENTS.md`, MCP) need no server at all.

## Repository layout

```
src/
  XafLogicExplainer.Core                 Roslyn extraction engine — no DevExpress reference
  XafLogicExplainer.Cli                  the `xaflogic` command
  XafLogicExplainer.CopilotSync          PeopleWorks Copilot target + AI enrichment
  XafLogicExplainer.DescriptionAnnotator generates missing [Description] attributes
  XafLogicExplainer.Blazor               in-app help panel for XAF Blazor apps
```

Only `XafLogicExplainer.Blazor` references DevExpress packages; it needs the DevExpress NuGet feed
and a license to build. Everything else builds anywhere, which is why CI can verify it for free.

## Contributing

The single most valuable contribution is telling us **what the extractor missed**. XAF is enormous,
every codebase uses a different slice of it, and no single project exercises the whole framework.
There is an [extraction-gap issue template](.github/ISSUE_TEMPLATE/extraction-gap.yml) for exactly
this: show the XAF pattern your project uses and what the tool failed to see.

See [CONTRIBUTING.md](CONTRIBUTING.md). Bug reports, docs and translations are equally welcome.

## License

[MIT](LICENSE).

An independent community project — not affiliated with, endorsed by, or supported by
Developer Express Inc. It contains no DevExpress source code and needs no DevExpress license to
build or run. *DevExpress*, *XAF* and *eXpressApp Framework* are trademarks of Developer Express Inc.

Built by [Pedro Hernández](https://github.com/peopleworks) (PeopleWorks), Microsoft MVP for .NET —
for the DevExpress and XAF community.
