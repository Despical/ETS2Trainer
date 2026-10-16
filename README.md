<p align="center">
  <img src="src/ETS2Trainer/Assets/Trainer.png" width="80" alt="ETS2Trainer icon">
</p>

<h1 align="center">ETS2Trainer</h1>
<p align="center">A portable Euro Truck Simulator 2 trainer for Windows, with an in-game overlay and English / Turkish support.</p>

<p align="center">
  <a href="https://github.com/Despical/ETS2Trainer/actions/workflows/build.yml"><img src="https://github.com/Despical/ETS2Trainer/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://github.com/Despical/ETS2Trainer/releases/latest"><img src="https://img.shields.io/github/v/release/Despical/ETS2Trainer" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPLv3-blue" alt="GPLv3 license"></a>
  <img src="https://img.shields.io/badge/platform-Windows%20x64-0078D4" alt="Windows x64">
  <img src="https://img.shields.io/badge/languages-English%20%2F%20T%C3%BCrk%C3%A7e-F3B65A" alt="English and Turkish">
</p>

## Download and install

1. Open the [latest release](https://github.com/Despical/ETS2Trainer/releases/latest).
2. Download the **ETS2Trainer-...-win-x64.zip** asset and extract the complete archive to a folder of your choice.
3. Run **ETS2Trainer.exe** or **Start Trainer.cmd**.
4. Start ETS2, load your profile and enter driving mode. The connection status updates automatically.

The release includes the .NET runtime. No separate .NET installation, installer or game plugin is required. SHA-256 checksums are included with each release.

## Features

| Page | Available controls |
| --- | --- |
| Vehicle | View fuel, truck, trailer and cargo damage; repair everything or individual components; fill the tank; prevent new damage and wear; enable unlimited fuel. |
| Profile | View and change money and total XP, and see the current level when supported by the game build. |
| World | View the game clock, advance to a custom time, use day/night presets, and prevent driver fatigue. |
| History | Review the latest 100 connection and action entries from the current session. |
| Settings | Select the language, change the menu key, enable automatic startup with ETS2, open the preferences folder and quit the trainer. |

Component repairs cover the engine, transmission, cabin, chassis, wheels and attached trailer. **Repair everything** also repairs cargo when the active loaded trailer can be verified. Existing damage is preserved when damage prevention is enabled; use a repair action to remove it.

## In-game menu

- Press **Insert** to show or hide the menu. **Esc** returns to the game.
- Change the key in **Settings → In-game menu key**: click the key button, then press the key you want. Esc cancels key selection.
- Use **borderless or windowed mode** for the overlay.
- Hiding the menu keeps continuous features running. Use **Settings → Quit trainer** to exit completely.

The overlay follows the game window and display scaling. The menu key is registered while the game or its open menu is active. A key already registered by another application is rejected, and the previous binding is retained.

## Start automatically with ETS2

Enable **Settings → Automatic startup → Start the trainer when ETS2 launches**. The choice is saved immediately and the background watcher starts right away. It also starts when you next sign in to Windows, so you do not have to open the trainer first.

The watcher checks for the game approximately every 1.5 seconds and opens one trainer window per game session. It does not attach to the game or enable protections itself. While ETS2 is closed, it stays hidden. If you quit the trainer while the game is still running, it stays closed until the next game launch. Starting the trainer manually remains available.

Turn the same option off to stop the watcher and remove its startup registration. Automatic startup is off by default. It uses your current Windows user's startup entry, requires no administrator privileges and installs no service. Windows must allow the entry under **Settings → Apps → Startup**; if you disable it there, enable it again in Windows as well.

Keep the extracted application folder in a permanent location. If you move it, open **ETS2Trainer.exe** from the new location once so the saved startup path can be updated.

## Languages and saved preferences

The interface supports **Türkçe** and **English**. Select either language in **Settings → Language**; labels, descriptions, status messages and history update immediately. The initial language is Turkish.

These choices are remembered between launches:

- Damage prevention, unlimited fuel and fatigue prevention.
- Always on top for the desktop window.
- Automatic startup when ETS2 launches.
- Menu hotkey and interface language.

Saved protections are applied automatically when the required game state is available. Closing the trainer restores the original damage coefficients while retaining your preference for the next launch.

Open **Settings → Settings files → Open settings folder** to access:

```text
%LOCALAPPDATA%\ETS2Trainer
```

| File | Contents |
| --- | --- |
| `settings.json` | Menu hotkey. |
| `protection.json` | Damage, fuel and fatigue preferences. |
| `interface.json` | Always-on-top and automatic-startup preferences. |
| `language.json` | Selected language. |

If automatic startup encounters a problem, `startup.log` in the same folder contains the latest error.

To reset preferences, turn off automatic startup, quit the trainer and remove the relevant file or this folder. A new launch uses the defaults.

## Compatibility

- 64-bit Windows.
- Euro Truck Simulator 2, Steam x64 build **1.61.1.9** is the currently verified game build.
- New game updates may require compatibility changes. Unverified fields remain unavailable and unknown values appear as `—`.
- Advancing the clock can affect active job deadlines. Money and XP changes become persistent when the game saves.

## Update

If automatic startup is enabled, turn it off first to release the application file. Quit the trainer through **Settings → Quit trainer**, extract the new release to the application folder and start it again. Re-enable automatic startup if desired. Other preferences are stored separately in Local AppData and are retained when replacing the application files.

## Uninstall

1. Turn off **Settings → Automatic startup** to stop the background watcher and remove its Windows startup entry.
2. Quit the trainer through **Settings → Quit trainer**.
3. Delete the folder where you extracted the application.
4. To remove saved preferences, also delete `%LOCALAPPDATA%\ETS2Trainer`.
5. If present, the application's extracted runtime cache under `%TEMP%\.net\ETS2Trainer` can also be removed after exit.

No game plugin or service needs to be uninstalled. Changes already saved into your ETS2 profile, such as money or XP, remain in the game save.

## Build from source

Install the **.NET 8 SDK** on Windows, then run:

```powershell
git clone https://github.com/Despical/ETS2Trainer.git
cd ETS2Trainer
./build.ps1
```

The portable output is written to `dist/`. To choose another output folder:

```powershell
./build.ps1 -OutputDirectory ./artifacts/publish
```

The GitHub Actions workflow builds and packages Windows x64 downloads for pushes and pull requests. Its package is available in the workflow run's artifacts.

## Troubleshooting

- **Waiting for ETS2:** load a profile and enter driving mode. Only one ETS2 process should be running.
- **A feature is unavailable:** read its tooltip for the current reason. Confirm the game build matches the supported version.
- **The overlay minimizes the game:** switch ETS2 to borderless or windowed mode.
- **The menu key is unavailable:** select another key in Settings. F12, Windows keys and modifier combinations are not accepted.
- **A preference does not return after restart:** check the result message and that the settings folder is writable.
- **Automatic startup does not run:** check the option in the trainer, the Windows Startup apps setting, and `startup.log` in the preferences folder. Open the trainer once after moving its folder.

For problems or suggestions, use the [issue templates](https://github.com/Despical/ETS2Trainer/issues/new/choose). Include the trainer version, ETS2 version, display mode and steps to reproduce.

## Contributing and support

See [CONTRIBUTING.md](CONTRIBUTING.md), [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) and [SECURITY.md](SECURITY.md).

If you would like to support development: [GitHub Sponsors](https://github.com/sponsors/Despical), [Buy Me a Coffee](https://buymeacoffee.com/despical) or [Patreon](https://www.patreon.com/despical).

## License

Copyright (C) 2026 Despical. Licensed under **GNU GPL v3.0 or later**; see [LICENSE](LICENSE). Bundled Microsoft runtime components retain their own licenses and notices in [licenses/](licenses/).

Euro Truck Simulator 2 is a product of SCS Software. This project is independent and is not affiliated with SCS Software.
