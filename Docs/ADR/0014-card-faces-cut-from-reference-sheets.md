# ADR 014 - Card faces cut from the reference sheets

**Status:** proposed. The faces are superseded by ADR 050 (composed from the vintage pack); the
backs stand.

**Context.** GDD §13 planned 156 faces rebuilt as vector components at 256 × 358 (5:7). The owner's
approved deck sheets (`card-designs/full-decks/`) draw narrower cards (about 0.62:1) with
painted court figures, and the owner wants the reference art matched exactly.

**Decision.** Each face is cut from its sheet at its real proportions, with exact rounded-corner
alpha and no baked shadow; the three backs are cut from mockup 15. All 159 files share one pixel
size and geometry (`Tools/ArtPipeline/export-decks.ps1`, `ArtSource/Prompts/Decks_Batch.md`).

**Consequences.** Faces are about the sheets' native resolution: sharp at 1080p hand size,
slightly soft on 1440p phones. A later vector rebuild can replace files one for one under the
same names.
