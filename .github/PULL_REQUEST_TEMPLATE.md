<!--
Thanks for contributing. Small and focused beats large and comprehensive.
-->

## What this changes

<!-- One or two sentences. -->

## Why

<!-- If this handles an XAF pattern the extractor missed, name the pattern and link the issue. -->

Closes #

## The pattern, if this is an extraction change

<!--
Paste the reduced snippet that motivated the change — the one that was previously misread or
skipped. It becomes the test case. No proprietary code: rename the entities.
-->

```csharp

```

## Checklist

- [ ] `dotnet build XAFLogicExplainer.slnx` is clean (CI treats warnings as errors)
- [ ] No DevExpress reference was added to `XafLogicExplainer.Core`
- [ ] Extraction still works on a project that does not compile
- [ ] An unrecognized variation of this pattern is skipped, not thrown on
- [ ] `CHANGELOG.md` updated under `[Unreleased]`, if this is user-visible
