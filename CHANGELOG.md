# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **`xaflogic agents`** — writes `AGENTS.md`, `CLAUDE.md` and `.github/copilot-instructions.md`
  so any AI coding agent understands the analyzed application. No account, key or server.
  - Output is **tiered**: a compact index (~11 KB) that agents load on every request, and detail
    files in `.xaflogic/` (~70 KB) they open only when a question needs them. An `AGENTS.md` is
    prepended to every conversation, so putting the full documentation there would consume the
    context the user's actual question needs.
  - The index leads with **ground rules**: which ORM this application uses and which APIs
    therefore do not exist in it, that the inventories are complete so an absent entity is
    genuinely absent, and that some behavior lives in the Model Editor rather than in C#.
  - **Conventions are inferred from the codebase** — namespaces, folder layout, base classes, how
    associations and validation are written — so generated code matches the surrounding style
    instead of a generic tutorial.
  - Includes **real criteria expressions** taken from the source. XAF's criteria language is
    neither SQL nor C#, and worked examples teach the dialect better than a description of it.
  - Generated text is written between markers: anything you wrote by hand is preserved, and
    regenerating produces byte-identical output when nothing has changed.
- `IDocumentationSink`, making a publishing target something the caller chooses. PeopleWorks
  Copilot is now one implementation of it rather than the destination the tool is built around.
- **`xaflogic mcp`** — a Model Context Protocol server (ModelContextProtocol 2.1.0) exposing seven
  tools: `xaf_overview`, `xaf_search`, `xaf_entity`, `xaf_controller`, `xaf_rules`, `xaf_model`
  and `xaf_refresh`. Unlike generated files it reads live source, so it cannot go stale.
  - Asking for something absent returns the complete inventory and states plainly that it does not
    exist, rather than a bare "not found" that invites an agent to invent it anyway.
  - Extractions are cached per project and invalidated by a cheap size-and-timestamp fingerprint,
    so a conversation's worth of questions costs one parse but an edit is still noticed.
  - Finds the XAF module by itself when started from a solution directory, which is what lets the
    plugin declare `xaflogic mcp` with no arguments.
- **Installable Claude Code plugin** at `plugins/xaf-logic-explainer`, carrying the skill and the
  MCP server: `/plugin marketplace add peopleworks/XAFLogicExplainer`.

### Changed

- Target framework is now **.NET 10**.

### Fixed

- `nameof(...)` in attribute arguments was recorded as the literal text `nameof(Numero)` rather
  than `Numero`. Attribute values were read with `expression.ToString().Trim('"')`, which is right
  for a string literal and wrong for everything else — and `[XafDefaultProperty(nameof(X))]` is
  how current C# is written. The bad value reached generated documentation and MCP responses as
  though it were a real property name. A shared `SyntaxLiteral` reader now resolves string
  literals, `nameof`, and concatenated strings across all three analyzers.

### Fixed

- `ModuleAnalyzer` reported business entities as required XAF modules. It accepted any invocation
  whose expression contained `Add`, so `AdditionalExportedTypes.Add(typeof(Cliente))` was read as
  a module dependency. On a real project this listed nine entities and six framework base types
  among twelve genuine modules. It now matches the target collection, and
  `AdditionalExportedTypes` feeds the registered-types list where it belongs.

### Planned

- MCP server, so any agent can query an XAF codebase live
- Agent skill installable from this repository, alongside DevExpress's own `dx-xaf` plugin
- AI provider abstraction (OpenAI, Azure OpenAI, Anthropic, Ollama) for `--enrich`
- xUnit test suite over a synthetic XAF fixture that needs no DevExpress reference
- Optional DevExpress ground-truth catalog, generated locally by licensees

## [0.9.0] — 2026-08-10

First public release. The extraction engine has been running in production against real XAF
applications; this is the point where it becomes a community project.

### Added

- Roslyn-based extraction of entities, properties, associations and XAF attributes
- Controller and action extraction, including target criteria and handler code
- Business rule extraction from validation attributes and code rules
- `ModuleUpdater` seed data and module configuration extraction
- Navigation group and item extraction
- Model Editor (`.xafml`) extraction, merging module and platform files the way XAF merges them
- XPO and EF Core support, auto-detected from `using` statements, overridable with `--orm`
- Incremental extraction via a SHA-256 hash over `.cs` and `.xafml` sources
- `diff` command reporting what changed between extractions
- `watch` command with debounced re-extraction
- Multi-project configuration and `--all` batch processing
- `--enrich`, generating AI business-logic summaries per controller and per action
- Bilingual output (English and Spanish)
- PeopleWorks Copilot publishing target
- `DescriptionAnnotator`, which writes missing `[Description]` attributes back into source
- Blazor in-app help panel for XAF Blazor applications
- MSBuild `.targets` for extraction on build

### Fixed

- The packaged MSBuild integration never worked. Three problems, none of which could surface
  before the package was installed from a feed: the file was named `XafLogicExplainer.targets`
  when NuGet only auto-imports `build/<PackageId>.targets`; it resolved the CLI through a path
  into this repository's own `bin/` directory, which cannot exist on a consumer's machine; and
  it passed a `--config` flag the CLI has never had. It now invokes the installed `xaflogic`
  tool, and CI verifies packaging on every push.
- The MSBuild integration defaulted to `sync`, uploading documentation on every Release build.
  It now defaults to `extract`, which makes no network call, and does nothing at all unless
  `XafLogicExplainerRunOnBuild` is set. Publishing from a build step should be a decision.
- `DescriptionAnnotator` defaulted its resource name to a specific client project, so an
  unconfigured run targeted someone else's resource. The default is removed.

### Notes

- Versioned 0.9.0 rather than 1.0.0 on purpose: the extraction engine is mature, but the
  agent-facing surface is still landing. 1.0.0 follows the MCP server.
- `XafLogicExplainer.Core` references no DevExpress assemblies and needs no DevExpress license.
  Only the Blazor widget does.

[Unreleased]: https://github.com/peopleworks/XAFLogicExplainer/compare/v0.9.0...HEAD
[0.9.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.9.0
