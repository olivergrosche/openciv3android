using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Godot;

/// <summary>
/// Civ3's ambient sounds on the map: now and then a lark, a cricket, a hawk or a howling wolf, chosen by the terrain
/// in view. Civ3 decides this inside its executable; this follows the same idea with its eight sounds from
/// Sounds/Ambience Sfx. Every few seconds it picks a random known land tile on screen and plays one of the sounds that
/// fit its terrain: larks and crickets over open land, woodlark, oriole and wolves in woods, hawks and eagles over hills
/// and mountains, wolves on the tundra. The sea stays quiet. The volume is the "Ambience" slider in the preferences.
/// </summary>
public partial class AmbienceSoundscape : Node {
	// Keys are terrain keys (overlay terrain first, then base terrain); values are "ambience.*" keys in Lua audio.lua.
	private static readonly Dictionary<string, string[]> SoundsByTerrain = new() {
		{ "grassland", ["meadowlark", "skylark", "cricket"] },
		{ "plains", ["meadowlark", "skylark", "cricket"] },
		{ "flood plain", ["meadowlark", "cricket"] },
		{ "forest", ["woodlark", "oriole", "wolf"] },
		{ "jungle", ["oriole", "cricket"] },
		{ "marsh", ["cricket"] },
		{ "hills", ["hawk", "skylark", "wolf"] },
		{ "mountains", ["eagle", "hawk"] },
		{ "volcano", ["eagle"] },
		{ "tundra", ["wolf"] },
		{ "desert", ["hawk"] },
	};
	private const double MinPause = 8, MaxPause = 20; // seconds between two sounds

	public Game Game { get; init; }
	public MapView MapView { get; init; }

	private readonly Random random = new();
	private double untilNext = 5;
	private bool unavailable;

	public override void _Process(double delta) {
		untilNext -= delta;
		if (unavailable || untilNext > 0) {
			return;
		}
		untilNext = MinPause + random.NextDouble() * (MaxPause - MinPause);
		if (Game.controller == null || Game.ShowsScreenOverMap()) {
			return;
		}

		string sound = PickSound();
		if (sound == null) {
			return;
		}
		try {
			GlobalAudio.manager.PlayAmbienceAudio("ambience." + sound);
		} catch (Exception) {
			// A game mode without these sounds (OpenCiv3's own media) stays silent.
			unavailable = true;
		}
	}

	private string PickSound() {
		Vector2 screen = MapView.getVisibleAreaSize();
		string result = null;
		EngineStorage.ReadGameData((GameData gameData) => {
			for (int attempt = 0; attempt < 8 && result == null; attempt++) {
				Vector2 point = new((float)random.NextDouble() * screen.X, (float)random.NextDouble() * screen.Y);
				Tile tile = MapView.tileOnScreenAt(gameData.map, point);
				if (tile == null || tile == Tile.NONE || !Game.controller.tileKnowledge.isTileKnown(tile)) {
					continue;
				}
				if (SoundsByTerrain.TryGetValue(tile.overlayTerrainType.Key, out string[] sounds)
					|| SoundsByTerrain.TryGetValue(tile.baseTerrainType.Key, out sounds)) {
					result = sounds[random.Next(sounds.Length)];
				}
			}
		});
		return result;
	}
}
