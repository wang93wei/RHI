# Upstream integration
Merge upstream/main e9eef5d into current feat/i18n. Preserve existing localization and upstream behavior. Complete new user-visible text in all five catalogs.

## Constraints
No local validation, builds, tests, or .NET installation. Use GitHub Windows CI only. Push current branch as needed for requested CI. Preserve manual workflows.

## Acceptance
Upstream commit is an ancestor. New UI uses localization keys with five translations and matching placeholders. Remote Build & Test and Package pass. Record run links and limitations. Archive this task.

## Additional requirement
Update AGENTS.md against final merged source, architecture, localization and CI workflow facts.
