# ADR 013 - No records or init accessors

**Status:** proposed.

**Context.** GDD §31 asks for records for events and DTOs. On Unity's .NET Standard 2.1 profile,
records and `init` need a hand-written `IsExternalInit`, which then collides with the real one
when the same sources are compiled by the dotnet test harness on .NET 10 (and with
`InternalsVisibleTo`).

**Decision.** First-party code uses `readonly struct` and `sealed class` with get-only
properties and constructors, with value equality written by hand where it matters (events,
snapshots, views).

**Consequences.** Slightly more code; identical behaviour in Unity and in the harness.
