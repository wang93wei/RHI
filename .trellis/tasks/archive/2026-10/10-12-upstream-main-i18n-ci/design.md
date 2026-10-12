# Design

Merge upstream/main without committing first, resolve conflicts preserving local i18n and workflows, then integrate new OptiScaler F5 UI into existing localization paths. Retain upstream service behavior, data and configuration files. Ensure new F5 INIs ship in the portable/installer package. Use stable selector Tag values and localize display text only. Keep dependency versions aligned. Publish current branch to origin for manual Windows CI; no deployment/release. Git merge abort is the recovery point before commit; retain original HEAD 00885d3 as safety reference.
