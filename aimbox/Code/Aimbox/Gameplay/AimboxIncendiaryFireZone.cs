namespace Sandbox;

[Title( "Aimbox Incendiary Fire Zone" )]
[Category( "Aimbox" )]
public sealed class AimboxIncendiaryFireZone : Component
{
	const float TickIntervalSeconds = 0.25f;
	const float PlaneBaseSizeInches = 100f;
	const string FloorPlaneModelPath = "models/dev/plane.vmdl";
	const string FloorMaterialPath = "materials/dev/primary_white_emissive_trans.vmat";

	AimboxPlayerController _thrower;
	AimboxWeaponId _weaponId;
	float _radius;
	float _damagePerSecond;
	float _durationTotal;
	TimeUntil _lifetime;
	TimeSince _lastTick;
	ModelRenderer _outerFloor;
	ModelRenderer _innerFloor;

	public void Init(
		AimboxPlayerController thrower,
		AimboxWeaponId weaponId,
		Vector3 origin,
		float radius,
		float damagePerSecond,
		float durationSeconds )
	{
		_thrower = thrower;
		_weaponId = weaponId;
		_radius = Math.Max( 32f, radius );
		_damagePerSecond = Math.Max( 1f, damagePerSecond );
		_durationTotal = Math.Max( 0.5f, durationSeconds );
		_lifetime = _durationTotal;
		_lastTick = 0f;
		WorldPosition = origin;

		BuildFloorVisuals();
	}

	protected override void OnUpdate()
	{
		if ( (float)_lifetime <= 0f )
		{
			GameObject.Destroy();
			return;
		}

		UpdateFloorVisuals();

		if ( _lastTick < TickIntervalSeconds )
			return;

		_lastTick = 0f;
		var tickDamage = _damagePerSecond * TickIntervalSeconds;
		if ( tickDamage <= 0.01f )
			return;

		AimboxGrenadeDamage.ApplyAreaDamage(
			_thrower,
			_weaponId,
			WorldPosition,
			_radius,
			tickDamage,
			useQuadraticFalloff: false,
			horizontalOnly: true );
	}

	void BuildFloorVisuals()
	{
		if ( Application.IsDedicatedServer || Application.IsHeadless || !Game.IsPlaying )
			return;

		var material = Material.Load( FloorMaterialPath );
		var model = Model.Load( FloorPlaneModelPath );
		if ( model is null )
			return;

		_outerFloor = CreateFloorDisc( "Incendiary Floor", model, material, 1f );
		_innerFloor = CreateFloorDisc( "Incendiary Floor Hot", model, material, 0.62f );
	}

	ModelRenderer CreateFloorDisc( string name, Model model, Material material, float radiusFraction )
	{
		var disc = new GameObject( true, name );
		disc.SetParent( GameObject );
		disc.LocalPosition = Vector3.Up * 0.35f;
		disc.LocalRotation = Rotation.Identity;

		var diameter = _radius * 2f * radiusFraction;
		var scale = diameter / PlaneBaseSizeInches;
		disc.LocalScale = new Vector3( scale, scale, 1f );

		var renderer = disc.Components.Create<ModelRenderer>();
		renderer.Model = model;
		if ( material is not null )
			renderer.MaterialOverride = material;

		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		renderer.RenderOptions.Game = true;
		_ = EnsureGameRenderLayerAsync( renderer );
		return renderer;
	}

	async Task EnsureGameRenderLayerAsync( ModelRenderer renderer )
	{
		if ( !renderer.IsValid() )
			return;

		var deadline = DateTime.UtcNow.AddSeconds( 2 );
		while ( renderer.IsValid() && renderer.SceneObject is null && DateTime.UtcNow < deadline )
			await Task.DelayRealtimeSeconds( 0.016f );

		if ( !renderer.IsValid() )
			return;

		renderer.RenderOptions.Game = true;
		if ( renderer.SceneObject is not null )
			renderer.RenderOptions.Apply( renderer.SceneObject );
	}

	void UpdateFloorVisuals()
	{
		if ( _outerFloor is null || !_outerFloor.IsValid() )
			return;

		var remaining = (float)_lifetime;
		var life01 = _durationTotal <= 0f ? 0f : Math.Clamp( remaining / _durationTotal, 0f, 1f );
		var pulse = 0.84f + 0.16f * MathF.Sin( (float)Time.Now * 9.5f );

		ApplyFloorTint( _outerFloor, life01, pulse, hotCore: false );

		if ( _innerFloor is not null && _innerFloor.IsValid() )
			ApplyFloorTint( _innerFloor, life01, pulse, hotCore: true );
	}

	static void ApplyFloorTint( ModelRenderer renderer, float life01, float pulse, bool hotCore )
	{
		var alpha = life01 * (hotCore ? 0.92f : 0.72f) * pulse;
		if ( hotCore )
		{
			renderer.Tint = new Color( 1f, 0.72f, 0.12f, alpha );
			return;
		}

		renderer.Tint = new Color( 1f, 0.38f, 0.04f, alpha );
	}
}
