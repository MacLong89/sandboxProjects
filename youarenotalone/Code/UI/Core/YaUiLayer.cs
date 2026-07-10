namespace Sandbox;

/// <summary>Strict render hierarchy — higher values draw on top. Never assign z-index outside this table.</summary>
public enum YaUiLayer
{
	PassiveOverlay = 10,
	Hud = 20,
	Notification = 30,
	Tooltip = 40,
	ModalScrim = 79,
	Fullscreen = 80,
	Critical = 90
}

public static class YaUiLayerZ
{
	public static int ToZIndex( YaUiLayer layer ) => layer switch
	{
		YaUiLayer.PassiveOverlay => 10,
		YaUiLayer.Hud => 20,
		YaUiLayer.Notification => 30,
		YaUiLayer.Tooltip => 40,
		YaUiLayer.ModalScrim => 79,
		YaUiLayer.Fullscreen => 80,
		YaUiLayer.Critical => 90,
		_ => 20
	};
}
