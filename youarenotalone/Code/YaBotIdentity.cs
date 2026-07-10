namespace Sandbox;

/// <summary>Stable display name for practice bots (shown in kill feed, scoreboard, etc.).</summary>
[Title( "YouAreNotAlone — Bot identity" )]
[Category( "YouAreNotAlone" )]
[Icon( "badge" )]
public sealed class YaBotIdentity : Component
{
	[Property] public string DisplayName { get; set; } = "";

	protected override void OnDestroy()
	{
		if ( !string.IsNullOrWhiteSpace( DisplayName ) )
			YaBotDisplayNames.Release( DisplayName );
	}
}
