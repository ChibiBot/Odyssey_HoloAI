# Ship HoloAI

A RimWorld 1.6 (Odyssey) mod that gives your gravship a soul.

Inspired by the ship AI of *Andromeda*: the **HoloCore** is a gravship facility housing a
shipboard intelligence personified as a holographic avatar. She roams your ship, talks with
the crew, boosts morale, and announces threats and ship status in her own voice.

The default persona is **P.R.I.S.M.** — *Photonic Response Intelligence System Matrix*.
Future versions will add additional HoloCore personas acquirable through trade and
swappable in the core.

## Requirements

- RimWorld 1.6 with the **Odyssey** expansion
- [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077)

## Building

```sh
dotnet build Source/ShipHoloAI/ShipHoloAI.csproj -c Release
```

The DLL is emitted directly into `1.6/Assemblies/`. The repo root *is* the mod root —
symlink it into the game's `Mods/` folder to play:

```sh
ln -s /path/to/this/repo "<RimWorld>/Mods/ShipHoloAI"
```

## Dev crew

This project is developed with a crew of Claude Code subagents — see `.claude/agents/`.
