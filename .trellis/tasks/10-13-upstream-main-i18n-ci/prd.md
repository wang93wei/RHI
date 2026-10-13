# Upstream main integration

## Requirements
Merge fetched upstream/main b9ba0dc into current feat/i18n. Preserve existing localization and manual Windows CI. Localize added or changed user-visible text across all five catalogs. Include upstream release 2.8.7, nightly update fixes, links, manifest addon and database updates.

## Constraints
No local validation of any kind; no .NET install, build or tests. All executable validation runs on GitHub Windows CI. Use sub-agents for merge/localization and independent review. Preserve unrelated changes. Commit and push current branch as necessary for CI. Archive completed task.

## Acceptance
- upstream/main b9ba0dc is included in branch ancestry.
- Upstream functional changes and existing F5/i18n behavior coexist.
- Five-language catalog and affected source review complete.
- Build & Test and Package workflows pass on same work SHA; failures fixed remotely.
- Report commit, upstream SHA, CI evidence and unverified Windows UI behavior.
