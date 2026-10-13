# Integration source review

Implement and independent check agents completed source review. No new merge blockers found. No local executable verification was run.

## Changes
- Merge upstream main b9ba0dc into feat/i18n; release 2.8.7, nightly update/link fixes, managed Depth3D Cursor addon and PCGW database changes.
- Resolve Extras and Staging conflicts while retaining Loc and F5 branches.
- Extend latest-nightly UpdateAsync refresh to bulk updater with null build hint. Explicit pinned hint retains selected directory after download.
- Four new keys in all five catalogs: Nightly tooltip, ready/checking progress, staging unavailable.

## Existing risks outside this integration fix
- InstallAsync (OptiScalerService.Install.cs:87/92/95) checks global Nightly staging for explicit pinned build. A missing selected build may fail during deployment.
- Pinned install/update version reporting (Install.cs:445/500/1341/1351) uses global StagedVersionNightly, which may differ from deployed selection.
- Latest download failure can leave old staging ready (Install.cs:1095/1345); old files may deploy and update flag may clear. No new behavior introduced for this failure path.

## Verification boundary
Both GitHub Windows workflows must pass on work SHA. CI does not establish real download/deployment, Windows UI/layout/live language switching or game behavior. No regression fixture was added for network/filesystem staging; existing test suite is not evidence for those operations.
