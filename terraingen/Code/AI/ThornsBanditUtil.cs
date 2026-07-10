namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Player;
using Terraingen.Rendering;

/// <summary>Small helpers shared by bandit AI and presentation.</summary>
public static class ThornsBanditUtil
{
	public const string WorldWeaponChildName = "WeaponWorld";

	/// <summary>Uniform scale for third-person <c>w_*</c> meshes parented under Citizen <c>Body</c> (matches thorns).</summary>
	public static readonly Vector3 WorldWeaponLocalScale = new( 2f, 2f, 2f );

	/// <summary>Default offset when parented under Citizen <c>Body</c> (fallback when hand bone is unavailable).</summary>
	public static readonly Vector3 WorldWeaponLocalPositionRelBody = new( 12f, -8f, 32.5f );

	/// <summary>Default fallback if <c>Body</c> is missing (Z-up from feet).</summary>
	public static readonly Vector3 WorldWeaponLocalPositionIfNoBody = new( 0f, 0f, 40f );

	public static readonly string[] CitizenTpWeaponRightHandBoneCandidates =
	{
		"hand_R",
		"wrist_R",
		"Hold_R",
		"weapon_hand_R"
	};

	public static GameObject FindChild( GameObject root, string name )
	{
		if ( !root.IsValid() || string.IsNullOrEmpty( name ) )
			return default;

		foreach ( var ch in root.Children )
		{
			if ( ch.IsValid() && ch.Name == name )
				return ch;
		}

		return default;
	}

	public static bool PlayerIsMounted( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return false;

		return playerRoot.Components.Get<ThornsPlayerMountController>( FindMode.EnabledInSelf )?.IsMounted == true;
	}

	public static ThornsAnimalBrain ResolveMountedAnimalBrain( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return null;

		return playerRoot.Components.Get<ThornsPlayerMountController>( FindMode.EnabledInSelf )?.ResolveMountedBrain();
	}

	public static bool TryResolveCombatAttackerRoot( GameObject attackerRoot, out GameObject chaseRoot )
	{
		chaseRoot = attackerRoot;
		if ( !attackerRoot.IsValid() )
			return false;

		var gameplay = attackerRoot.Components.GetInAncestorsOrSelf<ThornsPlayerGameplay>( true );
		if ( gameplay.IsValid() )
		{
			chaseRoot = gameplay.GameObject;
			return true;
		}

		var animal = attackerRoot.Components.GetInAncestorsOrSelf<ThornsAnimalBrain>( true );
		if ( animal.IsValid() )
		{
			chaseRoot = animal.GameObject;
			return true;
		}

		var bandit = attackerRoot.Components.GetInAncestorsOrSelf<ThornsBanditBrain>( true );
		if ( bandit.IsValid() )
		{
			chaseRoot = bandit.GameObject;
			return true;
		}

		return attackerRoot.IsValid();
	}

	/// <summary>Normalize hit proxies / child colliders to the pawn root tames and AI should chase.</summary>
	public static GameObject ResolveHostileChaseRoot( GameObject any )
	{
		if ( !any.IsValid() )
			return default;

		return TryResolveCombatAttackerRoot( any, out var root ) && root.IsValid() ? root : any;
	}

	public static SkinnedModelRenderer GetOrCreateWorldSkinnedModelRenderer( GameObject go )
	{
		if ( !go.IsValid() )
			return default;

		var smr = go.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelf );
		smr = smr.IsValid() ? smr : go.Components.Create<SkinnedModelRenderer>();
		ThornsWorldShadowUtil.EnableWorldShadows( smr );
		return smr;
	}

	public static GameObject FindDescendantNamed( GameObject root, string name )
	{
		if ( !root.IsValid() || string.IsNullOrEmpty( name ) )
			return default;

		foreach ( var ch in root.Children )
		{
			if ( !ch.IsValid() )
				continue;

			if ( ch.Name == name )
				return ch;

			var nested = FindDescendantNamed( ch, name );
			if ( nested.IsValid() )
				return nested;
		}

		return default;
	}

	/// <summary>
	/// Places the third-person weapon mesh on the animated right-hand bone (world space each frame).
	/// Returns false if Body/skin/bone is missing — caller should fall back to <see cref="ParentWorldWeaponToCitizenRig"/>.
	/// </summary>
	public static bool TryAlignThirdPersonWeaponToCitizenRightHand(
		GameObject pawnRoot,
		GameObject weaponWorld,
		Vector3 gripOffsetInHandSpace,
		Rotation gripRotationAfterHand )
	{
		if ( !pawnRoot.IsValid() || !weaponWorld.IsValid() )
			return false;

		var body = FindDescendantNamed( pawnRoot, "Body" );
		if ( !body.IsValid() )
			return false;

		var skin = body.Components.Get<SkinnedModelRenderer>();
		if ( !skin.IsValid() )
			return false;

		foreach ( var boneName in CitizenTpWeaponRightHandBoneCandidates )
		{
			var boneGo = skin.GetBoneObject( boneName );
			if ( !boneGo.IsValid() )
				continue;

			var handWorld = boneGo.WorldTransform;

			if ( weaponWorld.Parent != body )
				weaponWorld.SetParent( body );

			var worldRot = handWorld.Rotation * gripRotationAfterHand;
			var worldPos = handWorld.Position + handWorld.Rotation * gripOffsetInHandSpace;
			weaponWorld.WorldRotation = worldRot;
			weaponWorld.WorldPosition = worldPos;
			return true;
		}

		return false;
	}

	public static void ParentWorldWeaponToCitizenRig( GameObject pawnRoot, GameObject weaponWorld ) =>
		ParentWorldWeaponToCitizenRig(
			pawnRoot,
			weaponWorld,
			WorldWeaponLocalPositionRelBody,
			Rotation.Identity,
			WorldWeaponLocalPositionIfNoBody );

	public static void ParentWorldWeaponToCitizenRig(
		GameObject pawnRoot,
		GameObject weaponWorld,
		Vector3 localPositionRelBody,
		Rotation localRotationRelBody,
		Vector3 localPositionIfNoBody )
	{
		if ( !pawnRoot.IsValid() || !weaponWorld.IsValid() )
			return;

		var body = FindDescendantNamed( pawnRoot, "Body" );
		if ( body.IsValid() )
		{
			if ( weaponWorld.Parent != body )
				weaponWorld.SetParent( body );

			weaponWorld.LocalPosition = localPositionRelBody;
			weaponWorld.LocalRotation = localRotationRelBody;
			return;
		}

		if ( weaponWorld.Parent != pawnRoot )
			weaponWorld.SetParent( pawnRoot );

		weaponWorld.LocalPosition = localPositionIfNoBody;
		weaponWorld.LocalRotation = Rotation.Identity;
	}
}
