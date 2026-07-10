namespace Sandbox;

/// <summary>Generic follow target — player, animal, or NPC via one code path.</summary>
public readonly struct ThornsAnimalFollowTarget
{
	public GameObject Root { get; init; }
	public float DesiredDistanceMin { get; init; }
	public float DesiredDistanceMax { get; init; }
	public float SprintCatchUpDistance { get; init; }

	public bool IsValid => Root.IsValid();

	public static ThornsAnimalFollowTarget ForOwner( GameObject ownerRoot, ThornsWildlifeSpeciesDefinition def )
	{
		var bulky = ThornsWildlifeBrain.HostTameUsesBulkyFollowSpacing( def.Kind );
		var min = bulky ? 280f : 180f;
		var max = bulky ? 420f : 320f;
		return new ThornsAnimalFollowTarget
		{
			Root = ownerRoot,
			DesiredDistanceMin = min,
			DesiredDistanceMax = max,
			SprintCatchUpDistance = 480f,
		};
	}

	public static ThornsAnimalFollowTarget ForLeader( GameObject leaderRoot, float spacing )
	{
		return new ThornsAnimalFollowTarget
		{
			Root = leaderRoot,
			DesiredDistanceMin = spacing * 0.65f,
			DesiredDistanceMax = spacing * 1.35f,
			SprintCatchUpDistance = spacing * 2.4f,
		};
	}

	/// <summary>Trail behind leader facing — avoids radial slots that spin with follower position and cause orbit loops.</summary>
	public Vector3 DesiredSlotWorld( Vector3 selfFlat, int slotIndex = 0 )
	{
		_ = selfFlat;
		if ( !IsValid )
			return selfFlat;

		var leaderFlat = Root.WorldPosition.WithZ( 0 );
		var forward = ThornsWildlifeBrain.HostGetOwnerPlanarForward( Root );
		var right = new Vector3( -forward.y, forward.x, 0f );
		if ( right.LengthSquared < 1e-4f )
			right = Vector3.Right;
		else
			right = right.Normal;

		var back = Math.Clamp( DesiredDistanceMin * 0.82f, 120f, DesiredDistanceMax );
		var lateralSign = slotIndex % 2 == 0 ? 1f : -1f;
		var lateral = right * ( DesiredDistanceMin * 0.22f * lateralSign );
		return leaderFlat - forward * back + lateral;
	}
}
