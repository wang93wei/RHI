# Execution

1. Record clean baseline and fetch upstream/main and origin/feat/i18n.
2. Merge upstream/main b9ba0dc with --no-commit; resolve conflicts and affected localization.
3. Independent source review of diff, nightly lifecycle, localization and dependencies. Do not execute local checks, including coverage/whitespace checks.
4. Scoped merge/work commit and push feat/i18n.
5. Dispatch build.yml and package.yml on same work SHA. Review runner logs and artifacts. Fix CI failures and repeat only required checks.
6. Record verified evidence/spec changes; archive task and journal; push metadata.

Validation only on Windows GitHub Actions: strict catalog check and gate regression tests, Release x64 solution build, --no-build application tests, publish/helper build, Inno Setup and portable artifacts. UI/game interaction is outside CI evidence.
