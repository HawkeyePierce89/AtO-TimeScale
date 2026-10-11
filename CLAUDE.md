# CLAUDE.md

BepInEx 5 plugin for Across the Obelisk that changes `Time.timeScale` with hotkeys and shows the multiplier in an IMGUI overlay.

## Layout

- `src/Plugin.cs`: entry point, config bindings, `ApplyScale`, startup scale, config change handlers, hidden `DontDestroyOnLoad` host object, Harmony instance.
- `src/TimeScaleBehaviour.cs`: hotkeys, external-reset guard, `Ultrafast.Tick()` call and the `OnGUI` overlay.
- `src/Ultrafast.cs`: optional Ultrafast mode; resolves `GameManager`/`SettingsManager` through `AccessTools`, assigns `configGameSpeed` once prefs are loaded, and applies the lazy `SetFastMode` postfix.
- `src/ScaleLogic.cs`: pure logic (step parsing, next/previous, clamping, formatting).
- `tests/AtO-TimeScale.Tests`: xUnit tests; the project links `../../src/ScaleLogic.cs` directly.
- `nuget.config`: nuget.org plus the BepInEx feed for the compile-only Unity/BepInEx packages.
- `scripts/check-version.ps1`: checks that a `v<version>` tag matches the csproj `Version`; used by the release workflow.
- `.github/workflows/`: `ci.yml` (restore, build, test, DLL artifact) and `release.yml` (tag-triggered GitHub Release).

## Commands

- `dotnet build -c Release -warnaserror` must succeed.
- `dotnet test tests/AtO-TimeScale.Tests`

## Rules

- `ScaleLogic.cs` stays plain C# with no Unity or game types and uses `System.Math`, not `Mathf`, because the test project compiles it without Unity.
- Resolve game types with `AccessTools.TypeByName`, never `typeof`; enum values through `Enum.Parse` by name, never by number.
- Wrap every Unity callback, config change handler and patch body in try/catch that logs through `Plugin.Log`.
- Never use `Time.deltaTime` or `Time.time` in the plugin: they scale with the value the plugin sets.
- The only Harmony patch is Ultrafast's postfix on `SettingsManager.SetFastMode`, applied lazily the first time `EnableUltrafast` is on. Changing the time scale itself patches nothing.
- `GameManager.configGameSpeed` is assigned only in `GameManager.Start` (set to `Slow`, then `LoadPrefs`, then `PrefsLoaded = true`), `SettingsManager.LoadPrefs` and `SettingsManager.SetFastMode`. Ultrafast waits until `GameManager.Instance != null && PrefsLoaded` before assigning; there is one persistent `GameManager`, so no per-scene reapply.
- Unity and BepInEx assemblies come from the BepInEx NuGet feed (`nuget.config`) as compile-only references; the game assembly is never referenced. Nothing is copied to the output except the plugin DLL and pdb.
- The version, GUID and name are set only in the csproj (`Version`, `PluginGuid`, `PluginName`) and reach code through the generated `PluginInfo` class.
