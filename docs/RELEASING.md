# Releasing

Three packages ship together and share one version number:
`XafLogicExplainer.Core`, `.Cli` and `.Mcp`.

## One-time setup

Nothing publishes until this exists. The workflow stores no API key — it trades its OIDC token for
a short-lived one at run time — so the policy has to be created on nuget.org first.

**nuget.org → Account → Trusted Publishing → Add:**

| Field | Value |
| --- | --- |
| Package Owner | `peopleworksservices` |
| Repository Owner | `peopleworks` |
| Repository | `XAFLogicExplainer` |
| Workflow file | `nuget.yml` |

Until the packages exist, NuGet treats the IDs as unclaimed; the first successful push claims them
for that owner. All three IDs were free as of 2026-08-10.

## Cutting a release

1. **Bump the version in five files — six strings.** `StatesTheSameVersionEverywhereItIsWritten`
   checks all of them against the built assembly, and the pack job checks the manifest against the
   tag:
   - `Directory.Build.props` → `<Version>` — the one everything else is compared to
   - `src/XafLogicExplainer.Mcp/.mcp/server.json` → `version` **and** `packages[0].version`; the
     second is what the MCP registry resolves to on nuget.org. Keep the file free of a BOM
   - `plugins/xaf-logic-explainer/.claude-plugin/plugin.json` → `version`
   - `plugins/xaf-logic-explainer/skills/xaf-application-knowledge/SKILL.md` → `version`
   - `README.md` → the `**vx.y.z.**` that opens the Status section

   The last three are neither built nor packed, so nothing but that test forces them to move. Both
   plugin files sat at 0.9.0 through two releases, telling anyone who installed the plugin they had
   a version that no longer existed.
2. **Move the CHANGELOG's `[Unreleased]` content** under a new `## [x.y.z] — <date>` heading, and
   update the link definitions at the bottom.
3. Merge to `main` and confirm CI is green.
4. **Publish a GitHub Release tagged `vx.y.z`** — the `v` prefix is what tells the workflow this is
   a package release.

The job then runs the tests, packs, and refuses to push if anything disagrees:

- the tag does not match the packed version — the classic footgun, and `--skip-duplicate` would
  otherwise report success while shipping nothing
- a package is missing its `.snupkg` — symbol gaps only surface months later, when someone tries
  to step into the library
- the MCP manifest's versions have drifted — the registry would advertise a version that does not
  exist

## After the packages are live

- **MCP registry.** The `.mcp/server.json` manifest is packed inside `XafLogicExplainer.Mcp`, which
  is what the registry reads. Submit at <https://github.com/modelcontextprotocol/registry> once the
  package is on nuget.org.
- **CodeGuilds.** Submit at <https://codeguilds.dev>, as `SignsofAI` did.
- **Verify the tool actually installs**, from a machine that has never seen it:
  ```bash
  dotnet tool install -g XafLogicExplainer.Cli
  xaflogic --help
  dnx XafLogicExplainer.Mcp --yes -- --help
  ```
  A package that packs cleanly and fails to install is a real outcome; the pack job cannot catch it.

  Install to a temporary directory when the machine is your own, so a verification cannot leave you
  on a version you did not choose to run:
  ```bash
  dotnet tool install --tool-path /tmp/verify XafLogicExplainer.Cli
  /tmp/verify/xaflogic --version   # stamps the commit, so the binary ties back to the merge
  ```

  **Do not trust one read of the feed.** `api.nuget.org/v3-flatcontainer/<id>/index.json` is cached
  per CDN edge, and during the 0.13.0 verification two requests seconds apart returned `0.12.1` and
  `0.13.0` for the same package. A single read can report the previous version and look exactly
  like a push that failed. Check each package twice, and treat a successful install from the feed
  as the proof rather than the index.

## Version policy

`0.x` while the extractor is still meeting codebases we did not write. Extraction behaviour changed
in six places for 0.10.0 and was verified against one real application — a good result, and also a
measure of how much moved. **1.0.0 is earned when the extractor has read XAF projects from outside
this shop**, not on a date.

## What is not automated

- **Code signing.** Not done. It is a .NET Foundation eligibility criterion and the honest gap to
  name if the project is ever submitted.
- **The site** deploys on its own from `site/**` (`pages.yml`); it carries no version.
