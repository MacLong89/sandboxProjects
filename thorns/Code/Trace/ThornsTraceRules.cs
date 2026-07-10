namespace Sandbox;

/// <summary>Immutable flags applied to <see cref="Scene.Trace"/> ray builders.</summary>
public readonly struct ThornsTraceRuleSet
{
	public ThornsTraceRuleSet( bool useHitPosition, bool useHitboxes, bool usePhysicsWorld )
	{
		UseHitPosition = useHitPosition;
		UseHitboxes = useHitboxes;
		UsePhysicsWorld = usePhysicsWorld;
	}

	public bool UseHitPosition { get; }

	public bool UseHitboxes { get; }

	public bool UsePhysicsWorld { get; }

	public static ThornsTraceRuleSet For( ThornsTraceProfile profile ) =>
		profile switch
		{
			ThornsTraceProfile.WeaponHitscan => new ThornsTraceRuleSet( true, true, true ),
			ThornsTraceProfile.WeaponFeedbackWorld => new ThornsTraceRuleSet( true, true, true ),
			ThornsTraceProfile.InteractionUse => new ThornsTraceRuleSet( true, false, true ),
			ThornsTraceProfile.BuildingPlacementView => new ThornsTraceRuleSet( true, false, true ),
			ThornsTraceProfile.BuildingStructurePickPiercing => new ThornsTraceRuleSet( true, false, true ),
			ThornsTraceProfile.BuildingTerrainSupportDown => new ThornsTraceRuleSet( true, false, true ),
			ThornsTraceProfile.MovementProbe => new ThornsTraceRuleSet( true, true, true ),
			ThornsTraceProfile.AiLineOfSight => new ThornsTraceRuleSet( true, true, true ),
			ThornsTraceProfile.FootstepGround => new ThornsTraceRuleSet( true, false, true ),
			ThornsTraceProfile.TerrainChunkSnapDown => new ThornsTraceRuleSet( true, true, true ),
			ThornsTraceProfile.TerrainInteriorSampleDown => new ThornsTraceRuleSet( true, true, true ),
			ThornsTraceProfile.AirdropGroundSnapDown => new ThornsTraceRuleSet( true, true, true ),
			ThornsTraceProfile.TamingWorldPick => new ThornsTraceRuleSet( true, true, true ),
			_ => new ThornsTraceRuleSet( true, true, true )
		};
}
