using Godot;
using System;
using C7Engine;
using Serilog;

public partial class MainMenu : Node {
	private ILogger log;

	[Export]
	Civ3FileDialog LoadDialog;
	[Export]
	Control NoCiv3Options;
	[Export]
	FileDialog SetCiv3HomeDialog;
	[Export]
	Civ3FileDialog LoadScenarioDialog;
	[Export]
	MenuButtonContainer ButtonContainer;
	[Export]
	Preferences preferences;

	GlobalSingleton Global;

	public override void _EnterTree() {
		GlobalAudio.manager = GetNode<AudioManager>("/root/GlobalAudioManager");

		base._EnterTree();
	}

	public override void _Ready() {
		log = LogManager.ForContext<MainMenu>();
		log.Debug("enter MainMenu._Ready");

		// Log where we're loading data from and whether the original
		// Civilization III media was found. This makes path-related problems
		// (missing C7.ini, an unset civ3InstallDir) diagnosable from the log
		// instead of only showing up as missing or placeholder graphics.
		log.Information("Data dir: {GameDir}; settings: {SettingsFile}; civ3 install: {Civ3Root}; standalone media: {Standalone}, classic media available: {Classic}",
			GamePaths.BaseDir, C7Settings.SettingsFilePath, Util.Civ3Root,
			C7Settings.UseStandaloneMode(), ClassicGraphicsAvailable());

		DisplayServer.WindowSetTitle((string)ProjectSettings.GetSetting("application/config/name"));

		try {
			DisplayTitleScreen();
			PlayMusic();
		} catch (Exception ex) {
			log.Error(ex, "Could not set up the main menu");
		}
	}

	// The menu buttons are sized for a mouse. On a tablet they get 1.6x larger, on a phone (AndroidBootstrap.TouchUiFactor)
	// as large as fits between the title and the copyright lines and left of the sidebar, growing from the menu's top
	// left corner. The wider "Civilization III was not found" options would otherwise reach into the sidebar. Measured
	// again whenever DisplayTitleScreen switches between those options and the menu.
	private async void EnlargeMenuForTouch() {
		Control menu = GetNode<Control>("Control/Background/PanelContainer");
		Control footer = GetNode<Control>("Control/Background/RichTextLabel2");
		Control sidebar = GetNode<Control>("Control/Background/MarginContainer/PanelContainer2");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		menu.ResetSize(); // the container keeps the size of the wider options otherwise
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		float scale = 1.6f * AndroidBootstrap.TouchUiFactor;
		float roomDown = footer.GlobalPosition.Y - 8 - menu.GlobalPosition.Y;
		float roomRight = sidebar.GlobalPosition.X - 16 - menu.GlobalPosition.X;
		if (menu.Size.Y > 0 && menu.Size.X > 0) {
			scale = Math.Max(1, Math.Min(scale, Math.Min(roomDown / menu.Size.Y, roomRight / menu.Size.X)));
		}
		menu.Scale = new Vector2(scale, scale);
	}

	private void DisplayTitleScreen() {
		if (OS.GetName() == "Android") {
			EnlargeMenuForTouch();
		}

		// To pass data between scenes, putting path string in a global singleton and reading it later in createGame
		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		Global.ResetLoadGameFields();

		LoadDialog.SetDirectoryForLoading(@"Conquests/Saves");
		LoadScenarioDialog.SetDirectoryForLoading(@"Conquests/Scenarios");
		LoadScenarioDialog.GoToScenarioSetupAfterLoading = true;

		if (!C7Settings.UseStandaloneMode() && !ClassicGraphicsAvailable()) {
			NoCiv3Options.Visible = true;
			ButtonContainer.Visible = false;
			return;
		}

		ButtonContainer.Visible = true;
		ButtonContainer.CreateButtons();

		// TODO: enable buttons are features are implemented
		ButtonContainer.NewGame.Pressed += GoToWorldSetup;
		ButtonContainer.QuickStart.Pressed += QuickStartGame;
		ButtonContainer.Tutorial.Pressed += QuickStartGame;
		ButtonContainer.Tutorial.Visible = false;
		ButtonContainer.LoadGame.Pressed += LoadGame;
		ButtonContainer.LoadScenario.Pressed += LoadScenario;
		ButtonContainer.HallOfFame.Pressed += HallOfFame;
		ButtonContainer.HallOfFame.Visible = false;
		ButtonContainer.Preferences.Pressed += ShowPreferences;
		ButtonContainer.Credits.Pressed += showCredits;
		ButtonContainer.Exit.Pressed += _on_Exit_pressed;

		ButtonContainer.ToggleGraphics.Pressed += () => {
			// Without Civ3 files, the button asks for their folder (copied to a phone after starting in standalone mode).
			if (C7Settings.UseStandaloneMode() && !ClassicGraphicsAvailable()) {
				_on_SetCiv3Home_pressed();
				return;
			}
			Global.ToggleStandaloneMode();
			GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
		};
		SetToggleGraphicsText();

		// Hide if valid path is present as proven by reaching this point in code
		NoCiv3Options.Visible = false;
	}

	private bool ClassicGraphicsAvailable() {
		if (string.IsNullOrEmpty(Util.Civ3Root)) {
			return false;
		}

		string[] basePaths = ["Conquests", "civ3PTW", ""];
		foreach (string basePath in basePaths) {
			string relPath = string.IsNullOrEmpty(basePath) ? "Art/buttonsFINAL.pcx" : $"{basePath}/Art/buttonsFINAL.pcx";
			if (Util.FileExistsIgnoringCase(Util.Civ3Root, relPath) != null) {
				return true;
			}
		}

		return false;
	}

	// The button works as a switch: it shows the media in use, and pressing it changes to the other. The menu itself
	// looks the same with both, so a label like "Use OpenCiv3 Media" left players unsure whether anything happened.
	private void SetToggleGraphicsText() {
		if (C7Settings.UseStandaloneMode()) {
			ButtonContainer.ToggleGraphics.Text = "Graphics: OpenCiv3";
		} else {
			ButtonContainer.ToggleGraphics.Text = "Graphics: Civilization III";
		}
	}

	public void GoToWorldSetup() {
		PlayButtonPressedSound();
		GetTree().ChangeSceneToFile("res://UIElements/NewGame/world_setup.tscn");
	}

	public async void QuickStartGame() {
		log.Information("start game button pressed");
		PlayButtonPressedSound();
		QuickStartSetup.Init(Global);
		LoadingOverlay.Show(this, L.T("Loading the game …"));
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GetTree().ChangeSceneToFile("res://C7Game.tscn");
	}

	public void LoadGame() {
		log.Information("load game button pressed");
		PlayButtonPressedSound();
		LoadDialog.Popup();
	}

	public void LoadScenario() {
		log.Information("load scenario button pressed");
		PlayButtonPressedSound();
		LoadScenarioDialog.Popup();
	}

	public void showCredits() {
		log.Information("credits button pressed");
		GetTree().ChangeSceneToFile("res://Credits.tscn");
	}

	public void HallOfFame() {
		PlayButtonPressedSound();
	}

	public void ShowPreferences() {
		PlayButtonPressedSound();
		preferences.ShowPreferences();
	}

	public void _on_Exit_pressed() {
		GetTree().Quit(); // no need to notify the scene tree
	}

	private void _on_SetCiv3Home_pressed() {
		if (OS.GetName() == "Android") {
			// Start where the Civ3 files are copied to, and make the dialog big enough for fingers.
			SetCiv3HomeDialog.CurrentDir = AndroidBootstrap.SharedStorage + "/Documents";
			AndroidBootstrap.FitDialogForTouch(SetCiv3HomeDialog, new Vector2(570, 450));
		}
		SetCiv3HomeDialog.PopupCentered();
	}

	private void _on_SetCiv3HomeDialog_dir_selected(string path) {
		path = AndroidBootstrap.Civ3RootOf(path);
		string previous = Util.Civ3Root;
		Util.Civ3Root = path;
		if (!ClassicGraphicsAvailable()) {
			// Keep the old setting, so that a wrong pick does not hide Civ3 files that are found automatically later.
			Util.Civ3Root = previous;
			var notFound = new AcceptDialog {
				Title = "Civilization III",
				DialogText = L.F("There are no Civilization III files in {0}.\nChoose the folder that contains Art and Conquests.", path),
			};
			if (OS.GetName() == "Android") {
				notFound.ContentScaleFactor = 1.2f * AndroidBootstrap.TouchUiFactor;
				AndroidBootstrap.StyleDialogButtons(notFound);
			}
			AddChild(notFound);
			notFound.PopupCentered();
			return;
		}
		C7Settings.SetValue("locations", "civ3InstallDir", path);
		C7Settings.SaveSettings();
		if (C7Settings.UseStandaloneMode()) {
			// Reached from the media button in standalone mode: switch to the Civ3 media right away.
			Global.ToggleStandaloneMode();
			GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
			return;
		}
		// Reached from NoCiv3Options, shown when DisplayTitleScreen found no Civ3 files.
		DisplayTitleScreen();
	}

	private void UseStandaloneModePressed() {
		Global.ActivateGameMode(GamePaths.standalone);
		DisplayTitleScreen();
	}

	private void PlayMusic() {
		GlobalAudio.manager.PlayMusic("menu.main_menu_1");
	}

	private void PlayButtonPressedSound() {
		GlobalAudio.manager.PlayUIAudio("buttons.button_1");
	}
}
