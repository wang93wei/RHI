# Merge upstream main b057033

## Requirements
- Merge upstream/main b057033 into the existing feat/i18n branch.
- Preserve upstream PCGW data and existing localization behavior.
- Assess whether the update introduces user-visible strings requiring localization.
- Do not fix defects that also exist upstream.
- No local validation commands and no local .NET installation. Review source only; run all validation in GitHub Windows CI.

## Acceptance criteria
- upstream/main b057033 is an ancestor of the delivered branch.
- Independent review covers merge scope and localization impact.
- Manual Build & Test and Package workflows succeed for the integration commit; record run URLs and SHA.
- Task is archived and journal is recorded after completion.

## Bounded plan
This is a data-only merge: upstream changes one file, database/pcgw_data.json. Preserve that file exactly as upstream. No application code, workflow or locale changes are expected. Commit and push to origin/feat/i18n are necessary for the requested remote CI. Keep workflows manual. If CI fails, distinguish upstream baseline defects from integration defects before any repair. No new spec contract is expected.
