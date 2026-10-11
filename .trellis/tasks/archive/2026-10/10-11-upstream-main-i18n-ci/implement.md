# Execution
1. Merge upstream without committing; resolve conflicts.
2. Parallel localization of disjoint UI/service groups; consolidate catalogs.
3. Review source without running local checks.
4. Commit and push feat/i18n, dispatch Build & Test and Package.
5. Inspect remote logs; fix failures and rerun affected CI.
6. Update relevant specs, record exact CI evidence, archive and journal.

Rollback: pre-merge HEAD a54a62d; preserve via normal history, avoid destructive reset.
