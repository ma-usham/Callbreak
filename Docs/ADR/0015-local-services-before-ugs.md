# ADR 015 - Local services until Unity Gaming Services are linked

**Status:** proposed.

**Context.** GDD §16 puts Authentication, Cloud Save, Remote Config and Analytics on Unity Gaming
Services. Those need the project linked to a Unity Cloud project and dashboard configuration,
which only the owner can do.

**Decision.** The ports (`IAuthService`, `ISaveStore`, `IAnalytics`, and config assets for
Remote Config values) are implemented locally: a stable anonymous GUID player id, JSON files
in the app's data folder with atomic writes, analytics logged in development builds only, and
ScriptableObject defaults for tunables.

**Consequences.** Everything works offline and in the Editor today. Linking UGS later adds
adapters behind the same ports (UGS sign-in, Cloud Save mirroring, Remote Config overrides,
UGS Analytics) without touching screens or rules.
