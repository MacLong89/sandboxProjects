namespace Terraingen.Minerals;

using Terraingen.GameData;
using Terraingen.Player;

/// <summary>Detects pickaxe tools for mineral harvesting.</summary>
public static class ThornsPickaxeTools
{
	public static bool IsPickaxeItemId( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return false;

		var id = ThornsItemIdAliases.Canonicalize( itemId ).ToLowerInvariant();
		if ( id.Contains( "axe" ) && !id.Contains( "pick" ) )
			return false;

		return id.Contains( "pickaxe" );
	}

	public static bool PlayerHasPickaxeEquipped( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return false;

		var gameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( !gameplay.IsValid() )
			return false;

		return gameplay.TryGetActiveHotbarItemId( out var itemId ) && IsPickaxeItemId( itemId );
	}
}
