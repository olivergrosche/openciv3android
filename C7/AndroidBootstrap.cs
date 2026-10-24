using System;
using System.IO;
using Godot;

/****
	Android has no writable working directory and no executable folder next to the game data, but C7 reads C7.ini,
	log.txt, ./Lua, ./Text and Assets relative to those. This autoload is listed first in project settings, so its
	static constructor runs before GlobalSingleton reads the settings: it switches to DataDir and points CIV3_HOME at
	Civ3Dir. Both folders are copied to the device by hand; reading them needs the "All files access" permission.
****/
public partial class AndroidBootstrap : Node {
	public const string DataDir = "/storage/emulated/0/Documents/OpenCiv3";
	public const string Civ3Dir = "/storage/emulated/0/Documents/Civ3";

	static AndroidBootstrap() {
		if (OS.GetName() != "Android") {
			return;
		}
		try {
			Directory.CreateDirectory(DataDir);
			Directory.SetCurrentDirectory(DataDir);
			if (System.Environment.GetEnvironmentVariable("CIV3_HOME") == null) {
				System.Environment.SetEnvironmentVariable("CIV3_HOME", Civ3Dir);
			}
		} catch (Exception e) {
			GD.PrintErr($"OpenCiv3: cannot use {DataDir} ({e.Message}); grant \"All files access\" and restart");
			OS.RequestPermissions();
		}
		MoonSharp.Interpreter.Script.GlobalOptions.Platform = new AndroidLuaPlatform();
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
		}
	}

	// MoonSharp's io module opens the console streams whenever a Script is created, and Android has no console
	// (Console.OpenStandardInput throws PlatformNotSupportedException).
	private class AndroidLuaPlatform : MoonSharp.Interpreter.Platforms.DotNetCorePlatformAccessor {
		public override Stream IO_GetStandardStream(MoonSharp.Interpreter.Platforms.StandardFileType type) => Stream.Null;
	}
}
