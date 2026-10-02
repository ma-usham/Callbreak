# EOS setup (online rooms)

Online rooms run on Epic Online Services through the PlayEveryWare plugin
(`com.playeveryware.eos` 6.2.0, EOS SDK 1.19). A device signs in with a Device ID (EOS Connect,
no Epic account). A host lists its room code as an EOS lobby, and guests then connect to the host
over EOS P2P (`Callbreak.Networking.Eos.EosPeerNetwork`).

The plugin reads its config from `Assets/StreamingAssets/EOS/`. Those files hold the client
credentials, so they are **gitignored**, and every fresh clone repeats the steps below. Until the
credentials are complete, the game uses the PurrNet relay (development only) when the Boot
scene sets one, and has no online rooms otherwise. The Boot log says which.

## Values from the Developer Portal (not secret)

Organization "DayZero Games", product "Callbreak".

| Field | Value |
| --- | --- |
| Product Name | `Callbreak` |
| Product Id | `81a01d36db9d420986d4930c91aeb886` |
| Version | `1` |
| Sandbox | `Live`: `dbff24a9921d4336b5a7bfcad8578f96` |
| Deployment | `Live Deployment`: `e4ff8448a32343ada9516d729547ce2d` (sandbox Live) |
| Client | `Callbreak Game`, client policy `Callbreak Peer2Peer` (Peer2Peer type: Lobbies, Sessions etc., user required) |

The client ID and client secret are not written here or anywhere in the repository. They are in
the Developer Portal under Product Settings > Clients.

## Steps

1. In the Unity Editor, open **EOS Plugin > EOS Configuration**.
2. Under Product Configuration, enter the Product Name, Product Id and Version from the table.
   Then enter the Live sandbox and the Live Deployment (with its sandbox) under Production
   Environments.
3. In the **Clients** row (labelled "Client"), paste the client's **Client ID** and
   **Client Secret**. Leave the **Key** button alone: it holds a generated encryption key.
4. Click **Save All Changes**.
5. On each of the **Windows**, **Android** and **iOS** tabs, set **Deployment** to
   "Live Deployment" and **Client Credentials** to the client, then click **Save All Changes**
   again. (Windows is the Editor's tab.)
6. Check it with
   `Tools/Unity/playcheck.sh wait:10 "eval:return Callbreak.Editor.EosCheck.Start();" wait:40 "eval:return Callbreak.Editor.EosCheck.Report();"`.
   The report should say `verdict=PASS`, after signing in, listing a test code, finding this
   player as its owner, taking it down and finding it gone. It names no credential. EosCheck
   runs in Play mode only.

Steps 1, 2 and 5 can also be done from code, without the window, through the plugin's Config API
(paste into `Tools/Unity/ueval.sh '...'`). The credentials still come from step 3:

```csharp
var product = PlayEveryWare.EpicOnlineServices.Config.Get<PlayEveryWare.EpicOnlineServices.ProductConfig>();
product.ProductName = "Callbreak";
product.ProductId = new System.Guid("81a01d36db9d420986d4930c91aeb886");
product.ProductVersion = "1";
var deployment = new PlayEveryWare.EpicOnlineServices.Deployment
{
    SandboxId = PlayEveryWare.EpicOnlineServices.SandboxId.FromString("dbff24a9921d4336b5a7bfcad8578f96"),
    DeploymentId = new System.Guid("e4ff8448a32343ada9516d729547ce2d"),
};
if (product.Environments.Deployments.Count == 0) { product.Environments.AddDeployment(deployment); }
product.Write();
foreach (var platform in new[] { PlayEveryWare.EpicOnlineServices.PlatformManager.Platform.Windows, PlayEveryWare.EpicOnlineServices.PlatformManager.Platform.Android, PlayEveryWare.EpicOnlineServices.PlatformManager.Platform.iOS })
{
    PlayEveryWare.EpicOnlineServices.PlatformConfig config;
    if (PlayEveryWare.EpicOnlineServices.PlatformManager.TryGetConfig(platform, out config)) { config.deployment = deployment; config.Write(); }
}
UnityEditor.AssetDatabase.Refresh();
return "written";
```

## Which files, and why they stay local

| File in `Assets/StreamingAssets/EOS/` | Holds |
| --- | --- |
| `eos_product_config.json` | product, sandbox, deployment, and the client credentials (`Clients`) |
| `eos_windows_config.json`, `eos_android_config.json`, `eos_ios_config.json` | that platform's deployment and its own copy of the client credentials |

These files, their `.meta` files and `EOS.meta` are in `.gitignore`. They still ship inside every
build, because StreamingAssets is copied as it is: anyone who unpacks the app can read them (see
the plugin's `Documentation~/eos_config_security.md`). The client policy limits what they allow.

A build checks the config first: without it, the plugin's build validator stops an Android or iOS
build ("config file not found, Set EOS configuration").

## In the Editor

- EOS starts when Play mode starts (`ProjectLifetimeScope`), and the plugin shuts it down when
  Play mode ends, from its `EOSManager` object.
- Nothing may destroy that object while playing: a later Play session would then find the SDK
  still running.
- The scripting define `EOS_DO_NOT_UNLOAD_SDK_ON_SHUTDOWN` (Player Settings, on Android, iOS and
  Standalone) makes the plugin keep the SDK loaded between Play sessions and re-use it. Without
  it, the plugin unloads the SDK's DLL in the Editor, and a Play session that started after an
  unclean end once hung the Editor while doing so (2 Oct 2026). Players are not affected: Android
  and iOS never shut EOS down.

## Android builds

The plugin's own Android module (`eos_dependencies.androidlib`) is written for old Android
tools. Unity 6.3's Gradle 9.1 and AGP 9.0 reject it, and the build stops with
"Could not find method jcenter()" (2 Oct 2026). `Assets/_Project/Scripts/Editor/AndroidEosGradle.cs`
fits it to the generated Gradle project on every Android build, without touching the plugin's
files or the Player Settings:

- **The module's build file:**
  - drops its old `buildscript` block and `jcenter()`;
  - gets the namespace `com.pew.eos_dependencies`;
  - uses the SDK levels of the Player Settings;
  - loses the outdated `buildToolsVersion`.
- **The module's manifest** loses its `package=`.
- **The launcher** turns on core-library desugaring, because the EOS SDK's aar requires it
  (`desugar_jdk_libs`).
- **R8 rules** let a minified release build pass: annotations of the Tink library come in through
  androidx.security.

Each step does nothing once a plugin update fixes its files, and the hook can then go.
`Assets/EOS/link.xml` (EOS Plugin > Advanced > Create link.xml) keeps the plugin's config readers
from being stripped by IL2CPP. `Assets/Plugins/Android/gradleTemplate.properties` is the plugin's
Gradle properties template (AndroidX).
