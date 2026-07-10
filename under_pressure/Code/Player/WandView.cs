namespace UnderPressure;

/// <summary>
/// First-person viewmodel for whatever tool is equipped: a pressure-washer wand, a scrub
/// brush, or a squeegee. Assembled from box primitives parented to the camera view
/// (bottom-right) and rebuilt when the equipped tool (or the Nozzle upgrade) changes. The
/// working tip is exposed in world space so the spray jet / contact effect originates there.
///
/// Local frame follows the s and box camera convention: +X forward, +Y left, +Z up, so the
/// tool is pushed forward (+X), right (-Y) and down (-Z).
/// </summary>
public sealed class WandView : Component
{
	private static readonly Color Metal = new( 0.34f, 0.36f, 0.4f );
	private static readonly Color Dark = new( 0.14f, 0.15f, 0.17f );
	private static readonly Color Grip = new( 0.16f, 0.46f, 0.86f );
	private static readonly Color Orange = new( 0.95f, 0.55f, 0.15f );
	private static readonly Color Gold = new( 1f, 0.8f, 0.25f );
	private static readonly Color TipBlue = new( 0.5f, 0.85f, 1f );
	private static readonly Color Rubber = new( 0.12f, 0.13f, 0.15f );
	private static readonly Color Bristle = new( 0.92f, 0.86f, 0.6f );
	private static readonly Color WoodHandle = new( 0.62f, 0.44f, 0.26f );
	private static readonly Color YellowGrip = new( 0.95f, 0.78f, 0.2f );

	private ToolType _tool = (ToolType)(-1);
	private int _tier = -1;
	private Vector3 _tip = new( 28f, -4f, -5f );
	private readonly List<GameObject> _parts = new();

	// Eased 0..1 "scrubbing" amount so the brush bob fades in/out instead of snapping.
	private float _scrub;

	/// <summary>World-space working tip, where the jet / contact effect originates.</summary>
	public Vector3 NozzleWorldPos => WorldTransform.PointToWorld( _tip );

	protected override void OnStart() => Rebuild();

	protected override void OnUpdate()
	{
		var tool = GameCore.Instance?.Tools.Equipped ?? ToolType.PressureWasher;
		var tier = NozzleTier();
		if ( tool != _tool || (tool == ToolType.PressureWasher && tier != _tier) )
			Rebuild();

		AnimateScrub();
	}

	/// <summary>While scrubbing (brush equipped + M1 held), bob the whole viewmodel up and down
	/// with a little forward push so it reads as an active scrubbing motion.</summary>
	private void AnimateScrub()
	{
		var scrubbing = _tool == ToolType.ScrubBrush && (PressureWasher.Instance?.IsSpraying ?? false);
		var target = scrubbing ? 1f : 0f;
		_scrub += (target - _scrub) * Math.Clamp( Time.Delta * 12f, 0f, 1f );

		if ( _scrub < 0.001f )
		{
			GameObject.LocalPosition = Vector3.Zero;
			return;
		}

		var t = Time.Now * 20f;
		var bob = MathF.Sin( t ) * 2.2f;            // up/down along view-up (-Z is down)
		var push = MathF.Abs( MathF.Sin( t ) ) * 1f; // slight forward jab each stroke
		GameObject.LocalPosition = new Vector3( push, 0f, bob ) * _scrub;
	}

	/// <summary>Tier 0..3 driven by the Nozzle Width upgrade level.</summary>
	private static int NozzleTier()
	{
		var lvl = GameCore.Instance?.Upgrades.Level( UpgradeId.Nozzle ) ?? 0;
		if ( lvl <= 0 ) return 0;
		if ( lvl < 4 ) return 1;
		if ( lvl < 9 ) return 2;
		return 3;
	}

	private void Rebuild()
	{
		_tool = GameCore.Instance?.Tools.Equipped ?? ToolType.PressureWasher;
		_tier = NozzleTier();

		foreach ( var part in _parts )
			part?.Destroy();
		_parts.Clear();

		switch ( _tool )
		{
			case ToolType.ScrubBrush: BuildBrush(); break;
			case ToolType.Squeegee: BuildSqueegee(); break;
			case ToolType.Gun: BuildGun(); break;
			default: BuildWasher( _tier ); break;
		}
	}

	private void BuildWasher( int tier )
	{
		// Grows a little each tier; stays small and basic overall.
		var reach = 26f + tier * 4f;
		var girth = 3.6f + tier * 0.5f;
		var baseP = new Vector3( 14f, -5f, -8f );
		_tip = baseP + new Vector3( reach, 1.5f, 3.5f );

		var dir = (_tip - baseP).Normal;
		var along = dir.EulerAngles;
		var lanceLen = (_tip - baseP).Length;
		var mid = (baseP + _tip) * 0.5f;

		Add( Scenery.Box( GameObject, "Grip", new Vector3( 11f, -5f, -14f ), new Vector3( 7f, 7f, 15f ), Grip, new Angles( 22f, 0f, 0f ), GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Trigger", new Vector3( 16f, -5f, -9f ), new Vector3( 9f, 6f, 10f ), Dark, default, GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Lance", mid, new Vector3( lanceLen, girth, girth ), Metal, along, GameMaterials.Metal ) );

		if ( tier >= 1 )
		{
			var bandColor = tier >= 3 ? Gold : Orange;
			Add( Scenery.Box( GameObject, "Band", baseP + dir * lanceLen * 0.55f, new Vector3( girth + 2.5f, girth + 2.5f, girth + 2.5f ), bandColor, along, GameMaterials.Metal ) );
		}

		if ( tier >= 2 )
			Add( Scenery.Box( GameObject, "NozzleBody", baseP + dir * lanceLen * 0.82f, new Vector3( girth + 3f, girth + 2f, girth + 2f ), Grip, along, GameMaterials.Metal ) );

		Add( Scenery.Box( GameObject, "NozzleTip", _tip, new Vector3( 4f + tier, 3f + tier * 0.5f, 3f + tier * 0.5f ), TipBlue, along, GameMaterials.Metal ) );
	}

	private void BuildBrush()
	{
		// A compact handheld scrub brush: short grip and a small bristle head.
		var grip = new Vector3( 14f, -5f, -10f );
		var head = new Vector3( 22f, -4f, -4f );
		_tip = new Vector3( 25f, -4f, -4f );

		Add( Scenery.Box( GameObject, "Grip", grip, new Vector3( 4.5f, 4.5f, 10f ), Dark, new Angles( 18f, 0f, 0f ), GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Pole", new Vector3( 18f, -4.5f, -6.5f ), new Vector3( 10f, 2.6f, 2.6f ), WoodHandle, new Angles( -18f, 0f, 0f ), GameMaterials.Wood ) );
		Add( Scenery.Box( GameObject, "Head", head, new Vector3( 5f, 12f, 4.5f ), WoodHandle, default, GameMaterials.Wood ) );
		Add( Scenery.Box( GameObject, "Bristles", _tip, new Vector3( 3.5f, 11f, 3.5f ), Bristle, default, GameMaterials.Leaves ) );
	}

	private void BuildSqueegee()
	{
		// A long handle held blade-forward, with a long thin rubber blade far out ahead of
		// the camera. Larger X reaches further away, so the whole tool sits deeper in view.
		var grip = new Vector3( 14f, -5f, -12f );
		var neck = new Vector3( 32f, -4.5f, -5f );
		_tip = new Vector3( 50f, -4f, -2f );

		Add( Scenery.Box( GameObject, "Grip", grip, new Vector3( 7f, 7f, 16f ), YellowGrip, new Angles( 18f, 0f, 0f ), GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Neck", neck, new Vector3( 34f, 3.2f, 3.2f ), Metal, new Angles( -16f, 0f, 0f ), GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Spine", new Vector3( 48f, -4f, -2f ), new Vector3( 3f, 44f, 3f ), Metal, default, GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Blade", _tip, new Vector3( 1.8f, 46f, 2.4f ), Rubber, default, GameMaterials.Metal ) );
	}

	private void BuildGun()
	{
		var grip = new Vector3( 10f, -4f, -10f );
		_tip = new Vector3( 34f, -3f, -4f );

		Add( Scenery.Box( GameObject, "Grip", grip, new Vector3( 5f, 8f, 14f ), Dark, new Angles( 12f, 0f, 0f ), GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Slide", new Vector3( 22f, -3f, -4f ), new Vector3( 24f, 5f, 6f ), Metal, new Angles( -4f, 0f, 0f ), GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Barrel", _tip, new Vector3( 14f, 3f, 3f ), Metal, new Angles( -4f, 0f, 0f ), GameMaterials.Metal ) );
		Add( Scenery.Box( GameObject, "Sight", new Vector3( 18f, -3f, 0f ), new Vector3( 4f, 2f, 4f ), Orange, default, GameMaterials.Metal ) );
	}

	private void Add( GameObject go ) => _parts.Add( go );
}
