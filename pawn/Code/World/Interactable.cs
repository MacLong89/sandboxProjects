namespace PawnShop;

public enum InteractKind
{
	Door,
	Counter,
	Customer,
	Workbench,
	Computer,
	Radio,
	StockShelf,
	DisplayShelf,
	BackDoor,
	PawnCage,
	StockTable,
	Chore,
}

/// <summary>
/// A world region the player can aim at and press Use on. Detection is a simple
/// point-in-box test against the player's aim ray, so no physics setup is needed.
/// </summary>
public sealed class Interactable : Component
{
	[Property] public InteractKind Kind { get; set; }
	public Vector3 HalfExtents { get; set; } = new( 40, 40, 40 );

	/// <summary>Customer actor this zone belongs to (Kind == Customer).</summary>
	public CustomerActor Customer { get; set; }

	/// <summary>Inventory item bound to a stock / display shelf interact.</summary>
	public int ItemId { get; set; } = -1;

	/// <summary>Display slot index for empty shelf placement.</summary>
	public int SlotIndex { get; set; } = -1;

	/// <summary>Active chore id (Kind == Chore).</summary>
	public int ChoreId { get; set; } = -1;

	/// <summary>Whether an aim ray passes through this zone within range.</summary>
	public bool IntersectsRay( Vector3 origin, Vector3 dir, float range )
	{
		var bounds = new BBox( WorldPosition - HalfExtents, WorldPosition + HalfExtents );

		const int steps = 24;
		for ( var i = 0; i <= steps; i++ )
		{
			var p = origin + dir * (range * i / steps);
			if ( bounds.Contains( p ) )
				return true;
		}
		return false;
	}

	/// <summary>Prompt text for the HUD when focused.</summary>
	public string Prompt
	{
		get
		{
			var game = GameManager.Instance;
			if ( game is null ) return "";

			switch ( Kind )
			{
				case InteractKind.Door:
					return game.State switch
					{
						GameState.MorningPrep => "Open the shop",
						GameState.ShopOpen => "Close early",
						_ => "",
					};
				case InteractKind.Counter:
					var waiting = game.Customers?.CustomerAtCounter;
					if ( game.Negotiation.Active ) return "";
					return waiting is not null ? $"Serve {waiting.Profile.Name}" : "";
				case InteractKind.Customer:
					if ( game.Negotiation.Active || Customer is null ) return "";
					return Customer.IsAtCounter ? $"Serve {Customer.Profile.Name}" : "";
				case InteractKind.Workbench:
					if ( game.CarriedItem is { } heldBench )
						return $"Clean / work on {heldBench.Name}";
					return "Open workbench";
				case InteractKind.Computer:
					if ( game.CarriedItem is { } heldPc )
						return $"Research {heldPc.Name}";
					return "Check the books";
				case InteractKind.Radio:
					return game.MusicOn ? "Turn radio off" : "Turn radio on";
				case InteractKind.StockShelf:
					var stock = game.Inventory.Get( ItemId );
					if ( stock is null ) return "";
					if ( game.Chores?.CarryingTrash == true || game.CarriedItem is not null )
						return "Hands full — press Q to drop";
					return $"Pick up {stock.Name}";
				case InteractKind.DisplayShelf:
					if ( game.Chores?.CarryingTrash == true )
						return "Hands full — press Q to drop";
					if ( game.CarriedItem is { } carried )
					{
						if ( SlotIndex >= 0 && ShopLayout.SlotAvailable( SlotIndex, game.Save ) )
							return $"Place {carried.Name} here";
						return "";
					}
					var shown = game.Inventory.Get( ItemId );
					if ( shown is null ) return "";
					return $"Take {shown.Name} off display";
				case InteractKind.BackDoor:
					if ( game.Chores?.CarryingTrash == true )
						return "Take trash out to the dumpster";
					if ( game.Crate.OfferedToday && !game.Crate.BoughtToday )
						return $"Buy sealed crate ({GameConstants.FormatCash( game.Crate.Cost )})";
					return "Step into the back alley";
				case InteractKind.PawnCage:
					var n = game.Inventory.Pawned.Count();
					return n > 0 ? $"Pawn lockup ({n} held)" : "Pawn lockup (empty)";
				case InteractKind.StockTable:
					if ( game.Chores?.CarryingTrash == true ) return "Drop trash bag here";
					if ( game.CarriedItem is not null ) return $"Put {game.CarriedItem.Name} on the table";
					return "Sort backroom stock";
				case InteractKind.Chore:
					var chore = game.Chores?.Get( ChoreId );
					return chore is null ? "" : game.Chores.PromptFor( chore );
			}
			return "";
		}
	}

	/// <summary>Perform the interaction. Returns true if something happened.</summary>
	public bool Use()
	{
		var game = GameManager.Instance;
		if ( game is null ) return false;

		switch ( Kind )
		{
			case InteractKind.Door:
				if ( game.State == GameState.MorningPrep ) { game.OpenShop(); return true; }
				if ( game.State == GameState.ShopOpen ) { game.BeginClosing( auto: false ); return true; }
				return false;

			case InteractKind.Counter:
				return game.Customers?.TryServeCounterCustomer() ?? false;

			case InteractKind.Customer:
				if ( Customer is not null && Customer.IsAtCounter )
					return game.Customers?.TryServeCounterCustomer() ?? false;
				return false;

			case InteractKind.Workbench:
				if ( game.CarriedItem is not null )
					game.FocusInventoryItem( game.CarriedItemId );
				game.OpenManagement( 0 );
				return true;

			case InteractKind.Computer:
				if ( game.CarriedItem is not null )
				{
					game.ResearchItem( game.CarriedItem );
					return true;
				}
				game.OpenManagement( 4 );
				return true;

			case InteractKind.Radio:
				game.ToggleMusic();
				return true;

			case InteractKind.StockShelf:
				return game.TryPickupItem( ItemId );

			case InteractKind.DisplayShelf:
				if ( game.CarriedItem is not null && SlotIndex >= 0 )
					return game.TryPlaceCarriedOnSlot( SlotIndex );
				if ( ItemId >= 0 )
					return game.TryPickupItem( ItemId );
				return false;

			case InteractKind.BackDoor:
				// Carrying trash: the back door is the dump action (no need to hunt the alley prop).
				if ( game.Chores?.CarryingTrash == true )
					return game.Chores.DumpTrash();
				if ( game.Crate.OfferedToday && !game.Crate.BoughtToday )
					return game.Crate.Buy( game ) is not null;
				game.Toast( "Back alley is through this doorway — dumpster is just outside to the right.", "door_front" );
				return true;

			case InteractKind.PawnCage:
				game.OpenManagement( 1 );
				return true;

			case InteractKind.StockTable:
				if ( game.Chores?.CarryingTrash == true || game.CarriedItem is not null )
					return game.DropHeld();
				game.OpenManagement( 0 );
				return true;

			case InteractKind.Chore:
				return game.Chores?.TryComplete( ChoreId ) ?? false;
		}
		return false;
	}
}
