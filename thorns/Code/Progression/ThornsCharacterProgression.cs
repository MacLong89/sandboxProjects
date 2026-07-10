namespace Sandbox;

/// <summary>
/// THORNS_EVERYTHING_DOCUMENT §death: character level retained; XP progress within current level resets on death (see <see cref="ThornsVitals"/> source of truth).
/// Spend tree: <see cref="ThornsPlayerUpgrades"/>; ordered goals: <see cref="ThornsPlayerMilestones"/>.
/// </summary>
[Title( "Thorns — Character Progression (stub)" )]
[Category( "Thorns" )]
[Icon( "trending_up" )]
[Order( 48 )]
public sealed class ThornsCharacterProgression : Component
{
	[Property] public int CharacterLevel { get; set; } = 1;

	[Property] public float XpProgressInCurrentLevel { get; set; }

	/// <summary>Host-only: apply death penalty to XP progress (level unchanged).</summary>
	public void HostApplyDeathXpPlaceholderRule()
	{
		if ( !Networking.IsHost )
			return;

		var vitals = Components.Get<ThornsVitals>();
		if ( vitals.IsValid() )
		{
			vitals.HostApplyDeathXpPlaceholderRule();
			return;
		}

		XpProgressInCurrentLevel = 0f;
		Log.Info( $"[Thorns] XP death rule (placeholder): level kept at {CharacterLevel}, progress in level reset to 0" );
	}
}
