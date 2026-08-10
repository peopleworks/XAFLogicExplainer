# Notice

This project is licensed under the [MIT License](LICENSE). This file records what it does and does
not contain with respect to DevExpress, and is not part of the license terms.

## Relationship to DevExpress

XAFLogicExplainer analyzes source code written against the DevExpress eXpressApp Framework (XAF).
It is an **independent community project**: not affiliated with, endorsed by, or supported by
Developer Express Inc.

Questions about XAF itself belong in the
[DevExpress Support Center](https://supportcenter.devexpress.com/), not in this repository's issues.

## No DevExpress code is included

This repository contains **no DevExpress source code**, and no DevExpress license is required to
build or run it.

Extraction works by parsing the C# and `.xafml` files in *your* project as text, using Roslyn's
syntax analysis. The analyzed project is never compiled, and `XafLogicExplainer.Core` links against
no DevExpress assembly.

The one exception is `src/XafLogicExplainer.Blazor`, the optional in-app help panel for XAF Blazor
applications. It references `DevExpress.ExpressApp.Blazor` and therefore needs the licensed
DevExpress NuGet feed to build. Nothing else in the repository depends on it, and continuous
integration skips it.

## The optional ground-truth catalog

A planned tool (`tools/XafLogicExplainer.DxCatalog`) reads a local DevExpress installation that
*you* already license, and writes a catalog of framework metadata outside this repository so that
extraction can distinguish your own logic from built-in framework behavior.

**No catalog derived from DevExpress sources is distributed here**, and running that tool is
entirely optional — without it, extraction behaves exactly as it does today.

## Trademarks

*DevExpress*, *XAF*, *eXpressApp Framework* and *XPO* are trademarks of Developer Express Inc.
*Claude* is a trademark of Anthropic. *Copilot* is a trademark of Microsoft. They are used here
only to identify the software this project interoperates with.
