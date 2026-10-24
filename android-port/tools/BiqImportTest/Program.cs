// Checks whether OpenCiv3 (src-dev) can import a Civ3 Conquests scenario (.biq) and turn it into game data, without
// starting the game: dotnet run -- <scenario.biq> <Civ3 folder> <C7/Lua folder>
using System;
using System.IO;
using System.Linq;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;

class Program {
	static string FindText(string civ3, string searchFolders, string file) {
		foreach (string folder in (searchFolders ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)) {
			string p = Path.Combine(civ3, "Conquests", "Scenarios", folder.Trim(), "Text", file);
			if (File.Exists(p)) return p;
		}
		return Path.Combine(civ3, "Conquests", "Text", file);
	}

	static int Main(string[] args) {
		string biq = args[0], civ3 = args[1], lua = args[2];
		var start = DateTime.Now;
		SaveGame save = ImportCiv3.ImportBiq(biq, Path.Combine(civ3, "Conquests", "conquests.biq"),
			s => { string p = FindText(civ3, s, "PediaIcons.txt"); Console.WriteLine($"PediaIcons for '{s}': {p}"); return p; },
			s => FindText(civ3, s, "Civilopedia.txt"));
		Console.WriteLine($"imported in {(DateTime.Now - start).TotalMilliseconds:F0} ms");
		Console.WriteLine($"map {save.Map.tilesWide}x{save.Map.tilesTall}, civs {save.Civilizations.Count}, players {save.Players.Count}, cities {save.Cities.Count}, unit types {save.UnitPrototypes.Count}, units {save.Units.Count}, techs {save.Techs.Count}, governments {save.Governments.Count}");
		Console.WriteLine("civs: " + string.Join(", ", save.Civilizations.Select(c => c.name)));
		Console.WriteLine("players: " + string.Join(", ", save.Players.Select(p => $"{p.civilization}{(p.human ? "*" : "")}")));
		start = DateTime.Now;
		GameMode mode = GameMode.Load(lua, save.GameModeConfig);
		GameData data = save.ToGameData(mode.behaviors);
		Console.WriteLine($"game data in {(DateTime.Now - start).TotalMilliseconds:F0} ms: {data.players.Count} players, {data.mapUnits.Count} units, {data.cities.Count} cities, turn {data.turn}");
		foreach (Player p in data.players.Take(3)) {
			Console.WriteLine($"{p.civilization.name} knows: " + string.Join(", ", p.knownTechs.Select(id => data.techs.Find(t => t.id == id)?.Name)));
		}
		Tech trash = data.techs.Find(t => t.Name == "Trashcan");
		Console.WriteLine($"Trashcan era={trash?.EraCivilopediaName} cost={trash?.Cost} known by {data.players.Count(p => p.knownTechs.Contains(trash.id))} players");
		return 0;
	}
}
