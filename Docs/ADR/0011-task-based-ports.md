# ADR 011 - Task-based application ports

**Status:** proposed.

**Context.** GDD §16 and the Agent Handbook show ports returning Unity's `Awaitable`. That ties
`Callbreak.Application` to UnityEngine, so its sessions, bot driver and flow could only be tested
inside the Editor.

**Decision.** Ports and services in `Callbreak.Application` return `System.Threading.Tasks.Task`
and take `CancellationToken`s; only `Application/Config` (ScriptableObjects) touches UnityEngine.
The dotnet harness (`Tools/DotnetTests`) compiles and tests the rest outside Unity. Unity code
awaits these tasks normally; continuations run on Unity's synchronisation context.

**Consequences.** Faster, Editor-free tests for everything above the rules engine. Adapters
that wrap Unity async APIs convert with `Awaitable` → `Task` at the edge.
