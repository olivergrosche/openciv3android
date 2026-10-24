using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Godot;

/****
	Android has no writable working directory and no executable folder next to the game data, but C7 reads C7.ini,
	log.txt, ./Lua, ./Text and Assets relative to those. This autoload is listed first in project settings, so its
	static constructor runs before GlobalSingleton reads the settings: it switches to DataDir, unpacked from the APK,
	and points CIV3_HOME at the Civ3 files (FindCiv3), which the player copies to the device. Reading both needs the
	"All files access" permission; until the game has it and its data, a setup screen covers the main menu.
****/
public partial class AndroidBootstrap : Node {
	public const string DataDir = "/storage/emulated/0/Documents/OpenCiv3";
	public const string Civ3Dir = "/storage/emulated/0/Documents/Civ3";
	public const string SharedStorage = "/storage/emulated/0";

	// Assets, Lua and Text, packed by android-port/scripts/build-apk.sh, and the build they come from. The copy in
	// DataDir records the version it was unpacked from in .data-version.
	private const string DataZip = "res://AndroidData/data.zip";
	private const string DataVersion = "res://AndroidData/data-version.txt";
	private const string FileAccessPermission = "android.permission.MANAGE_EXTERNAL_STORAGE";

	private enum SetupStep { Ready, NeedsFileAccess, NeedsData }
	private static SetupStep setupStep = SetupStep.Ready;

	static AndroidBootstrap() {
		if (OS.GetName() != "Android") {
			return;
		}
		MoonSharp.Interpreter.Script.GlobalOptions.Platform = new AndroidLuaPlatform();
		setupStep = PrepareDataDir();
	}

	// Without "All files access" Android hides every file in Documents that is not a picture, video or sound, so the game
	// would quietly find neither its data nor Civ3.
	private static SetupStep PrepareDataDir() {
		if (!OS.GetGrantedPermissions().Contains(FileAccessPermission)) {
			return SetupStep.NeedsFileAccess;
		}
		try {
			Directory.CreateDirectory(DataDir);
			Directory.SetCurrentDirectory(DataDir);
		} catch (Exception e) {
			GD.PrintErr($"OpenCiv3: cannot use {DataDir} ({e.Message})");
			return SetupStep.NeedsFileAccess;
		}
		if (System.Environment.GetEnvironmentVariable("CIV3_HOME") == null) {
			System.Environment.SetEnvironmentVariable("CIV3_HOME", FindCiv3() ?? Civ3Dir);
		}
		if (!Godot.FileAccess.FileExists(DataVersion)) {
			return SetupStep.Ready; // a build without packed data, set up with deploy.sh --data
		}
		string stamp = Path.Combine(DataDir, ".data-version");
		bool current = File.Exists(stamp) && File.ReadAllText(stamp) == Godot.FileAccess.GetFileAsString(DataVersion);
		return current ? SetupStep.Ready : SetupStep.NeedsData;
	}

	// Replaces Assets, Lua and Text in DataDir with the APK's; C7.ini, Saves and log.txt stay. The APK stores the zip
	// compressed, and every seek in it would inflate it again from the start, so it is first copied out in one go.
	private static void InstallGameData(Action<int> progress) {
		string copy = Path.Combine(OS.GetCacheDir(), "data.zip");
		using (Godot.FileAccess source = Godot.FileAccess.Open(DataZip, Godot.FileAccess.ModeFlags.Read))
		using (FileStream target = File.Create(copy)) {
			if (source == null) {
				throw new IOException($"cannot open {DataZip}");
			}
			ulong length = source.GetLength();
			while (source.GetPosition() < length) {
				target.Write(source.GetBuffer(1 << 20));
				progress((int)(source.GetPosition() * 20 / length));
			}
		}
		try {
			foreach (string dir in new[] { "Assets", "Lua", "Text" }) {
				string path = Path.Combine(DataDir, dir);
				if (Directory.Exists(path)) {
					Directory.Delete(path, true);
				}
			}
			using ZipArchive zip = ZipFile.OpenRead(copy);
			for (int i = 0; i < zip.Entries.Count; i++) {
				ZipArchiveEntry entry = zip.Entries[i];
				if (entry.FullName.EndsWith('/')) {
					continue;
				}
				string target = Path.Combine(DataDir, entry.FullName);
				Directory.CreateDirectory(Path.GetDirectoryName(target));
				entry.ExtractToFile(target, true);
				progress(20 + i * 80 / zip.Entries.Count);
			}
		} finally {
			File.Delete(copy);
		}
		File.WriteAllText(Path.Combine(DataDir, ".data-version"), Godot.FileAccess.GetFileAsString(DataVersion));
	}

	private Label setupText;
	private Button setupButton;
	private bool restartFailed;

	public override void _Ready() {
		if (setupStep != SetupStep.Ready) {
			ShowSetupScreen();
		}
	}

	// Covers the main menu until the game can read its data: asks for "All files access", unpacks the game data, then
	// starts the app again, because the game reads its settings and rules only at start.
	private async void ShowSetupScreen() {
		// Only after the first frame: until then the translations are not loaded, and the texts would stay English.
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		var layer = new CanvasLayer { Layer = 128 };
		// Parchment, like the game's popups, whose theme draws dark text.
		var background = new ColorRect { Color = new Color(0.91f, 0.86f, 0.74f), MouseFilter = Control.MouseFilterEnum.Stop };
		background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var box = new VBoxContainer { CustomMinimumSize = new Vector2(720, 0) };
		box.AddThemeConstantOverride("separation", 40);
		setupText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center };
		setupText.AddThemeFontSizeOverride("font_size", (int)(28 * TouchUiFactor));
		setupText.AddThemeColorOverride("font_color", new Color(0.18f, 0.12f, 0.06f));
		setupButton = new Button { Text = L.T("Allow access"), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		setupButton.AddThemeFontSizeOverride("font_size", (int)(32 * TouchUiFactor));
		foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) {
			setupButton.AddThemeColorOverride(state, new Color(0.98f, 0.95f, 0.88f));
		}
		foreach ((string state, Color color) in new[] { ("normal", new Color(0.45f, 0.29f, 0.14f)), ("hover", new Color(0.52f, 0.34f, 0.17f)),
				("pressed", new Color(0.33f, 0.21f, 0.1f)), ("focus", new Color(0.45f, 0.29f, 0.14f)) }) {
			var style = new StyleBoxFlat { BgColor = color, ContentMarginLeft = 40, ContentMarginRight = 40, ContentMarginTop = 16, ContentMarginBottom = 16 };
			style.SetCornerRadiusAll(10);
			setupButton.AddThemeStyleboxOverride(state, style);
		}
		setupButton.Pressed += OnSetupButtonPressed;
		box.AddChild(setupText);
		box.AddChild(setupButton);
		center.AddChild(box);
		layer.AddChild(background);
		layer.AddChild(center);
		GetTree().Root.AddChild(layer);
		ShowSetupStep();
	}

	private void ShowSetupStep() {
		if (setupStep == SetupStep.NeedsFileAccess) {
			setupText.Text = L.T("OpenCiv3 Android needs “All files access” to read its game data in Documents/OpenCiv3 and your Civilization III files.");
			setupButton.Visible = true;
			return;
		}
		setupText.Text = L.F("Setting up the game data … {0} %", 0);
		setupButton.Visible = false;
		new Thread(InstallGameDataThenRestart) { IsBackground = true }.Start();
	}

	private void OnSetupButtonPressed() {
		if (restartFailed) {
			GetTree().Quit();
		} else {
			OS.RequestPermission(FileAccessPermission);
		}
	}

	private void InstallGameDataThenRestart() {
		try {
			InstallGameData(percent => Callable.From(() => setupText.Text = L.F("Setting up the game data … {0} %", percent)).CallDeferred());
			Callable.From(Restart).CallDeferred();
		} catch (Exception e) {
			GD.PrintErr($"OpenCiv3: cannot unpack the game data ({e})");
			Callable.From(() => setupText.Text = L.F("Could not set up the game data: {0}", e.Message)).CallDeferred();
		}
	}

	// The same restart Godot itself uses after losing its graphics context (org.godotengine.godot.utils.ProcessPhoenix).
	private void Restart() {
		try {
			GodotObject activity = (GodotObject)Engine.GetSingleton("AndroidRuntime").Call("getActivity");
			GodotObject phoenix = (GodotObject)Engine.GetSingleton("JavaClassWrapper").Call("wrap", "org.godotengine.godot.utils.ProcessPhoenix");
			phoenix.Call("triggerRebirth", activity);
		} catch (Exception e) {
			GD.PrintErr($"OpenCiv3: cannot restart ({e.Message})");
			restartFailed = true;
			setupText.Text = L.T("Done. Please start OpenCiv3 Android again.");
			setupButton.Text = L.T("Close");
			setupButton.Visible = true;
		}
	}

	// Civ3 may be copied to Documents/Civ3 or, under any name, into Documents or Download (such as the GOG folder
	// "Sid Meier's Civilization III Complete"). Returns the first of these folders that has Civ3's art, or null.
	public static string FindCiv3() {
		if (IsCiv3Root(Civ3Dir)) {
			return Civ3Dir;
		}
		foreach (string parent in new[] { "Documents", "Download" }) {
			try {
				foreach (string dir in Directory.GetDirectories(Path.Combine(SharedStorage, parent)).Order()) {
					if (IsCiv3Root(dir)) {
						return dir;
					}
				}
			} catch (Exception) {
				// No such folder, or no permission yet.
			}
		}
		return null;
	}

	// The Civ3 root has Art and either Conquests or civ3PTW; the same test as MainMenu.ClassicGraphicsAvailable.
	public static bool IsCiv3Root(string dir) {
		foreach (string basePath in new[] { "Conquests", "civ3PTW", "" }) {
			if (File.Exists(Path.Combine(dir, basePath, "Art", "buttonsFINAL.pcx"))) {
				return true;
			}
		}
		return false;
	}

	// Users often pick Conquests, civ3PTW or Art in the folder dialog instead of the Civ3 folder above them.
	public static string Civ3RootOf(string dir) {
		for (string d = dir; !string.IsNullOrEmpty(d); d = Path.GetDirectoryName(d)) {
			if (IsCiv3Root(d)) {
				return d;
			}
		}
		return dir;
	}

	// The interface is laid out for a 768 pixel high screen. On a tablet that is about 12 cm, on a phone in landscape
	// only about 7 cm, so phones (short side under 3.5 inches) draw the map HUD, popups and menus larger still. The
	// full-screen views (city screen, advisors) already fill the height and cannot grow.
	private static float? touchUiFactor;
	public static float TouchUiFactor => touchUiFactor ??= ComputeTouchUiFactor();

	private static float ComputeTouchUiFactor() {
		Vector2I size = DisplayServer.ScreenGetSize();
		float shortSideInches = Math.Min(size.X, size.Y) / (float)Math.Max(DisplayServer.ScreenGetDpi(), 1);
		return shortSideInches < 3.5f ? 1.5f : 1f;
	}

	// Two fingers magnify the whole screen, as in the Wine port of Civ3 from 2016: pinch to zoom in and out, drag with
	// two fingers to move around. It is for the fixed 1024x768 screens (main menu, game setup, city screen, advisors,
	// popups), which are tiny on a phone; on the map itself pinching zooms the map (Game.HandleMagnifyGesture), and the
	// magnification ends as soon as the map is back in front. Godot maps touches back through the global canvas
	// transform, so taps still hit what is under the finger.
	private const float MaxMagnification = 4;
	private float magnification = 1;
	private Vector2 magnifierOrigin;
	private Node magnifiedScene;

	// Not over the map, and not while a dialog window (file dialog) is open: Godot does not map touches into such
	// windows through the magnification, and the window takes the gestures, so it could not be zoomed out again.
	private bool CanMagnify() {
		return OS.GetName() == "Android"
			&& !(GetTree().CurrentScene is Game game && !game.ShowsScreenOverMap())
			&& GetViewport().GetEmbeddedSubwindows().Count == 0;
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (!CanMagnify()) {
			return;
		}
		if (@event is InputEventMagnifyGesture magnify) {
			// Gesture positions arrive in unmagnified coordinates; keep the point between the fingers in place.
			float zoom = Mathf.Clamp(magnification * magnify.Factor, 1, MaxMagnification);
			magnifierOrigin += (magnification - zoom) * magnify.Position;
			magnification = zoom;
			magnifiedScene = GetTree().CurrentScene;
			ApplyMagnification();
			GetViewport().SetInputAsHandled();
		} else if (@event is InputEventPanGesture pan && magnification > 1) {
			// Android reports a fifth of the finger movement in device pixels, reversed (Godot does not transform it).
			magnifierOrigin -= pan.Delta * 5 / GetViewport().GetStretchTransform().Scale.X;
			ApplyMagnification();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta) {
		if (magnification > 1 && (GetTree().CurrentScene != magnifiedScene || !CanMagnify())) {
			magnification = 1;
			ApplyMagnification();
		}
	}

	private void ApplyMagnification() {
		Vector2 size = GetViewport().GetVisibleRect().Size;
		if (magnification <= 1.01f) {
			magnification = 1;
			magnifierOrigin = Vector2.Zero;
		}
		// The magnified screen always covers the whole display.
		magnifierOrigin = magnifierOrigin.Clamp(size - size * magnification, Vector2.Zero);
		GetViewport().GlobalCanvasTransform = new Transform2D(0, Vector2.One * magnification, 0, magnifierOrigin);
	}

	// The Android back gesture acts like the Escape key (closes popups, menus, the city screen and advisors) instead of
	// quitting the app (application/config/quit_on_go_back is off).
	public override void _Notification(int what) {
		if (what == NotificationWMGoBackRequest) {
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
			Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
		} else if (what == NotificationApplicationResumed && setupStep == SetupStep.NeedsFileAccess && setupText != null) {
			// Back from the "All files access" settings page.
			setupStep = PrepareDataDir();
			if (setupStep == SetupStep.Ready) {
				Restart();
			} else if (setupStep == SetupStep.NeedsData) {
				ShowSetupStep();
			}
		}
	}

	// MoonSharp's io module opens the console streams whenever a Script is created, and Android has no console
	// (Console.OpenStandardInput throws PlatformNotSupportedException).
	private class AndroidLuaPlatform : MoonSharp.Interpreter.Platforms.DotNetCorePlatformAccessor {
		public override Stream IO_GetStandardStream(MoonSharp.Interpreter.Platforms.StandardFileType type) => Stream.Null;
	}
}
