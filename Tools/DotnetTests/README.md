# Dotnet test harness

Builds and tests the engine-free code outside Unity, so rules work never needs the Editor closed.
The projects here compile the very same files Unity compiles:

| Project | Sources |
| --- | --- |
| `Callbreak.Core` | `Assets/_Project/Scripts/Core/**` |
| `Callbreak.Bots` | `Assets/_Project/Scripts/Bots/**` |
| `Callbreak.Application` | `Assets/_Project/Scripts/Application/**`, without `Config/` and any `Unity/` folder |
| `Callbreak.Tests` | `Assets/_Project/Tests/EditMode/{Cards,Rules,Scoring,Match,Bots,Application,Support}/**` |

```bash
dotnet test Tools/DotnetTests/Callbreak.Tests
dotnet test Tools/DotnetTests/Callbreak.Tests --filter "FullyQualifiedName~Rules"
```

The libraries target `netstandard2.1` with C# 9, the same surface Unity 6000.3 gives them. Tests
use NUnit 3.14 here and NUnit 3.5 inside Unity, so keep to what 3.5 has: `Assert.AreEqual`,
`Assert.That`, `Assert.Throws`, `[Test]`, `[TestCase]`, `[TestCaseSource]`, `[SetUp]`. No
`Assert.Multiple`, no `async` tests.

A green run here does not replace the Editor's Test Runner before a merge; it is the fast loop.

## Staging

`Tools/DotnetTests/Staging/Application/**` and `Staging/Tests/**` are compiled into the harness
like the real folders but live outside `Assets/`, so the shared Unity Editor never sees them.
Write a larger engine-free piece there, get it compiling and green, then move the files into
`Assets/_Project/Scripts/Application` and `Assets/_Project/Tests/EditMode`. The folder is
git-ignored and should be empty between tasks.
