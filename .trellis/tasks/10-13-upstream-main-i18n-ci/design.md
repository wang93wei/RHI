# Design

Use a normal merge without committing until conflict resolution and localization are complete. Keep upstream functionality while retaining Loc calls, stable variant tags and existing F5 lifecycle. Localize upstream tooltip semantics through current service. Treat URLs, protocol values, filenames and log messages as stable data. Preserve existing manual workflow_dispatch and default NuGet location. Release and database/manifest changes remain upstream-owned content.

Merge implementation agent owns upstream changed source, catalogs and necessary focused regression tests. Coordinator owns task/spec/session files and CI dispatch. Independent check agent reviews integration after implementation settles. Rollback uses pre-merge HEAD 4fbae53 (no destructive reset without need). Commit/push to feat/i18n, then run both manual Windows workflows. Fix only observed integration/CI failures or proven defects within affected paths.
