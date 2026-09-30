# AGENTS.md: Callbreak

Unity 6000.3.18f1 · URP · uGUI + TextMeshPro · PurrNet (server-authoritative) · VContainer
Source of truth: Docs/GDD/Callbreak_GameDesign_TechArchitecture_v1.1.pdf
Full agent rules: Docs/Team/Callbreak_Agent_Handbook_v1.0.pdf

## How you work
- One task brief at a time. Branch: agent/<task-id>-<short-name> from latest main.
- State your plan (files, tests, questions) before editing. Open a PR when done; never merge.
- Edit only the files your brief allows. Reading anything is fine.

## Always
- Architecture per GDD §17-22. Core and Bots: no UnityEngine (noEngineReferences).
- Dependencies point inward; Presentation never references Networking.
- Constructor injection (VContainer). Views: MonoBehaviour + [SerializeField] private only.
- Scores are integer tenths. Rules come from RuleSet, never literals. Seats advance via
  Seat.Next(rules.Direction) (anticlockwise).
- A seat's hand leaves the server only by TargetRpc to that seat. Bots get PlayerView only.
- Tests first for rules and scoring; run the relevant suites before you report.
- User-facing text goes in Localization tables. Match surrounding code style.
- A running Unity Editor is driven through the Unity CLI (unity status / unity command),
  not by editing .unity/.prefab/.asset YAML.

## Never
- Merge, push to main, force-push, or rewrite history.
- Edit Packages/, ProjectSettings/, .github/, Assets/Settings/ or AGENTS.md.
- Add, remove or upgrade packages; add asmdef references.
- Weaken, skip, [Ignore] or delete tests to get green.
- Change game rules, UI copy or visual design (the owner decides these).
- Use singletons, static mutable state, Find*, Resources.Load, async void (except Unity
  events).
- Put another seat's cards in public RPCs, SyncVars, logs, analytics or exceptions.
- Commit secrets, keystores, tokens, personal data, Library/ or builds.
- Claim something works that you didn't run. Write "not verified" instead.

## Ask first when
- The GDD is ambiguous or conflicts with the brief or the code.
- You need a package, setting, asmdef reference, or a public Core or Networking API change.
- The change grows beyond about 10 files, a test outside your area fails, or the Editor is in Safe Mode.

## Verify
- unity status (Editor ready?) · unity pipeline list (Safe Mode?) · unity command (its tools)
- unity test D:\UnityProjects\Callbreak --mode EditMode [--filter <Namespace>] --timeout 600
- unity test D:\UnityProjects\Callbreak --mode PlayMode --timeout 900 (views, flows, network)
- Exit 0 = pass · 8 = tests failed (fix the code) · 6 = run broke (compile, license, timeout).
- The project can't be open in the Editor during batch `unity test`; otherwise use the Editor's
  commands.
- Pure C# (Core, Bots and the engine-free parts of Application) also builds and tests outside
  Unity: dotnet test Tools/DotnetTests (see its README).

## Report (PR description)
Task · Summary · Files · Tests (added + suite counts) · Commands run + exit codes ·
Acceptance checklist · Deviations · Not verified · Questions · Suggestions
