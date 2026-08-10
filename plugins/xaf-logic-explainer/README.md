# xaf-logic-explainer

A Claude Code plugin that lets an agent query *your* DevExpress XAF application.

```
/plugin marketplace add peopleworks/XAFLogicExplainer
/plugin install xaf-logic-explainer@peopleworks-xaf
```

It installs one skill and one MCP server.

## Prerequisite

```bash
dotnet tool install -g XafLogicExplainer.Cli
```

The MCP server runs as `xaflogic mcp`. Started from a solution directory it finds the XAF module on
its own; for an unusual layout or several projects, set a default once:

```bash
xaflogic config --project "C:\MySolution\MyApp.Module"
```

No DevExpress license is required. Analysis reads your source as text using Roslyn and never
compiles the project or links against DevExpress assemblies.

## What the agent gains

| Tool | Answers |
| --- | --- |
| `xaf_overview` | What this application is, and the complete list of everything in it |
| `xaf_search` | Where a field, concept or business term is defined |
| `xaf_entity` | Every property, relationship, rule and calculation on one entity |
| `xaf_controller` | What an action does, including the C# that runs when it fires |
| `xaf_rules` | What the application validates, computes, hides and disables |
| `xaf_model` | Model Editor customizations, which exist in no C# file |
| `xaf_refresh` | Re-read the source, if you think the cached view is stale |

## Use it with the official DevExpress skills

They solve different halves of the same problem, and work best together:

```
/plugin marketplace add DevExpress/agent-skills
/plugin install dx-xaf@DevExpress-agent-skills
```

`dx-xaf` teaches how XAF works. This teaches what your application does. An agent with only the
first will write correct XAF against entities you do not have.

## Not affiliated with DevExpress

An independent community project. See
[the repository](https://github.com/peopleworks/XAFLogicExplainer) and its `NOTICE.md`.
