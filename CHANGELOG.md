# Changelog

All notable changes to this project are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.15.0] — 2026-08-23

How it works, not only what exists.

Every release until now answered the same shape of question. What entities does this application
have. What does this controller do. What does that rule forbid. All of it true, all of it
declarations — and a business process is not a declaration. It is a path across several of them, and
nothing in the extractor walked a path.

`xaflogic walkthrough --from ApproveOrder` walks it. The scope is **computed**, by a bounded
breadth-first traversal, and not chosen by a language model — because a model asked what belongs in
"the approval process" answers authoritatively, in a form nobody can check, and is wrong in the
places that look exactly like the places it is right. The Mermaid diagram is emitted from that
traversal arrow for arrow, for the same reason and more sharply: a diagram is believed at a glance,
so an invented edge in one is worth less than no diagram at all.

The two halves that make it honest are the ones that took the most care. **A call the walk cannot
follow is printed rather than dropped** — a virtual method is followed to the declaration written
beside the call and reported with every override that may replace it, and the document says outright
that the bodies it never entered mean entities missing from the account. And **`--since` reports what
changed in that one process** against a stored snapshot, which is the question no conversational
agent can answer, because none of them has a yesterday.

`--narrate` is opt-in and, deliberately, the least load-bearing thing here. The model receives the
numbered steps and the code behind them; the only prose that reaches a reader is a paragraph it
managed to key to a step that exists. A document generated with an empty narration is
byte-for-byte the one generated with none.

Also in this release: every declaration now says where it is, `file:line`, in the extraction and in
the MCP tools — the foundation the walkthrough needed and worth having on its own, since the tools
used to hand an agent a name and leave it to search for it. Four appearance-rule defects, two of
them found by [@MBrekhof]. Any model can now answer, and a key is enough.

And the Markdown we generate is Markdown, which took two goes. A seed method's source was wrapped in
a `<details>` fold that collapses on GitHub and nowhere else ([#28]). Then, running the output
through a real Word converter, [@MBrekhof] found the second half of the same defect: a generic base
type was written bare, and `<DetailView>` is an *inline* HTML tag rather than a block, so an export
drops it and github.com's sanitizer strips it — every controller read as deriving from plain
`ViewController` ([#36], fixed in [#39]). Type names are written as code now, and the guard was
widened from lines that *open* with `<` to a tag anywhere on a line. The route from an extraction to
a Word document is written down in the README, and there is deliberately no exporter here: reading
an XAF application needs no DevExpress, and that stays true.

402 tests, zero warnings.

### Added

- **Entities, controllers, actions and methods say where they are declared** ([#23], first step).
  The extraction knew which *file* a class was in and nothing narrower, so `xaf_entity` and
  `xaf_controller` handed an agent a name and left it to search the file for it. Each now carries a
  one-based line, taken from the **identifier token** rather than from the declaration's span — a
  span begins at the first attribute, and `Customer` sits behind a doc comment and four of them, so
  the two answers are four lines apart and only one of them is the line anybody means. Actions and
  methods carry their own file as well as their own line, because a partial controller's members
  need not be declared in the file the controller is cited at. The MCP tools name the file once and
  then cite members by line alone; a member in a *different* file is given in full, which is exactly
  the case where a reader would otherwise open the wrong one. Prerequisite for the walkthrough,
  where every claim is supposed to carry a `file:line` a reader can check.

- **The walk that decides what belongs to one process** ([#23], phase 1). `ProcessSlice.From` takes
  an action, a controller method, a controller or an entity by name and walks outward from it —
  breadth-first, bounded by depth, syntax-only like everything else here. It follows an action to
  the handler it runs, a method to the methods it calls and the entities it names, a controller to
  the entities it is activated for, and an entity to the rules that govern it. Every node carries
  the file and line a reader can open.
  The scope is computed rather than asked for, and that is the governing decision: a model asked
  what belongs in "the approval process" answers authoritatively, in a form nobody can check, and is
  wrong in the places that look exactly like the places it is right.
  **A call it cannot follow is printed, not dropped.** A virtual declaration is followed to what is
  written beside the call, and reported with every override that may replace it — which is honest
  about the consequence, because the bodies that were not entered mean entities missing from the
  slice. So is the depth bound: a walk that ran out of things to reach is a whole process, and a
  walk that stopped at its limit is a view of one, and rendering them identically is how a document
  claims completeness it does not have.
  Methods now record `virtual`/`abstract` and `override`, which is what makes that distinction
  possible at all. Nothing consumes the slice yet — the Mermaid diagram, the document, the CLI
  command and the MCP tool are phase 2.

- **`xaflogic walkthrough` and `xaf_walkthrough`** ([#23], phase 2). The slice becomes a document,
  and the feature is now usable: a Mermaid diagram, everything that takes part with the place it is
  declared, the ordered steps each citing a `file:line`, the calls the walk could not follow, and
  what the walk deliberately is not. In both languages, offline, with no API key and no network.
  **The diagram is emitted from the walk's own edge set — node for node, arrow for arrow.** Nothing
  decides what to draw. Ask a model for a Mermaid diagram of a process and it will produce one,
  including edges that do not exist, drawn with a confidence indistinguishable from the true ones,
  in a format whose whole value is that a reader believes it at a glance. A test counts the arrows
  against the edges, because an invented one is exactly what a spot check of a diagram that looks
  right would miss.
  `xaf_walkthrough` is the first MCP tool that answers a question about a *process*. The other ten
  return atoms, so an agent asked how something works has to guess which atoms to fetch and then
  guess whether it has them all — and the guess that stops one atom early produces a confident
  answer with a step missing from it.

- **`xaflogic walkthrough --narrate`** ([#23], phase 3). Opt-in prose over a walk that has already
  been computed: one paragraph on what the process is for, and a sentence or two under each step,
  each sitting directly beneath the citation it belongs to.
  **The model narrates; it does not discover.** It receives the numbered steps and the code behind
  them, and the only thing that reaches a reader is a paragraph it managed to key to a step that
  exists — a paragraph keyed to step 99 of a nine-step process is dropped before rendering, and so
  is fluent prose attached to no step at all. The point is not that such a sentence would probably
  be wrong; it is that nobody could check it, and an ordinary reader cannot tell a fluent sentence
  about real code from a fluent sentence about code that is not there.
  The model is also told what the walk could not follow, so it does not narrate its way over the one
  gap the analysis already knows about.
  Failure costs prose and not the document: no key, or a provider that does not answer, prints why
  and writes the walkthrough anyway. Phases 1 and 2 stand entirely on their own, which is what makes
  the model optional rather than load-bearing — and a test pins that a document generated with an
  empty narration is byte-for-byte the one generated with none.
  `XafLogicExplainer.Core` still references nothing but Roslyn: narration arrives at the generator
  as plain text keyed to steps that already exist.

- **`xaflogic walkthrough --since`** ([#23], phase 4 — the last one). Re-walks the same process over
  a stored snapshot and reports what is different about **this** process: a step added, a rule now
  governing it, a branch gone, a body rewritten, and a call the trace can no longer follow. Reads
  the same `_Previous.json` that `xaflogic diff` does, or any snapshot given by path.
  This is the part no conversational agent can imitate, because none of them has a yesterday. Asked
  what changed in the commission calculation since the last release, a model can only re-read
  today's code and describe it fluently.
  Comparing two walks by their node sets alone would have missed the most ordinary change there is —
  somebody edits a method body and leaves every call in it alone — so each node now carries a
  fingerprint of its own substance: a method's body, a rule's condition and effect, an action's
  caption and criteria. Whitespace is collapsed first, so reformatting is not reported as a change
  of behaviour. A controller and an entity have no fingerprint; what matters about them is elsewhere
  in the slice, and giving them one would report the same edit twice.
  Three states that had to stay distinct: the process is unchanged, the process did not exist at the
  snapshot, and no snapshot could be read — the last one stops the run rather than writing a
  document with the section missing, because an absent section reads exactly like "nothing changed".

### Fixed

- **An appearance rule keeps its condition however the attribute was written** ([#22], reported by
  [@MBrekhof]). `AppearanceAttribute` has three constructors and two pass the criteria by position —
  `(id, criteria)` and `(id, appearanceItemType, criteria)`. Only the named form was read, so a rule
  written either of the other two ways was extracted with no condition and then documented as
  applying **always**, which is the strongest claim a rule can make, printed about rules whose whole
  purpose is a condition. The criteria is the last positional argument in both overloads that carry
  one, so both are read without knowing which constructor was called; a named argument still wins.
  The expression index — the page that introduces itself as every distinct expression in the
  application — heals on its own, because it already gathered appearance criteria and there were
  none to gather.

- **A rule over an Action is no longer called a field** ([#22]). `AppearanceItemType` was never read,
  so a rule disabling the `Delete` action was documented as governing a column called Delete — a
  confident sentence about something that does not exist. It is written two ways in real code:
  positionally as the enum, and by name as a string, which is the form the DevExpress examples use;
  both are read and normalised to one value. Absent means the XAF default, `ViewItem`, and is
  recorded as absent so a rule that said nothing can be told from one that said `ViewItem`. The
  Markdown now says *actions* or *layout items* where it applies, the MCP tool says
  `Delete (actions)`, and both diffs treat the item type as part of a rule's identity — a rule
  repointed from a column to an action of the same name changes nothing else.

- New fixture, `AppearanceSolution`, whose rules are written every way the attribute allows. Every
  existing fixture wrote `Criteria =` named, which is the whole reason the suite agreed. 402 tests.

- **An appearance rule written on a property is read** ([#21], thanks [@MBrekhof]).
  `AppearanceAttribute` is usable on a class or on a property, and the documentation teaches the
  property form first: a rule on `UnitPrice` and a rule on the class naming
  `TargetItems = "UnitPrice"` are two spellings of one rule. Only the class spelling was read, so
  the other produced nothing at all — and an entity's section is presented as its complete
  inventory, so a rule governing a property and reported nowhere left the reader concluding the
  property was unconditionally editable. A property rule that names no `TargetItems` of its own now
  records the property it was written on, which is what the class spelling states outright; an
  explicit `TargetItems` is left alone. Measured on a 196-entity application: 25 rules to 33.

- **Two unnamed appearance rules stay two rules through the fold** ([#21], thanks [@MBrekhof]).
  The fold keyed them on `Id` alone, so an empty identifier made them one rule and the second was
  dropped in silence. An empty identifier is ordinary rather than an omission — a rule written on a
  property already says what it governs.

- **An appearance rule with no name, or no criteria, is rendered as what it is.** The Markdown and
  the MCP tool printed `- **** — when ``:` — an empty bold span where the identifier goes, and a
  condition that reads as though it failed to load. In XAF a rule that declares no criteria is
  permanently active, which is the stronger of the two claims; the HTML explainer had already
  settled on `always` and the other two never received it. Reachable before rules were read off
  properties — one unnamed rule per class is enough — and made ordinary by reading them.

- **The diff reports appearance rules that were added, removed, edited or repurposed.** It keyed
  them on `Id` alone and collected them into a set, which failed three ways from the one key: every
  unnamed rule in an application collapsed into a single entry, so adding or removing one reported
  no change; a rewritten criteria kept its identifier, so the edit reported nothing; and a rule
  changed from disabling a field to hiding it reported nothing either, which is the whole of what
  an appearance rule does. Probed at the time: an application went from two declared rules to
  three and the diff reported zero changes.

### Changed

- **Any model will do, and a key is enough** ([#24]). Every AI feature reached PeopleWorks Copilot
  for its credentials — not for a key the user had configured, but for the *model's* key, fetched
  from an account. On a public MIT project that meant no outside user could run any of them:
  `--enrich` refused without an API URL and token and told the reader to configure credentials for
  a service they had never heard of, and the Description Annotator asked for `COPILOT_API_TOKEN`.
  There is now one resolver shared by both, taking the first route that is configured: `--api-key`
  on the command line, then `OPENAI_API_KEY` or `ANTHROPIC_API_KEY` in the environment, then a
  PeopleWorks Copilot account — which still works untouched, as one option among several rather
  than the gate. `--ai-base-url` reaches any OpenAI-compatible endpoint, including a local one, and
  `--ai-model` names the model. Someone with none of them configured is now told all four.
  A key is never read from or written to the configuration file: the endpoint and the model name
  are settings, a key is a secret, and that file lives in a home directory that gets copied around.

- **The Markdown we generate is Markdown** ([#28]). A seed method's source was wrapped in a
  `<details>` fold. That collapses on GitHub and nowhere else: in a Word or PDF export, in a plain
  Markdown viewer, and to a model reading the file, the wrapper is literal text and the fold's
  label — "Source code of PopulateStatuses" — stops being a label and becomes a line of markup. It
  is now a heading, which survives the trip and takes its place in the document outline. The fold
  is lost on GitHub; these files are read far more often than they are scrolled. Found by walking
  the output against the Markdig converter in [mcpOffice], whose documented behaviour for an HTML
  block is to emit it as plain text — every other construct we write already maps to a real Word
  equivalent, so this one call site was the whole distance between an extraction and a document
  somebody can hand over.

### Internal

- First tests over `ProjectDiffEngine`, which is why the key above survived.

- Every sample project's Markdown is now checked, in both languages, for a line that opens raw HTML
  outside a code fence.

- Citations are checked by reading the fixture back off disk: the cited line must really contain the
  declaration, across every sample project. It is the only assertion that catches an off-by-one or a
  span that starts at an attribute.

- New fixture, `WalkthroughSolution`, whose proportions are its point: one action, one handler, and a
  `Recalculate` that two controllers override — so a walk reporting only what it can resolve
  produces a confident, complete-looking account of a process whose body it never saw. No existing
  fixture could reach that case. 391 tests.

[#23]: https://github.com/peopleworks/XAFLogicExplainer/issues/23
[#24]: https://github.com/peopleworks/XAFLogicExplainer/issues/24
[#22]: https://github.com/peopleworks/XAFLogicExplainer/issues/22
[#28]: https://github.com/peopleworks/XAFLogicExplainer/issues/28
[#36]: https://github.com/peopleworks/XAFLogicExplainer/pull/36
[#39]: https://github.com/peopleworks/XAFLogicExplainer/pull/39
[mcpOffice]: https://github.com/MBrekhof/mcpOffice

[#21]: https://github.com/peopleworks/XAFLogicExplainer/pull/21
[@MBrekhof]: https://github.com/MBrekhof

## [0.14.0] — 2026-08-16

Everything that governs an entity, under the entity.

0.13.0 gave each entity the columns it persists and stopped one door short. What is written on a
property travelled with the property — a folded `Number` row correctly said required — and what is
written on the class did not. So an entity's section could say a column was required and, three
headings later, document no rule requiring it: the two halves of the same page disagreeing, with
the property half telling the truth.

The rules were never missing from the application, only from the place a reader looks. A
`RuleCriteria` on an audit base is enforced every time anything in the application is saved. An
`[Appearance]` greys a field on every screen below it. An association gives every descendant a
collection that really is populated. All three were documented under the base alone, which on a
real application means documented nowhere anybody reads.

The other half of [#14] was filed as genuinely debatable, and it turned out to be a question about
scale rather than about relationships: an index, a count, a diagram and a search are answering
*what does this application declare*, while an entity's section is answering *what governs this
entity*. Following the fold everywhere would have made every total a measurement of the class
hierarchy — one rule on a base shared by two hundred entities reported two hundred times. So each
folded declaration carries the class that wrote it, and each rendering chooses.

A minor rather than a patch: `ExtractedValidationRule` gains `Id` and `Contexts`, all three
declaration types gain `InheritedFrom` and `Clone()`. Additive, and invisible to the CLI and the
MCP server.

### Fixed

- **A rule a class inherits is now listed under the class that inherits it** ([#14]). Folding
  carried what lives on a property — the folded `Number` row correctly said required — and left
  everything recorded on the class behind. A `RuleCriteria` on an audit base is enforced every
  time any entity in the application is saved, an `[Appearance]` greys a field on every screen
  below it, and an association gives every descendant a collection that really is populated; all
  three appeared under the base alone. A reader told the inventories were complete read an
  entity's section and was told of no rule. Each folded declaration now names the class that wrote
  it, in the entity's properties as well, where it had been recorded since 0.13.0 and shown
  nowhere.

- **A validation rule's positional arguments are read into the fields they name.** The four-
  argument form — `[RuleCriteria("id", DefaultContexts.Save, "Total >= 0", "A sale total cannot be
  negative.")]` — put the message in the field that holds what the rule enforces, and left the
  message field empty. Every fixture in the suite passed its message as `CustomMessageTemplate =`,
  so 299 tests agreed with the wrong answer. A rule now also carries its identifier and its
  validation contexts, which were read as `arg0` and `arg1` and printed to the published
  documentation that way.

### Changed

- **Counts, indexes, diagrams and searches report what the application declares**, while an
  entity's own section reports everything that governs it. One rule on a base shared by two
  hundred entities is one rule; following the fold everywhere would have made every total, map and
  search result a measurement of the class hierarchy instead. This is the half of [#14] filed as
  debatable, and the answer is that the two readings are answering different questions.

### Internal

- **The workflows run current actions.** `actions/checkout` v4 → v7, `actions/setup-dotnet` v4 →
  v6, `github/codeql-action` v3 → v4. Every run had been annotating that the first two target
  Node 20 and were being *forced* onto Node 24; the third carries a deprecation dated December
  2026. The jobs that would have broken first are `nuget.yml` and `mcp-registry.yml`, which
  nothing exercises until a release is being published.

- **`docs/RELEASING.md` records how a publish is verified.** Install with `--tool-path`, so
  verifying cannot leave you on a version you did not choose to run, and read the nuget.org index
  twice: it is cached per CDN edge, and during the 0.13.0 verification two requests seconds apart
  returned `0.12.1` and `0.13.0` for the same package.

[#14]: https://github.com/peopleworks/XAFLogicExplainer/issues/14

## [0.13.0] — 2026-08-14

The entities an application actually has, and all of the columns they actually persist.

Every fix here came from outside. [@MBrekhof](https://github.com/MBrekhof) read the code before
filing, separated the reports by *cause* rather than by symptom, and kept finding the next one in
the review of the last — three issues and five pull requests, each of which turned out to be a
different way of asking the same question: what is this tool entitled to call an entity, and what
is it entitled to leave out.

The number that measures it, against the demos DevExpress ships with 26.1 rather than against our
own fixtures: `FeatureCenter.NET.XPO` **43 → 140** entities, `MainDemo.NET.XPO` 14 → 17,
`OutlookInspiredDemo.NET.EFCore` 23 → 24. Anyone evaluating this tool by pointing it at
FeatureCenter was seeing under a third of it — under an `AGENTS.md` telling their agent the
inventory was complete. That shape is the one thing this project exists to prevent, and it was
happening in the place a newcomer was most likely to look.

A minor rather than a patch: `OrmType.Unknown` is a new member on a public enum and
`ExtractionOptions.BaseTypeNames` changed its default. Neither affects the CLI or the MCP server;
both are breaking for code calling `XafLogicExplainer.Core` directly.

### Changed

- **`OrmType.Unknown` is a new member of a public enum**, returned whenever no evidence names an
  ORM. Anyone consuming `XafLogicExplainer.Core` directly and switching exhaustively over
  `OrmType` has a new case to handle; anyone using the CLI or the MCP server has nothing to do.
  The same note `IControllerAnalyzer.AnalyzeControllerFile` got in 0.12.0, for the same reason —
  on 0.x this is what a minor is for.

- **`ExtractionOptions.BaseTypeNames` is the single source of the list.** The CLI, the MCP server
  and the test harness each passed their own copy, so the default in `Core` was four names while
  every caller passed five — four copies with three chances to disagree about what an entity is.
  The callers now use the default.

### Fixed

- **`Unknown` now reaches every place the ORM is reported.** The agent files learned it; the HTML
  explainer and the MCP `xaf_overview` kept deciding in a binary with no third answer, so a project
  whose ORM could not be determined was reported as **XPO** by both. The MCP one was the worse of
  the two: it prints the ORM two lines above "These lists are complete, not sampled", from a tool
  whose description tells the agent that anything absent does not exist in the application. All
  three now go through one `Orm` helper — the defect was never the wrong answer, it was that three
  places were each entitled to one.

- **The ORM is read as syntax, and is `Unknown` when nothing says.** Detection scanned raw file
  text for `DevExpress.Persistent.BaseImpl.EF` and fell through to XPO, so an EF Core application
  whose entities do not use the DevExpress EF base implementation — a legacy schema, its security
  tables in another project — was reported as XPO. That is not a hole in the document: ground rule
  1 then tells the agent that `DbContext`, `DbSet<T>` and EF migrations "do not exist in this
  application and must never be suggested", which forbids the only correct answer. Signals are now
  ranked by what it costs to be wrong about them — a `DbSet<T>` registered on a context first,
  then `using` directives and base classes — and where neither ORM leaves a trace, the rule is
  omitted rather than guessed. Reading text also counted a *mention*: a comment naming the
  namespace was enough, which is how the fixture for this fix first passed against the old code.

- **Entities are found through a base class the project wrote itself.** Classification matched a
  class's own base list against the root names and stopped there, so an application with a shared
  base — auditing, a key convention, a display-name property — lost every business object below it.
  The inversion is what makes it severe: the abstract base *is* matched, so the inventory reported
  the one class that is not a table and omitted the ones that are. Selection now repeats until a
  round changes nothing, exactly as `SelectControllers` does, and resolves a base name through the
  deriving file's own scope rather than by simple name, so a `Contracts.Order` beside a
  `BusinessObjects.Order` still resolves to the base it actually named. On the demos shipped with
  26.1: `FeatureCenter.NET.XPO` 43 → 140 entities, `MainDemo.NET.XPO` 14 → 17, and
  `OutlookInspiredDemo.NET.EFCore` 23 → 24 — the last of which is an EF Core application, where an
  entity that is not registered as a `DbSet<T>` had no fallback either.

- **`PersistentBase` and `XPBaseObject` are recognised as persistent bases.** The XPO hierarchy is
  `PersistentBase` → `XPBaseObject` → `XPCustomObject` → `XPObject`, with `XPLiteObject` also under
  `XPBaseObject`. The list held the three leaves and neither of the classes above them, so a hole
  sat in the middle of a documented API — DevExpress names all five as bases a persistent class may
  derive from, and recommends `PersistentBase`. Deriving from the higher bases is what you do when
  the table brings its own key, which is the same population as the legacy schemas the DbSet roster
  was added for. `FeatureCenter.NET.XPO` gains `OidGenerator`, `NoKeyPropertyNamedBaseObject` and
  `LayoutDemoObject`.

- **An entity carries the properties it inherits.** A class found through a base declared in the
  same project was reported with only the columns it declares itself: `PriorityOrder` listed
  `Rank` and omitted the `Name` and `Number` it persists. Finding those classes at all is what
  0.12.1 was about, and it converted a silent omission into a stated one — the entity now appeared
  under a heading presenting the application's tables, with two thirds of its columns absent, in a
  document that tells an agent its inventories are complete. At scale it is the shared base that
  hurts: an application on an `AuditedObject` lost whatever that base holds from *every* entity,
  which is normally the audit fields an agent most needs to know it must not set by hand. Each
  entity now folds in its ancestors' properties in declaration order from the root down, each
  marked with the class that declared it; a property the class redeclares stays its own, and the
  abstract base is marked as abstract. `FeatureCenter.NET.XPO` folds 151 properties over 146
  entities, `MainDemo.NET.XPO` 26 over 17 — where `Employee` reaches `Photo` through `Person` and
  is correctly told it comes from `Party`.

  Summaries of fixed width name an entity's **own** columns first. The full listings read root
  down, the way the class does, but a five-slot table sharing its width with a six-column audit
  base spends every slot on the base — and then every row of the entity table names the same
  columns and none of the ones that tell one entity from another. Rules and associations an entity
  inherits are still listed only under the class that declares them (#14).

## [0.12.1] — 2026-08-13

Entities the application declares, rather than the ones that inherit from the right class.

The first release that came from outside. [@MBrekhof](https://github.com/MBrekhof) reported an XAF
application of 221 entities over a legacy LIMS schema, of which this tool found **three** — and
filed it against the argument the project is built on: a class that is never seen cannot be
reported as missing, and `AGENTS.md` goes on to tell the agent its inventory is complete. That is
not a gap in a document, it is an agent confidently wrong about which tables exist.

The fix and the three defects found reviewing it are all downstream of one rule, which is worth
saying plainly because it cuts both ways: **a name is not an identity.** Reading entities from a
roster of bare names finds the classes a base list misses, and then also finds a DTO that merely
shares a name, every half of a `partial` class, and a type mentioned in a method body.

### Fixed

- **EF Core entities are found by their `DbSet<T>` registration**, not only by their base class.
  An application mapped onto an existing schema rarely derives from `BaseObject` — the tables
  bring their own keys, so the project writes its own base class or maps a plain POCO — and every
  one of those was dropped, silently, while `AGENTS.md` went on describing its inventory as
  complete. Only classes declared in the analyzed source qualify, so the framework tables a
  DbContext also registers (`ModuleInfo`, `FileData`, `ModelDifference`) stay out. On a
  221-entity application over a legacy LIMS schema this moves extraction from 3 entities to 210.
  Thanks to [@MBrekhof](https://github.com/MBrekhof).

- **A `partial` class is one entity, not one per file.** Matching by base class could only ever
  match once, because one part declares the base list; matching by the `DbSet` roster matches on
  the name, so every part matched — and the scaffolded split that produces two parts is exactly
  what the roster is for. The class came out twice, each copy holding half its columns: two
  incomplete truths with nothing to say they were the same class. The parts are now folded into
  one entity, which also recovers the members XPO extraction had always dropped where a
  hand-written part carries `: BaseObject` and a generated part carries the mapping.

- **The `DbSet` roster no longer matches on a bare name.** A name is not an identity: an
  application may keep a `Contracts.Invoice` DTO beside its `BusinessObjects.Invoice` entity, and
  the roster turned the DTO into a table. Registrations now carry the namespaces they could have
  been naming — the registering file's usings, its own namespace, and the namespaces enclosing it
  — which is ordinary C# lookup, the part of it syntax can see.

- **Business object files are read in a fixed order.** The directory hands them over in whatever
  order the file system keeps them, and that is not the same order on two machines: NTFS compares
  names without case, ext4 by byte, so `Shipment.Generated.cs` sorts after `Shipment.cs` on one and
  before it on the other. Extraction is now ordered by path, so a document regenerated on a laptop
  and in CI can be compared — which is most of what regenerating it is for.

- **Only a `DbContext`'s own properties count as registrations.** `DbSet<T>` written as a local or
  a parameter is a type name in a method body, not the application declaring a table. Contexts are
  found through their base chain as well, so an application whose contexts derive from a shared
  `AuditedDbContext` still registers everything.

## [0.12.0] — 2026-08-11

What runs when you open this screen.

Most of this release is corrections, and they came from an audit rather than from a bug report:
three reviewers on disjoint axes — one against the DevExpress sources, one against the new code,
one reading only the generated output and never the generator. The third found what the other two
structurally could not, which is the argument for keeping all three.

### Changed

- **The README and the landing page lead with the screens**, and every raster figure was retaken
  from a report generated by the current code. `site/capture-screenshots.py` now lives in the
  repository and regenerates the report before shooting it — the previous figures came from an
  uncommitted script in a scratch directory, and three commits later they showed headings that no
  longer existed. A figure nothing can regenerate is a claim nothing keeps true.
- **`IControllerAnalyzer.AnalyzeControllerFile` now returns a list** rather than a single
  controller, because a file can declare more than one and returning the first silently lost the
  rest. Breaking for anyone calling `Core` directly; `xaflogic` and the MCP server are unaffected.

### Added

- **The screen inventory.** Every view the application has, and the logic XAF loads onto each one.
  Neither half of that can be read from the repository: the Model Editor stores only the views
  somebody changed, and the rest are generated at startup from the business classes — so the
  fourteen-entity demo has **54 views, 54 of which appear in no file**. The id rules are the
  framework's own generators: `{Class}_ListView`, `{Class}_DetailView`, `{Class}_LookupListView`,
  and `{Class}_{Collection}_ListView` for every collection.
  - Activation is a transcription of `ViewController.IsFitToView`, condition by condition, and each
    match records *why* — so the answer can be checked instead of trusted.
  - Actions are filtered by their own targeting, which can be narrower than their controller's.
  - Immediately found the thing it was built to find: the demo's `CustomizeExpiryEditorController`
    is a `ViewController<DetailView>` that names no object type, so it runs on the detail view of
    **all fourteen** classes. Its own comment says it customizes "every expiry field".
  - `xaf_view` (MCP) — call it with no argument for the inventory, or with a view id for the whole
    picture. A `Screens` section in the HTML explainer, and a `_Screens.md` detail file beside the
    other agent documentation.
  - What it deliberately does not claim: `Active["reason"] = …` is set at run time from data, so a
    controller listed on a view can still switch itself off. This is what XAF *loads*, and it says
    so wherever it is reported.
  - A controller restricted to a `TargetViewId` that is not a literal is listed apart rather than
    against every screen — that would invent an appearance on all of them.
- **The framework's controllers on each screen**, when a catalog is present. Two layers, kept apart
  everywhere they are reported: what this team wrote gets the full treatment, what XAF provides is
  named compactly with its official one-line description. On the demo, `Prescription_DetailView`
  runs 2 of the team's controllers and 32 of the framework's.
  - Scoped to the modules the application registers, so a WinForms controller never appears on a
    Blazor screen. The platform module is registered by the application builder and named in no
    source — the platform project beside the module is the evidence that it is there.
  - Only what XAF would actually instantiate: abstract types, generic definitions and `[Obsolete]`
    types are excluded, mirroring the framework's own `IsValidControllerType`. `WindowController`
    descendants are excluded too — they belong to a window, not a view.
  - The 164 framework controllers that restrict nothing are recorded once rather than under all 54
    screens, where they would bury the ones a reader came for.
  - A controller whose targeting the catalog could not determine is left out of both lists. Unknown
    is not unrestricted.

- **Controller targeting is now read in full.** XAF decides where a controller activates with four
  conditions ANDed together — nesting, view type, object type and view id — and only two of them
  were being read. All four are now extracted, normalized to the way XAF evaluates them, and
  reported per controller.
  - `TargetViewId` is split on `;`, because XAF accepts a list in that one string and compares
    against each. An id it cannot resolve to a literal is kept as the expression that produced it
    rather than dropped, since a controller restricted to an unnamed view is still restricted.
  - Targeting assigned to an *action* (`someAction.TargetViewId = …`) is deliberately not read as
    the controller's. XAF evaluates the action's own copy separately, to narrow it further inside
    an already-active controller.
- **The ground-truth catalog now records where every framework controller activates**, given the
  DevExpress source component. Reflection over the assemblies cannot see it: four out of five
  built-in controllers assign their targeting inside a constructor, which assembly metadata does
  not carry. The catalog builder reads those constructors with the same syntax analysis the rest of
  the project uses. On DevExpress 26.1 that is 386 of 386 controllers, up from 5.
  - `xaflogic catalog build --dx-sources <Components/Sources>` when they are not beside the
    assemblies, and the command now states how many controllers it could answer for.
  - Each entry records whether its targeting came from `sources` (complete) or `reflection` (a
    lower bound), so nothing downstream can mistake a partial answer for a whole one.

### Fixed

- **Inventories that promised to be complete and were not.** The worst shape of error this project
  can make: a list headed "every expression in this application" reads as authoritative, and a
  reader has no way to see what is missing.
  - The criteria index drew from four of the six places criteria occur, and called the result
    *every* expression while deduplicating by expression. It now reads all six — including the two
    it never touched: **the expression a `RuleCriteria` enforces** (its own criteria is a different
    field from the one saying when the rule applies, and only the second was read) and **an action's
    `TargetObjectsCriteria`**, which on the demo is `Not IsDispensed`, the condition governing its
    single operation. On the demo the index went from six expressions to nine.
  - **`[Indexed(Unique = true)]` was captured nowhere.** A constraint the user meets as a save that
    fails, enforced below the application, absent from documents promising every rule.
  - **Classes with `[DefaultClassOptions]` and no `[NavigationItem]` were missing from navigation.**
    They go into XAF's `Default` group — still in the menu, and dropped from an inventory headed
    "what a user sees in the menu".
  - **Interfaces were not ancestors.** XAF's object-type test is `IsAssignableFrom`, which an
    interface satisfies, and DevExpress targets interfaces — `ChangePasswordController` targets
    `IAuthenticationStandardUser`. Following the base class alone made every interface-targeted
    controller match no view at all, silently.
  - **Collections were filed as calculated properties.** An XPO collection is getter-only, so it
    satisfied `IsComputed` and appeared under derived logic, inviting a reader to treat a persistent
    relationship as a formula.
  - **Appearance rules were printed without their effect or their screen** — "when `OnHand = 0` ()",
    a condition with no consequence. Font colour, back colour and the context are now shown.
  - **"9 rules"** counted two of the six kinds of rule the page documents. It now names them.
- **Controllers were being dropped from extraction entirely** — the worst shape of failure this
  project has, because a controller that is never seen cannot be reported as missing. Two causes,
  both silent:
  - Only the **first** controller class in a file was read. Grouping small controllers in one file
    is ordinary C#; every one after the first vanished.
  - A class counted as a controller only if it derived **directly** from `ViewController`,
    `ObjectViewController` or `WindowController`. Real XAF code does not look like that: it extends
    shipped controllers and its own base classes. `ArchiveController : DeleteObjectsViewController`
    — the example the README advertises as what the catalog makes possible — was never extracted,
    so `FrameworkBaseType` could never be set and the feature could not fire.
  - Discovery now follows base classes to a fixed point, through the application's own classes and
    the catalog, falling back to the naming convention only when the file imports XAF. A probe with
    five controller classes across three files reported one; it now reports all of them, and the
    derived ones inherit their base's targeting.
  - Every unit test passed throughout, because they all built their `ExtractedController` lists by
    hand. The new tests go through the real pipeline against a fixture written in that shape.
- **A controller extending a class this analysis cannot see was reported as unrestricted.**
  Targeting is inherited, so an unresolvable base means the targeting is *unknown* — and unknown is
  not "runs on every screen". Those controllers are now listed apart, with the base that could not
  be resolved, exactly as an unreadable `TargetViewId` already was.
- **Abstract controllers were reported as running on screens.** XAF registers only what it can
  instantiate; an abstract base hands its targeting down and activates on nothing itself.
- **`TargetViewId` was trimmed and XAF does not trim it.** `"A; B"` never activates on `B`, because
  the entry XAF compares is `" B"`. The untrimmed id is now what gets reported, which is also what
  makes the typo visible to whoever wrote it.
- **A controller a registered descendant replaces was still listed on screens.** XAF activates only
  the most derived controller of an inheritance chain — registering a descendant evicts its base
  (`SharedControllersManager.RegisterController`). Every screen of a Blazor application therefore
  listed `ModificationsController` *and* `BlazorModificationsController`, duplicating every Save
  action, and credited shipped behaviour to the framework in the one application that replaced it.
  The survivor now says what it replaces, which is the sentence worth reading.
- **Targeting was only read from constructor blocks**, so three ordinary shapes came out as
  "restricts nothing, runs on every screen": an expression-bodied constructor, an assignment through
  `base.`, and `InitializeComponent` — where the XAF designer puts it, which is every migrated
  application.
- **An action's targeting written in an object initializer was attributed to its controller.** Only
  the `action.TargetViewId = …` form was guarded, and the initializer form is the one DevExpress
  documentation uses.
- **A condition the reader could not understand was treated as no restriction.**
  `TargetViewType = isList ? ViewType.ListView : ViewType.DetailView` was reported as a confident
  restriction to `DetailView` — the last word in the expression — and
  `TargetObjectType = FindTypeInfo(name).Type` as no restriction at all. Both are now recorded as
  unreadable, which keeps the controller out of the per-screen lists and into the one that says why.
- **Window controllers were listed on every screen.** They belong to a window and have none of the
  four view conditions, so "unrestricted" put them everywhere.
- **The platform project was matched by substring**, so `Winery.Module` or `Darwin.Core` pulled every
  WinForms framework controller onto a Blazor application's screens. Whole dotted segments now.
- **Nested list views were invented for collections that never get one.** XAF generates one only
  when the collection holds a business class, so a `List<string>` produced a fabricated view id —
  and framework controllers were then reported on a screen that does not exist.
- **`ViewController<DetailView>` reported no view type at all.** Only generic bases with two
  arguments were read, so the single-argument form — which is how DevExpress documentation writes
  controllers, and how the demo fixture's own `DispenseController` is written — lost half its
  targeting.
- **Targeting inherited from a base controller was ignored**, which reported any controller whose
  base class does the targeting as running on every view in the application. It is set in a
  constructor and constructors run base-first, so it is inherited; reading each class in isolation
  understated 33 controllers in the DevExpress framework alone. Both an application's own base
  classes and framework ones are now followed.
- **The packed MCP server README advertised seven of nine tools**, omitting `xaf_editors` and
  `xaf_migrations` — and that is the README nuget.org renders and the MCP directories import. The
  test that keeps the front page honest now covers it too.
- **Catalog entries sharing a name at two arities overwrote each other.** The sources pass matched
  declarations to catalog types by bare name, so `ObjectViewController` and
  `ObjectViewController<TView, TObject>` were the same key and whichever file was read last won.
  The concrete `ObjectViewController` was reported as running on every screen instead of on object
  views; ten controllers were affected.
- **View identifiers were printed in upper case** in the explainer, because they sit in a row
  header and row headers are uppercased. XAF view ids are case-sensitive, and they are printed
  precisely so someone can go and find one — `PATIENT_PRESCRIPTIONS_LISTVIEW` is an id that does
  not exist. Caught by looking at a screenshot, which is the only place it was visible.
- **Generated prose that said more than the source proves.** Found by reading the output rather than
  the code, which is the only way any of these surface:
  - `AGENTS.md` announced a custom editor as *"requested with `[EditorAlias(…)]`"* while the
    explainer, from the same extraction, said nothing requested it. The alias is what an editor
    offers, not evidence anything asks for it — and the index is the file read on every request.
  - *"Register the type in `X`, as the other 4 are"* presented `AdditionalExportedTypes` as an
    obligation. XAF finds business classes declared in a module by itself; that collection is for
    types it cannot find.
  - *"Controllers live in `…/`"* named one folder chosen by a coin flip between two. Both are
    listed now, with which belongs in which.
  - *"existing databases only, from 0.0.0.0"* — the guard is `> 0.0.0.0`, so the one version named
    as the lower bound is the one version excluded.
  - *"These ran once … and never again"* stated execution history. The source proves the guard, not
    that any particular database ever passed through it: each runs **at most once** per database.
  - *"on any view"* for a controller labelled from `TargetObjectType` alone, ignoring the view type
    it also restricts.
  - A heading asserting *"screens that do not follow their type"* over a section whose own rows said
    nothing requests the editor.
- **`ObjectView` was read as deriving directly from `View`.** It derives from `CompositeView`, so a
  controller targeting `CompositeView` reaches dashboards as well as list and detail views —
  checked against the 26.1 sources rather than assumed.

## [0.11.0] — 2026-08-10

Everything that lives *outside* the business classes.

### Added

- **Custom property and list editors.** A property rendered by one does not show the control its
  type implies, and the business class says nothing about it — the same category of hidden
  behaviour as the Model Editor. They also live in the platform project (`*.Blazor.Server`,
  `*.Win`) *beside* the module, so nobody reading the business objects ever meets them.
  - Detected from `[PropertyEditor]`, `[ListEditor]` and `[ViewItem]`, and from editor base types
    for the abstract editors a team writes once and never decorates.
  - **Alias constants are resolved across the solution.** The attribute reads
    `CustomEditorAliases.BarcodeScannerPropertyEditor`; the reader needs the value XAF matches on,
    and the constant is declared in the module while the editor sits in the platform project, so a
    project read on its own resolves nothing.
  - **Client assets are recorded** — the JavaScript an editor cannot work without. Behaviour in
    neither C# nor XML, and the reason a control breaks when somebody renames a file.
  - Also finds **built-in editors reconfigured at run time** through
    `View.CustomizeViewItemControl<T>()`. There is no custom editor class to find: a controller
    reaches into a built-in editor's component model, leaving no trace on the entity or in the
    Model Editor.
  - Surfaced in `AGENTS.md` as a ground rule, in the explainer, and through a new `xaf_editors`
    MCP tool.
  - Registration is read the way the DevExpress documentation defines it: `isDefault: true` means
    the editor replaces the default for that type **everywhere**, while `false` means it is merely
    *selectable* in the Model Editor. Only the first is reported as being used by an entity —
    listing every string property in an application as "uses the barcode scanner" would be plainly
    false.
- **Version-gated data migrations** from the module updater — the blocks guarded by
  `CurrentDBVersion < new Version("1.1.0.0")`. Each ran **once**, on somebody's production
  database, and never again. Reading the code that runs today cannot recover what they did, which
  is why an agent asked "why does this column contain that?" reasons from current code and invents
  a cause.
  - Records the version being upgraded to, the "existing databases only" lower bound, **which
    schema phase it ran in** — a block running before the schema changed could not use the new
    columns — the methods it calls, and the code itself.
  - Captures **the comment above the block**, which is usually the only surviving record of *why*,
    and the question anyone reading a migration actually has.
  - Kept separate from seed data throughout: seed data says what a fresh database contains,
    migrations say what happened to every database that was not fresh.
  - Surfaced in `AGENTS.md`, the explainer, and a new `xaf_migrations` MCP tool.
- A **fourteen-entity demo application** (`Fixtures/DemoSolution`) with a platform project, a
  custom editor and a version-gated updater, so the diagrams and screenshots show a realistic
  application that belongs to nobody.

- **`xaflogic explain`** — a single self-contained HTML page explaining the application to a
  *person*. The same extraction already serves agents; this is the reader who has just inherited a
  ten-year-old XAF application, or has to hand one over.
  - **A map of the domain model**, drawn from the association attributes scattered across the
    codebase. Most teams have never seen theirs: it exists in one person's head, which is exactly
    the knowledge that leaves when they do. Hovering an entity isolates what it touches.
  - Every entity with its properties and what each one is; every action with the code it runs;
    validation with the message the user will actually see; and the Model Editor settings that
    exist in no C# file.
  - **An index of every criteria expression in the application**, gathered from attributes spread
    across the source. XAF's criteria language is neither SQL nor C#, and it is nowhere collected.
  - Client-side search across everything, light and dark, and no request to the network — it has
    to open from an email attachment on a machine with no internet.
  - The layout is computed at generation time, not in the browser, so the same source always
    draws the same diagram and a regenerated page produces a readable diff.

### Fixed

- Explicit interface implementations were extracted as separate properties, so
  `object ISecurityUserLoginInfo.User => User;` put a second `User` row, typed `object`, into
  every rendering — reading as a modelling mistake the team had not made.
- Code shown in the explainer kept the indentation it had in its source file. Roslyn hands back
  text starting where the node starts, so a method body opened flush left and then jumped eight
  columns.
- The demo application attributed its backing fields rather than its properties, which is not how
  XPO is written — DevExpress's own persistent classes attribute the property. Half its
  relationships and rules were therefore invisible, and the map, the README and the site all
  rendered an application simpler than the one in the fixture. Its shape is now pinned by a test,
  since the previous suite passed either way.
- Cross-references between entities in the explainer were rendered in the browser's default link
  blue, a colour the rest of the page never uses.

## [0.10.1] — 2026-08-10

### Fixed

- `XafLogicExplainer.Mcp` packed the repository README, which does not carry the
  `mcp-name: io.github.peopleworks/xaf-logic-explainer` line. The MCP registry reads that line out
  of the published package to confirm that whoever submits the registry entry also owns the NuGet
  package, so 0.10.0 could never have been registered. The package now carries its own README.

  The line looks like decoration and is not. Removing or rewording it breaks registry publishing
  quietly — the next release simply stops being accepted, with nothing in the build to say why.

## [0.10.0] — 2026-08-10

First release published to NuGet. Version 0.9.0 was the point the repository went public; nothing
was ever pushed to a package feed under it, so everything since is gathered here.

Three packages: `XafLogicExplainer.Core` (the extraction engine),
`XafLogicExplainer.Cli` (the `xaflogic` tool) and `XafLogicExplainer.Mcp`
(an MCP server, installable on its own with `dnx`).

Still 0.x deliberately. The extraction engine is production-proven, but this release changed its
behaviour in six places and has been verified against one real application. 1.0.0 is earned once
the extractor has read codebases we did not write.

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

- **DevExpress ground-truth catalog** (`xaflogic catalog build`, or the standalone `xafcatalog`).
  Reads a locally licensed DevExpress installation and records what the framework itself provides:
  attributes, controllers, model interfaces and modules, with the official summaries and
  documentation links DevExpress ships. On 26.1 that is ~850 types across 50 assemblies.
  - Extraction can then distinguish *your* logic from the framework's: a controller extending
    `DeleteObjectsViewController` is changing how deletion works application-wide, and an
    attribute in neither XAF nor .NET is one your team invented — its meaning exists in your
    codebase and in no documentation.
  - Read with `MetadataLoadContext`, so DevExpress code is never executed. The catalog is written
    to `~/.xaflogic/catalog/`, **never into a repository**, because it is derived from licensed
    software. Extraction behaves exactly as before when no catalog is present.
  - Generic bases every controller shares (`ViewController`, `ObjectViewController`,
    `WindowController`) are deliberately not reported: listing them annotated the entire
    application with "A View Controller" and buried the one case worth noticing.
- **Test suite** (`tests/XafLogicExplainer.Tests`, xUnit v3): 129 tests over synthetic XAF fixtures
  in both XPO and EF Core. The fixtures are XAF source that is never compiled — extraction parses
  it as text — so the suite needs no DevExpress licence and no private feed, and CI verifies the
  whole engine on a public runner. It runs in under a second.

### Changed

- Target framework is now **.NET 10**.

### Fixed by the new tests

Writing the suite surfaced six extraction bugs, all of which had been producing confidently wrong
documentation:

- **A project living under a directory named `bin` extracted as empty.** Five analyzers tested
  `path.Contains("bin")`, matching the substring anywhere in an absolute path — so `C:\bin\Sales\`,
  or any folder whose name merely contains those letters, had every source file silently skipped.
  Matching is now on whole path segments, and only below the directory being analyzed, since build
  output is always beneath the project root.
- **Property-level validation rules lost their message.** Class-level and property-level rules were
  read by two code paths that had drifted apart; only the class-level one populated
  `MessageTemplate`. Property-level rules are the ordinary way to write XAF validation, and the
  message is the most useful part of a rule, so it was missing from exactly the rules people write
  most. Both paths now share one reader.
- **`ViewController<DetailView>` was reported as targeting `DetailView`.** The single generic
  argument of `ViewController<T>` constrains the *view*, not the business type; only
  `ObjectViewController<TView, TObject>` names an object. An explicit
  `TargetObjectType = typeof(X)` in the constructor is now read first, since it states the intent
  outright and was previously unreachable.
- **Seed data was only found in a file named `Updater.cs`.** Any other name — `SeedDataUpdater`,
  `DemoDataUpdater`, an updater split per area — meant the application was reported as having no
  seed data at all, silently. The fallback now looks for a class that actually derives from
  `ModuleUpdater`.
- **`ObjectSpace.CreateObject<T>()` seed records were invisible.** Only `new Customer(session)` was
  recognized, which is the older Session-based style; a modern updater works against
  `IObjectSpace`. On a real 19-entity application this raised the seed methods found from 4 to 9.
- **Seed methods were counted twice.** Each one is reached both by following calls out of
  `UpdateDatabaseAfterUpdateSchema` and by the sweep over every method in the class, and the
  duplicate was a perfect copy — so it read as two genuinely separate operations.

### Fixed

- `nameof(...)` in attribute arguments was recorded as the literal text `nameof(Numero)` rather
  than `Numero`. Attribute values were read with `expression.ToString().Trim('"')`, which is right
  for a string literal and wrong for everything else — and `[XafDefaultProperty(nameof(X))]` is
  how current C# is written. The bad value reached generated documentation and MCP responses as
  though it were a real property name. A shared `SyntaxLiteral` reader now resolves string
  literals, `nameof`, and concatenated strings across all three analyzers.

### Fixed

- `ModuleAnalyzer` reported business entities as required XAF modules. It accepted any invocation
  whose expression contained `Add`, so `AdditionalExportedTypes.Add(typeof(Customer))` was read as
  a module dependency. On a real project this listed nine entities and six framework base types
  among twelve genuine modules. It now matches the target collection, and
  `AdditionalExportedTypes` feeds the registered-types list where it belongs.

### Planned

- AI provider abstraction (OpenAI, Azure OpenAI, Anthropic, Ollama) for `--enrich`
- Splitting the 1,500-line `Program.cs` into one file per command

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

[Unreleased]: https://github.com/peopleworks/XAFLogicExplainer/compare/v0.15.0...HEAD
[0.15.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.15.0
[0.14.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.14.0
[0.13.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.13.0
[0.12.1]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.12.1
[0.12.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.12.0
[0.11.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.11.0
[0.10.1]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.10.1
[0.10.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.10.0
[0.9.0]: https://github.com/peopleworks/XAFLogicExplainer/releases/tag/v0.9.0
