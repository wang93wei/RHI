# Upstream main integration

## Requirements
Merge latest upstream/main into current feat/i18n and preserve existing localization and manual CI behavior. Complete five-language localization for added or changed UI. Verify and fix through GitHub Windows CI only. Never run local validation or install .NET. Archive this task on completion.

## Acceptance
- Record upstream and integration SHAs; current branch contains fetched upstream main.
- New/changed UI uses localization keys with equivalent meanings and placeholders in all five catalogs; protocol values remain stable.
- Build & Test and Package succeed on the integration code SHA; report links and artifacts.
- Preserve unrelated edits; record and archive task. Windows UI/game behavior remains outside CI evidence.
