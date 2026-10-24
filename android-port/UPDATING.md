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

This regenerates `names.csv` and `ui.csv`. It then lists the names and interface texts that have no German yet: add names (new units, cities…) to `android-port/l10n/names_de.tsv`, also when German keeps the English name, and texts to `android-port/l10n/ui_de.tsv`. Run it again.
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

Install with `deploy.sh pixel`, then `deploy.sh tablet`. At its first start the new APK replaces Assets, Lua and Text in `Documents/OpenCiv3` with its own and restarts.

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

## 6. Release

Releases are named `android-N` (android-1 to android-3 in October 2026).

1. In the Android preset of `C7/export_presets.cfg`, raise `version/code` by one and set `version/name="android-N (OpenCiv3 <upstream version>)"`. Commit as "Release android-N".
2. `android-port/scripts/build-apk.sh`. The APK is signed with `openciv3android.keystore`; a build with any other key cannot update installed copies.
3. **Test as a new player before publishing**, on one device. `deploy.sh` grants file access by itself, so it does not count.
   - Move `Documents/OpenCiv3` aside, `adb uninstall io.github.olivergrosche.openciv3android`, install the APK.
   - Follow the release notes step by step: setup screen, "All files access", unpacking, restart, Civ3 found, Quick Start, saving and loading.
   - Every sentence of the notes must have been seen working.
   - Afterwards put `Documents/OpenCiv3` back and start the app once. It updates the data and keeps `C7.ini` and `Saves`.
4. Push, then publish with the APK and the Giant Earth zip (see README, "Giant Earth"). Start the notes from the previous release (`gh release view android-<N-1> --json body -q .body`) and replace its "Changes since" section:

   ```
   gh release create android-N --repo olivergrosche/openciv3android --target "$(git rev-parse origin/android)" \
     --title "openciv3android N" --notes-file notes.md openciv3android-N.apk giant-earth-openciv3android.zip
   ```

   The newest release becomes "Latest". If an older one is broken, put a warning at the top of its notes instead of deleting it.

## Godot version change

- **Upstream moves to Godot 4.5 or newer:** the patched template is no longer needed, because 4.5 loads the .NET assemblies differently.
  1. Download the matching mono editor and export templates.
  2. Update `GODOT` in `scripts/env.sh` and the editor settings (JDK, Android SDK).
  3. Set `custom_template/release=""` in the Android preset of `export_presets.cfg`.
  4. Test on the Pixel first. It is the device that crashed on 4.4.1.
- **Another 4.4.x version:** check whether `gd_mono.cpp` still has the `tpa_list.utf8().get_data()` line. If it does, set `GODOT_VERSION` in `godot/build-template.sh` and run it.
