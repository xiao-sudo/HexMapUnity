## Agent skills

### Issue tracker

Issues are tracked as local Markdown files under `.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

The repository uses the default canonical triage labels. See `docs/agents/triage-labels.md`.

### Domain docs

This is a single-context repository using root `CONTEXT.md` and `docs/adr/`. See `docs/agents/domain.md`.

### File editing priority

1. Use `apply_patch` for manual edits and localized changes.
2. If the patch helper is unavailable because of an environment failure, use a UTF-8 no-BOM complete-file writer only for newly created or intentionally regenerated files, then reread the result to verify it.
3. Use `Set-Content` only as a small one-off fallback when the stable UTF-8 writer is unavailable; do not use whole-file writes for existing files with user changes.
### C# member naming

Private and instance member fields in `HexMap` must use the `m_` prefix, for example `m_Radius`, `m_CellsByCoordinate`, and `m_Cells`. Keep local variables and method parameters without this prefix