# AtO-TimeScale: BepInEx plugin that changes Time.timeScale with hotkeys

## Overview

Build a BepInEx 5 plugin for Across the Obelisk (v1.7.7.1, Unity 2022.3.62f2, Mono) that changes `UnityEngine.Time.timeScale` with hotkeys and shows the multiplier in an IMGUI overlay. Nothing in the game is patched for this. The only Harmony patch is an optional Ultrafast mode (off by default): it forces `GameManager.configGameSpeed` to `Ultrafast` and keeps it there after the settings menu toggles fast mode.

The repository copies the skeleton of `C:\git\AtO-ShufflePeek`: build, `nuget.config`, generated `PluginInfo`, the version script, CI, release, test layout and code style.

## Context

- Files involved (all new except README.md):
  - `AtO-TimeScale.csproj`, `nuget.config`, `.gitignore`, `CLAUDE.md`, `README.md` (rewrite)
  - `src/Plugin.cs`: entry point, config bindings, host object, startup scale, config change handlers
  - `src/TimeScaleBehaviour.cs`: `MonoBehaviour` with hotkeys, external-reset guard, Ultrafast deferral tick and `OnGUI` overlay
  - `src/ScaleLogic.cs`: pure logic (parsing, next/previous, clamping, formatting); no Unity or game types
  - `src/Ultrafast.cs`: reflection over `GameManager`/`SettingsManager`, deferred assignment, `SetFastMode` postfix
  - `tests/AtO-TimeScale.Tests/AtO-TimeScale.Tests.csproj`, `tests/AtO-TimeScale.Tests/ScaleLogicTests.cs`
  - `scripts/check-version.ps1`, `.github/workflows/ci.yml`, `.github/workflows/release.yml`
- Related patterns (from AtO-ShufflePeek):
  - Static `ConfigEntry` fields on `Plugin` and a static `Plugin.Log`.
  - A hidden `DontDestroyOnLoad` host `GameObject` with `HideFlags.HideAndDontSave`.
  - `Patches.TryPatch`-style helper that logs and skips missing members.
  - The overlay's `Fail()` logs once and hides.
  - The test csproj links the pure file with `<Compile Include="../../src/X.cs" Link="X.cs" />`.
- Verified in the decompiled game (`Assembly-CSharp.dll`, 1.7.7.1):
  - `Enums.ConfigSpeed { Slow, Fast, Ultrafast }`.
  - `GameManager.Instance` and `SettingsManager.Instance` are static properties with private setters.
  - `GameManager.Awake` calls `DontDestroyOnLoad`, so there is one persistent instance.
  - `configGameSpeed` is assigned in exactly three places:
    - `GameManager.Start` sets it to `Slow`, immediately calls `SettingsManager.Instance.LoadPrefs()`, then sets the public bool field `PrefsLoaded = true`.
    - `SettingsManager.LoadPrefs` sets it from the `fastMode` pref.
    - `SettingsManager.SetFastMode(bool)` sets it and saves the pref.
  - `LoadPrefs` has no other caller, so Ultrafast never needs reapplying per scene. It only has to wait until `GameManager.Instance != null && PrefsLoaded`.
  - `fastMode` is stored with `PlayerPrefs.SetInt(key, value ? 1 : 0)` and read as `GetInt(key) == 1`. When the key is missing, the game's default is `Slow`.
- Dependencies:
  - `UnityEngine.Modules [2022.3.62]` and `BepInEx.Core [5.4.21]` (HarmonyX comes transitively), both compile-only from the BepInEx feed.
  - xUnit test stack as in ShufflePeek: `Microsoft.NET.Test.Sdk 17.14.1`, `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.4`.
  - .NET SDK 10.

## Development Approach

- **Testing approach**: TDD for `ScaleLogic` (tests first, then implementation). The rest is Unity- or game-dependent and xUnit cannot reach it. Those tasks end with:
  - a `-warnaserror` Release build;
  - the test suite;
  - scripted checks (output listing, metadata inspection, greps).
- Complete each task fully before moving to the next.
- Follow the CLAUDE.md rules:
  - try/catch in every Unity callback, config change handler and patch body, logging through `Plugin.Log`;
  - game types only through `AccessTools.TypeByName`, never `typeof`;
  - no `Time.deltaTime`/`Time.time` inside the plugin;
  - `System.Math` (not `Mathf`) in `ScaleLogic.cs`.
- Shell-neutral verification commands; anything pwsh-specific is invoked through `pwsh` explicitly.
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. Nothing is written into the game folder.
- **CRITICAL: every task MUST include new/updated tests or explicit verification steps.**
- **CRITICAL: all tests must pass before starting the next task.**

## Implementation Steps

### Task 1: Project skeleton

**Files:**
- Create: `AtO-TimeScale.csproj`
- Create: `nuget.config`
- Create: `.gitignore`
- Create: `src/Plugin.cs`

- [x] Copy `AtO-ShufflePeek.csproj` as `AtO-TimeScale.csproj` and change:
  - `AssemblyName` to `AtO-TimeScale` and `RootNamespace` to `AtOTimeScale`;
  - `Version` stays `1.0.0`, `PluginGuid` becomes `hawkeyepierce89.ato.timescale`, `PluginName` becomes `TimeScale`;
  - the generated `PluginInfo` namespace becomes `AtOTimeScale` (keep the `%3B` semicolons).
  - Everything else stays the same: the `tests/**` removals, the compile-only package references, `GenerateDependencyFile=false` and `IncludeSourceRevisionInInformationalVersion=false`.
- [x] Copy `nuget.config` and `.gitignore` (`bin/`, `obj/`, `.vs/`, `*.user`) verbatim.
- [x] Minimal `src/Plugin.cs`:
  - `[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]`, static `Log`;
  - `Awake` with try/catch that logs `"{Name} {Version} loaded"`.
- [x] Verify:
  - `dotnet build -c Release -warnaserror` passes with zero warnings;
  - `bin/Release/netstandard2.1/` holds only `AtO-TimeScale.dll` and `AtO-TimeScale.pdb`;
  - `obj/Release/netstandard2.1/PluginInfo.g.cs` contains the new GUID, name and `1.0.0`.

### Task 2: Pure scale logic with tests (TDD)

**Files:**
- Create: `tests/AtO-TimeScale.Tests/AtO-TimeScale.Tests.csproj`
- Create: `tests/AtO-TimeScale.Tests/ScaleLogicTests.cs`
- Create: `src/ScaleLogic.cs`

- [x] Test csproj copied from ShufflePeek's:
  - `net10.0`, `RootNamespace` `AtOTimeScale.Tests`;
  - links `../../src/ScaleLogic.cs`;
  - same xUnit package versions.
- [x] Write the tests first. The API is `internal static class ScaleLogic` in namespace `AtOTimeScale`; make internals visible to the tests or declare it `public static`, whichever builds warning-free.
  - Constants: `MinScale = 0.25`, `MaxScale = 20`, `DefaultSteps = "1, 1.5, 2, 3, 4, 5, 8"`.
  - `double[] ParseSteps(string text, out bool usedDefault)`:
    - split on `,`, trim, skip empty entries;
    - `double.TryParse` with `NumberStyles.Float` and `CultureInfo.InvariantCulture`;
    - drop non-finite values (`NaN`, `Infinity`);
    - clamp to [0.25, 20], sort ascending, remove duplicates within a 1e-4 tolerance;
    - if nothing remains (or the input is null), return the parsed default list with `usedDefault = true`.
  - `double Clamp(double scale)`: clamps to [0.25, 20] and returns 1.0 for a non-finite input.
  - `double Next(double[] steps, double current)`: the first step greater than `current + 1e-4`, otherwise `current`.
  - `double Previous(double[] steps, double current)`: the last step less than `current - 1e-4`, otherwise `current`. The tolerance absorbs float round-trips through `Time.timeScale`, for example `1.1f`.
  - `bool IsNormal(double scale)`: `|scale - 1| < 1e-4`.
  - `string Format(double scale)`: `"x" + scale.ToString("0.0", CultureInfo.InvariantCulture)`.
- [x] Test cases:
  - Parsing:
    - the default string;
    - whitespace and empty entries (`" 2 ,, 1 , "`);
    - garbage (`"abc, 2, NaN, Infinity, 1e999"`);
    - duplicates (`"2, 2.0, 2.00001"`);
    - out-of-range values (`"0, 0.1, 50"` → `[0.25, 20]`);
    - unsorted input;
    - null, empty, and all-garbage input fall back to the default list with `usedDefault = true`;
    - a comma-decimal culture does not affect parsing.
  - `Next`/`Previous`:
    - at both ends (unchanged);
    - stepping through the whole default list both ways;
    - from a value between steps (1.7 → 2 and → 1.5);
    - from a value below the first step and above the last;
    - with `(double)1.1f` against a step of 1.1.
  - `Clamp`: low, high, NaN and infinity.
  - `Format`: `x1.5`, `x2.0`, `x0.5`, `x0.3` for 0.25, and `x2.5` under `de-DE` (set `CultureInfo.CurrentCulture` in the test and restore it in `finally`).
- [x] Implement `src/ScaleLogic.cs` with only `System`, `System.Globalization` and `System.Linq` if needed; no `Mathf`.
- [x] Run `dotnet test tests/AtO-TimeScale.Tests`; all tests pass. `dotnet build -c Release -warnaserror` still passes and the output is still DLL and pdb only.

### Task 3: Config, time scale control and hotkeys

**Files:**
- Modify: `src/Plugin.cs`
- Create: `src/TimeScaleBehaviour.cs`

- [x] Config bindings in `Plugin.Awake` (static fields, ShufflePeek style):
  - `General`:
    - `Enabled` (true);
    - `Steps` (`ScaleLogic.DefaultSteps`);
    - `StartupScale` (float 1.0, `AcceptableValueRange` 0.25–20);
    - `RememberLastScale` (false);
    - `LastScale` (float 1.0, described as written by the plugin);
    - `EnableUltrafast` (false).
  - `Overlay`:
    - `PositionX` (20) and `PositionY` (200), defaults chosen so the overlay does not sit on ShufflePeek's at 20,20;
    - `FontSize` (18, range 10–48);
    - `HideAtNormalSpeed` (true).
  - `Hotkeys`: `Slower` (F6), `Faster` (F7), `Reset` (F9).
- [x] State on `Plugin`:
  - `Steps` (`double[]`), reparsed on `Steps.SettingChanged`, with a warning when it falls back to the defaults;
  - `CurrentScale` (double), the value the plugin last applied.
- [x] `ApplyScale(double scale, string reason)`:
  - clamp through `ScaleLogic.Clamp` and store `CurrentScale`;
  - if `Enabled`, set `Time.timeScale = (float)CurrentScale`;
  - write `LastScale`;
  - log `Time scale x2.0 (<reason>)` with the new value.
- [x] Startup: the initial scale is `LastScale` when `RememberLastScale` is on, otherwise `StartupScale`. Apply it once in `Awake` (only touch `timeScale` when enabled).
- [x] `Enabled.SettingChanged` (in try/catch):
  - false: set `Time.timeScale = 1f` and log;
  - true: reapply `CurrentScale` and log.
- [x] Create the hidden `DontDestroyOnLoad` host object `TimeScale` with `TimeScaleBehaviour`. Its `Update` (try/catch) does the following:
  - When enabled, it handles the `Faster`/`Slower`/`Reset` `IsDown()` presses using `ScaleLogic.Next`/`Previous` and 1.0. When a press does not change the value (end of the list), it logs nothing.
  - External-reset guard: when enabled and `Mathf.Abs(Time.timeScale - (float)CurrentScale) > 1e-4f`, it reapplies the scale and logs a warning only the first time.
  - It uses no `Time.deltaTime`/`Time.time`.
- [x] Verify:
  - `dotnet build -c Release -warnaserror` passes;
  - `git grep -n -e "Time.deltaTime" -e "Time.time\b" src/` returns nothing;
  - `dotnet test tests/AtO-TimeScale.Tests` passes.

### Task 4: Overlay

**Files:**
- Modify: `src/TimeScaleBehaviour.cs`

- [x] `OnGUI` (try/catch):
  - Return early when the overlay has failed, when `Enabled` is off, or when `HideAtNormalSpeed` is on and `ScaleLogic.IsNormal(CurrentScale)`.
  - Otherwise build a `GUIStyle` from `GUI.skin.label` with the `FontSize` from config, `wordWrap = false`.
  - The text is `ScaleLogic.Format(CurrentScale)`. Measure it with `CalcSize`, draw the `Color(0,0,0,0.7)` box with `Texture2D.whiteTexture` and 8px padding at `PositionX`/`PositionY` (read every frame), then the label.
- [x] `Fail(where, e)` as in ShufflePeek:
  - the first failure logs an error and sets an `overlayFailed` flag that hides the overlay for the session;
  - the Update path keeps its own log-once flag, so a hotkey error cannot spam the log.
- [x] Verify: `dotnet build -c Release -warnaserror` passes and `dotnet test tests/AtO-TimeScale.Tests` passes.

### Task 5: Optional Ultrafast mode

**Files:**
- Create: `src/Ultrafast.cs`
- Modify: `src/Plugin.cs`
- Modify: `src/TimeScaleBehaviour.cs`

- [x] `Ultrafast.Resolve()` resolves the following through `AccessTools`, each logged as a warning and the feature disabled if missing:
  - `GameManager` via `TypeByName`;
  - its static `Instance` property;
  - the `PrefsLoaded` and `configGameSpeed` fields;
  - the enum values `Ultrafast`, `Fast` and `Slow`, via `Enum.Parse(configGameSpeedField.FieldType, name)`, never by number;
  - `SettingsManager` and its `SetFastMode(bool)` method.
- [x] A `TryPatch` helper (as in ShufflePeek) applies the `SetFastModePostfix`. The patch is applied lazily, the first time Ultrafast is enabled (at startup or at runtime), using the `Harmony` instance created in `Plugin.Awake` with `PluginInfo.Guid`. With the option off at startup, the plugin calls nothing and logs `0 patches applied`.
- [x] Deferred assignment:
  - `Ultrafast.Tick()` is called from `TimeScaleBehaviour.Update`.
  - While a `pending` flag is set, it waits until `GameManager.Instance != null` and `PrefsLoaded` is true, so that `GameManager.Start` and `LoadPrefs` cannot overwrite the value.
  - It then sets `configGameSpeed` to `Ultrafast`, logs it, and clears `pending`.
  - No per-scene reapply is needed (verified: single `DontDestroyOnLoad` instance, `LoadPrefs` called only from `GameManager.Start`).
- [x] `SetFastModePostfix` (try/catch): if `EnableUltrafast` is on, set `configGameSpeed` back to `Ultrafast` and log at debug level.
- [x] `EnableUltrafast.SettingChanged` (try/catch):
  - on: resolve and patch if not done yet, then set `pending`;
  - off: clear `pending`; if the instance exists and prefs are loaded, set `configGameSpeed` to `Fast` when `PlayerPrefs.HasKey("fastMode") && PlayerPrefs.GetInt("fastMode") == 1`, otherwise `Slow`, and log. The postfix stays installed but is a no-op while the option is off.
- [x] Ultrafast is governed only by `EnableUltrafast`, independent of `Enabled`.
- [x] Verify:
  - `dotnet build -c Release -warnaserror` passes;
  - `git grep -n "typeof(GameManager\|typeof(SettingsManager" src/` returns nothing;
  - a metadata check (pwsh with `System.Reflection.Metadata`) shows no `Assembly-CSharp` reference, and `BepInPlugin` arguments of `hawkeyepierce89.ato.timescale` / `TimeScale` / `1.0.0`;
  - `dotnet test tests/AtO-TimeScale.Tests` passes.

### Task 6: Version script and GitHub Actions

**Files:**
- Create: `scripts/check-version.ps1`
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release.yml`

- [x] Copy all three files from AtO-ShufflePeek and replace every `AtO-ShufflePeek` with `AtO-TimeScale` (csproj path, test project path, artifact name, DLL path, zip name). Nothing else changes.
- [x] Verify:
  - `git grep -n -i shufflepeek -- .github scripts` returns nothing;
  - `pwsh scripts/check-version.ps1 -Tag v1.0.0` exits 0 and prints `1.0.0`;
  - `-Tag v9.9.9` and `-Tag 1.0.0` exit non-zero;
  - run the CI command sequence locally (both restores, `build -warnaserror`, test); it passes;
  - YAML syntax check with `actionlint` if available, otherwise Python `yaml`.

### Task 7: Verify acceptance criteria

- [x] Delete `bin/` and `obj/` of both projects, then:
  - `dotnet build -c Release -warnaserror` reports zero warnings;
  - `dotnet test tests/AtO-TimeScale.Tests` passes.
- [x] The output directory contains only `AtO-TimeScale.dll` and `AtO-TimeScale.pdb`.
- [x] Re-run the metadata inspection:
  - no `Assembly-CSharp`;
  - `UnityEngine.CoreModule`, `UnityEngine.IMGUIModule`, `UnityEngine.InputLegacyModule` (not needed: `KeyboardShortcut` lives in BepInEx, so no direct reference), `BepInEx` and `0Harmony` present;
  - correct `BepInPlugin` arguments.
- [x] `1.0.0` is hand-written only in `AtO-TimeScale.csproj`, and the GUID only there.
- [x] Grep check: only `Ultrafast.cs` calls `harmony.Patch`, and no game method is patched elsewhere.

### Task 8: Documentation

**Files:**
- Modify: `README.md`
- Create: `CLAUDE.md`

- [x] Rewrite the README in the ShufflePeek style:
  - title and CI badge (`HawkeyePierce89/AtO-TimeScale`), and what the plugin does;
  - a hotkeys table (F6/F7/F9 and why F5/F8/F1/F12 are avoided);
  - a config table for every entry in `BepInEx/config/hawkeyepierce89.ato.timescale.cfg`;
  - steps parsing rules and the [0.25, 20] clamp, with a note that slowing below 1.0 works only if the step list contains values below 1;
  - `RememberLastScale`/`StartupScale` precedence;
  - the Ultrafast option;
  - Building (only the .NET SDK, compile-only NuGet references, no game assembly);
  - Versioning and Releases (`<Version>` in the csproj, pushing a `v<version>` tag);
  - Installation into `BepInEx/plugins`.
- [x] Multiplayer section:
  - the game syncs through ready barriers (`NetworkManager.SetStatusReady` and friends) and each client animates locally;
  - mixed scales only make fast players wait for the slowest one, so the plugin must be on every client for the whole session to speed up;
  - Ultrafast is almost a no-op in multiplayer because its branches are guarded by `!IsMultiplayer()`.
- [x] Honesty section:
  - Steam leaderboard submissions are not blocked, so runs played at a changed scale reach the leaderboards;
  - there is no anti-cheat;
  - the game's `Time.time`-based timings (tooltip delays, double-click windows) also scale.
- [x] `CLAUDE.md` in the ShufflePeek format:
  - Layout (each `src` file, tests, `nuget.config`, the script, workflows);
  - Commands;
  - Rules:
    - `ScaleLogic.cs` stays pure;
    - `TypeByName` only;
    - try/catch everywhere;
    - unscaled time only;
    - the only patch is Ultrafast's `SetFastMode` postfix;
    - `configGameSpeed` facts (assigned in `GameManager.Start`, `LoadPrefs` and `SetFastMode`; wait for `PrefsLoaded`);
    - version, GUID and name only in the csproj.
- [x] Re-read both files against the final code and config names. Run `dotnet test tests/AtO-TimeScale.Tests` once more.
- [x] Move this plan to `docs/plans/completed/`.

## Post-Completion (manual, owner)

- Push the branch, open the PR and confirm that CI is green.
- Copy the CI artifact DLL into `<game>/BepInEx/plugins`, then check:
  - the log shows `TimeScale 1.0.0 loaded` and `0 patches applied`;
  - F7 steps through 1, 1.5, 2, 3, 4, 5, 8 and F6 steps back;
  - F9 resets;
  - the overlay shows `x2.0` and disappears at 1.0;
  - each change is logged;
  - turning `Enabled` off through ConfigurationManager returns the game to normal speed.
- Optionally turn on `EnableUltrafast`: the `SetFastMode` patch is logged, combat animations use Ultrafast, and toggling fast mode in settings keeps Ultrafast.
- After the merge, decide whether to push `v1.0.0` to publish the release.
