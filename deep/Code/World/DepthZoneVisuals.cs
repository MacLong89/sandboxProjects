namespace Deep;

/// <summary>
/// Depth-driven look: water column tint, skybox mute, camera clear, and light.
/// Progressive darker blue with depth.
/// </summary>
public sealed class DepthZoneVisuals : Component
{
	private DirectionalLight _sun;
	private SkyBox2D _skybox;
	private Color _skyboxSurfaceTint = new( 0.7f, 0.85f, 1f );
	private DepthZone _lastZone = (DepthZone)(-1);

	protected override void OnStart()
	{
		_sun = Scene.GetAllComponents<DirectionalLight>().FirstOrDefault();
		_skybox = Scene.GetAllComponents<SkyBox2D>().FirstOrDefault();
		if ( _skybox is not null && _skybox.IsValid() )
			_skyboxSurfaceTint = _skybox.Tint;

		Apply( 0f );
	}

	public void Apply( float depthMeters )
	{
		var balance = DeepGame.Instance?.Balance ?? BalanceConfig.Defaults;
		var zone = balance.ZoneAtDepth( depthMeters );
		var color = ColorForDepth( depthMeters, balance );
		var brightness = BrightnessForDepth( depthMeters, balance ) * (1f + balance.VisibilityBonus);
		brightness = Math.Clamp( brightness, 0.04f, 1.35f );
		// Final water read used by clear color + underwater fill plate.
		var water = WaterColor( color, brightness );

		DeepGame.Instance?.DiveCamera?.SetBackground( DayNightVisuals.Apply( water, DeepGame.Instance?.Clock ) );
		OceanBackdrop.Instance?.ApplyDepthLook( water, brightness, depthMeters );
		ApplySkybox( depthMeters, water );
		ApplySun( depthMeters, color, brightness );

		if ( zone != _lastZone )
		{
			_lastZone = zone;
			if ( DeepGame.Instance?.Phase == GamePhase.Diving )
			{
				var label = zone switch
				{
					DepthZone.Sunlit => "Shallow Waters",
					DepthZone.BlueDepths => "Blue Depths",
					DepthZone.Twilight => "Twilight Zone",
					DepthZone.Midnight => "Midnight Zone",
					DepthZone.Abyssal => "Abyssal Plains",
					_ => "Hadal Trenches"
				};
				DeepGame.Instance.ShowMessage( label, 1.8f );
			}
		}
	}

	/// <summary>
	/// SkyBox2D replaces Camera.BackgroundColor whenever it is enabled.
	/// Mute / disable it underwater so the depth water color can show.
	/// </summary>
	private void ApplySkybox( float depthMeters, Color water )
	{
		if ( _skybox is null || !_skybox.IsValid() )
			return;

		// Surface: keep the day sky. Once submerged, kill it — BackgroundColor + water plate take over.
		if ( depthMeters < 1.2f )
		{
			_skybox.Enabled = true;
			var blend = MathX.Clamp( depthMeters / 1.2f, 0f, 1f );
			_skybox.Tint = Color.Lerp( _skyboxSurfaceTint, water, blend * 0.85f );
		}
		else
		{
			_skybox.Enabled = false;
		}
	}

	private void ApplySun( float depthMeters, Color color, float brightness )
	{
		if ( _sun is null || !_sun.IsValid() )
			return;

		if ( depthMeters < 2f )
		{
			_sun.LightColor = new Color( 1f, 0.96f, 0.88f ) * MathX.Lerp( 1.05f, 0.55f, depthMeters / 2f );
			_sun.SkyColor = Color.Lerp( new Color( 0.55f, 0.75f, 0.95f ), color, depthMeters / 2f );
		}
		else
		{
			_sun.LightColor = color * MathF.Max( brightness, 0.05f );
			_sun.SkyColor = color * MathF.Max( brightness * 0.35f, 0.02f );
		}
	}

	public static Color WaterColor( Color hue, float brightness ) =>
		hue * MathF.Max( brightness, 0.03f );

	/// <summary>1.0 at surface → near-black at max depth (smooth continuous falloff).</summary>
	public static float BrightnessForDepth( float depthMeters, BalanceConfig balance )
	{
		balance ??= BalanceConfig.Defaults;
		var max = MathF.Max( 1f, balance.MaxOceanDepthMeters );
		var t = MathX.Clamp( depthMeters / max, 0f, 1f );
		// Ease in sooner so mid-dive already feels murky, abyss reads near-black.
		var curved = MathF.Pow( t, 0.75f );
		return MathX.Lerp( 1f, 0.04f, curved );
	}

	/// <summary>Continuous blue → deep navy → near-black, based purely on depth.</summary>
	public static Color ColorForDepth( float depthMeters, BalanceConfig balance )
	{
		balance ??= BalanceConfig.Defaults;
		var max = MathF.Max( 1f, balance.MaxOceanDepthMeters );
		var t = MathX.Clamp( depthMeters / max, 0f, 1f );

		var surface = new Color( 0.38f, 0.78f, 0.88f );
		var shallow = new Color( 0.16f, 0.45f, 0.7f );
		var mid = new Color( 0.06f, 0.18f, 0.42f );
		var deep = new Color( 0.02f, 0.05f, 0.14f );
		var abyss = new Color( 0.005f, 0.01f, 0.03f );

		if ( t < 0.15f )
			return Color.Lerp( surface, shallow, t / 0.15f );
		if ( t < 0.4f )
			return Color.Lerp( shallow, mid, (t - 0.15f) / 0.25f );
		if ( t < 0.7f )
			return Color.Lerp( mid, deep, (t - 0.4f) / 0.3f );
		return Color.Lerp( deep, abyss, (t - 0.7f) / 0.3f );
	}
}
