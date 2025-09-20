# Odyssey HoloAI

A RimWorld: Odyssey addon that projects an untouchable holographic assistant inside your gravship. The AI can treat injuries, haul, research and socialise with the crew while remaining tethered to the ship interior.

## Features

- **Immune to harm** – the hologram ignores damage, cannot be downed and vanishes instead of dying.
- **Gravship-bound** – a dedicated map component scans player gravships and constrains the AI to tiles that you tag as gravship floors.
- **Automatic spawn** – once a valid anchor (bridge, computer, etc.) is detected the assistant will materialise within range.
- **Work ready** – generated as a colonist with strong Medical, Intellectual and Social skill levels to help the crew.

## Configuration

Open the mod settings and list the terrain and building defNames that define your gravship interior. By default the mod targets the vanilla Odyssey `Odyssey_Gravship*` defs. If you use custom tiles, add them (one per line) to the settings text boxes. Only terrain on the list counts as valid for the hologram to stand on.

You can also list the anchor buildings (projectors, bridges, computers) that should seed the gravship area. The area extends across all contiguous allowed tiles that touch any listed building.

## Source

The `Source/` directory contains a C# project targeting `net472`.

### Building

1. Install the .NET SDK (6.0 or newer works well for building `net472` projects).
2. Point the build at your RimWorld managed assemblies. You can either:
   - set the `RIMWORLD_MANAGED_PATH` environment variable to the game's `*Data/Managed` directory (for example `RimWorldWin64_Data/Managed`, `RimWorldWin_Data/Managed`, or `RimWorldLinux_Data/Managed`), or
   - pass the `RimWorldManagedPath` MSBuild property (e.g. `dotnet build Source/Odyssey_HoloAI/Odyssey_HoloAI.csproj -p:RimWorldManagedPath="/path/to/RimWorldWin64_Data/Managed"`).

   When the mod folder is placed alongside the game installation, the project also attempts to discover these directories automatically.
3. Build the project:

   ```bash
   dotnet build Source/Odyssey_HoloAI/Odyssey_HoloAI.csproj
   ```

The compiled assembly is written to `Source/Assemblies/`. Copy the resulting DLL into the mod's `Assemblies/` folder when packaging a release.
