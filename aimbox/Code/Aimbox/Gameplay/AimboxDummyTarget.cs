using Sandbox.Citizen;

namespace Sandbox;

[Title( "Aimbox Dummy Target" )]
[Category( "Aimbox" )]
public sealed class AimboxDummyTarget : Component
{
	const string AimSphereVisualChildName = "AimSphereVisual";
	const string AimSphereModelPath = "models/dev/sphere.vmdl";
	const string AimSphereMaterialPath = "materials/dev/primary_white_emissive_trans.vmat";
	public const float AimSphereRadius = 11f;
	static readonly Color AimSphereTint = new( 0.95f, 0.28f, 0.18f );

	[Property] public int MaxHealth { get; set; } = 250;
	[Property] public float RespawnSeconds { get; set; } = 1.25f;
	[Property] public string ModelPath { get; set; } = "models/citizen/citizen.vmdl";
	[Property] public bool AimCircleMode { get; set; }
	[Property] public float AimSphereRadiusScale { get; set; } = 1f;

	public int Health { get; private set; }
	public bool IsAlive { get; private set; } = true;

	GameObject _body;
	SkinnedModelRenderer _renderer;
	GameObject _aimSphereVisual;
	ModelRenderer _sphereRenderer;
	bool _aimSphereBuilt;
	CitizenAnimationHelper _anim;
	GameObject _weaponWorld;
	SkinnedModelRenderer _weaponRenderer;
	Collider _collider;
	TimeSince _deadTime;
	Vector3 _spawnPosition;
	Rotation _spawnRotation;

	protected override void OnStart()
	{
		_spawnPosition = WorldPosition;
		_spawnRotation = WorldRotation;
		Health = MaxHealth;

		if ( AimCircleMode )
			return;

		Log.Info( $"[Aimbox] Dummy OnStart on '{GameObject.Name}' using model '{ModelPath}'." );
		BuildCitizenDummy();
	}

	public void ConfigureAimSphere( float radiusScale = 1f )
	{
		AimCircleMode = true;
		AimSphereRadiusScale = radiusScale;
		BuildAimSphere();
	}

	protected override void OnUpdate()
	{
		if ( AimCircleMode )
			return;

		if ( IsAlive )
		{
			TickCitizenPresentation();
			return;
		}

		if ( _deadTime < RespawnSeconds )
			return;

		Respawn();
	}

	protected override void OnPreRender()
	{
		if ( AimCircleMode || !IsAlive )
			return;

		TickWeaponPresentation();
	}

	public bool TakeDamage( AimboxPlayerController attacker, AimboxWeaponId weapon, float damage, bool headshot ) =>
		TakeDamage( attacker as IAimboxCombatActor, weapon, damage, headshot );

	public bool TakeDamage( IAimboxCombatActor attacker, AimboxWeaponId weapon, float damage, bool headshot )
	{
		if ( !IsAlive )
			return false;

		Health = Math.Max( 0, Health - (int)MathF.Round( damage ) );
		FlashDamageTint( headshot );

		var killed = Health <= 0;
		var aimMode = AimboxAimModeRules.IsAimMode( AimboxGame.Instance?.Match.Mode ?? default );
		if ( attacker is AimboxPlayerController player && aimMode )
			AimboxAimDrillController.Instance?.OnTargetDamaged( this, player, headshot, killed );

		if ( !killed )
			return false;

		if ( AimCircleMode && aimMode )
			return true;

		Die( attacker, weapon, headshot );
		return true;
	}

	void Die( AimboxPlayerController attacker, AimboxWeaponId weapon, bool headshot ) =>
		Die( attacker as IAimboxCombatActor, weapon, headshot );

	void Die( IAimboxCombatActor attacker, AimboxWeaponId weapon, bool headshot )
	{
		IsAlive = false;
		_deadTime = 0;
		SetRenderersEnabled( false );

		if ( attacker is AimboxPlayerController player )
		{
			if ( AimboxAimModeRules.IsAimMode( AimboxGame.Instance?.Match.Mode ?? default ) )
				return;

			player.ConfirmDummyKill( weapon, headshot );
		}
	}

	void Respawn()
	{
		Log.Info( $"[Aimbox] Dummy '{GameObject.Name}' respawned." );
		WorldPosition = _spawnPosition;
		WorldRotation = _spawnRotation;
		Health = MaxHealth;
		IsAlive = true;
		SetRenderersEnabled( true );
		ResetTint();
	}

	public void ForceRespawnAt( Vector3 position, Rotation rotation )
	{
		_spawnPosition = position;
		_spawnRotation = rotation;
		_deadTime = RespawnSeconds;
		Respawn();
	}

	public void InstantRespawnAt( Vector3 position, Rotation rotation )
	{
		_spawnPosition = position;
		_spawnRotation = rotation;
		WorldPosition = position;
		WorldRotation = rotation;
		Health = MaxHealth;
		IsAlive = true;
		_deadTime = 0;
		SetRenderersEnabled( true );
		ResetTint();
	}

	void BuildCitizenDummy()
	{
		_body = AimboxCitizenPresentation.FindChild( GameObject, AimboxCitizenPresentation.BodyChildName );
		if ( !_body.IsValid() )
		{
			_body = new GameObject( true, AimboxCitizenPresentation.BodyChildName );
			_body.SetParent( GameObject );
		}
		_body.LocalPosition = Vector3.Zero;
		_body.LocalRotation = Rotation.Identity;
		_body.LocalScale = Vector3.One;

		_renderer = _body.Components.Get<SkinnedModelRenderer>() ?? _body.Components.Create<SkinnedModelRenderer>();
		_renderer.Model = Model.Load( ModelPath );
		_renderer.Tint = Color.White;
		_renderer.UseAnimGraph = true;
		_renderer.CreateBoneObjects = true;

		_anim = _body.Components.Get<CitizenAnimationHelper>() ?? _body.Components.Create<CitizenAnimationHelper>();
		_anim.Target = _renderer;

		EnsureWorldWeapon();

		var capsule = Components.Get<CapsuleCollider>() ?? Components.Create<CapsuleCollider>();
		AimboxHitboxes.ConfigureCitizenCapsule( capsule );
		_collider = capsule;
	}

	void BuildAimSphere()
	{
		if ( _aimSphereBuilt )
			return;

		_aimSphereBuilt = true;
		GameObject.LocalScale = Vector3.One;

		_aimSphereVisual = FindAimSphereVisual();
		if ( !_aimSphereVisual.IsValid() )
		{
			_aimSphereVisual = new GameObject( true, AimSphereVisualChildName );
			_aimSphereVisual.SetParent( GameObject );
		}

		_aimSphereVisual.LocalPosition = Vector3.Zero;
		_aimSphereVisual.LocalRotation = Rotation.Identity;

		var model = Model.Load( AimSphereModelPath );
		if ( model is null || model.IsError )
		{
			Log.Warning( $"[Aimbox AIM] Failed to load '{AimSphereModelPath}' for aim target visual." );
			model = null;
		}

		_aimSphereVisual.LocalScale = ResolveAimSphereVisualScale( model, EffectiveAimSphereRadius );

		_sphereRenderer = _aimSphereVisual.Components.Get<ModelRenderer>() ?? _aimSphereVisual.Components.Create<ModelRenderer>();
		_sphereRenderer.Model = model;
		if ( Material.Load( AimSphereMaterialPath ) is { IsValid: true } material )
			_sphereRenderer.MaterialOverride = material.CreateCopy();

		_sphereRenderer.Tint = AimSphereTint;
		_sphereRenderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		_sphereRenderer.RenderOptions.Game = true;

		var sphere = Components.Get<SphereCollider>() ?? Components.Create<SphereCollider>();
		sphere.Radius = EffectiveAimSphereRadius;
		sphere.Center = Vector3.Zero;
		_collider = sphere;

		_ = ApplyAimSphereRenderLayerAsync( _sphereRenderer );
	}

	float EffectiveAimSphereRadius => AimSphereRadius * Math.Max( 0.25f, AimSphereRadiusScale );

	GameObject FindAimSphereVisual()
	{
		foreach ( var child in GameObject.Children )
		{
			if ( child.IsValid() && string.Equals( child.Name, AimSphereVisualChildName, StringComparison.OrdinalIgnoreCase ) )
				return child;
		}

		return default;
	}

	public float GetAimSphereColliderWorldRadius()
	{
		if ( !AimCircleMode )
			return 0f;

		var sphere = Components.Get<SphereCollider>();
		if ( sphere is null )
			return 0f;

		return GetWorldUniformScale( GameObject ) * sphere.Radius;
	}

	public float GetAimSphereVisualWorldRadius()
	{
		if ( !AimCircleMode || !_sphereRenderer.IsValid() || _sphereRenderer.Model is null || _sphereRenderer.Model.IsError )
			return 0f;

		var bounds = _sphereRenderer.Model.Bounds;
		var modelDiameter = Math.Max( bounds.Size.x, Math.Max( bounds.Size.y, bounds.Size.z ) );
		if ( modelDiameter <= 0.001f )
			return 0f;

		var visualGo = _sphereRenderer.GameObject;
		return modelDiameter * GetWorldUniformScale( visualGo ) * 0.5f;
	}

	static float GetWorldUniformScale( GameObject go )
	{
		if ( go is null || !go.IsValid() )
			return 1f;

		var scale = go.WorldScale;
		return Math.Max( scale.x, Math.Max( scale.y, scale.z ) );
	}

	static Vector3 ResolveAimSphereVisualScale( Model model, float targetRadius )
	{
		if ( model is null || model.IsError )
			return Vector3.One;

		var bounds = model.Bounds;
		var modelDiameter = Math.Max( bounds.Size.x, Math.Max( bounds.Size.y, bounds.Size.z ) );
		if ( modelDiameter <= 0.001f )
			return Vector3.One;

		var scale = (targetRadius * 2f) / modelDiameter;
		return new Vector3( scale, scale, scale );
	}

	static async System.Threading.Tasks.Task ApplyAimSphereRenderLayerAsync( ModelRenderer renderer )
	{
		if ( !renderer.IsValid() )
			return;

		var deadline = DateTime.UtcNow.AddSeconds( 2 );
		while ( renderer.IsValid() && renderer.SceneObject is null && DateTime.UtcNow < deadline )
			await System.Threading.Tasks.Task.Delay( 16 );

		if ( !renderer.IsValid() )
			return;

		renderer.RenderOptions.Game = true;
		if ( renderer.SceneObject is not null )
			renderer.RenderOptions.Apply( renderer.SceneObject );
	}

	void FlashDamageTint( bool headshot )
	{
		if ( AimCircleMode )
		{
			if ( _sphereRenderer.IsValid() )
				_sphereRenderer.Tint = headshot ? new Color( 1f, 0.95f, 0.35f ) : new Color( 1f, 0.55f, 0.2f );
			return;
		}

		if ( _renderer.IsValid() )
			_renderer.Tint = headshot ? new Color( 1f, 0.22f, 0.16f ) : new Color( 1f, 0.78f, 0.25f );
	}

	void ResetTint()
	{
		if ( AimCircleMode )
		{
			if ( _sphereRenderer.IsValid() )
				_sphereRenderer.Tint = AimSphereTint;
			return;
		}

		if ( _renderer.IsValid() )
			_renderer.Tint = Color.White;
	}

	void SetRenderersEnabled( bool enabled )
	{
		if ( AimCircleMode )
		{
			if ( _sphereRenderer.IsValid() )
				_sphereRenderer.Enabled = enabled;
		}
		else
		{
			if ( _renderer.IsValid() )
				_renderer.Enabled = enabled;
			if ( _weaponRenderer.IsValid() )
				_weaponRenderer.Enabled = enabled;
		}

		if ( _collider is not null )
			_collider.Enabled = enabled;
	}

	void TickCitizenPresentation()
	{
		if ( !_anim.IsValid() )
			return;

		_anim.WithVelocity( Vector3.Zero );
		_anim.IsGrounded = true;
		_anim.DuckLevel = 0f;
		_anim.AimAngle = WorldRotation;
		_anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
	}

	void EnsureWorldWeapon()
	{
		_weaponWorld = AimboxCitizenPresentation.FindChild( GameObject, AimboxCitizenPresentation.WorldWeaponChildName );
		if ( !_weaponWorld.IsValid() )
		{
			_weaponWorld = new GameObject( true, AimboxCitizenPresentation.WorldWeaponChildName );
			_weaponWorld.SetParent( GameObject );
		}

		_weaponRenderer = _weaponWorld.Components.Get<SkinnedModelRenderer>() ?? _weaponWorld.Components.Create<SkinnedModelRenderer>();
		if ( AimboxWeaponResourceLoad.TryLoadWeaponWorldModel( AimboxWeaponResourceLoad.M4WorldModelPath, "dummy/bandit m4", out var model ) )
			_weaponRenderer.Model = model;
		_weaponRenderer.UseAnimGraph = false;
		_weaponRenderer.Tint = Color.White;
		_weaponRenderer.Enabled = true;
	}

	void TickWeaponPresentation()
	{
		if ( !_weaponWorld.IsValid() )
			EnsureWorldWeapon();

		if ( !_weaponWorld.IsValid() )
			return;

		_weaponWorld.LocalScale = AimboxCitizenPresentation.WorldWeaponLocalScale;
		var handAttached = AimboxCitizenPresentation.TryAlignWeaponToCitizenRightHand( GameObject, _weaponWorld );
		if ( !handAttached )
			AimboxCitizenPresentation.ParentWorldWeaponToBodyFallback( GameObject, _weaponWorld, 0f );

		AimboxCitizenPresentation.WireCitizenHandIk( GameObject, _weaponWorld, handAttached );
	}
}
