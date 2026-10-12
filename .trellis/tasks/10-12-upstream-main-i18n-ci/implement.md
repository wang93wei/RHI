# Execution

1. [x] Fetch upstream/main and origin/current branch; record clean baseline and SHAs.
2. [x] Delegate merge/conflict resolution and localization to implementation agents with exclusive file ownership.
3. [x] Delegate independent source review with fixes, without local validation. Add remote checks when CI does not cover required parity/package contracts.
4. [x] Commit and push integration; dispatch build.yml and package.yml for feat/i18n.
5. [x] Read remote results and failure logs; delegate fixes and repeat affected CI until success.
6. Record upstream/code/run evidence, update relevant specs if needed, commit, archive, record journal and push.

Validation runs only on GitHub Windows runners. No local tests, lint, catalog validation, build or package commands. Git state and source inspection are allowed.
