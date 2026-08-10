# XAF Logic Explainer - Codebase Documentation

## Purpose
XAF Logic Explainer extracts functional metadata from XAF source projects and generates structured documentation (Markdown/JSON), so that AI coding agents can answer questions about a *specific* XAF application rather than about XAF in general. Publishing that documentation to a remote target is optional; today the one implemented target is PeopleWorks Copilot.

Extraction is Roslyn **syntax** analysis. The analyzed project is never compiled, and `XafLogicExplainer.Core` references no DevExpress assemblies — which is what makes the engine buildable and testable without a DevExpress license.

## Solution Structure
- `src/XafLogicExplainer.Core`
  - Extraction and analysis engine.
  - Produces normalized domain models (`ExtractedProject`, entities, controllers, seed data, model editor metadata).
  - Includes diff engine and markdown generators.
- `src/XafLogicExplainer.CopilotSync`
  - Upload/synchronization layer for PeopleWorks Copilot APIs.
  - Wraps API DTOs, HTTP client logic, and incremental sync orchestration.
- `src/XafLogicExplainer.Cli`
  - End-user command line app (`xaflogic`) for configuration, extraction, sync, watch, and diff operations.
- `src/XafLogicExplainer.Blazor`
  - Optional UI integration module that adds a floating Copilot panel to XAF Blazor applications.
- `src/XafLogicExplainer.DescriptionAnnotator`
  - Separate utility for scanning missing `[Description]` attributes and generating/applying AI suggestions.

Only `XafLogicExplainer.Blazor` references DevExpress packages, so it is the one project that needs the licensed DevExpress NuGet feed. CI builds everything else.

## Core Extraction Flow
1. `LogicExtractor.ExtractFromSourceDirectory` starts orchestration.
2. `ProjectHashCalculator` computes project fingerprint.
3. `EntityAnalyzer` extracts business objects, properties, relationships, and validation/appearance rules.
4. `ControllerAnalyzer` extracts controllers, actions, execute handlers, and referenced entities.
5. `UpdaterAnalyzer` extracts seed data methods and records.
6. `ModuleAnalyzer` extracts registered types and required modules.
7. `ModelAnalyzer` parses and merges `xafml` model metadata.
8. `MarkdownDocumentationGenerator` converts extracted data into sectioned markdown and JSON.

## Diff Flow
- `ProjectDiffEngine` compares previous and current `ExtractedProject` snapshots.
- `DiffMarkdownGenerator` emits human-readable change reports with localized labels (`en`/`es`).

## Synchronization Flow
1. `IncrementalSyncService` checks if sources changed using saved hash.
2. If changed (or forced), it runs extraction and generation.
3. `DocumentationUploader` uploads section documents + a full combined document.
4. On success, the new hash is persisted for future incremental runs.

## Description Annotator Flow
1. `DescriptionScanner` identifies classes/properties missing `[Description]`.
2. `AiDescriptionGenerator` builds prompts and requests suggestions in batch.
3. `SourceFileModifier` applies attributes through Roslyn rewrite.
4. A markdown report is generated for audit/review.

## Documented Critical `HACK:` Spots
The following `HACK:` markers exist intentionally to highlight non-ideal but currently necessary behavior:
- `src/XafLogicExplainer.Core/Analyzers/LogicExtractor.cs`
  - `csproj` metadata extraction uses regex over raw XML text.
- `src/XafLogicExplainer.Core/Analyzers/EntityAnalyzer.cs`
  - File exclusion logic keeps legacy substring behavior (`obj/bin`) instead of full pattern evaluation.
- `src/XafLogicExplainer.Core/Analyzers/ControllerAnalyzer.cs`
  - Action detection uses broad `"Action"` fallback to maximize recall in heterogeneous codebases.
- `src/XafLogicExplainer.Core/Hashing/ProjectHashCalculator.cs`
  - Sibling directory IO errors are swallowed to keep hash computation resilient.

## Suggested Future Refactors
- Replace regex csproj parsing with `XDocument`.
- Replace coarse file filtering with real glob matcher for include/exclude patterns.
- Introduce stronger controller-action type identification (symbol analysis instead of string heuristics).
- Add structured diagnostics channel for non-fatal IO exceptions during hash and model discovery.

