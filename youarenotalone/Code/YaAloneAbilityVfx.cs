namespace Sandbox;

/// <summary>Host-authoritative brief ability VFX (no gameplay).</summary>
public static class YaAloneAbilityVfx
{
	public static void SpawnDashBurstLocal( Vector3 worldPos )
	{
		SpawnRingBurst( worldPos + Vector3.Up * 12f, new Color( 0.35f, 0.95f, 0.92f, 0.85f ), 64f, 0.12f );
	}

	public static void SpawnParanoiaPulseLocal( Vector3 worldPos )
	{
		SpawnRingBurst( worldPos + Vector3.Up * 24f, new Color( 0.55f, 0.15f, 0.75f, 0.9f ), 120f, 0.22f );
	}

	public static void SpawnMimicShimmerLocal( Vector3 worldPos )
	{
		SpawnRingBurst( worldPos + Vector3.Up * 40f, new Color( 0.95f, 0.95f, 1f, 0.75f ), 48f, 0.16f );
	}

	static void SpawnRingBurst( Vector3 pos, Color tint, float lightRadius, float lifeSeconds )
	{
		var go = new GameObject( true, "YaAbilityVfx" );
		go.WorldPosition = pos;

		var pl = go.Components.Create<PointLight>();
		pl.LightColor = tint;
		pl.Radius = lightRadius;

		for ( var i = 0; i < 3; i++ )
		{
			var spark = new GameObject( true, $"YaAbilitySpark{i}" );
			spark.SetParent( go );
			spark.LocalPosition = Vector3.Random * 12f;
			var sl = spark.Components.Create<PointLight>();
			sl.LightColor = tint;
			sl.Radius = lightRadius * 0.35f;
		}

		var d = go.Components.Create<YaDestroyAfterSeconds>();
		d.Seconds = lifeSeconds;
	}
}
