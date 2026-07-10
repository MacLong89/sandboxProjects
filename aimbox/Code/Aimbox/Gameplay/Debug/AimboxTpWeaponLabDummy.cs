using Sandbox.Citizen;

namespace Sandbox;

[Title( "Aimbox TP Weapon Lab Dummy" )]
[Category( "Aimbox/Debug" )]
public sealed class AimboxTpWeaponLabDummy : Component, Component.ExecuteInEditor
{
	GameObject _body;
	SkinnedModelRenderer _renderer;
	CitizenAnimationHelper _anim;
	GameObject _weaponWorld;
	SkinnedModelRenderer _weaponRenderer;
	AimboxWeaponId? _lastWeapon;
	string _lastPath;

	protected override void OnStart() => EnsureRig();

	protected override void OnUpdate()
	{
		if ( !_anim.IsValid() )
			EnsureRig();

		TickCitizenPose();
	}

	protected override void OnPreRender()
	{
		var lab = AimboxThirdPersonWeaponLab.Instance;
		if ( lab is null || !lab.IsValid() )
			return;

		EnsureWeaponObject();
		UpdateWeaponModel( lab.Weapon );
		ApplyWeaponTransform( lab );
	}

	void EnsureRig()
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
		_renderer.Model = Model.Load( AimboxCitizenPresentation.CitizenVmdl );
		_renderer.Tint = new Color( 0.88f, 0.82f, 0.76f, 1f );
		_renderer.UseAnimGraph = true;
		_renderer.CreateBoneObjects = true;

		_anim = _body.Components.Get<CitizenAnimationHelper>() ?? _body.Components.Create<CitizenAnimationHelper>();
		_anim.Target = _renderer;

		EnsureWeaponObject();
	}

	void TickCitizenPose()
	{
		if ( !_anim.IsValid() )
			return;

		var lab = AimboxThirdPersonWeaponLab.Instance;
		_anim.WithVelocity( Vector3.Zero );
		_anim.IsGrounded = true;
		_anim.DuckLevel = lab is not null && lab.PreviewCrouch ? 1f : 0f;
		_anim.AimAngle = WorldRotation;
		_anim.HoldType = CitizenAnimationHelper.HoldTypes.Rifle;
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

		_weaponRenderer = _weaponWorld.Components.Get<SkinnedModelRenderer>() ?? _weaponWorld.Components.Create<SkinnedModelRenderer>();
		_weaponRenderer.UseAnimGraph = false;
		_weaponRenderer.Tint = Color.White;
	}

	void UpdateWeaponModel( AimboxWeaponId weaponId )
	{
		var def = AimboxWeapons.Get( weaponId );
		var path = def?.WorldModelPath ?? "";
		if ( _lastWeapon == weaponId && string.Equals( _lastPath, path, StringComparison.Ordinal ) )
			return;

		_lastWeapon = weaponId;
		_lastPath = path;

		if ( AimboxWeaponResourceLoad.TryLoadWeaponWorldModel( path, $"tp-lab {weaponId}", out var model ) )
			_weaponRenderer.Model = model;
		else
			_weaponRenderer.Model = default;
	}

	void ApplyWeaponTransform( AimboxThirdPersonWeaponLab lab )
	{
		if ( !_weaponWorld.IsValid() )
			return;

		_weaponWorld.LocalScale = lab.WeaponLocalScale;

		if ( lab.UseBodyFallback )
		{
			var body = AimboxCitizenPresentation.FindDescendantNamed( GameObject, AimboxCitizenPresentation.BodyChildName );
			if ( body.IsValid() )
			{
				if ( _weaponWorld.Parent != body )
					_weaponWorld.SetParent( body );

				var duckDrop = lab.PreviewCrouch ? Vector3.Down * AimboxCitizenPresentation.WorldWeaponDuckBodyDrop : Vector3.Zero;
				_weaponWorld.LocalPosition = lab.BodyFallbackLocalPosition + duckDrop;
				_weaponWorld.LocalRotation = Rotation.From( lab.WeaponLocalRotation );
				lab.NotifyAttach( $"Body fallback @ {lab.BodyFallbackLocalPosition + duckDrop}" );
				return;
			}
		}

		if ( TryParentToHandBone( lab, out var boneName ) )
		{
			_weaponWorld.LocalPosition = lab.WeaponLocalPosition;
			_weaponWorld.LocalRotation = Rotation.From( lab.WeaponLocalRotation );
			lab.NotifyAttach( $"Hand bone '{boneName}'" );
			return;
		}

		_weaponWorld.SetParent( GameObject );
		_weaponWorld.LocalPosition = lab.WeaponLocalPosition;
		_weaponWorld.LocalRotation = Rotation.From( lab.WeaponLocalRotation );
		lab.NotifyAttach( "No hand bone — using pawn-root fallback" );
	}

	bool TryParentToHandBone( AimboxThirdPersonWeaponLab lab, out string boneName )
	{
		boneName = "";
		if ( !AimboxCitizenPresentation.TryGetCitizenBodySkin( GameObject, out var skin ) )
			return false;

		skin.CreateBoneObjects = true;

		if ( !string.IsNullOrWhiteSpace( lab.PreferredParentBone )
		     && TryParentToBone( skin, lab.PreferredParentBone ) )
		{
			boneName = lab.PreferredParentBone;
			return true;
		}

		foreach ( var candidate in new[] { "hand_R", "Hold_R", "wrist_R", "weapon_hand_R", "hand_R_IK_target" } )
		{
			if ( TryParentToBone( skin, candidate ) )
			{
				boneName = candidate;
				return true;
			}
		}

		return false;
	}

	bool TryParentToBone( SkinnedModelRenderer skin, string boneName )
	{
		if ( !skin.Model.IsValid() || !skin.Model.Bones.HasBone( boneName ) )
			return false;

		var bone = skin.GetBoneObject( skin.Model.Bones.GetBone( boneName ) );
		if ( !bone.IsValid() )
			bone = skin.GetBoneObject( boneName );

		if ( !bone.IsValid() )
			return false;

		if ( _weaponWorld.Parent != bone )
			_weaponWorld.SetParent( bone );
		return true;
	}
}
