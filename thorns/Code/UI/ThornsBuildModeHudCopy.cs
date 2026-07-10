using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>Readable build-mode HUD strings (no resource abbreviations).</summary>
public static class ThornsBuildModeHudCopy
{
	public static string BuildToolbarKeysLine()
	{
		var parts = new List<string>( ThornsBuildToolbar.SlotCount );
		foreach ( var entry in ThornsBuildToolbar.Entries )
			parts.Add( $"{entry.SlotIndex + 1} {entry.Label}" );

		return string.Join( "  ·  ", parts );
	}

	public static string BuildFooter( ThornsBuildToolMode mode ) =>
		mode == ThornsBuildToolMode.Place
			? "Left click to place  ·  R to rotate  ·  B to exit build mode"
			: "Left click on the highlighted piece  ·  B to exit build mode";

	public static string BuildActionStatusLine( ThornsBuildingController build, ThornsInventory inv )
	{
		if ( build is null || !build.IsValid() || inv is null || !inv.IsValid() )
			return "";

		if ( build.ToolMode == ThornsBuildToolMode.Remove )
			return "Remove: left click a piece to demolish. Resources are refunded.";

		if ( build.ToolMode == ThornsBuildToolMode.Upgrade )
		{
			var stone = inv.ClientMirrorCountItemId( "stone" );
			var metal = inv.ClientMirrorCountItemId( "metal" );
			return
				$"Upgrade: costs stone or metal (twice that piece's wood cost). You have {stone} stone, {metal} metal.";
		}

		if ( !ThornsBuildingDefinitions.TryGet( build.SelectedStructureDefId, out var def ) )
			return "Select a piece with keys 1 through 7.";

		var needs = new List<(string ItemId, int Quantity)>();
		if ( !string.IsNullOrWhiteSpace( def.RequiredPlacementItemId ) )
			needs.Add( (def.RequiredPlacementItemId, 1) );

		if ( def.ResourceCosts is { Length: > 0 } )
		{
			foreach ( var c in def.ResourceCosts )
				needs.Add( (c.ItemId, c.Quantity) );
		}

		if ( needs.Count == 0 )
			return $"Place {def.DisplayName}.";

		var needText = string.Join( ", ", needs.Select( n => FormatQuantityResource( n.Quantity, n.ItemId ) ) );
		var haveText = string.Join( ", ", needs.Select( n =>
		{
			var have = inv.ClientMirrorCountItemId( n.ItemId );
			return $"{have} {DisplayResource( n.ItemId )}";
		} ) );

		var ok = needs.All( n => inv.ClientMirrorCountItemId( n.ItemId ) >= n.Quantity );
		return ok
			? $"Place {def.DisplayName}: need {needText}  ·  you have {haveText}"
			: $"Place {def.DisplayName}: need {needText}  ·  you have {haveText}  (not enough)";
	}

	public static string FormatQuantityResource( int quantity, string itemId ) =>
		$"{quantity} {DisplayResource( itemId )}";

	public static string DisplayResource( string itemId )
	{
		if ( string.IsNullOrWhiteSpace( itemId ) )
			return "resource";

		if ( string.Equals( itemId, "wood", StringComparison.OrdinalIgnoreCase ) )
			return "wood";
		if ( string.Equals( itemId, "stone", StringComparison.OrdinalIgnoreCase ) )
			return "stone";
		if ( string.Equals( itemId, "metal", StringComparison.OrdinalIgnoreCase ) )
			return "metal";

		return ThornsItemRegistry.ResolveDisplayName( itemId );
	}
}
