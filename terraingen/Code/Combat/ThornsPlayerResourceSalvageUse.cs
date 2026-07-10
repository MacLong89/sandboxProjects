namespace Terraingen.Combat;

using Sandbox;
using Terraingen.Player;

/// <summary>Empty-hands punch salvage prompts for trees and stone nodes.</summary>
[Title( "Thorns Player Resource Salvage" )]
[Category( "Player" )]
public sealed class ThornsPlayerResourceSalvageUse : Component
{
	public static bool HasSalvageTargetInFront( GameObject playerRoot ) =>
		ThornsGatherSalvage.HasSalvageTargetInFront( playerRoot );

	public static bool TryGetPromptVerb( GameObject playerRoot, out string verbPhrase ) =>
		ThornsGatherSalvage.TryGetPromptVerb( playerRoot, out verbPhrase );
}
