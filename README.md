# AtO-TimeScale

[![CI](https://github.com/HawkeyePierce89/AtO-TimeScale/actions/workflows/ci.yml/badge.svg)](https://github.com/HawkeyePierce89/AtO-TimeScale/actions/workflows/ci.yml)

A BepInEx 5 plugin for Across the Obelisk (v1.7.7.1, Unity 2022.3, Mono). It speeds the whole game up (or slows it down) by changing Unity's `Time.timeScale` with hotkeys, and shows the current multiplier in a small overlay.

## What it does

- The Faster and Slower hotkeys step through a configurable list of multipliers (`1, 1.5, 2, 3, 4, 5, 8` by default). The Reset hotkey returns to x1.0. Every change is written to the BepInEx log.
- The overlay shows the current multiplier rounded to one decimal place, for example `x2.0` (a step of `0.25` shows as `x0.3`, but 0.25 is what gets applied). By default it is hidden at normal speed.
- Nothing in the game is patched for this: the plugin only sets `Time.timeScale`. If something else in the game resets `Time.timeScale`, the plugin puts its value back on the next frame and logs a warning the first time.
- Turning `Enabled` off returns the game to normal speed immediately; turning it back on restores the chosen multiplier. While it is off, the hotkeys are ignored, the overlay is hidden, and the plugin no longer puts `Time.timeScale` back if the game changes it.
- An optional Ultrafast mode (off by default) forces the game's own combat speed setting to Ultrafast. See [Ultrafast](#ultrafast).

## Hotkeys

| Action | Default | Config key |
|--------|---------|------------|
| Slower (previous step) | `F6` | `Hotkeys.Slower` |
| Faster (next step) | `F7` | `Hotkeys.Faster` |
| Reset to x1.0 | `F9` | `Hotkeys.Reset` |

The defaults avoid keys that are already taken: `F8` is the AtO-ShufflePeek overlay toggle, `F5` belongs to BepInEx ConfigurationManager, and `F1`/`F12` are used by the game's developer mode. At either end of the step list a press does nothing.

## Configuration

The config file `BepInEx/config/hawkeyepierce89.ato.timescale.cfg` is created on first launch.

| Section   | Key                 | Default                 | Description |
|-----------|---------------------|-------------------------|-------------|
| `General` | `Enabled`           | `true`                  | Apply the chosen time scale. When off, the game runs at normal speed. |
| `General` | `Steps`             | `1, 1.5, 2, 3, 4, 5, 8` | Multipliers the Faster/Slower hotkeys step through. |
| `General` | `StartupScale`      | `1`                     | Time scale applied at game start, from 0.25 to 20. |
| `General` | `RememberLastScale` | `false`                 | Start with `LastScale` instead of `StartupScale`. |
| `General` | `LastScale`         | `1`                     | Last applied time scale. Written by the plugin. |
| `General` | `EnableUltrafast`   | `false`                 | Force the game's combat speed setting to Ultrafast. |
| `Overlay` | `PositionX`         | `20`                    | Left edge of the overlay, in screen pixels. |
| `Overlay` | `PositionY`         | `200`                   | Top edge of the overlay, in screen pixels. Below the AtO-ShufflePeek overlay at 20,20. |
| `Overlay` | `FontSize`          | `18`                    | Font size, from 10 to 48. |
| `Overlay` | `HideAtNormalSpeed` | `true`                  | Hide the overlay while the time scale is x1.0. |
| `Hotkeys` | `Slower`            | `F6`                    | Step to the previous (slower) time scale. |
| `Hotkeys` | `Faster`            | `F7`                    | Step to the next (faster) time scale. |
| `Hotkeys` | `Reset`             | `F9`                    | Reset the time scale to x1.0. |

Changes made in-game take effect immediately, for example through BepInEx ConfigurationManager. BepInEx 5 does not watch the `.cfg` file, so edits made to the file directly while the game is running are picked up on the next launch.

### Steps

`Steps` is a comma-separated list of numbers with a `.` decimal point, whatever the system locale:

- Spaces and empty entries are ignored, and so are entries that are not finite numbers.
- Every value is clamped to the range [0.25, 20].
- The list is sorted, and duplicates (within 0.0001) are removed.
- If nothing valid remains, the default list is used and a warning is logged.

The Slower hotkey only goes below x1.0 if the list contains values below 1, for example `0.5, 1, 1.5, 2, 3, 4, 5, 8`. With the default list, Slower stops at x1.0.

### Startup scale

At game start the plugin applies `LastScale` when `RememberLastScale` is on, and `StartupScale` otherwise. The plugin writes every applied scale to `LastScale`, so with `RememberLastScale` on the game starts at the speed you left it at.

### Ultrafast

The game has two combat speed settings, normal and fast (the fast mode toggle in the settings menu), and a third value, Ultrafast, that the menu never selects. With `EnableUltrafast` on, the plugin sets the game's speed setting to Ultrafast once the game has loaded its preferences, and keeps it there when the fast mode toggle in the settings menu is changed. This is the only Harmony patch in the plugin: a postfix on `SettingsManager.SetFastMode`. It is applied the first time Ultrafast is enabled, so with the option off the log shows `0 patches applied`.

Turning `EnableUltrafast` off restores the speed setting from the saved fast mode preference. Ultrafast is independent of `Enabled` and stacks with the time scale. If the game's speed setting can't be found (for example after a game update), the plugin logs a `... not found, Ultrafast disabled` warning and leaves the setting alone; the time scale hotkeys keep working.

## Multiplayer

- The game keeps clients in step with ready barriers (`NetworkManager.SetStatusReady` and related calls): each client plays animations locally, then waits until every player is ready.
- Mixed scales do not desync a session, but faster players simply wait for the slowest one. The plugin has to be installed and set to the same speed on every client for the whole session to go faster.
- Ultrafast does almost nothing in multiplayer, because the game's Ultrafast branches are guarded by `!IsMultiplayer()`.

## Honesty

- Steam leaderboard submissions are not blocked: runs played at a changed time scale are submitted to the leaderboards like any other run.
- There is no anti-cheat in the game, so nothing detects or prevents the plugin.
- The game's own timings based on `Time.time` scale too, for example tooltip delays and double-click windows. At high multipliers tooltips appear sooner and double-clicks must be faster.

## Building

The only requirement is the .NET SDK 10 (the plugin targets netstandard2.1 and the tests target net10.0).

Unity, BepInEx and Harmony reference assemblies come from the BepInEx NuGet feed declared in `nuget.config`, as compile-only package references, so nothing is copied to the output except the plugin DLL and its pdb. The game assembly is not referenced at all: game types are resolved through reflection at runtime. A game install is needed only to run the plugin, not to build it.

Build the plugin:

```sh
dotnet build -c Release -warnaserror
```

This produces `bin/Release/netstandard2.1/AtO-TimeScale.dll`.

Run the unit tests for the step logic. They need no game or Unity assemblies:

```sh
dotnet test tests/AtO-TimeScale.Tests
```

### Versioning and releases

The plugin version is set in one place, `<Version>` in `AtO-TimeScale.csproj`. The GUID and name are set next to it (`PluginGuid`, `PluginName`). The build generates a `PluginInfo` class from these properties, and the assembly version attributes come from the same `Version`.

To publish a release, bump `<Version>`, merge, and push a `v<version>` tag (for example `v1.0.0`). The release workflow checks that the tag matches the csproj version with `scripts/check-version.ps1`, builds and tests, then creates a GitHub Release with the DLL and a zip.

## Releases

Download `AtO-TimeScale.dll`, or the `AtO-TimeScale-v<version>.zip` that also holds the README and LICENSE, from the [GitHub Releases](https://github.com/HawkeyePierce89/AtO-TimeScale/releases) page. Untagged builds are available as the `AtO-TimeScale` artifact of each [CI run](https://github.com/HawkeyePierce89/AtO-TimeScale/actions/workflows/ci.yml).

## Installing

1. Install BepInEx 5 for Across the Obelisk.
2. Copy `AtO-TimeScale.dll` into `<game folder>/BepInEx/plugins`. The build does not copy it for you.
3. Start the game. The BepInEx log should show `TimeScale <version> loaded` and `0 patches applied` (1 with `EnableUltrafast` on).

## License

See [LICENSE](LICENSE).
