namespace Terraingen.Player;

using Sandbox.Citizen;
using Terraingen.AI;
using Terraingen.Combat;
using Terraingen.Rendering;

/// <summary>Citizen mesh + animation helper for third-person bandit NPCs and remote players.</summary>
public static class ThornsCitizenRig
{
	public const string CitizenVmdl = "models/citizen/citizen.vmdl";
	public const string BodyChildName = "Body";

	public static readonly Vector3 WorldWeaponHandLocalPosition = new( 10f, -6f, -3f );
	public static readonly Angles WorldWeaponHandLocalEulerDegrees = new( 0f, 0f, -90f );
	public static readonly Vector3 WorldWeaponLocalPositionRelBody = new( 12f, -8f, 32.5f );
	public static readonly Vector3 WorldWeaponLocalPositionIfNoBody = new( 0f, 0f, 40f );
	public const float WorldWeaponDuckBodyDrop = 22f;

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

	public static void SetupCitizenBody( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() )
			return;

		EnsureCitizenBodyInternal( pawnRoot, createBodyDriver: true, createThirdPersonWeapon: false );
	}

	public static void EnsureRemotePlayerThirdPersonPresentation( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() || ThornsLocalPlayer.IsLocallyControlledPawn( pawnRoot ) )
			return;

		EnsureCitizenBodyInternal( pawnRoot, createBodyDriver: true, createThirdPersonWeapon: true );
	}

	static void EnsureCitizenBodyInternal( GameObject pawnRoot, bool createBodyDriver, bool createThirdPersonWeapon )
	{
		GameObject bodyGo = default;
		foreach ( var ch in pawnRoot.Children )
		{
			if ( ch.Name == BodyChildName )
			{
				bodyGo = ch;
				break;
			}
		}

		if ( !bodyGo.IsValid() )
		{
			bodyGo = new GameObject( true, BodyChildName );
			bodyGo.SetParent( pawnRoot );
		}

		bodyGo.LocalPosition = Vector3.Zero;
		bodyGo.LocalRotation = Rotation.Identity;
		bodyGo.LocalScale = Vector3.One;

		var skin = bodyGo.Components.Get<SkinnedModelRenderer>();
		if ( !skin.IsValid() )
		{
			foreach ( var c in bodyGo.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
				c?.Destroy();

			foreach ( var c in bodyGo.Components.GetAll<CitizenAnimationHelper>( FindMode.EverythingInSelfAndDescendants ) )
				c?.Destroy();

			skin = bodyGo.Components.Create<SkinnedModelRenderer>();
			skin.Model = Model.Load( CitizenVmdl );
			skin.Tint = new Color( 0.88f, 0.82f, 0.76f, 1f );
			skin.UseAnimGraph = true;
			ThornsWorldShadowUtil.EnableWorldShadows( skin );
		}

		var helper = bodyGo.Components.Get<CitizenAnimationHelper>();
		if ( !helper.IsValid() )
		{
			helper = bodyGo.Components.Create<CitizenAnimationHelper>();
			helper.Target = skin;
		}

		var viewGo = ThornsBanditUtil.FindChild( pawnRoot, "View" );
		if ( viewGo.IsValid() )
			helper.EyeSource = viewGo;

		if ( createBodyDriver && !pawnRoot.Components.Get<ThornsCitizenBodyDriver>( FindMode.EnabledInSelf ).IsValid() )
			pawnRoot.Components.Create<ThornsCitizenBodyDriver>();

		ThornsCitizenCombatHitboxes.EnsureOnCitizenPawn( pawnRoot );

		if ( createThirdPersonWeapon && !pawnRoot.Components.Get<ThornsThirdPersonWeaponVisual>( FindMode.EnabledInSelf ).IsValid() )
			pawnRoot.Components.Create<ThornsThirdPersonWeaponVisual>();

		if ( !pawnRoot.Components.Get<PlayerController>( FindMode.EverythingInSelf ).IsValid() )
			_ = pawnRoot.Components.Get<ThornsNpcFootstepAudio>() ?? pawnRoot.Components.Create<ThornsNpcFootstepAudio>();
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

	public static bool TryAlignWeaponToCitizenRightHand( GameObject pawnRoot, GameObject weaponWorld, Vector3 bobPawnLocal = default )
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
		var body = ThornsBanditUtil.FindDescendantNamed( pawnRoot, BodyChildName );
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

	public static void WireCitizenHandIk( GameObject pawnRoot, GameObject weaponWorld, bool handAttached )
	{
		if ( !TryGetCitizenBodySkin( pawnRoot, out var skin ) )
			return;

		var helper = skin.GameObject.Components.Get<CitizenAnimationHelper>();
		if ( !helper.IsValid() )
			return;

		helper.IkRightHand = handAttached ? null : weaponWorld.IsValid() ? weaponWorld : null;
	}

	static void ApplyWorldWeaponHandLocalTransform( GameObject weaponWorld, Vector3 extraLocalOffset = default )
	{
		weaponWorld.LocalRotation = Rotation.From( WorldWeaponHandLocalEulerDegrees );
		weaponWorld.LocalPosition = WorldWeaponHandLocalPosition + extraLocalOffset;
	}

	static bool TryGetCitizenBodySkin( GameObject pawnRoot, out SkinnedModelRenderer skin )
	{
		skin = default;
		if ( !pawnRoot.IsValid() )
			return false;

		var body = ThornsBanditUtil.FindDescendantNamed( pawnRoot, BodyChildName );
		if ( !body.IsValid() )
			return false;

		skin = body.Components.Get<SkinnedModelRenderer>();
		return skin.IsValid();
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
}
