# Ship HoloAI

A RimWorld 1.6 (Odyssey) mod that gives your gravship a soul.

Inspired by the ship AI of *Andromeda*: the **HoloCore** is a gravship facility housing a
shipboard intelligence personified as a holographic avatar. She roams your ship, talks with
the crew, announces threats and ship status in her own voice, and survives launch and
landing tucked safely inside her core. Her look is built from vanilla pawn assets passed
through a custom hologram filter — luminance-ramped, edge-faded, scanlined, dissolving
into light particles at the hem.

## Personas

The default persona is **P.R.I.S.M.** — *Photonic Response Intelligence System Matrix* —
the unfailingly polite companion bundled with every hull, who seeks out crew near their
breaking point for a quiet word. Five more personas ship as **persona matrices**, sold by
exotic goods traders and swappable at the core via a codex-style selection screen (live
hologram portraits, manufacturer lore, and per-persona bios included). Each has a
shipboard aura and a signature ability she performs in person:

| Persona | Specialty | Signature ability |
|---|---|---|
| **V.E.S.T.A.** | Morale | Tucks sleeping crew in; the corridors smell faintly of bread |
| **H.E.R.M.E.S.** | Speed | Personally scrubs the decks — filth is just cargo nobody shipped |
| **A.T.H.E.N.A.** | Research | Impromptu skill seminars for idle crew, with verbal footnotes |
| **A.C.E.S.O.** | Medical | Emergency stabilization when no doctor responds |
| **I.X.I.A.** | Warden | Runs the brig in person; prison breaks ×4 rarer aboard |

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

An env-gated self-test harness (`HOLOAI_SELFTEST=1`, inert for players) asserts the
avatar lifecycle, persona installs, and every signature ability headlessly — see
`CLAUDE.md` for the QA workflow.

## History

This repo is the **2.0 full reconstruction** of the mod. The original attempt is
preserved on the `legacy/v1` branch.

## Dev crew

This project is developed with a crew of Claude Code subagents — see `.claude/agents/`.
