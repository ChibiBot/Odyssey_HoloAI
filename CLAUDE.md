# Ship HoloAI — project guide

RimWorld 1.6 (Odyssey) mod. The repo root **is** the mod root; it is symlinked into the
game's `Mods/` folder as `ShipHoloAI`. The game only loads `About/`, `1.6/`, `Textures/`,
`Languages/` — `Source/`, `.claude/`, docs are invisible to it.

## Conventions

- **packageId**: `chibi.shipholoai`
- **Def prefix**: `HoloAI_` on every defName
- **C# namespace**: `ShipHoloAI` (assembly `ShipHoloAI.dll`)
- **Persona**: P.R.I.S.M. (Photonic Response Intelligence System Matrix) — calm, warm,
  lightly formal ship AI; speaks in first person about "my hull", "my corridors".

## Paths (this machine)

- Game: `/mnt/ai/SteamLibrary/steamapps/common/RimWorld` (v1.6.4871, all DLCs)
- Vanilla defs to crib from: `<game>/Data/*/Defs` — gravship defs in
  `Data/Odyssey/Defs/ThingDefs_Buildings/Buildings_Gravship.xml` (`PilotSubpersonaCore`
  is our template; `GravEngine` is the facility hub we patch)
- Managed DLLs: `<game>/RimWorldLinux_Data/Managed/`
- Harmony DLL: `/mnt/ai/SteamLibrary/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll`
- Game config + logs: `~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/`
  (`Config/ModsConfig.xml`, `Player.log`)

## Build & test

```sh
dotnet build Source/ShipHoloAI/ShipHoloAI.csproj -c Release   # → 1.6/Assemblies/ShipHoloAI.dll
```

- net472 offline build: `FrameworkPathOverride=/usr/lib/mono/4.7.2-api` (no NuGet needed;
  Krafs.Rimworld.Ref is the CI alternative if ever needed).
- Smoke test: launch `<game>/RimWorldLinux -quicktest`, then
  `grep -iE "error|exception|HoloAI" Player.log`. Kill the game after the map loads.
- Gravship testing needs a dev-mode Odyssey start (god-mode place `GravEngine`; it
  generates its own substructure footprint).
- Decompile vanilla for API signatures with `ilspycmd` into `Decompiled/` (gitignored).

## Architecture notes (the load-bearing decisions)

- The avatar is a **ToolUser** (mechanoid-pattern) pawn, NOT Humanlike — Humanlike drags
  in backstories, needs, colonist bar, and the humanlike render tree. Consequence: no
  `Pawn_InteractionsTracker`; all social interactions are fired **manually** from C#
  (`PlayLogEntry_Interaction` + `MoteMaker.MakeInteractionBubble` + `TryGainMemory`).
- `Building_HoloCore` is an `IThingHolder`; the avatar pawn lives in its `ThingOwner`
  whenever unprojected. `DeSpawn` always stores the avatar first — that is how she
  survives gravship launch/landing and how save/load stays consistent.
- Mood from chats is a **plain `Thought_Memory`**, not a social thought — social opinion
  math about a ToolUser breaks things.
- Thought/interaction defs must resolve grammar with a named pawn: P.R.I.S.M. gets a
  `NameSingle` at generation.

## Dev crew

Seven subagent personas live in `.claude/agents/` (Hikari=lead, Akari=C#, Niji=XML defs,
Ruri=art, Hoshimi=QA, Kotoha=writing, Tsumugi=build). Delegate matching work to them by
name when the user asks for crew involvement.
