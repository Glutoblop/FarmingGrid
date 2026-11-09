# Farming Grid

Farming Grid is a client-side Valheim plugin for spacing crops and saplings. Select a plant with the Cultivator and aim at the soil to see a 5 × 5 grid. The highlighted square is where the planting preview snaps.

- **Green** means the slot has room to grow.
- **Yellow** means the slot has enough room, with a small margin before the next plant or obstacle. You can still plant there.
- **Red** means the slot is too close to a plant or obstacle, or fails a placement check such as reach, biome, or cultivated soil.

Farming Grid checks clearance again when you plant. A spacing rejection uses Valheim's usual “Needs more space” message and leaves your seed and stamina intact. Grid spacing follows the selected plant's growth radius and collider footprint. The grid stays aligned to the world as you move, and the spacing can change when you select a different plant. Vanilla growth conditions such as sunlight and temperature still apply. Each player who wants the grid needs the plugin on their own client.

## Install

Select **Farming Grid** in the [Valheim Mod Installer](https://github.com/Glutoblop/ValheimPlugins/releases/latest/download/ValheimModInstaller.zip), or unzip [FarmingGrid.zip](https://github.com/Glutoblop/FarmingGrid/releases/latest/download/FarmingGrid.zip) into your Valheim directory with BepInEx installed.

To turn off the grid and spacing checks, set `Enabled = false` in `BepInEx/config/jordan.valheim.farminggrid.cfg`.

## Build and release

Run `dotnet build -c Release -p:GameDir="C:\path\to\Valheim"` with the game and BepInEx installed. The installer reads the DLL from `dist/`. Pass `-p:InstallToGame=true` to also copy the DLL to the local BepInEx plugins directory.

Run `release\release.ps1 -BuildOnly` to build the ZIP and DLL in `release/out/`. To publish the initial version, push `main` and run `python release/make_release.py`. For subsequent releases, use `release\Release.bat` or run `release\release.ps1 -Bump patch -Changes "Describe the changes"` from an up-to-date checkout.
