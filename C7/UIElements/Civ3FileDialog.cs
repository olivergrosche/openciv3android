using C7Engine;
using C7GameData;
using Godot;
using Serilog;

[GlobalClass]
public partial class Civ3FileDialog : FileDialog {
	// An object for passing information (like save file paths) between scenes.
	GlobalSingleton Global;
	private ILogger log;

	// If true, go to scenario setup after loading, for scenarios.
	public bool GoToScenarioSetupAfterLoading = false;

	public override void _Ready() {
		base._Ready();
		log = LogManager.ForContext<Civ3FileDialog>();

		FileMode = FileDialog.FileModeEnum.OpenFile;
		Access = AccessEnum.Filesystem;

		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		FileSelected += OnFileSelected;

		if (OS.GetName() == "Android") {
			AndroidBootstrap.FitDialogForTouch(this, Size);
		}
	}

	public void SetDirectoryForLoading(string RelPath) {
		CurrentDir = DirectoryFor(RelPath);
		FileMode = FileDialog.FileModeEnum.OpenFile;
	}

	public void SetDirectoryForSaving(string RelPath) {
		CurrentDir = DirectoryFor(RelPath);
		FileMode = FileDialog.FileModeEnum.SaveFile;
	}

	// On Android, saved games go to Documents/OpenCiv3/Saves next to the game data. A Civ3 folder copied to the device
	// has no Conquests/Saves (Civ3 creates it on the first save), and for a missing folder the dialog shows the top of
	// the shared storage, where saved games then got lost among the user's files.
	private static string DirectoryFor(string relPath) {
		if (OS.GetName() == "Android" && relPath == "Conquests/Saves") {
			string saves = System.IO.Path.Combine(AndroidBootstrap.DataDir, "Saves");
			System.IO.Directory.CreateDirectory(saves);
			return saves;
		}
		return Util.Civ3Root + "/" + relPath;
	}

	// Loading and saving take seconds with big maps: the notice is drawn first (two frames), then the work blocks.
	private async void OnFileSelected(string path) {
		if (FileMode == FileDialog.FileModeEnum.OpenFile) {
			log.Information($"loading {path}");
			Global.LoadGamePath = path;
			LoadingOverlay.Show(GetTree().CurrentScene, GoToScenarioSetupAfterLoading ? L.T("Loading the scenario …") : L.T("Loading the game …"));
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (GoToScenarioSetupAfterLoading) {
				GetTree().ChangeSceneToFile("res://UIElements/NewGame/scenario_setup.tscn");
			} else {
				GetTree().ChangeSceneToFile("res://C7Game.tscn");
			}
		} else {
			if (!path.EndsWith(".json")) {
				path = path + ".json";
			}

			log.Information($"Saving game to {path}");
			CanvasLayer notice = LoadingOverlay.Show(GetTree().CurrentScene, L.T("Saving the game …"));
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			EngineStorage.ReadGameData((GameData gameData) => {
				C7GameData.Save.SaveGame.FromGameData(gameData).Save(path);
			});
			notice.QueueFree();
		}
	}
}
