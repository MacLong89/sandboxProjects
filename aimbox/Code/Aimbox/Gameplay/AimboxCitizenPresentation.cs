using Sandbox.Citizen;

namespace Sandbox;

public static class AimboxCitizenPresentation
{
	public const string CitizenVmdl = "models/citizen/citizen.vmdl";
	public const string BodyChildName = "Body";
	public const string WorldWeaponChildName = "WeaponWorld";

	public static readonly Vector3 WorldWeaponLocalScale = new( 1.5f, 1.5f, 1.5f );
	public static readonly Vector3 WorldWeaponLocalPositionRelBody = new( 18f, -12f, 18f );
	public static readonly Vector3 WorldWeaponLocalPositionIfNoBody = new( 0f, 0f, 40f );
	/// <summary>Matches standing eye (64) − crouch eye (42) so body fallback lowers with duck.</summary>
	public const float WorldWeaponDuckBodyDrop = AimboxHitboxes.CitizenCrouchHeightDrop;
	/// <summary>sbox w_* meshes are authored sideways relative to citizen rifle hand bones.</summary>
	public static readonly Angles WorldWeaponHandLocalEulerDegrees = new( 0f, 0f, -90f );
	/// <summary>Hand-bone local nudge: +X forward, +Y toward body center, −Z down.</summary>
	public static readonly Vector3 WorldWeaponHandLocalPosition = new( 10f, -6f, -3f );

	static readonly string[] RightHandBoneCandidates =
	[
		"hand_R",
		"Hold_R",
		"wrist_R",
		"weapon_hand_R"
	];

	static readonly string[] RightHandIkFallbackBoneCandidates =
	[
		"hand_R_IK_target"
	];

	public static void EnsureCitizenBody( AimboxPlayerController player )
	{
		if ( player is null || !player.IsValid() )
			return;

		EnsureCitizenBodyInternal( player.GameObject, player.IsProxy );
	}

	public static void EnsureCitizenBody( AimboxBotController bot )
	{
		if ( bot is null || !bot.IsValid() )
			return;

		EnsureCitizenBodyInternal( bot.GameObject, alwaysVisible: true );
	}

	static void EnsureCitizenBodyInternal( GameObject root, bool alwaysVisible )
	{
		var body = FindChild( root, BodyChildName );
		if ( !body.IsValid() )
		{
			body = new GameObject( true, BodyChildName );
			body.SetParent( root );
		}

		body.LocalPosition = Vector3.Zero;
		body.LocalRotation = Rotation.Identity;
		body.LocalScale = Vector3.One;

		var skin = body.Components.Get<SkinnedModelRenderer>();
		if ( !skin.IsValid() )
			skin = body.Components.Create<SkinnedModelRenderer>();

		skin.Model = Model.Load( CitizenVmdl );
		skin.Tint = new Color( 0.88f, 0.82f, 0.76f, 1f );
		skin.UseAnimGraph = true;
		skin.CreateBoneObjects = true;
		skin.Enabled = alwaysVisible;

		var helper = body.Components.Get<CitizenAnimationHelper>();
		if ( !helper.IsValid() )
			helper = body.Components.Create<CitizenAnimationHelper>();
		helper.Target = skin;

		if ( !root.Components.Get<AimboxCitizenBodyDriver>( FindMode.EnabledInSelf ).IsValid() )
			root.Components.Create<AimboxCitizenBodyDriver>();

		if ( !root.Components.Get<AimboxThirdPersonWeaponVisual>( FindMode.EnabledInSelf ).IsValid() )
			root.Components.Create<AimboxThirdPersonWeaponVisual>();

		var collider = root.Components.Get<CapsuleCollider>() ?? root.Components.Create<CapsuleCollider>();
		AimboxHitboxes.ConfigureCitizenCapsule( collider );
	}

	public static void SetLocalBodyHidden( AimboxPlayerController player, bool hidden )
	{
		var body = FindChild( player?.GameObject, BodyChildName );
		if ( !body.IsValid() )
			return;

		foreach ( var renderer in body.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.IsValid() )
				renderer.Enabled = !hidden;
		}
	}

	public static GameObject FindChild( GameObject root, string name )
	{
		if ( root is null || !root.IsValid() || string.IsNullOrWhiteSpace( name ) )
			return default;

		foreach ( var child in root.Children )
		{
			if ( child.IsValid() && string.Equals( child.Name, name, StringComparison.OrdinalIgnoreCase ) )
				return child;
		}

		return default;
	}

	public static GameObject FindDescendantNamed( GameObject root, string name )
	{
		if ( root is null || !root.IsValid() || string.IsNullOrWhiteSpace( name ) )
			return default;

		foreach ( var child in root.Children )
		{
			if ( !child.IsValid() )
				continue;

			if ( string.Equals( child.Name, name, StringComparison.OrdinalIgnoreCase ) )
				return child;

			var nested = FindDescendantNamed( child, name );
			if ( nested.IsValid() )
				return nested;
		}

		return default;
	}

	public static bool TryGetCitizenBodySkin( GameObject pawnRoot, out SkinnedModelRenderer skin )
	{
		skin = default;
		if ( !pawnRoot.IsValid() )
			return false;

		var body = FindDescendantNamed( pawnRoot, BodyChildName );
		if ( !body.IsValid() )
			return false;

		skin = body.Components.Get<SkinnedModelRenderer>();
		return skin.IsValid();
	}

	public static Vector3 ComputeMovementBobOffsetLocal( Vector3 worldVelocity, float time )
	{
		var speed = worldVelocity.WithZ( 0 ).Length;
		if ( speed < 40f )
			return Vector3.Zero;

		var phase = time * (5.2f + speed * 0.016f );
		var vertical = MathF.Abs( MathF.Sin( phase ) ) * MathF.Min( speed * 0.013f, 4.5f );
		var lateral = MathF.Sin( phase * 2f + MathF.PI * 0.25f ) * MathF.Min( speed * 0.0045f, 1.4f );
		var forward = MathF.Sin( phase ) * MathF.Min( speed * 0.0025f, 0.9f );
		return new Vector3( forward, lateral, vertical );
	}

	public static bool TryAlignWeaponToCitizenRightHand(
		GameObject pawnRoot,
		GameObject weaponWorld,
		Vector3 bobPawnLocal = default )
	{
		if ( !TryGetCitizenBodySkin( pawnRoot, out var skin ) || !weaponWorld.IsValid() )
			return false;

		skin.CreateBoneObjects = true;

		foreach ( var boneName in RightHandBoneCandidates )
		{
			if ( TryAttachWeaponToCitizenBone( skin, pawnRoot, weaponWorld, boneName, bobPawnLocal, applyBob: false ) )
				return true;
		}

		foreach ( var boneName in RightHandIkFallbackBoneCandidates )
		{
			if ( TryAttachWeaponToCitizenBone( skin, pawnRoot, weaponWorld, boneName, bobPawnLocal, applyBob: true ) )
				return true;
		}

		return false;
	}

	static bool TryAttachWeaponToCitizenBone(
		SkinnedModelRenderer skin,
		GameObject pawnRoot,
		GameObject weaponWorld,
		string boneName,
		Vector3 bobPawnLocal,
		bool applyBob )
	{
		if ( !TryGetCitizenBoneObject( skin, boneName, out var bone ) )
			return false;

		if ( weaponWorld.Parent != bone )
			weaponWorld.SetParent( bone );

		var bobLocal = Vector3.Zero;
		if ( applyBob && bobPawnLocal.LengthSquared > 0.01f )
			bobLocal = BobPawnLocalToBoneLocal( bone, bobPawnLocal, pawnRoot.WorldRotation );

		ApplyWorldWeaponHandLocalTransform( weaponWorld, bobLocal );
		return true;
	}

	static Vector3 BobPawnLocalToBoneLocal( GameObject bone, Vector3 bobPawnLocal, Rotation pawnWorldRotation )
	{
		var worldOffset = pawnWorldRotation * bobPawnLocal;
		return bone.WorldRotation.Inverse * worldOffset;
	}

	public static void ParentWorldWeaponToBodyFallback(
		GameObject pawnRoot,
		GameObject weaponWorld,
		float duckLevel = 0f,
		Vector3 bobPawnLocal = default )
	{
		if ( !pawnRoot.IsValid() || !weaponWorld.IsValid() )
			return;

		duckLevel = Math.Clamp( duckLevel, 0f, 1f );
		var duckDrop = Vector3.Down * (WorldWeaponDuckBodyDrop * duckLevel);
		var body = FindDescendantNamed( pawnRoot, BodyChildName );
		if ( body.IsValid() )
		{
			if ( weaponWorld.Parent != body )
				weaponWorld.SetParent( body );

			weaponWorld.LocalPosition = WorldWeaponLocalPositionRelBody + duckDrop + bobPawnLocal;
			weaponWorld.LocalRotation = Rotation.From( WorldWeaponHandLocalEulerDegrees );
			return;
		}

		if ( weaponWorld.Parent != pawnRoot )
			weaponWorld.SetParent( pawnRoot );
		weaponWorld.LocalPosition = WorldWeaponLocalPositionIfNoBody + duckDrop + bobPawnLocal;
		weaponWorld.LocalRotation = Rotation.From( WorldWeaponHandLocalEulerDegrees );
	}

	static void ApplyWorldWeaponHandLocalTransform( GameObject weaponWorld, Vector3 extraLocalOffset = default )
	{
		weaponWorld.LocalRotation = Rotation.From( WorldWeaponHandLocalEulerDegrees );
		weaponWorld.LocalPosition = WorldWeaponHandLocalPosition + extraLocalOffset;
	}

	static bool TryGetCitizenBoneObject( SkinnedModelRenderer skin, string boneName, out GameObject boneObject )
	{
		boneObject = default;
		if ( string.IsNullOrWhiteSpace( boneName ) || !skin.IsValid() || !skin.Model.IsValid() )
			return false;

		if ( skin.Model.Bones.HasBone( boneName ) )
		{
			var bone = skin.Model.Bones.GetBone( boneName );
			boneObject = skin.GetBoneObject( bone );
			if ( boneObject.IsValid() && boneObject.Scene is not null )
				return true;
		}

		boneObject = skin.GetBoneObject( boneName );
		return boneObject.IsValid() && boneObject.Scene is not null;
	}

	public static void WireCitizenHandIk( GameObject pawnRoot, GameObject weaponWorld, bool handAttached )
	{
		if ( !TryGetCitizenBodySkin( pawnRoot, out var skin ) )
			return;

		var helper = skin.GameObject.Components.Get<CitizenAnimationHelper>();
		if ( !helper.IsValid() )
			return;

		if ( handAttached )
		{
			helper.IkRightHand = null;
			return;
		}

		helper.IkRightHand = weaponWorld.IsValid() ? weaponWorld : null;
	}
}

[Title( "Aimbox Citizen Body Driver" )]
[Category( "Aimbox" )]
public sealed class AimboxCitizenBodyDriver : Component
{
	CitizenAnimationHelper _anim;
	IAimboxCombatActor _pawn;
	Vector3 _lastPos;
	bool _hasLastPos;
	float _lastWorldYaw;

	protected override void OnStart()
	{
		_pawn = Components.Get<AimboxPlayerController>() as IAimboxCombatActor
		        ?? Components.Get<AimboxBotController>() as IAimboxCombatActor;
		foreach ( var helper in GameObject.Components.GetAll<CitizenAnimationHelper>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( helper.IsValid() )
			{
				_anim = helper;
				break;
			}
		}

		_lastWorldYaw = GameObject.WorldRotation.Angles().yaw;
	}

	protected override void OnUpdate()
	{
		if ( !_anim.IsValid() )
			return;

		_pawn ??= Components.Get<AimboxPlayerController>() as IAimboxCombatActor
		          ?? Components.Get<AimboxBotController>() as IAimboxCombatActor;
		var dt = MathF.Max( Time.Delta, 0.0001f );
		var vel = _pawn?.GetMovementVelocity() ?? Vector3.Zero;
		if ( vel.LengthSquared < 1f )
		{
			var pos = GameObject.WorldPosition;
			if ( !_hasLastPos )
			{
				_lastPos = pos;
				_hasLastPos = true;
			}

			vel = ( pos - _lastPos ) / dt;
			_lastPos = pos;
		}
		else if ( _hasLastPos )
			_lastPos = GameObject.WorldPosition;

		_anim.WithVelocity( vel );
		_anim.IsGrounded = IsGrounded();
		_anim.DuckLevel = _pawn?.IsCrouching == true ? 1f : 0f;

		var yawNow = GameObject.WorldRotation.Angles().yaw;
		var yawDelta = ( yawNow - _lastWorldYaw ).NormalizeDegrees();
		_anim.MoveRotationSpeed = MathX.Lerp( _anim.MoveRotationSpeed, yawDelta / dt, Math.Clamp( dt * 8f, 0f, 1f ) );
		_lastWorldYaw = yawNow;
		_anim.AimAngle = _pawn is null ? GameObject.WorldRotation : _pawn.EyeRotation;
		_anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
	}

	bool IsGrounded()
	{
		var tr = Scene.Trace.Ray( GameObject.WorldPosition + Vector3.Up * 8f, GameObject.WorldPosition + Vector3.Down * 14f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();
		return tr.Hit;
	}
}

[Title( "Aimbox Third Person Weapon Visual" )]
[Category( "Aimbox" )]
public sealed class AimboxThirdPersonWeaponVisual : Component
{
	IAimboxCombatActor _pawn;
	GameObject _weaponWorld;
	SkinnedModelRenderer _renderer;
	AimboxWeaponId? _lastWeapon;
	string _lastPath;

	protected override void OnStart()
	{
		_pawn = Components.Get<AimboxPlayerController>() as IAimboxCombatActor
		        ?? Components.Get<AimboxBotController>() as IAimboxCombatActor;
		EnsureWeaponObject();
	}

	protected override void OnUpdate()
	{
		_pawn ??= Components.Get<AimboxPlayerController>() as IAimboxCombatActor
		          ?? Components.Get<AimboxBotController>() as IAimboxCombatActor;
		if ( _pawn is null )
			return;

		EnsureWeaponObject();
		UpdateModel();

		var shouldShow = _pawn.ShowThirdPersonBody && _pawn.IsAlive;
		if ( _renderer.IsValid() )
			_renderer.Enabled = shouldShow;
	}

	protected override void OnPreRender()
	{
		if ( _pawn is null || !_pawn.ShowThirdPersonBody || !_pawn.IsAlive || !_weaponWorld.IsValid() )
			return;

		_weaponWorld.LocalScale = AimboxCitizenPresentation.WorldWeaponLocalScale;
		var duckLevel = _pawn.IsCrouching ? 1f : 0f;
		var bob = AimboxCitizenPresentation.ComputeMovementBobOffsetLocal( _pawn.GetMovementVelocity(), Time.Now );
		var handAttached = AimboxCitizenPresentation.TryAlignWeaponToCitizenRightHand( GameObject, _weaponWorld, bob );
		if ( !handAttached )
			AimboxCitizenPresentation.ParentWorldWeaponToBodyFallback( GameObject, _weaponWorld, duckLevel, bob );

		AimboxCitizenPresentation.WireCitizenHandIk( GameObject, _weaponWorld, handAttached );
	}

	void EnsureWeaponObject()
	{
		if ( _weaponWorld.IsValid() )
			return;

		_weaponWorld = AimboxCitizenPresentation.FindChild( GameObject, AimboxCitizenPresentation.WorldWeaponChildName );
		if ( !_weaponWorld.IsValid() )
		{
			_weaponWorld = new GameObject( true, AimboxCitizenPresentation.WorldWeaponChildName );
			_weaponWorld.SetParent( GameObject );
		}

		_weaponWorld.LocalScale = AimboxCitizenPresentation.WorldWeaponLocalScale;
		_renderer = _weaponWorld.Components.Get<SkinnedModelRenderer>();
		if ( !_renderer.IsValid() )
			_renderer = _weaponWorld.Components.Create<SkinnedModelRenderer>();
		_renderer.UseAnimGraph = false;
	}

	void UpdateModel()
	{
		if ( AimboxGame.Instance is not { WeaponPackagesReady: true } )
			return;

		var weapon = _pawn.CurrentWeapon?.Definition;
		var presentationWeapon = _pawn is AimboxPlayerController player
			? player.ThirdPersonPresentationWeaponId
			: _pawn.ActiveWeapon;
		var path = AimboxGrenadeCatalog.IsGrenadeWeapon( presentationWeapon )
			? AimboxGrenadeCatalog.ResolveWorldModelPath( presentationWeapon )
			: weapon?.WorldModelPath ?? "";
		if ( _lastWeapon == presentationWeapon && string.Equals( _lastPath, path, StringComparison.Ordinal ) )
			return;

		_lastWeapon = presentationWeapon;
		_lastPath = path;

		if ( AimboxWeaponResourceLoad.TryLoadWeaponWorldModel( path, $"third-person {_pawn.ActiveWeapon}", out var model ) )
		{
			_renderer.Model = model;
			_renderer.Tint = Color.White;
		}
		else
		{
			_renderer.Model = default;
		}
	}
}
