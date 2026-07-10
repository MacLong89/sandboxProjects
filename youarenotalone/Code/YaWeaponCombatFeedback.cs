namespace Sandbox;

/// <summary>
/// Authoritative hit endpoints are packed by the host and mirrored with <see cref="YaWeapon.RpcFireOutcome"/> — clients spawn visuals only (THORNS §3 host-validated combat).
/// </summary>
public enum YaWeaponImpactSurfaceKind
{
	None = 0,
	Terrain = 1,
	Metal = 2,
	Player = 3
}

/// <summary>Client-only brief hit flashes (no tracer mesh). No gameplay or damage logic.</summary>
public static class YaWeaponCombatFeedback
{
	public const float ImpactFxLifeSeconds = 0.075f;

	public static Vector3 UnpackHitMm( int xMm, int yMm, int zMm ) =>
		new Vector3( xMm / 1000f, yMm / 1000f, zMm / 1000f );

	public static void PackHitMm( Vector3 worldPos, out int xMm, out int yMm, out int zMm )
	{
		xMm = (int)MathF.Round( worldPos.x * 1000f );
		yMm = (int)MathF.Round( worldPos.y * 1000f );
		zMm = (int)MathF.Round( worldPos.z * 1000f );
	}

	/// <summary>Guns: optional hit puff only (skipped for <see cref="YaWeaponImpactSurfaceKind.None"/> when no damage).</summary>
	public static void SpawnGunTracerAndImpactLocal(
		Vector3 traceStartWorld,
		Vector3 traceEndWorld,
		YaWeaponImpactSurfaceKind surfaceKind,
		bool damageAppliedToTarget )
	{
		_ = traceStartWorld;

		if ( !damageAppliedToTarget && surfaceKind == YaWeaponImpactSurfaceKind.None )
			return;

		SpawnImpactLocal( traceEndWorld, surfaceKind, damageAppliedToTarget );
	}

	/// <summary>Melee / misc: impact only at authoritative endpoint.</summary>
	public static void SpawnImpactOnlyLocal( Vector3 hitWorld, YaWeaponImpactSurfaceKind surfaceKind )
	{
		SpawnImpactLocal( hitWorld, surfaceKind, damageAppliedToTarget: true );
	}

	static void SpawnImpactLocal( Vector3 worldPos, YaWeaponImpactSurfaceKind surfaceKind, bool damageAppliedToTarget )
	{
		var go = new GameObject( true );
		go.Name = "YaImpact";
		go.WorldPosition = worldPos;
		var (tint, light) = surfaceKind switch
		{
			YaWeaponImpactSurfaceKind.Player => (new Color( 0.95f, 0.2f, 0.1f, 0.9f ), 110f),
			YaWeaponImpactSurfaceKind.Metal => (new Color( 1f, 0.85f, 0.25f, 0.85f ), 80f),
			YaWeaponImpactSurfaceKind.Terrain => (new Color( 0.55f, 0.45f, 0.32f, 0.75f ), 60f),
			_ => damageAppliedToTarget
				? (new Color( 0.9f, 0.9f, 0.9f, 0.6f ), 50f)
				: (new Color( 0.7f, 0.7f, 0.7f, 0.35f ), 30f)
		};

		var pl = go.Components.Create<PointLight>();
		pl.LightColor = tint;
		pl.Radius = light * 0.12f;

		for ( var i = 0; i < 4; i++ )
		{
			var sparkGo = new GameObject( true, $"YaImpactSpark{i}" );
			sparkGo.SetParent( go );
			sparkGo.WorldPosition = worldPos + Vector3.Random.WithZ( MathF.Abs( Vector3.Random.z ) ) * 6f;
			var spark = sparkGo.Components.Create<PointLight>();
			spark.LightColor = tint;
			spark.Radius = light * 0.05f;
		}

		var d = go.Components.Create<YaDestroyAfterSeconds>();
		d.Seconds = ImpactFxLifeSeconds;
	}
}

/// <summary>Destroys the hosting <see cref="GameObject"/> after a short delay (FX cleanup).</summary>
[Title( "Thorns — Destroy After Seconds" )]
[Category( "Thorns" )]
public sealed class YaDestroyAfterSeconds : Component
{
	[Property] public float Seconds { get; set; } = 0.1f;

	double _spawnTime;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		_spawnTime = Time.Now;
	}

	protected override void OnUpdate()
	{
		if ( Time.Now >= _spawnTime + Seconds )
			GameObject.Destroy();
	}
}
