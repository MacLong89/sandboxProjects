namespace RunGun;

public enum EnemyType
{
	Brute,
	Rusher,
	Tank,
	Splitter,
	Shielded,
	Swarm,
	Spitter,
	Elite,
	Boss,
}

public static class EnemyTypePresentation
{
	public static Color TintFor( EnemyType type, bool elite ) => type switch
	{
		EnemyType.Rusher => new Color( 0.45f, 0.7f, 1f ),
		EnemyType.Tank => new Color( 0.25f, 0.35f, 0.55f ),
		EnemyType.Splitter => new Color( 0.55f, 0.35f, 0.95f ),
		EnemyType.Shielded => new Color( 0.25f, 0.55f, 0.95f ),
		EnemyType.Swarm => new Color( 0.55f, 0.65f, 0.85f ),
		EnemyType.Spitter => new Color( 0.35f, 0.85f, 0.75f ),
		EnemyType.Boss => new Color( 0.15f, 0.25f, 0.75f ),
		_ => elite ? new Color( 0.7f, 0.85f, 1f ) : DistrictTheme.SecurityTint,
	};

	public static float BaseScaleFor( EnemyType type ) => type switch
	{
		EnemyType.Tank => 1.55f,
		EnemyType.Boss => 2.2f,
		EnemyType.Swarm => 0.65f,
		EnemyType.Rusher => 0.95f,
		_ => 1.25f,
	};

	/// <summary>
	/// Final body scale. Tougher enemies read as physically bigger, so a late-run brute towers
	/// over an opening-stretch one and elites/bosses are unmistakable.
	/// </summary>
	public static float ScaleFor( EnemyType type, float health, bool elite )
	{
		var baseScale = BaseScaleFor( type );
		var hpNorm = Math.Clamp( health / GameConstants.EnemySizeHealthRef, 0f, 1f );

		// Bosses are already huge; grow them gently. Everyone else swells with health.
		var hpBonus = type == EnemyType.Boss ? hpNorm * 0.35f : hpNorm * GameConstants.EnemyMaxSizeBonus;
		var scale = baseScale * (1f + hpBonus);
		if ( elite ) scale *= GameConstants.EliteSizeMult;
		return scale;
	}
}
