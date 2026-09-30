# Tools/Unity

Scripts that drive the **open** Unity Editor through the Unity CLI (`unity command ...`). Several
people and agents share one Editor, so every script takes the lock `Temp/unity-editor.lock` first
and waits its turn (the lock's `owner` file says who holds it).

| Script | What it does |
| --- | --- |
| `node Tools/Unity/compile-check.js [--quiet] [--player]` | Compiles every assembly outside the Editor, so nothing broken ever reaches it. `--player` compiles the runtime assemblies with the Android defines and no Editor references. Needs no lock. |
| `Tools/Unity/ueval.sh [--compile] '<C#>'` | Runs C# in the Editor and prints the result and any new console errors. `--compile` runs the compile check and recompiles the Editor first. |
| `Tools/Unity/screen.sh <Name> [--compile]` | Builds one screen prefab from `ArtSource/Screens/<Name>/layout.json`, captures it at 16:9, 21:9 and 4:3 and compares the 16:9 capture with its mockup. |
| `Tools/Unity/playcheck.sh wait:N eval:'<C#>' shot:<png> log:N ...` | Plays the game from the Boot scene, runs the steps in order and always leaves Play mode again. Exit 4 when the run logged errors. |
| `Tools/Unity/utest.sh <EditMode\|PlayMode> [filter] [--no-compile]` | Runs the Unity tests and prints every test that did not pass. Exit 0 passed, 8 tests failed, 6 the run broke. |

Things worth knowing:

- **PlayMode tests** are started and then polled: entering Play mode reloads the scripts and
  drops the request that started them. The tests load the Boot scene themselves with the saves
  redirected to a temporary folder (`Assets/_Project/Tests/PlayMode/GameFixture.cs`), so they
  never touch the player's files.
- **Play From Boot** (`Callbreak/Play From Boot` in the menu bar): pressing Play always starts
  from the Boot scene. It stands aside for a PlayMode test run, which plays the Test Framework's
  own scene.
- A Bash call that is killed by its timeout leaves the lock behind. Run long jobs in the
  background; a lock older than 15 minutes is treated as dead.
- The engine-free code (Core, Bots, most of Application, the table's logic) also builds and tests
  without Unity: `dotnet test Tools/DotnetTests/Callbreak.Tests` (see its README).
