namespace ThinkDrink.Studio;

/// <summary>Creates configured world-space UI panels (gameplay screens).</summary>
public static class StudioWorldPanels
{
	public static GameObject CreatePanel<TPanel>(
		GameObject parent,
		string name,
		Vector3 localPos,
		Rotation localRot,
		Vector2 panelSizePx,
		float renderScale )
		where TPanel : Component, new()
	{
		var display = new GameObject( parent, true, name );
		display.LocalPosition = localPos;
		display.LocalRotation = localRot;
		return FinishPanel<TPanel>( display, panelSizePx, renderScale );
	}

	public static GameObject CreateWorldPanel<TPanel>(
		GameObject parent,
		string name,
		Vector3 worldPos,
		Rotation worldRot,
		Vector2 panelSizePx,
		float renderScale )
		where TPanel : Component, new()
	{
		var display = new GameObject( parent, true, name );
		display.WorldPosition = worldPos;
		display.WorldRotation = worldRot;
		return FinishPanel<TPanel>( display, panelSizePx, renderScale );
	}

	static GameObject FinishPanel<TPanel>( GameObject display, Vector2 panelSizePx, float renderScale )
		where TPanel : Component, new()
	{
		display.LocalScale = Vector3.One;

		display.Components.Create<WorldPanel>();
		display.Components.Create<TPanel>();

		var cfg = display.Components.Create<StudioWorldPanelConfig>();
		cfg.PanelSize = panelSizePx;
		cfg.RenderScale = renderScale;

		display.Components.Create<StudioWorldPanelSetup>();
		return display;
	}
}

/// <summary>Per-panel WorldPanel tuning — read by <see cref="ThinkDrink.StudioWorldPanelSetup"/>.</summary>
public sealed class StudioWorldPanelConfig : Component
{
	[Property] public Vector2 PanelSize { get; set; } = new( 1920f, 1080f );
	[Property] public float RenderScale { get; set; } = 4f;
}
