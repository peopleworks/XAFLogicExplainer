# Security Policy

## Reporting a vulnerability

Use GitHub's private reporting:
**[Report a vulnerability](https://github.com/peopleworks/XAFLogicExplainer/security/advisories/new)**
(Security → Advisories → Report a vulnerability). It stays private until we publish it together.

Please don't open a public issue for a vulnerability. If GitHub advisories don't work for you, email
`peopleworks@gmail.com` with `SECURITY` in the subject.

Expect a first reply within a week. This is a small project maintained alongside a day job, so a fix
may take longer than an acknowledgement — you'll hear where it stands either way. If you'd like
credit in the advisory, say so; if you'd rather stay anonymous, that's fine too.

## Supported versions

The latest release on NuGet. There are no long-term support branches.

## What the attack surface actually looks like

This tool reads your source code. That single fact defines everything that matters here.

| Component | Where it runs | What it touches |
| --- | --- | --- |
| `Core` (extraction) | Your machine | Reads `.cs` and `.xafml`. No network. No writes outside the output directory. |
| `Cli` — `extract`, `diff`, `status`, `watch` | Your machine | Local only. |
| `Cli` — `--enrich` | Your machine → an AI provider | **Sends controller and action source code off the machine.** |
| `Cli` — `sync`, `chat` | Your machine → a configured remote | **Sends generated documentation off the machine.** |
| `Blazor` widget | Your XAF app | Renders a panel inside your application. |

We are especially interested in reports about:

- **Source code leaving the machine from a command documented as local.** `extract`, `diff` and
  `status` must never make a network call. A path where they do is the most serious thing you
  could find here.
- **Credentials in the wrong place.** API tokens belong in `~/.xaflogic/config.json` or the
  environment. A path that writes one into extracted output, a log line, a diff report, or an
  `AGENTS.md` committed to a repository is a vulnerability — that is precisely how tokens end up
  on GitHub.
- **Path traversal in the target project path.** The tool walks directories you point it at and
  writes an output folder. A crafted project layout that makes it read or write outside that scope
  counts.
- **Anything in `--enrich` that sends more than it says it does** — for example including entire
  files, connection strings, or `appsettings.json` in a prompt.
- **Denial of service in the analyzers.** Roslyn parsing runs against untrusted-shaped source; a
  file that hangs extraction is a real bug.

## What isn't a vulnerability

- **A missed or misread XAF pattern.** The extractor is a heuristic over syntax, not a compiler.
  Being incomplete is a known property — please file it as an
  [extraction gap](../../issues/new?template=extraction-gap.yml) instead.
- **`--enrich` or `sync` sending your code somewhere.** That is the documented purpose of those
  commands. Their being *opt-in* is the security property; using them is your decision.
- **Needing a DevExpress license for the Blazor widget.** That's licensing, not security.

## A note for anyone running this on a client codebase

The generated documentation describes your business logic in plain language. It is derived from
proprietary source and should be treated with the same care as the source itself: mind where you
publish `AGENTS.md`, mind which AI provider `--enrich` is pointed at, and mind whether the remote
sink you configured is one your client has agreed to.
