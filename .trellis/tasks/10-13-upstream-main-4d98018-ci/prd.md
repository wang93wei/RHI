# Upstream main integration (4d98018)

## Requirements
Merge upstream/main 4d98018 into current feat/i18n. Upstream adds two data-only commits:
- d1c39d0 `manifest.json`: DLSS FG preset C (value 3) under the preset list.
- 4d98018 `dlss_manifest.json`: DLSS/DLSSD/DLSSG 310.9.16 and Streamline 2.14.16.0.

Assess i18n impact of the new data. Localize only user-visible text that the fork already localizes; keep protocol values (preset names/values, versions, URLs) unchanged.

## Constraints
No local validation of any kind; no .NET install, build, tests or catalog script runs. All executable validation runs on GitHub Windows CI (Build & Test + Package). Use sub-agents for review/localization assessment. Do not fix bugs that also exist upstream. Preserve upstream data formatting. Commit and push current branch as necessary for CI. Archive completed task.

## Acceptance
- upstream/main 4d98018 is included in branch ancestry.
- i18n impact assessed with file:line evidence; catalogs changed only if required (all five, 1:1 keys).
- Build & Test and Package workflows pass on the same work SHA; failures fixed remotely.
- Report commit, upstream SHA, CI evidence and unverified Windows UI behavior.
