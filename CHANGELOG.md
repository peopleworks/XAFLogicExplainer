# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned

- `AGENTS.md` / `CLAUDE.md` / `copilot-instructions.md` output — no infrastructure required
- MCP server, so any agent can query an XAF codebase live
- Agent skill installable from this repository, alongside DevExpress's own `dx-xaf` plugin
- `IDocumentationSink` abstraction, making PeopleWorks Copilot one target among several
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

### Notes

- Versioned 0.9.0 rather than 1.0.0 on purpose: the extraction engine is mature, but the
  agent-facing surface is still landing. 1.0.0 follows the MCP server.
- `XafLogicExplainer.Core` references no DevExpress assemblies and needs no DevExpress license.
  Only the Blazor widget does.

[Unreleased]: https://github.com/peopleworks/XAFLogicExplainer/compare/v0.9.0...HEAD
[0.9.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.9.0
