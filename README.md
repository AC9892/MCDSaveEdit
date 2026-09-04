# MCDSaveEdit Community Fork

[![GitHub](https://img.shields.io/github/license/cutflame/mcdsaveedit)](https://github.com/CutFlame/MCDSaveEdit/blob/master/LICENSE)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/cutflame/mcdsaveedit?label=latest)](https://github.com/CutFlame/MCDSaveEdit/releases/latest)
[![GitHub Release Date](https://img.shields.io/github/release-date/cutflame/mcdsaveedit)](https://github.com/CutFlame/MCDSaveEdit/releases/latest)
[![GitHub all releases](https://img.shields.io/github/downloads/cutflame/mcdsaveedit/total)](https://github.com/CutFlame/MCDSaveEdit/releases)
[![License](https://img.shields.io/github/license/AC9892/MCDSaveEdit)](LICENSE)
[![Latest release](https://img.shields.io/github/v/release/AC9892/MCDSaveEdit?label=latest)](https://github.com/AC9892/MCDSaveEdit/releases/latest)

MCDSaveEdit Community is an unofficial, maintained fork of Michael Holt's (CutFlame) Minecraft: Dungeons Save File Editor. It remains a Windows desktop editor and preserves the original save parsing and editing approach.

The original project and its contributors are credited at [CutFlame/MCDSaveEdit](https://github.com/CutFlame/MCDSaveEdit). This fork is maintained at [AC9892/MCDSaveEdit](https://github.com/AC9892/MCDSaveEdit). It is not affiliated with Mojang Studios or Microsoft.

> **Back up your saves.** Editing can produce a save the game cannot load. Community 1.6 automatically creates timestamped backups before replacing existing files, but keeping a separate copy of the whole save directory is still recommended.
![MCDSaveEdit screenshot]()


## Features

- Opens encrypted Minecraft Dungeons character `.dat` saves and supported decrypted JSON saves.
- Edits inventory, storage chest, equipment, currencies, enchantments, passives, and character statistics.
- Uses installed game `.pak` files for names and images, with bundled placeholders when game content is unavailable.
- Detects Steam, Minecraft Launcher, Xbox app/Microsoft Store, and common Steam Deck/Proton installations without scanning whole drives.
- Creates timestamped backups and replaces existing saves atomically.
- Preserves unrecognized root-level save fields during JSON round trips.
- Validates save structure, currencies, items, power, enchantments, passives, and duplicate indexes before writing.
- Provides persistent backup/validation settings and a metadata-rich backup browser.
- Tracks unsaved changes and prompts before opening another save, restoring, or closing.
- Includes persistent Light, Dark, and System themes, image-cache controls, local diagnostic logs, and a privacy-safe diagnostic report.

## Installation and supported platforms

Download a ZIP from this fork's [releases page](https://github.com/AC9892/MCDSaveEdit/releases), extract it, and run `MCDSaveEdit.exe`.

- Supported: Windows 10 and Windows 11, x64, with .NET Framework 4.8.
- Community-supported: Steam Deck/Linux through Wine or Proton; see [STEAMDECK.md](STEAMDECK.md).
- Console/container saves may need extraction or decryption outside this application and are not guaranteed to work.

## Game file detection

Full names and images require readable Minecraft Dungeons `.pak` files. Known locations include:

- Minecraft Launcher: `%LOCALAPPDATA%\Mojang\products\dungeons\dungeons\Dungeons\Content\Paks`
- Steam: `%PROGRAMFILES(X86)%\Steam\steamapps\common\MinecraftDungeons\Dungeons\Content\Paks`
- Xbox app / Microsoft Store: `C:\XboxGames\Minecraft Dungeons\Content\Dungeons\Content\Paks`

If automatic detection fails, select the `Paks` directory when prompted. Choosing no game content keeps basic save editing available with fallback images.

You can change or rescan the location later under **Settings > Game Files**. The selected path is validated and saved per Windows user. The detector checks a bounded list of known layouts and Steam libraries; it does not recursively scan an entire drive.

## Save formats

Minecraft Dungeons character files are normally encrypted `.dat` files. The editor decrypts a selected character in memory, edits its structured JSON data, and encrypts it again when writing a normal character save. It can also open already-decrypted JSON saves used for inspection or recovery. File contents are detected instead of trusting the extension alone, so decrypted JSON may still use `.dat`. Plain text, unrelated binary data, malformed JSON, and unsupported encrypted data are rejected without modifying the source.

## Usage

1. Copy your save directory somewhere safe. Character saves normally live below `%USERPROFILE%\Saved Games\Mojang Studios\Dungeons`.
2. Choose **File > Open** and select a character `.dat` file.
3. Edit the character and choose **File > Save** or **Save As**.
4. Existing targets are backed up under a sibling `Backups` directory before replacement. Use **File > Restore Backup** to browse them; the current save is backed up again before restoration.
5. Use **Settings** to configure backup retention, validation, theme, game files, and local logging, or **Tools > Validate Current Save** to review and copy a report without saving.

## Building

Requirements:

- Windows 10/11
- Visual Studio Build Tools 2019 or Visual Studio 2019/2022 with **.NET desktop build tools** and the .NET Framework 4.8 targeting pack
- NuGet CLI or Visual Studio package restore

```powershell
git clone --recurse-submodules https://github.com/AC9892/MCDSaveEdit.git
cd MCDSaveEdit
nuget restore MCDSaveEdit.sln
Copy-Item MCDSaveEdit/Data/Secrets.example.cs MCDSaveEdit/Data/Secrets.cs
.\build.ps1 -Configuration Debug
```

`Secrets.cs` is ignored by Git. Add a valid Minecraft Dungeons `.pak` AES key only for local game-content testing. Never commit keys. Save encryption does not use that game-content key.

## Development and submodules

The solution is currently .NET Framework 4.8 and uses legacy `packages.config`. Migration is staged to avoid breaking WPF, save encryption, and `.pak` extraction. See [docs/MODERNIZATION_PLAN.md](docs/MODERNIZATION_PLAN.md) and [CONTRIBUTING.md](CONTRIBUTING.md).

The repository pins compatible branches of:

- [DungeonTools](https://github.com/CutFlame/DungeonTools/tree/save-file-editor-1.1), used for save encryption/decryption.
- [PakReader](https://github.com/CutFlame/PakReader/tree/MCDSaveEdit), used for Unreal `.pak` content.

Initialize them with `git submodule update --init --recursive`; do not replace their pinned revisions without compatibility testing.

## Known limitations

- Raw tree editing, undo/redo, item import/export, and broader nested unknown-field preservation remain 1.7 roadmap work.
- Validation intentionally avoids altering unknown fields and only performs strict ID checks when game data is loaded.
- Unknown fields are currently preserved at the save root; nested model types still need an extension-data audit.
- Game-content integration tests need legally obtained local game files and are skipped in CI.
- The legacy .NET Framework/package format and pinned SkiaSharp/System.Text.Json versions remain until a separately tested SDK-style migration; see [docs/DEPENDENCY_AUDIT.md](docs/DEPENDENCY_AUDIT.md).

## Credits and license

MCDSaveEdit was created by Michael Holt ([CutFlame](https://github.com/CutFlame)). This community fork does not claim original authorship. Existing contributors, dependency authors, and runtime-extracted Mojang assets retain their respective attribution and licenses.

Licensed under the [MIT License](LICENSE). Minecraft is a trademark of Mojang Synergies AB. See the original project for its full historical credits.
