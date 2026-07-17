namespace DeepDive;

/// <summary>
/// Brief spear sprite that flies from the diver toward a harpoon target.
/// </summary>
public sealed class HarpoonSpearFx : Component
{
	public float Duration { get; set; } = 0.28f;
	public float SpriteWorldLength { get; set; } = 1.05f;

	private Vector3 _start;
	private Vector3 _end;
	private float _elapsed;
	private Action _onArrive;
	private SpriteRenderer _renderer;

	public static void Launch( Vector3 from, Vector3 to, Action onArrive )
	{
		var go = new GameObject( true, "HarpoonSpear" );
		go.WorldPosition = from.WithY( 0.35f );
		var fx = go.Components.Create<HarpoonSpearFx>();
		fx._start = from.WithY( 0.35f );
		fx._end = to.WithY( 0.35f );
		fx._onArrive = onArrive;

		var tex = DeepDivePixelArt.HarpoonSpear();
		fx._renderer = DeepDiveSprites.SpawnTexture( go, tex, 1f, name: "SpearSprite" );
		DeepDivePixelArt.ApplyWorldWidth( fx._renderer, fx.SpriteWorldLength, tex );
		fx.OrientToward( to );
	}

	protected override void OnUpdate()
	{
		_elapsed += Time.Delta;
		var t = MathF.Min( 1f, _elapsed / MathF.Max( Duration, 0.05f ) );
		var eased = 1f - (1f - t) * (1f - t);

		WorldPosition = Vector3.Lerp( _start, _end, eased );
		OrientToward( _end );

		if ( t < 1f )
			return;

		_onArrive?.Invoke();
		GameObject.Destroy();
	}

	private void OrientToward( Vector3 target )
	{
		var delta = target.WithY( WorldPosition.y ) - WorldPosition;
		if ( delta.Length < 0.01f )
			return;

		// Sprite lies in XZ; rotate around Y so the spear tip points at the target.
		var angle = MathF.Atan2( delta.z, delta.x ) * (180f / MathF.PI);
		WorldRotation = Rotation.From( 0f, angle + 90f, 0f );
	}
}
