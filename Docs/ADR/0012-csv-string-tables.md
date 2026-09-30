# ADR 012 - CSV string tables for v1 localisation

**Status:** proposed.

**Context.** GDD §15-16 choose the Unity Localization package (which brings Addressables). v1 ships
English only; Nepali waits for the Devanagari shaping spike (GDD §15). The package adds an
Addressables content build to every player build and a second asset system to maintain.

**Decision.** `ILocalization` (Application port) is backed by CSV string tables
(`Assets/_Project/Localization/{UI,Rules,Emotes}.csv`, columns `key,en,ne`), parsed by engine-free
code. Screen labels carry keys (collected from the screen layouts by an Editor tool); prefabs keep
the English text they were fitted with; `LocalizedText` follows the active language live.

**Consequences.** No Addressables build step in v1. Translators edit a CSV. Switching to the Unity
Localization package later means one new `ILocalization` adapter and importing the CSVs into
string tables; screens and presenters do not change.
