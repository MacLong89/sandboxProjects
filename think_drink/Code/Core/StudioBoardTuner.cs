namespace ThinkDrink;

using ThinkDrink.Studio;

/// <summary>Runtime tuning controls for the world-space question board.</summary>
public sealed class StudioBoardTuner : Component
{
	public const float DefaultScreenScale = 12f;
	public const float DefaultScreenCenterZ = 415f;
	public const float DefaultScreenY = -1008f;
	public static float ScreenScale { get; private set; } = DefaultScreenScale;
	public static float ScreenCenterZ { get; private set; }
	public static float ScreenY { get; private set; }
	public static int Revision { get; private set; }

	private WorldPanel _panel;
	private int _lastAppliedRevision = -1;

	public static string Readout =>
		$"Screen {ScreenScale:0.00}  Z {ScreenCenterZ:0}  Y {ScreenY:0}";

	protected override void OnAwake()
	{
		ScreenScale = DefaultScreenScale;
		ScreenCenterZ = DefaultScreenCenterZ;
		ScreenY = DefaultScreenY;
	}

	protected override void OnStart()
	{
		FindPanel();
		Apply();
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor ) return;

		HandleInput();

		if ( !_panel.IsValid() )
			FindPanel();

		if ( _lastAppliedRevision != Revision )
			Apply();
	}

	private void HandleInput()
	{
		var changed = false;

		if ( Input.Pressed( "Slot7" ) )
		{
			ScreenScale = Math.Clamp( ScreenScale - 0.25f, 0.25f, 16f );
			changed = true;
		}

		if ( Input.Pressed( "Slot8" ) )
		{
			ScreenScale = Math.Clamp( ScreenScale + 0.25f, 0.25f, 16f );
			changed = true;
		}

		if ( Input.Pressed( "Slot9" ) )
		{
			ScreenCenterZ = Math.Clamp( ScreenCenterZ - 16f, 32f, 1024f );
			changed = true;
		}

		if ( Input.Pressed( "Slot0" ) )
		{
			ScreenCenterZ = Math.Clamp( ScreenCenterZ + 16f, 32f, 1024f );
			changed = true;
		}

		if ( Input.Pressed( "Slot3" ) )
		{
			ScreenY = Math.Clamp( ScreenY - 64f, -1070f, 800f );
			changed = true;
		}

		if ( Input.Pressed( "Slot4" ) )
		{
			ScreenY = Math.Clamp( ScreenY + 64f, -1070f, 800f );
			changed = true;
		}

		if ( Input.Pressed( "Reload" ) )
		{
			ScreenScale = DefaultScreenScale;
			ScreenCenterZ = DefaultScreenCenterZ;
			ScreenY = DefaultScreenY;
			changed = true;
		}

		if ( changed )
		{
			Revision++;
			Log.Info( $"[ThinkDrink][BoardTuner] {Readout}" );
		}
	}

	private void FindPanel()
	{
		_panel = StudioEnvironment.Instance?.ScoreboardWorldPanel;
	}

	private void Apply()
	{
		if ( !_panel.IsValid() ) return;

		// Never inflate canvas pixels — scale the world footprint instead.
		_panel.PanelSize = StudioDimensions.BillboardPanelPx * ScreenScale;
		_panel.RenderScale = StudioDimensions.BillboardRenderScale;

		var display = _panel.GameObject;
		if ( display.IsValid() )
		{
			display.LocalScale = Vector3.One;
			display.WorldPosition = new Vector3(
				StudioDimensions.BillboardWorldPos.x,
				ScreenY,
				ScreenCenterZ );
		}

		_lastAppliedRevision = Revision;
	}
}
