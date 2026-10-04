# Tools/Unity

Scripts that drive the **open** Unity Editor through the Unity CLI (`unity command ...`). Several
people and agents share one Editor, so every script takes the lock `Temp/unity-editor.lock` first
and waits its turn (the lock's `owner` file says who holds it).

| Script | What it does |
| --- | --- |
| `node Tools/Unity/compile-check.js [--quiet] [--player \| --android]` | Compiles every assembly outside the Editor, so nothing broken ever reaches it. `--player` compiles the runtime assemblies with the Android defines and no Editor references; `--android` compiles the Editor assemblies as the Android build sees them. Needs no lock. |
| `Tools/Unity/ueval.sh [--compile] '<C#>'` | Runs C# in the Editor and prints the result and any new console errors. `--compile` runs the compile check and recompiles the Editor first. |
| `Tools/Unity/screen.sh <Name> [--compile]` | Builds one screen prefab from `ArtSource/Screens/<Name>/layout.json`, captures it at 16:9, 21:9 and 4:3 and compares the 16:9 capture with its mockup. |
| `Tools/Unity/playcheck.sh wait:N eval:'<C#>' shot:<png> log:N ...` | Plays the game from the Boot scene, runs the steps in order and always leaves Play mode again. Exit 4 when the run logged errors. |
| `Tools/Unity/utest.sh <EditMode\|PlayMode> [filter] [--no-compile]` | Runs the Unity tests and prints every test that did not pass. Exit 0 passed, 8 tests failed, 6 the run broke. |
| `Tools/Unity/android-export.sh` | Exports the Android player as a Gradle project into `Temp/AndroidExport` and prints the build report (errors and warnings) and the invite-link filter of the exported manifest. The Editor is busy for the whole build. |
| `Tools/Unity/android-apk.sh` | Builds a Development APK, `Builds/Android/Callbreak.apk` (Google's test ads): the Editor exports a development Gradle project into `Temp/AndroidApk`, Unity's Gradle builds it signed with the Android debug key (the project's keystore and Player Settings are left alone), and the script checks the AdMob app id in the APK's manifest. Holds the Editor lock for the whole build, IL2CPP's C++ included. |
| `powershell -File Tools/Unity/editor-dialog.ps1 [-DismissTestScene] [-ReloadScenes]` | Prints a modal dialog the Editor is showing (every `unity command` times out while one is up). `-DismissTestScene` answers Don't Save when only the Test Framework's scenes are listed; `-ReloadScenes` answers Reload when an open scene was changed on disk (e.g. by `git checkout`). |

Helpers for `playcheck.sh` steps (all in `Assets/_Project/Scripts/Editor`; each call returns a line
saying what it did):

| Call | What it does |
| --- | --- |
| `Callbreak.Editor.PlayDriver.Press("StartButton")` | Presses a button by object name on the open dialog, or else on the top screen. `PressIn(root, name)`, `Back()`, `CloseDialog()` and `Where()` complete it. |
| `Callbreak.Editor.AutoPlayer.Start()` | Plays the local seat through the real views: the call chips, a throw of the lowest legal card (pulled up out of the hand), Next Round. `Report()` and `Stop()`. |
| `Callbreak.Editor.RelayCheck.Start()` | With the running game hosting a room, joins it as a second player in the same process over its own relay connection and plays that seat. `Report()` says where both sides stand and whether they agree; `Stop()`. |
| `Callbreak.Editor.TableSimulation.Run(reduceMotion, captures)` | Outside Play mode: whole offline matches through the real prefabs and presenters, with a report in `Temp/TableSimulation`. |

Things worth knowing:

- **playcheck.sh plays with the real saves** of this machine's player (the PlayMode tests do not):
  an unfinished offline match makes "Resume your match?" come up at boot, which
  `PlayDriver.CloseDialog()` clears.
- **A script that does not compile blocks Play mode for everyone** as soon as the Editor refreshes.
  Run the compile check after every edit, and do not leave half an edit on disk.

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
