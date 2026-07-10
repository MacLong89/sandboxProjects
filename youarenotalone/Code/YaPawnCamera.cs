namespace Sandbox;

/// <summary>
/// Local-only active camera on a child of the pawn. Remote pawns keep this disabled (document: camera is local-player only).
/// Eye is offset in <see cref="EyeOffsetLocal"/> (default Z-up: along +Z from pawn feet).
/// First-person viewmodels should parent to this component's <see cref="GameObject"/> (same transform as <see cref="CameraComponent"/>), not Scene.Camera.
/// </summary>
[Title( "Thorns — Pawn Camera" )]
[Category( "Thorns" )]
[Icon( "videocam" )]
[Order( 100 )]
public sealed class YaPawnCamera : Component
{
	/// <summary>Eye position in pawn local space. For Z-up maps use +Z; for Y-up use +Y on this vector.</summary>
	[Property] public Vector3 EyeOffsetLocal { get; set; } = new Vector3( 0f, 0f, 52f );

	/// <summary>Near clip (world units). High values (e.g. 2) clip first-person weapon meshes parented to this camera.</summary>
	[Property] public float ZNearPlay { get; set; } = 0.08f;

	/// <summary>Hip-fire FOV when not aiming down sights.</summary>
	[Property] public float HipFieldOfView { get; set; } = 80f;

	/// <summary>ADS FOV (lower = zoom in).</summary>
	[Property] public float AdsFieldOfView { get; set; } = 20f;

	/// <summary>Higher = faster FOV blend to ADS/hip.</summary>
	[Property] public float AdsFovLerpSpeed { get; set; } = 10f;

	CameraComponent _camera;
	YaPawnMovement _movement;
	YaWeapon _weapon;

	protected override void OnAwake()
	{
		_camera = Components.GetOrCreate<CameraComponent>();
		_camera.IsMainCamera = false;
		_camera.Enabled = false;
		_camera.FieldOfView = HipFieldOfView;
		_camera.ZNear = ZNearPlay;
		_camera.ZFar = 12000f;

		_movement = GameObject.Parent?.Components.Get<YaPawnMovement>();
		_weapon = GameObject.Parent?.Components.Get<YaWeapon>();
	}

	protected override void OnStart()
	{
		if ( !YaPawn.IsLocalConnectionOwner( this ) )
			return;

		if ( !Game.IsPlaying )
			return;

		_camera.Enabled = true;
		_camera.IsMainCamera = true;

		foreach ( var other in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( !other.IsValid() || other == _camera )
				continue;
			other.IsMainCamera = false;
			other.Enabled = false;
		}
	}

	protected override void OnUpdate()
	{
		if ( !YaPawn.IsLocalConnectionOwner( this ) || !_camera.Enabled )
			return;

		if ( _movement is null || !_movement.IsValid() )
			_movement = GameObject.Parent?.Components.Get<YaPawnMovement>();
		if ( _weapon is null || !_weapon.IsValid() )
			_weapon = GameObject.Parent?.Components.Get<YaWeapon>();

		var parent = GameObject.Parent;
		if ( !parent.IsValid() )
			return;

		var pitch = _movement is not null && _movement.IsValid() ? _movement.LookAngles.pitch : 0f;
		var hasWeaponEquipped = _weapon is not null && _weapon.IsValid() && !string.IsNullOrWhiteSpace( _weapon.ClientMirrorCombatDefinitionId );
		var combatIdCam = _weapon.ClientMirrorCombatDefinitionId ?? "";
		var meleeEquipped = YaWeaponDefinitions.TreatsAsMeleeWeapon( YaWeaponDefinitions.Get( combatIdCam ), combatIdCam );
		var wantAds = hasWeaponEquipped && !meleeEquipped && (Input.Down( "Attack2" ) || Input.Down( "attack2" ));
		var targetFov = wantAds ? AdsFieldOfView : HipFieldOfView;
		_camera.FieldOfView = MathX.Lerp( _camera.FieldOfView, targetFov, Math.Clamp( Time.Delta * AdsFovLerpSpeed, 0f, 1f ) );

		GameObject.LocalPosition = EyeOffsetLocal;
		GameObject.LocalRotation = Rotation.FromAxis( Vector3.Right, pitch );
	}
}
