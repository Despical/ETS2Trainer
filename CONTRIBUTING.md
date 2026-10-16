# Contributing

Bug reports, translations and focused improvements are welcome. Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting started

1. Fork the repository and create a branch for your change.
2. Install the .NET 8 SDK on Windows.
3. Run `./build.ps1` from PowerShell.
4. Check the affected behavior in the desktop window and the game overlay.

Keep changes focused and follow the existing C# and XAML style. Repository files use UTF-8, four-space indentation and LF line endings; YAML and JSON use two spaces. Command scripts use CRLF.

## Translations

The interface supports Turkish and English. Turkish strings are the source keys; English translations are embedded in `src/ETS2Trainer/Localization/en.json`. Preserve format placeholders such as `{0}` and `{1}`, and keep labels short enough for the compact overlay.

## Game compatibility

Memory-related changes must validate the process, object types and fields before writing. Preserve the existing restoration behavior for damage coefficients. Explain the game build and evidence behind any compatibility change, and never include game executables, memory dumps or player profiles in a contribution.

## Pull requests

Describe the problem, the resulting behavior and how you checked it. Include screenshots for interface changes. Build artifacts and local development material are excluded from the repository.

Contributions are licensed under GPL-3.0-or-later, as described in [LICENSE](LICENSE).
