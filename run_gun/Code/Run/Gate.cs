namespace RunGun;

/// <summary>
/// A shootable buff gate covering one half of the lane. Bullets pump its value up; when the
/// player runs through it, the value is applied to a specific build stat. Left/right gates
/// offer different stats so every row is a real commit decision.
/// </summary>
public sealed class Gate : Component
{
	public BuildStat Stat { get; private set; }
	public GateOp Op { get; private set; }
	public float Value { get; private set; }
	public bool LeftSide { get; private set; }
	public bool Applied { get; set; }

	public float X => WorldPosition.x;
	public float MinY { get; private set; }
	public float MaxY { get; private set; }

	private ModelRenderer _frame;
	private UI.WorldLabel _label;
	private Color _accent;

	public void Setup( BuildStat stat, GateOp op, float value, bool leftSide )
	{
		Stat = stat;
		Op = op;
		Value = value;
		LeftSide = leftSide;

		var centerY = leftSide ? -GameConstants.LaneHalf * 0.5f : GameConstants.LaneHalf * 0.5f;
		MinY = leftSide ? -GameConstants.LaneHalf : 0f;
		MaxY = leftSide ? 0f : GameConstants.LaneHalf;
		WorldPosition = WorldPosition.WithY( centerY );

		_accent = BuildStatPresentation.ColorFor( stat );

		var slab = new GameObject( GameObject, true, "Slab" );
		slab.LocalPosition = new Vector3( 0f, 0f, GameConstants.GateHeight * 0.5f );
		slab.LocalScale = MeshPrimitives.BoxScale( new Vector3( 26f, GameConstants.GateWidth, GameConstants.GateHeight ) );
		_frame = slab.Components.Create<ModelRenderer>();
		_frame.Model = MeshPrimitives.Box;
		_frame.MaterialOverride = MeshPrimitives.Mat;
		_frame.Tint = _accent.WithAlpha( 0.55f );

		var labelGo = new GameObject( GameObject, true, "Label" );
		// Sit the label right on the barrier face, at eye level and nudged toward the player
		// (-X) so it reads on the near side of the slab instead of floating up in the sky.
		labelGo.LocalPosition = new Vector3( -18f, 0f, GameConstants.GateHeight * 0.55f );
		labelGo.LocalScale = Vector3.One * GameConstants.GateLabelScale;

		var wp = labelGo.Components.Create<Sandbox.WorldPanel>();
		// Tall canvas for a compact two-line stack (value over stat name) that fits one half.
		wp.PanelSize = new Vector2( 640f, 620f );
		wp.RenderScale = 1f;
		wp.LookAtCamera = true;
		wp.InteractionRange = 0f;
		wp.RenderOptions.Game = true;

		_label = labelGo.Components.Create<UI.WorldLabel>();
		_label.Accent = _accent;
		_label.Big = true;
		_label.Sub = BuildStatPresentation.ShortName( Stat );
		RefreshLabel();
	}

	public void Hit( RunState run )
	{
		if ( Applied ) return;
		run.OnGateHit();
		Value = run.PumpGateValue( Stat, Op, Value );
		RefreshLabel();
	}

	public void Apply( RunState run )
	{
		run.ApplyGateEffect( Stat, Op, Value );
		run.OnGateCross( Op == GateOp.Mult );
		VfxManager.Instance?.SpawnGatePop( WorldPosition, BuildStatPresentation.FormatGateLabel( Stat, Op, Value ), _accent );
	}

	public bool Contains( float y ) => y >= MinY && y <= MaxY;

	private void RefreshLabel()
	{
		if ( _label is null ) return;
		_label.Text = BuildStatPresentation.FormatGateValue( Stat, Op, Value );
		_label.Sub = BuildStatPresentation.ShortName( Stat );
	}
}
