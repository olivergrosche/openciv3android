# OpenCiv3 for Android (tablet and Pixel)

This branch is OpenCiv3's `Development` branch with the changes needed for an Android build:
- touch controls;
- a German translation;
- a fix for scenario imports;
- a patched Godot engine for Android 17.

It is played on a tablet (Android 16, Adreno, 2400x1504) and a Pixel 10 Pro XL (Android 17, PowerVR, 2404x1080). Upstream: https://github.com/C7-Game/OpenCiv3. There is no official Android build.

Updating to a newer upstream version: see [UPDATING.md](UPDATING.md).

## Repository

- Branch `android`: upstream `Development` plus a few commits:
  - scenario import fixes in the engine;
  - the Android port;
  - the German translation;
  - this folder.

  The remote `upstream` points to GitHub; there is nothing to push.
- `android-port/`, everything that is not game code:

| Path | Contents |
|---|---|
| `scripts/env.sh` | paths of the local toolbox and the adb serials of the devices; all scripts read it |
| `scripts/build-apk.sh` | builds `android-port/out/OpenCiv3.apk` |
| `scripts/deploy.sh` | installs the APK on `tablet` or `pixel`; `--data` also copies Assets, Lua and Text |
| `scripts/update-translations.sh` | regenerates `names.csv` and `ui.csv` and lists untranslated interface texts |
| `scripts/init-submodules.sh` | checks out `C7/Assets` and `C7/addons/markdownlabel` over https |
| `l10n/` | `ui_de.tsv` (German interface texts) and the Perl tools that turn it into `C7/Translations/ui.csv` |
| `tools/Civ3Names` | builds `C7/Translations/names.csv` from the German Civ3 rules |
| `tools/BiqImportTest` | imports a Civ3 scenario (.biq) and builds the game data on the PC, without Godot |
| `tools/BldgDump` | lists a .biq's buildings with required techs, hidden techs and the civs' free techs |
| `godot/` | patch for Godot 4.4.1, `build-template.sh` and `make-template.py` that build the patched export template |

## Toolbox (outside git)

`C:\Users\admin\dev\openciv3` (`TOOLBOX` in `scripts/env.sh`):

| Path | Contents |
|---|---|
| `dotnet` | .NET SDK 8.0.426, not on PATH |
| `godot\Godot_v4.4.1-stable_mono_win64` | Godot editor; export templates in `%APPDATA%\Godot\export_templates\4.4.1.stable.mono` |
| `godot-templates\android_release_4.4.1_tpafix.apk` | patched export template (see "Godot fix"); `export_presets.cfg` refers to it |
| `keystore\openciv3.keystore` | signing key; alias and password are set in `scripts/env.local.sh` |
| `Civ3` | GOG Civilization III Complete, extracted with innoextract, with the German patch and the Giant Earth scenario |
| `de-patch`, `giantearth` | German patch (extracted) and the Giant Earth download |
| `src`, `src-dev` | earlier working copies (0.3 release and the first Development port); history only |

The editor settings in `%APPDATA%\Godot\editor_settings-4.4.tres` point to the JDK (`dev\jdk17`) and the Android SDK (`dev\android-sdk`). Godot source and build environment for the template: `dev\godot-src`.

## Build and install

```
android-port/scripts/init-submodules.sh     # once per clone
android-port/scripts/build-apk.sh
android-port/scripts/deploy.sh tablet        # or pixel; add --data after an update
```

Installing ends a running game; save it first. The devices need:
- `Documents/Civ3`: Civ3 without exe, dll, pdf and videos, about 1.5 GB. It includes `Conquests/conquests.biq`, the German patch's rules, which scenario loading always reads; GOG has none.
- `Documents/OpenCiv3`: `Assets`, `Lua` and `Text` from `C7/`, plus `C7.ini` with `civ3InstallDir = /storage/emulated/0/Documents/Civ3`.

`deploy.sh` grants "All files access" (appops MANAGE_EXTERNAL_STORAGE).

## What the port changes

- **Data paths:** `AndroidBootstrap.cs` is the first autoload. On Android it:
  - switches to `Documents/OpenCiv3`;
  - sets `CIV3_HOME`;
  - gives MoonSharp a platform without console streams, because Android has none.

  `GamePaths.BaseDir` is the data folder.
- **Touch** (Android only), in `Game.cs` (`TouchControls`):
  - Dragging a unit moves it, and the goto path follows the finger. Fortified units can only be dragged when they are selected, so a swipe across a garrisoned city pans the map.
  - Taps count on release, and a double tap opens the city (`DoubleClickHandler`: 0.4 s).
  - Long press (0.6 s): on a tile next to the selected unit it picks the unit up; elsewhere it opens the tile menu ("Zoom to city", production).
  - Pinch zooms the map.
  - The back gesture acts as Escape.
- **Two-finger magnifier** on every screen that is not the map (main menu, setup, city screen, advisors, popups), like the Wine port of 2016:
  - It sets `Viewport.GlobalCanvasTransform` in `AndroidBootstrap`. Godot maps touches back through it.
  - It is off while a dialog window (file dialog) is open, because those windows would get the touches unmapped.
- **Sizes:**
  - The HUD (info box, tool bar) is 1.4x, popups and menus 1.2x, unit buttons 50 px, and the main menu 1.6x.
  - Phones get 1.5x more (`AndroidBootstrap.TouchUiFactor`: short side under 3.5 inches). Popups never grow past the screen height.
  - The full-screen views are fixed 1024x768 layouts and already fill the height. The magnifier is the answer for them.
- **Advisors:** F1-F11 do not exist on a tablet. On Android the advisor button opens a list of all advisors and screens (`Game.AdvisorMenu`).
- **Popups** (`InteractablePopUpController`): translated, larger, and one tap chooses an option.
- **Ambient sounds** (all platforms), in `C7/Audio/AmbienceSoundscape.cs`:
  - Like Civ3: every 8-20 s, a lark, cricket, oriole, hawk, eagle or wolf plays, chosen by the terrain of a random known land tile on screen.
  - The sounds are the eight files in `Sounds/Ambience Sfx`, set up in `C7/Lua/civ3/audio.lua`.
  - Civ3 decides this inside its executable, so the terrain-to-sound table is our own.
  - The volume is the "Ambience" slider.
  - The Lua change has to reach the devices: `deploy.sh ... --data`.
- **City screen:** shows the map at zoom 1 while open, because its tiles only fit the window at zoom 1, and restores the zoom on close.
- **Scenario import** (`C7Engine/C7GameData/ImportCiv3.cs`, `Player.cs`):
  - Scenarios without player setup (LEAD) or world size import.
  - Techs without an era ("Trashcan" in Giant Earth) are not researchable and keep what requires them locked.
  - The fourth tech prerequisite was read from the third.
- **Project:** `project.godot` has Android-only settings:
  - 1024x768 canvas_items/expand;
  - landscape that follows the sensor;
  - pan and pinch gestures;
  - GL compatibility renderer.

  `export_presets.cfg` has an "Android" preset: arm64, Assets excluded, keystore and patched template.

## German translation

Godot CSV translations in `C7/Translations`, following the system language:
- `names.csv` (about 400 names) comes from `tools/Civ3Names`. It matches the German patch's `Conquests.biq` to the English `ruleset.json` by civilopedia key, by a key derived from the English name, or through an alias table.
- `ui.csv` (about 330 interface texts) comes from `l10n/ui_de.tsv`.

Static Control texts are translated by Godot. Texts built in code go through `C7/L.cs`:
- `L.T(name)`;
- `L.F("Turn {0}", n)`;
- `L.Time("4000 BC")`;
- `L.Describe(unit)`.

The engine (C7Engine) has no Godot and stays English, apart from the messages that the UI rebuilds itself, such as hurry production.

## Godot fix (Android 17)

On the Pixel every Godot 4.4.1 .NET app crashed at start with `Assertion ... assembly.c:2718, condition 'corlib' not met`. `modules/mono/mono_gd/gd_mono.cpp` passes `tpa_list.utf8().get_data()`, the pointer of a temporary that is freed before `coreclr_initialize` reads it. Android 16 still has the old text in memory; Android 17 reuses it at once. The one-line fix is `godot/godot-4.4.1-tpa-fix.patch`.

`godot/build-template.sh` builds the engine library and puts it into a copy of the official template. That takes about 7 minutes and needs NDK 23.2, SCons and `swappy=no`. Godot 4.5 loads the assemblies differently and does not need this.

## Known limits

- Portrait does not work. The main menu, city screen, advisors and setup screens are fixed 1024x768 landscape layouts drawn on Civ3 artwork.
- The Civilopedia has data but no screen yet (upstream).
- Scenario rules OpenCiv3 does not know yet are ignored, for example Giant Earth's movement and settling limits. Free starting techs without their prerequisites are dropped (upstream decision); Civ3 would grant them.
