# XafLogicExplainer.Mcp — Model Context Protocol server

Lets an AI agent ask questions about **a specific DevExpress XAF application**: its entities,
controllers, actions, business rules, navigation and Model Editor customizations, read straight
from source with Roslyn.

An agent that has read every page of the DevExpress documentation still does not know that your
`Invoice` total is calculated from its lines, that `ApproveController` refuses to run when the
period is closed, or that three columns were hidden in the Model Editor and appear in no C# file
at all. It will confidently invent all three.

**No DevExpress licence is required.** Extraction is Roslyn *syntax* analysis: the project being
analyzed is never compiled, and this package never links against a DevExpress assembly.

## Run it

```json
{
  "mcpServers": {
    "xaf": { "command": "dnx", "args": ["XafLogicExplainer.Mcp", "--yes"] }
  }
}
```

Started from a solution directory it finds the XAF module by itself. To be explicit, pass
`--project <module directory>` or set `XAFLOGIC_PROJECT`.

## Tools

| Tool | Answers |
| --- | --- |
| `xaf_overview` | What this application is, and the complete list of everything in it |
| `xaf_search` | Where a field, concept or business term is defined |
| `xaf_entity` | Every property, relationship, rule and calculation on one entity |
| `xaf_controller` | What an action does — including the C# that runs when it fires |
| `xaf_rules` | What the application validates, computes, hides and disables |
| `xaf_model` | Model Editor customizations, which exist in no C# file |
| `xaf_editors` | Custom property editors, which live in the platform project beside the module |
| `xaf_migrations` | Version-guarded upgrade code that ran once and cannot be re-read from today's source |
| `xaf_view` | Everything loaded onto one screen — most views exist in no file, and neither does this answer |
| `xaf_refresh` | Re-read the source (changes are detected automatically) |

Extractions are cached per project and invalidated when the source changes, so a conversation's
worth of questions costs one parse — but an edit is still noticed.

## Absence is an answer

Ask for something that is not there and the reply is the useful one:

> There is no entity called 'PurchaseOrder' in this application.
> This is the complete list of 19 entities, extracted from the whole source tree: …
> If the user expects 'PurchaseOrder' to exist, it has not been created yet.

A bare "not found" invites an agent to assume it looked in the wrong place and invent the type
anyway. Extraction reads the whole tree, which is what makes the stronger claim true.

## Use it with the official DevExpress skills

They solve different halves of the same problem:

| Teaches the agent… | Tool |
| --- | --- |
| How XAF works in general | [DevExpress agent-skills](https://github.com/DevExpress/agent-skills) |
| What the documentation says | DevExpress Docs MCP server |
| **What YOUR application does** | **this** |

An agent with only the first two writes correct XAF against entities you do not have.

## Also available

- [`XafLogicExplainer.Cli`](https://www.nuget.org/packages/XafLogicExplainer.Cli) — the `xaflogic`
  command. Writes `AGENTS.md`, `CLAUDE.md` and Copilot instructions for agents without MCP, and
  hosts this same server as `xaflogic mcp`.
- [`XafLogicExplainer.Core`](https://www.nuget.org/packages/XafLogicExplainer.Core) — the
  extraction engine on its own.

## Links

- [Repository](https://github.com/peopleworks/XAFLogicExplainer) · [Site](https://peopleworks.github.io/XAFLogicExplainer/)
- The long version, on why so much XAF behaviour lives outside the business classes:
  [English](https://peopleworksgpt.com/your-coding-agent-knows-xaf-it-has-never-seen-your-application/) ·
  [español](https://peopleworks.com.do/2026/08/13/tu-agente-de-codigo-sabe-xaf-nunca-ha-visto-tu-aplicacion/)
- MIT licensed. An independent community project: not affiliated with, endorsed by, or supported
  by Developer Express Inc.

## MCP registry

Listed in the [official MCP registry](https://registry.modelcontextprotocol.io) under the name
below. The registry reads that line out of this README to verify that whoever publishes the
registry entry also owns the NuGet package, so **don't remove or reword it** — publishing would
start failing.

mcp-name: io.github.peopleworks/xaf-logic-explainer
