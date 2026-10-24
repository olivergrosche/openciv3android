# Updating to a new upstream version

The port is a handful of commits on top of upstream `Development`. An update moves them onto the new upstream state and rebuilds the generated files. Run everything from the repository root in Git Bash.

## 1. Fetch and look

```
git fetch upstream
git log --oneline android..upstream/Development | wc -l      # how much is new
git diff android...upstream/Development --stat -- C7/Game.cs C7/UIElements C7Engine/C7GameData/ImportCiv3.cs
grep Godot.NET.Sdk C7/C7.csproj; git show upstream/Development:C7/C7.csproj | grep Godot.NET.Sdk
```

If the Godot version changes, read "Godot version change" below first. Releases are tagged (`v0.3-dutch-preview-1`). To follow a release instead of `Development`, rebase onto the tag.

## 2. Rebase

```
git checkout android
git tag android-before-update                   # way back if it goes wrong: git reset --hard android-before-update
git rebase upstream/Development
```

Conflicts usually come up in the files the port touches. The `git show` of the port commit shows what the port meant there.

- `C7/Game.cs`: touch code (`TouchControls`, `HandleTouch`, `StartUnitDrag`, `EndUnitDrag`, the long press, `EnlargeHudForTouch`, `AdvisorMenu`, `ShowsScreenOverMap`). Upstream renames methods now and then, as with `ResolveMovement`, `SetGotoMode` and `tileCoordsInMainCamera`. Keep upstream's version and put the touch code back on top.
- Popups and screens with translated texts: keep upstream's new text and wrap it in `L.T`/`L.F` again.
- `C7Engine/C7GameData/ImportCiv3.cs`: the null checks for scenarios without LEAD and the world-size fallback. Upstream may have fixed these itself; then drop ours.
- `C7/project.godot`, `C7/export_presets.cfg`: keep upstream's settings and add the Android lines back (see the port commit).

Then:

```
android-port/scripts/init-submodules.sh         # the Assets submodule moves with upstream
```

## 3. Translations

```
android-port/scripts/update-translations.sh
```

This regenerates `names.csv` (local only: it is Civ3 content, git ignores it). It then lists the interface texts that have no German yet: add them to `android-port/l10n/ui_de.tsv` and run it again.
- Scene placeholders that code overwrites can stay untranslated (`Placeholder City`, `Income: 1234`).
- New texts built with `$"..."` in code are not found automatically. Look through the upstream diff for new popups (`Game_EmitMessages.cs`, `MsgShowTemporaryPopup`) and wrap them in `L.F`.

## 4. Build and test

```
android-port/scripts/build-apk.sh
```

Compile errors are usually renamed upstream methods in the port's code.

Check that scenarios still import:

```
dotnet run --project android-port/tools/BiqImportTest -c Release -- "<Civ3>/Conquests/Scenarios/Giant Earth.biq" "<Civ3>" C7/Lua
```

Install with `deploy.sh pixel --data`, then `deploy.sh tablet --data`. `--data` replaces Assets, Lua and Text on the device, and the old folders go to `Documents/OpenCiv3-previous`.

On each device check:
- **Startup and menus:** the main menu and Quick Start are German.
- **Map controls:**
  - dragging a unit, Fortify, and a long press next to the unit;
  - a double tap or long press on a city;
  - the advisor button list.
- **Screens:** popups, the city screen, and the magnifier on the city screen and in setup.
- **Scenarios:** "Load Scenario" with Giant Earth.

## 5. Commit

Commit the regenerated translations and any fixes as new commits, publish, then remove the safety tag:

```
git add -A C7/Translations android-port
git commit -m "Update German translation for <upstream version>"
git push --force-with-lease origin android     # the rebase rewrote the branch
git tag -d android-before-update
```

Push as GitHub user olivergrosche (`gh auth status`).

## Godot version change

- **Upstream moves to Godot 4.5 or newer:** the patched template is no longer needed, because 4.5 loads the .NET assemblies differently.
  1. Download the matching mono editor and export templates.
  2. Update `GODOT` in `scripts/env.sh` and the editor settings (JDK, Android SDK).
  3. Set `custom_template/release=""` in the Android preset of `export_presets.cfg`.
  4. Test on the Pixel first. It is the device that crashed on 4.4.1.
- **Another 4.4.x version:** check whether `gd_mono.cpp` still has the `tpa_list.utf8().get_data()` line. If it does, set `GODOT_VERSION` in `godot/build-template.sh` and run it.
