# Contributing

Thanks for being here. This project exists because XAF codebases are big, idiosyncratic, and no
single one exercises the whole framework — which means **the most valuable thing you can give this
project is a pattern it failed to understand.**

## The highest-value contribution: an extraction gap

XAF has dozens of ways to express the same idea. A property can be required through
`[RuleRequiredField]`, through a `RuleRequiredFieldAttribute` on the class, through
`ImmediatePostData` plus a code rule, or through Model Editor XML that never appears in C# at all.
The extractor knows some of those. It cannot know all of them without you.

If `xaflogic extract` missed something in your project, open an
[extraction gap issue](../../issues/new?template=extraction-gap.yml) with:

1. **A minimal snippet** of the XAF pattern — reduced to something you can publish
2. **What the tool produced** (or omitted)
3. **What it should have produced**

You do not need to write the fix. A well-reduced snippet is most of the work, and it becomes a test
case. Please don't paste proprietary business code: rename `Comision` to `Invoice` and it's still a
perfectly good bug report.

## Development setup

```bash
git clone https://github.com/peopleworks/XAFLogicExplainer
cd XAFLogicExplainer
dotnet build XAFLogicExplainer.slnx
```

**You do not need a DevExpress license to work on this project.** Extraction is Roslyn *syntax*
analysis: the code being analyzed is parsed as text and never compiled, and `XafLogicExplainer.Core`
references no DevExpress assemblies.

The one exception is `src/XafLogicExplainer.Blazor` (the in-app help panel), which references
`DevExpress.ExpressApp.Blazor` and therefore needs the DevExpress NuGet feed. Nothing else depends
on it, and CI skips it. If your restore fails only there, that's why — build the other projects and
carry on.

### Running the tests

```bash
dotnet test tests/XafLogicExplainer.Tests
```

107 tests, under a second, **no DevExpress required**. They run against synthetic XAF applications
in `tests/XafLogicExplainer.Tests/Fixtures/` — one XPO, one EF Core — which are XAF source the
suite never compiles, only parses.

That makes the fixtures the natural home for a bug report. If the extractor misreads a pattern,
adding it to a fixture and asserting the expected result is usually the entire fix, and the
regression can never come back quietly.

### Trying a change

```bash
dotnet run --project src/XafLogicExplainer.Cli -- extract --project "path/to/YourApp.Module"
```

Output lands in `.xaflogic-output/` inside the analyzed project.

## Where things live

| Path | What it is |
| --- | --- |
| `src/XafLogicExplainer.Core/Analyzers/` | The Roslyn analyzers. Most extraction fixes belong here. |
| `src/XafLogicExplainer.Core/Models/` | The extracted shape. Adding a concept usually starts here. |
| `src/XafLogicExplainer.Core/Generators/` | Turns the model into Markdown/JSON sections. |
| `src/XafLogicExplainer.Cli/` | The `xaflogic` command surface. |
| `src/XafLogicExplainer.CopilotSync/` | A publishing target and the AI enrichment pipeline. |

Adding support for a new XAF pattern is usually: recognize it in an analyzer → represent it in a
model → render it in the generator.

## House rules for code

- **No DevExpress reference creeps into `Core`.** It is what keeps this buildable and testable
  everywhere. If you need to know something about the framework, it belongs in the optional
  ground-truth catalog, not in a package reference.
- **Extraction must never require the target project to compile.** People run this on branches that
  are mid-refactor; that's often exactly when they need it.
- **A missed pattern should degrade, not crash.** Unknown attributes and unfamiliar syntax get
  skipped, not thrown on.
- Comments explain *why*, not *what*. The code already says what.
- Keep the build at zero warnings — CI treats them as errors.

## Pull requests

Small and focused beats large and comprehensive. Describe what XAF pattern the change handles and,
if you can, include the reduced snippet that motivated it so it can become a test.

By contributing you agree your work is licensed under the [MIT License](LICENSE).

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md).
