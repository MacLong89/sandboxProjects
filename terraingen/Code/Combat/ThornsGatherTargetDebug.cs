namespace Terraingen.Combat;

using Terraingen.Player;

[Title( "Thorns Gather Target Debug" )]
[Category( "Debug" )]
public sealed class ThornsGatherTargetDebug : Component
{
	/// <summary>Visualize gather pick reach when <c>gather_target_debug 1</c>.</summary>
	[ConVar( "gather_target_debug" )]
	public static bool OverlayEnabled { get; set; }

	protected override void OnUpdate()
	{
		if ( !OverlayEnabled )
			return;

		var player = ThornsPlayerGameplay.Local;
		if ( !player.IsValid() )
			return;

		var root = player.GameObject;
		if ( !root.IsValid() )
			return;

		ThornsGatherTargeting.DrawActiveGatherDebug( root );
	}
}
