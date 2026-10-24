using Godot;

// A "Loading" notice over the whole screen for the seconds that a saved game, a scenario or a new map takes. Without it
// the menu just froze after the choice, and players could not tell a slow load from a hang.
public static class LoadingOverlay {
	public static CanvasLayer Show(Node parent, string text) {
		float scale = OS.GetName() == "Android" ? AndroidBootstrap.TouchUiFactor : 1;
		var layer = new CanvasLayer { Layer = 100 };
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		// Parchment, like the game's popups.
		var style = new StyleBoxFlat { BgColor = new Color(0.91f, 0.86f, 0.74f), ContentMarginLeft = 48, ContentMarginRight = 48,
			ContentMarginTop = 24, ContentMarginBottom = 24 };
		style.SetCornerRadiusAll(10);
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", style);
		var label = new Label { Text = text };
		label.AddThemeColorOverride("font_color", new Color(0.18f, 0.12f, 0.06f));
		label.AddThemeFontSizeOverride("font_size", (int)(28 * scale));
		panel.AddChild(label);
		center.AddChild(panel);
		layer.AddChild(dim);
		layer.AddChild(center);
		parent.AddChild(layer);
		return layer;
	}
}
