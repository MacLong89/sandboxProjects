namespace Sandbox;

/// <summary>Debug overlay for animal AI state machine.</summary>
[Title( "Thorns — Animal AI debug" )]
[Category( "Thorns/Wildlife" )]
[Icon( "bug_report" )]
public sealed class ThornsAnimalAiDebugViz : Component
{
	[Property] public bool DrawGizmosEnabled { get; set; }
	[Property] public float LabelHeight { get; set; } = 96f;
	[Property] public bool DrawDetectionRadius { get; set; } = true;
	[Property] public bool DrawAttackRange { get; set; } = true;
	[Property] public bool DrawHomeRadius { get; set; } = true;
	[Property] public bool DrawPackLinks { get; set; } = true;

	protected override void DrawGizmos()
	{
		if ( !DrawGizmosEnabled || !Game.IsPlaying )
			return;

		var brain = Components.Get<ThornsWildlifeBrain>();
		if ( !brain.IsValid() )
			return;

		var ctx = brain.StateMachineContext;
		if ( ctx is null )
			return;

		var id = ctx.Identity ?? Components.Get<ThornsWildlifeIdentity>();
		var def = ctx.Definition ?? id?.Definition;
		var profile = id.IsValid() ? ThornsAnimalBehaviorProfile.Get( id.Species ) : default;
		var pos = GameObject.WorldPosition;
		var labelPos = pos + Vector3.Up * LabelHeight;
		var targetName = ctx.CurrentTarget.IsValid() ? ctx.CurrentTarget.Name : ctx.FocusTarget.IsValid() ? ctx.FocusTarget.Name : "none";
		var ownerName = ctx.OwnerPlayer.IsValid() ? ctx.OwnerPlayer.Name : "none";
		var leaderName = ctx.LeaderAnimal.IsValid() ? ctx.LeaderAnimal.Name : "none";

		Gizmo.Draw.Text(
			$"AI: {ctx.CurrentState}\nPrev: {ctx.PreviousState}\nMode: {ctx.BehaviorMode}\nRel: {ctx.LastRelationshipLabel}\nPack: {ctx.NearbyPackMembers}\nTarget: {targetName}\nThreat: {ctx.ThreatScore:0}\nOwner: {ownerName}\nLeader: {leaderName}",
			new Transform( labelPos ) );

		if ( ctx.CurrentTarget.IsValid() )
			Gizmo.Draw.Line( pos + Vector3.Up * 32f, ctx.CurrentTarget.WorldPosition + Vector3.Up * 32f );
		else if ( ctx.FocusTarget.IsValid() )
			Gizmo.Draw.Line( pos + Vector3.Up * 32f, ctx.FocusTarget.WorldPosition + Vector3.Up * 32f );

		if ( def is null )
			return;

		if ( DrawDetectionRadius )
		{
			Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.18f );
			var detectR = ThornsAnimalPerceptionService.GetEffectiveDetectionRadius( def, profile );
			Gizmo.Draw.LineSphere( pos.WithZ( pos.z + 8f ), detectR );
		}

		if ( DrawAttackRange )
		{
			Gizmo.Draw.Color = Color.Red.WithAlpha( 0.22f );
			Gizmo.Draw.LineSphere( pos.WithZ( pos.z + 8f ), def.AttackRange );
		}

		var home = ctx.HomePosition != default ? ctx.HomePosition : ctx.SpawnFlat;
		if ( DrawHomeRadius && home != default )
		{
			Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.25f );
			Gizmo.Draw.LineSphere( home + Vector3.Up * 8f, def.LeashRadius );
		}

		if ( DrawPackLinks && profile.PackPreference > 0.45f && id.IsValid() )
		{
			Gizmo.Draw.Color = Color.Orange.WithAlpha( 0.45f );
			var flat = pos.WithZ( 0 );
			var packR2 = ThornsAnimalPackCoordinator.PackRadius * ThornsAnimalPackCoordinator.PackRadius;
			foreach ( var packMate in ThornsPopulationDirector.HostWildlifeBrainsReadOnly )
			{
				if ( !packMate.IsValid() )
					continue;

				var go = packMate.GameObject;
				if ( go == GameObject || !go.IsValid() )
					continue;

				var mateId = go.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true );
				if ( !mateId.IsValid() || mateId.Species != id.Species || mateId.HostIsDead )
					continue;

				if ( ( go.WorldPosition.WithZ( 0 ) - flat ).LengthSquared > packR2 )
					continue;

				Gizmo.Draw.Line( pos + Vector3.Up * 40f, go.WorldPosition + Vector3.Up * 40f );
			}
		}
	}
}
