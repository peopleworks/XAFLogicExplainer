---
name: xaf-application-knowledge
description: >-
  Answer questions about the DevExpress XAF application in this repository using facts extracted
  from its own source code, rather than general XAF knowledge. Provides complete inventories of
  business entities, controllers and actions; every property with its type, attributes and
  relationships; validation and conditional-appearance rules; calculated property expressions;
  criteria strings as this codebase writes them; module registration and seed data; navigation
  structure; and Model Editor (.xafml) customizations that exist in no C# file. Use whenever
  someone asks what an entity contains, what a button or action does, what the system validates or
  computes, how a screen is configured, where something is defined, or how to add an entity,
  action or rule that fits this codebase. Also use before writing any code against this
  application, to confirm what actually exists.
compatibility: >-
  Requires the xaflogic CLI (dotnet tool install -g XafLogicExplainer.Cli) and a DevExpress XAF
  project. Works with XPO and EF Core. No DevExpress license needed; the analysis reads source as
  text and never compiles the target project.
metadata:
  author: PeopleWorks
  version: "0.9.0"
  repository: https://github.com/peopleworks/XAFLogicExplainer
---

# This XAF application

You have MCP tools that read *this* application's source code. Use them instead of guessing, and
instead of answering from general XAF knowledge.

## What these tools are for, and what they are not

There are three different kinds of knowledge involved in working on an XAF codebase, and confusing
them is what produces confident wrong answers:

| Question | Where the answer is |
| --- | --- |
| "How do XAF controllers work?" | The [DevExpress agent skills](https://github.com/DevExpress/agent-skills) (`dx-xaf`) |
| "What does the documentation say about `PopupWindowShowAction`?" | The DevExpress documentation MCP server |
| **"What does `ApproveInvoiceController` do in this app?"** | **These tools** |

These tools know nothing about XAF in general. They know everything about the application in front
of you. Install `dx-xaf` alongside this for the framework half.

## Start here

Call **`xaf_overview`** before answering anything about the application as a whole. It returns what
the application is, plus the complete list of entities, controllers, actions and navigation groups.
One call usually establishes enough to answer, or to know which detail tool to call next.

## The tools

| Tool | Use it when |
| --- | --- |
| `xaf_overview` | Any question about the application as a whole; always a safe first call |
| `xaf_search` | You know roughly what you are looking for but not where it lives |
| `xaf_entity` | Before writing or changing code that touches an entity |
| `xaf_controller` | Asked what a button, command or action actually does — returns the real handler code |
| `xaf_rules` | Asked what the system requires, forbids or computes |
| `xaf_model` | Asked how a screen or list is configured |
| `xaf_refresh` | Only if you believe the cached view is stale; changes are detected automatically |

## The inventories are complete

This is the most important thing to understand about these tools, and the easiest to misuse.

Extraction reads the **whole source tree**. It does not sample, and it does not stop early. So when
`xaf_overview` lists nineteen entities, that is every entity the application has, and when
`xaf_entity` reports that something does not exist, it genuinely does not exist.

**Do not** treat a negative result as "I must have looked in the wrong place" and then write code
against an invented type. Say plainly that it does not exist, and offer to create it. An agent that
invents a plausible-sounding entity is the specific failure these tools exist to prevent.

## Rules that hold for every answer

**Match the ORM.** `xaf_overview` reports whether this application uses XPO or EF Core. They are
both legitimate XAF and their base classes share names — `BaseObject` exists in both, in different
namespaces — so mixing them produces code that looks right and does not compile. If the application
uses XPO, never suggest `DbContext`, `DbSet<T>`, `OnModelCreating` or EF migrations. If it uses EF
Core, never suggest `Session`, `XPCollection<T>` or `UnitOfWork`.

**Criteria strings are not SQL and not C#.** XAF filters, validates and styles with its own
expression dialect. `xaf_entity` and `xaf_rules` return the real expressions this codebase uses;
follow their form rather than inventing a syntax.

**Some behavior exists only in XML.** Captions, list columns, filters and view settings can be set
in the Model Editor, and those override what the C# implies. If a question is about how a screen
behaves, check `xaf_model` before concluding anything from the business classes alone.

**Prefer the pattern already in the codebase.** XAF usually offers several ways to express one
idea. `xaf_entity` shows whether this team names associations explicitly, whether calculated
properties use `PersistentAlias`, and whether validation is attributes or controller code. Match
what is there; the goal is a change that survives review, not one that merely compiles.

## Setup, if the tools are not responding

The MCP server runs through the `xaflogic` CLI:

```bash
dotnet tool install -g XafLogicExplainer.Cli
xaflogic config --project "C:\MySolution\MyApp.Module"
```

The server finds the module automatically when started from a solution directory, so the `config`
step is only needed for an unusual layout or several projects at once.

## When MCP is unavailable

The same knowledge can be written to files instead:

```bash
xaflogic agents --project "C:\MySolution\MyApp.Module"
```

That produces `AGENTS.md` with the ground rules and inventories, plus detail files in `.xaflogic/`.
Useful for agents without MCP support, and for committing the context so a team shares it. The MCP
tools are better where available: they read live source and cannot go stale.
