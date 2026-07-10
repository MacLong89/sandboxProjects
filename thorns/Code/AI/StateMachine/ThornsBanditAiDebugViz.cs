namespace Sandbox;

/// <summary>Debug overlay for bandit AI state machine.</summary>
[Title( "Thorns — Bandit AI debug" )]
[Category( "Thorns/AI" )]
[Icon( "bug_report" )]
public sealed class ThornsBanditAiDebugViz : Component
{
	[Property] public bool DrawGizmosEnabled { get; set; }
	[Property] public float LabelHeight { get; set; } = 88f;

	protected override void DrawGizmos()
	{
		if ( !DrawGizmosEnabled || !Game.IsPlaying )
			return;

		var brain = Components.Get<ThornsBanditBrain>();
		if ( !brain.IsValid() )
			return;

		var ctx = brain.StateMachineContext;
		if ( ctx is null )
			return;

		var pos = GameObject.WorldPosition + Vector3.Up * LabelHeight;
		var targetName = ctx.CurrentTarget.IsValid() ? ctx.CurrentTarget.Name : "none";

		Gizmo.Draw.Text(
			$"Type: {ctx.Archetype.Type}\nAI: {ctx.CurrentState}\nTarget: {targetName}\nAlert: {ctx.AlertLevel}\nGroup: {ctx.GroupId}\nThreat: {ctx.ThreatScore:0}\nVision: {ctx.Archetype.VisionRangeWorld:0}",
			new Transform( pos ) );

		if ( ctx.CurrentTarget.IsValid() )
			Gizmo.Draw.Line( GameObject.WorldPosition + Vector3.Up * 32f, ctx.CurrentTarget.WorldPosition + Vector3.Up * 32f );

		if ( ctx.LastKnownTargetPosition != default )
		{
			Gizmo.Draw.Color = Color.Orange.WithAlpha( 0.4f );
			Gizmo.Draw.LineSphere( ctx.LastKnownTargetPosition + Vector3.Up * 8f, 24f );
		}
	}
}
