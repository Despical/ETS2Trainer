<p align="center">
  <img src="src/ETS2Trainer/Assets/Trainer.png" width="80" alt="ETS2Trainer icon">
</p>

<h1 align="center">ETS2Trainer</h1>
<p align="center">A portable Euro Truck Simulator 2 trainer for Windows, with an in-game overlay and English / Turkish support.</p>

<p align="center">
  <a href="https://github.com/Despical/ETS2Trainer/actions/workflows/build.yml"><img src="https://github.com/Despical/ETS2Trainer/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://github.com/Despical/ETS2Trainer/releases/latest"><img src="https://img.shields.io/badge/release-v0.9.0-blue" alt="Latest release: v0.9.0"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPLv3-blue" alt="GPLv3 license"></a>
  <img src="https://img.shields.io/badge/platform-Windows%20x64-0078D4" alt="Windows x64">
  <img src="https://img.shields.io/badge/languages-English%20%2F%20T%C3%BCrk%C3%A7e-F3B65A" alt="English and Turkish">
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Despical/ETS2Trainer/main/docs/screenshots/vehicle.png" width="1000" alt="English vehicle page with component repairs, cargo damage and protection controls">
  <br><sub>English interface preview · Example game values</sub>
</p>

<p align="center"><a href="#download-and-install">Download &amp; install</a> · <a href="#screenshots">Screenshots</a> · <a href="#automatic-startup-and-shutdown">Automatic startup &amp; shutdown</a></p>

## Download and install

### Setup — recommended

1. Open the [latest release](https://github.com/Despical/ETS2Trainer/releases/latest) and download **ETS2Trainer-...-Setup-x64.exe**.
2. Choose English or Turkish for the setup wizard, then select the installation folder. The default is `%LOCALAPPDATA%\Programs\ETS2Trainer` for your Windows account.
3. Choose whether to create a **desktop shortcut** and **start the trainer automatically when ETS2 launches**. Existing startup preferences are preselected when updating.
4. Finish Setup. Open the trainer from the desktop shortcut or Start menu, or let it start the next time you launch ETS2 if you selected automatic startup.
5. Start ETS2 normally, load your profile and enter driving mode.

Setup includes the .NET runtime, creates a Start menu entry and registers an uninstaller under **Windows Settings → Apps → Installed apps → ETS2Trainer**. It does not request administrator privileges; choose a folder your account can write to. Automatic startup can be changed later in the trainer's Settings.

### Portable ZIP

1. Open the [latest release](https://github.com/Despical/ETS2Trainer/releases/latest).
2. Download the **ETS2Trainer-...-win-x64.zip** asset and extract the complete archive to a folder of your choice.
3. Run **ETS2Trainer.exe** or **Start Trainer.cmd**.
4. Start ETS2, load your profile and enter driving mode. The connection status updates automatically.
5. Optional: in **Settings → Automatic startup & shutdown**, enable **Start the trainer when ETS2 launches**, **Close the trainer when ETS2 exits**, or both. Each choice is saved immediately.

Both downloads include the .NET runtime. No separate .NET installation or game plugin is required. SHA-256 checksums for Setup and the portable ZIP are included with each release.

## Features

| Page | Available controls |
| --- | --- |
| Vehicle | View fuel, truck, trailer and cargo damage; repair everything or individual components; fill the tank; prevent new damage and wear; enable unlimited fuel. |
| Profile | View and change money and total XP, and see the current level when supported by the game build. |
| World | View the game clock, advance to a custom time, use day/night presets, and prevent driver fatigue. |
| History | Review the latest 100 connection and action entries from the current session. |
| Debug | Enable developer mode, the console and free camera; select a profile and capture a new free camera key. |
| Settings | Select the language, change the menu key, choose automatic startup and shutdown with ETS2, open the preferences folder and quit the trainer. |

Component repairs cover the engine, transmission, cabin, chassis, wheels and attached trailer. **Repair everything** also repairs cargo when the active loaded trailer can be verified. Existing damage is preserved when damage prevention is enabled; use a repair action to remove it.

The **Debug** page reads ETS2's user folder under Documents; use **Choose folder** for a custom location. Developer mode and console settings apply globally. Select the intended local or Steam profile to change its free camera binding. Click the camera key button and press one key; Esc cancels. Keys already used by another game control or the trainer menu are rejected. The default camera key is the top-row **0**.

Close ETS2 completely before saving debug settings, then start it again. The page displays this restart requirement and disables saving while ETS2 is running, since the game can overwrite configuration files on exit. If automatic shutdown closes the trainer too, reopen the trainer manually to save. Changed files receive timestamped `.ets2trainer-*.bak` backups beside the originals. No savegame is edited. See SCS's [console documentation](https://modding.scssoft.com/wiki/Documentation/Engine/Console) and [input configuration documentation](https://modding.scssoft.com/wiki/Documentation/Engine/Advanced_input_configuration).

## Screenshots

English interface previews. The displayed game values and history entries are examples.

<table>
  <tr>
    <td width="50%"><strong>Profile</strong><br>Money, total XP and current level.<br><a href="docs/screenshots/profile.png"><img src="https://raw.githubusercontent.com/Despical/ETS2Trainer/main/docs/screenshots/profile.png" alt="English Profile page with balance, total experience and level"></a></td>
    <td width="50%"><strong>World</strong><br>Game time, day/night presets and fatigue control.<br><a href="docs/screenshots/world.png"><img src="https://raw.githubusercontent.com/Despical/ETS2Trainer/main/docs/screenshots/world.png" alt="English World page with time presets and fatigue prevention"></a></td>
  </tr>
  <tr>
    <td width="50%"><strong>Settings</strong><br>Language and saved startup/shutdown choices.<br><a href="docs/screenshots/settings.png"><img src="https://raw.githubusercontent.com/Despical/ETS2Trainer/main/docs/screenshots/settings.png" alt="English Settings page showing independent automatic startup and shutdown toggles"></a></td>
    <td width="50%"><strong>History</strong><br>Connection changes and recent actions.<br><a href="docs/screenshots/history.png"><img src="https://raw.githubusercontent.com/Despical/ETS2Trainer/main/docs/screenshots/history.png" alt="English History page with recent connection and action entries"></a></td>
  </tr>
</table>

## In-game menu

- Press **Insert** to show or hide the menu. **Esc** returns to the game.
- Change the key in **Settings → In-game menu key**: click the key button, then press the key you want. Esc cancels key selection.
- Use **borderless or windowed mode** for the overlay.
- Hiding the menu keeps continuous features running. Use **Settings → Quit trainer** to close the trainer. If automatic startup is enabled, the hidden watcher remains ready for the next game session.

The overlay follows the game window and display scaling. The menu key is registered while the game or its open menu is active. A key already registered by another application is rejected, and the previous binding is retained.

## Automatic startup and shutdown

Both options are in **Settings → Automatic startup & shutdown**. They work independently, are off by default and are remembered across trainer restarts.

### Start the trainer when ETS2 launches

Enable **Start the trainer when ETS2 launches** once. This registers the same executable in your current Windows user's startup entry and starts it in hidden watcher mode (`--watch-game`). The watcher also starts when you next sign in to Windows, so you do not have to open the menu first. No administrator privileges or service installation are needed.

**You or Steam start ETS2 as usual.** The watcher checks Windows' running processes approximately every 1.5 seconds for **`eurotrucks2.exe`**, using its process ID and start time to identify each game session. It starts the trainer once for a new session. The trainer does not start ETS2, require a special game shortcut or change Steam launch options.

While ETS2 is closed, the watcher stays hidden and does not attach to the game or enable protections. If you quit the trainer while the game is still running, it stays closed until the next game launch. You can still start it manually.

Turning this option off stops the watcher and removes its Windows startup entry. Windows must also allow the entry under **Settings → Apps → Startup**; if you disable it there, enable it again in Windows as well.

Keep the extracted application folder in a permanent location. If you move it, open **ETS2Trainer.exe** from the new location once so the saved startup path can be updated.

### Close the trainer when ETS2 exits

Enable **Close the trainer when ETS2 exits** to quit normally after the game process closes. It works for both automatic and manual trainer launches. Returning to ETS2's main menu, switching profiles, pausing or minimizing the game keeps the trainer open.

If you open the trainer while ETS2 is closed, it stays available until you start and then exit a game session. Turning this option off leaves the trainer open after game exit. When automatic startup is enabled, its separate hidden watcher keeps running after the menu closes so it can handle the next game launch.

## Languages and saved preferences

The interface supports **Türkçe** and **English**. Select either language in **Settings → Language**; labels, descriptions, status messages and history update immediately. The initial language is Turkish.

These choices are remembered between launches:

- Damage prevention, unlimited fuel and fatigue prevention.
- Always on top for the desktop window.
- Automatic startup when ETS2 launches and automatic shutdown when it exits.
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
| `interface.json` | Always-on-top, automatic-startup and automatic-shutdown preferences. |
| `language.json` | Selected language. |

If automatic startup encounters a problem, `startup.log` in the same folder contains the latest error.

To reset preferences, turn off automatic startup, quit the trainer and remove the relevant file or this folder. A new launch uses the defaults.

## Compatibility

- 64-bit Windows.
- Euro Truck Simulator 2, Steam x64 build **1.61.1.9** is the currently verified game build.
- New game updates may require compatibility changes. Unverified fields remain unavailable and unknown values appear as `—`.
- Advancing the clock can affect active job deadlines. Money and XP changes become persistent when the game saves.

## Update

**Setup installation:** run the newer Setup EXE. It closes the trainer and watcher normally, updates the application and preserves your saved preferences. Review the shortcut and automatic-startup options before installing. If an older portable overlay cannot be closed automatically, quit it through **Settings → Quit trainer** and retry Setup.

**Portable installation:** if automatic startup is enabled, turn it off first to release the application file. Quit through **Settings → Quit trainer**, extract the new ZIP to the application folder and start it again. Re-enable automatic startup if desired. Other preferences are stored separately in Local AppData and are retained when replacing the application files.

To move from portable to Setup, run Setup and use its new shortcut or Start menu entry afterward. Your existing preferences are reused. Once the new installation works, the old portable folder can be removed.

## Uninstall

**Setup installation:** open **Windows Settings → Apps → Installed apps → ETS2Trainer → Uninstall**, or run `unins000.exe` in the installation folder. The uninstaller closes the trainer and watcher normally, removes its startup entry and shortcuts, and deletes the installed application files. Saved preferences are retained; automatic startup is switched off. Unrelated files you added to the installation folder are retained.

**Portable installation:**

1. Turn off **Settings → Automatic startup & shutdown → Start the trainer when ETS2 launches** to stop the background watcher and remove its Windows startup entry.
2. Quit the trainer through **Settings → Quit trainer**.
3. Delete the folder where you extracted the application.
4. To remove saved preferences, also delete `%LOCALAPPDATA%\ETS2Trainer`.
5. If present, the application's extracted runtime cache under `%TEMP%\.net\ETS2Trainer` can also be removed after exit.

For either edition, delete `%LOCALAPPDATA%\ETS2Trainer` separately if you also want to remove saved preferences. No game plugin or service needs to be uninstalled. Changes already saved into your ETS2 profile, such as money or XP, remain in the game save.

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

Build the setup wizard from the published folder:

```powershell
./installer/build.ps1 -PublishDirectory ./artifacts/publish -OutputDirectory ./artifacts
```

The installer build downloads the pinned official **Inno Setup 7.1.0** compiler, verifies its SHA-256 and publisher signature, and extracts it in portable mode under `artifacts/build-tools/`. To use an existing compiler, pass `-CompilerPath` with the path to `ISCC.exe`.

The GitHub Actions workflow builds both Windows x64 downloads and generates their SHA-256 checksums for pushes and pull requests. The files are available in the workflow run's artifacts.

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

Copyright (C) 2026 Despical. Licensed under **GNU GPL v3.0 or later**; see [LICENSE](LICENSE). Bundled Microsoft runtime components retain their own licenses and notices in [licenses/](licenses/). The setup wizard is built with [Inno Setup](https://jrsoftware.org/isinfo.php); its copyright notices are retained.

Euro Truck Simulator 2 is a product of SCS Software. This project is independent and is not affiliated with SCS Software.
