namespace PawnShop;

public enum DisplayKind { WallShelf, GlassCase, ElectronicsShelf, FloorStand, PremiumCase }

/// <summary>One physical display slot in the shop.</summary>
public sealed class DisplaySlotDef
{
	public int Index { get; init; }
	public DisplayKind Kind { get; init; }
	public Vector3 Position { get; init; }     // where the item prop sits
	public float Yaw { get; init; }            // facing of the item
	public UpgradeId? Requires { get; init; }  // null = available from the start
	/// <summary>Where a browsing customer stands to look at this slot.</summary>
	public Vector3 BrowseSpot { get; init; }
}

/// <summary>
/// Fixed positions for the shop floor + backroom. The storefront sits toward -y;
/// the backroom (stock, bench, cage, back door) sits past the divider toward +y.
/// </summary>
public static class ShopLayout
{
	// Room: x -340..340 (width), y -HalfD..+HalfD (depth; -y = street). Units ~ inches.
	public const float HalfW = 340f;
	public const float HalfD = 420f;
	public const float WallH = 150f;
	public const float WallT = 8f;

	/// <summary>Divider between sales floor and staff backroom (along Y).</summary>
	public const float BackroomLine = 210f;

	public static readonly Vector3 PlayerSpawn = new( 0, 190, 4 );
	public static readonly Vector3 DoorOutside = new( 0, -HalfD - 70f, 0 );
	public static readonly Vector3 DoorInside = new( 0, -HalfD + 60f, 0 );
	public const float DoorWidth = 70f;

	/// <summary>Wholesaler crate drop at the back wall.</summary>
	public static readonly Vector3 BackDoorInside = new( 0, HalfD - 50f, 0 );
	public static readonly Vector3 BackDoorOutside = new( 0, HalfD + 40f, 0 );

	// Counter runs across the middle-back. Player behind (+y), customers in front (-y).
	public static readonly Vector3 CounterCenter = new( 0, 120, 0 );
	public static readonly Vector3 CounterSize = new( 260, 44, 42 );
	/// <summary>Where the presented item sits during negotiation.</summary>
	public static readonly Vector3 CounterItemSpot = new( 0, 120, 44 );
	/// <summary>Customer stand position at the counter.</summary>
	public static readonly Vector3 CounterCustomerSpot = new( 0, 60, 0 );

	/// <summary>Always-available stock table in the backroom (opens inventory / accepts drop).</summary>
	public static readonly Vector3 StockTable = new( -40, 300, 0 );
	/// <summary>Baseline cleaning/repair bench (upgraded bench replaces this interact).</summary>
	public static readonly Vector3 Workbench = new( -250, 300, 0 );
	/// <summary>Research desk spot.</summary>
	public static readonly Vector3 ResearchDesk = new( 130, 300, 0 );
	/// <summary>Pawn cage center.</summary>
	public static readonly Vector3 PawnCage = new( 250, 330, 0 );

	/// <summary>Door plant — waterable chore.</summary>
	public static readonly Vector3 PlantSpot = new( 95f, -HalfD + 30f, 0 );
	/// <summary>Counter polish zone (top surface, player side).</summary>
	public static readonly Vector3 CounterPolishSpot = new( 0, 120, 46 );
	/// <summary>Alley dumpster — just outside the back door, right side.</summary>
	public static readonly Vector3 Dumpster = new( 90f, HalfD + 35f, 0 );
	/// <summary>Broom closet prop in the backroom.</summary>
	public static readonly Vector3 BroomCloset = new( -300f, 250f, 0 );
	/// <summary>Utility sink / mop bucket.</summary>
	public static readonly Vector3 UtilitySink = new( -280f, 360f, 0 );

	/// <summary>Floor dirt spawn points on the sales floor.</summary>
	public static readonly Vector3[] DirtSpots =
	{
		new( -90, -40, 0 ),
		new( 70, -90, 0 ),
		new( -160, -140, 0 ),
		new( 140, -50, 0 ),
		new( 20, -180, 0 ),
		new( -200, 20, 0 ),
		new( 210, -160, 0 ),
		new( -40, -100, 0 ),
	};

	/// <summary>Dusty shelf / case spots.</summary>
	public static readonly Vector3[] DustSpots =
	{
		new( -HalfW + 30f, -180f, 50f ),
		new( -HalfW + 30f, 10f, 50f ),
		new( HalfW - 30f, -190f, 50f ),
		new( 200f, 40f, 50f ),
		new( 270f, 40f, 50f ),
		new( -200f, 40f, 50f ),
	};

	/// <summary>Messy box piles that need restacking (sales floor + near backroom).</summary>
	public static readonly Vector3[] MessPiles =
	{
		new( -250f, 40f, 0 ),
		new( 260f, -60f, 0 ),
		new( -180f, 240f, 0 ),
		new( 60f, 260f, 0 ),
		new( -100f, -200f, 0 ),
	};

	/// <summary>Trash bag spawn points.</summary>
	public static readonly Vector3[] TrashSpots =
	{
		new( 110f, 150f, 0 ),
		new( -140f, 160f, 0 ),
		new( 40f, -HalfD + 50f, 0 ),
		new( -220f, 280f, 0 ),
	};

	/// <summary>Queue positions behind the counter customer.</summary>
	public static readonly Vector3[] QueueSpots =
	{
		new( -55, -10, 0 ),
		new( -80, -85, 0 ),
		new( -105, -160, 0 ),
	};

	/// <summary>Physical shelf positions for backroom stock (mirrors inventory capacity visually).</summary>
	public static readonly Vector3[] StorageSlots = BuildStorageSlots();

	/// <summary>Shelf positions inside the pawn cage.</summary>
	public static readonly Vector3[] PawnSlots =
	{
		new( 230, 340, 28 ),
		new( 270, 340, 28 ),
		new( 230, 340, 62 ),
		new( 270, 340, 62 ),
	};

	public static readonly DisplaySlotDef[] Slots = BuildSlots();

	private static Vector3[] BuildStorageSlots()
	{
		var list = new List<Vector3>();
		// Three rack bays along the back wall, two shelves high, three across.
		for ( var bay = 0; bay < 3; bay++ )
		{
			var x0 = -240f + bay * 240f;
			for ( var shelf = 0; shelf < 2; shelf++ )
			{
				var z = 48f + shelf * 42f;
				for ( var col = 0; col < 3; col++ )
					list.Add( new Vector3( x0 - 40f + col * 40f, HalfD - 52f, z ) );
			}
		}
		// Overflow crates on the floor near the stock table.
		list.Add( new Vector3( 40, 280, 18 ) );
		list.Add( new Vector3( 80, 295, 18 ) );
		list.Add( new Vector3( -100, 285, 18 ) );
		return list.ToArray();
	}

	private static DisplaySlotDef[] BuildSlots()
	{
		var slots = new List<DisplaySlotDef>();

		// 0-3: Wall shelf A — left wall.
		for ( var i = 0; i < 4; i++ )
		{
			var y = -220f + i * 95f;
			slots.Add( new DisplaySlotDef
			{
				Index = slots.Count,
				Kind = DisplayKind.WallShelf,
				Position = new Vector3( -HalfW + 26f, y, 58f ),
				Yaw = -90f,
				BrowseSpot = new Vector3( -HalfW + 95f, y, 0 ),
			} );
		}

		// 4-5: Glass case — right of the counter, angled toward the floor.
		for ( var i = 0; i < 2; i++ )
		{
			var x = 200f + i * 70f;
			slots.Add( new DisplaySlotDef
			{
				Index = slots.Count,
				Kind = DisplayKind.GlassCase,
				Position = new Vector3( x, 40f, 46f ),
				Yaw = 180f,
				BrowseSpot = new Vector3( x, -40f, 0 ),
			} );
		}

		// 6-7: Electronics shelf — right wall.
		for ( var i = 0; i < 2; i++ )
		{
			var y = -220f + i * 95f;
			slots.Add( new DisplaySlotDef
			{
				Index = slots.Count,
				Kind = DisplayKind.ElectronicsShelf,
				Position = new Vector3( HalfW - 26f, y, 58f ),
				Yaw = 90f,
				BrowseSpot = new Vector3( HalfW - 95f, y, 0 ),
			} );
		}

		// 8-11: Wall shelf B (upgrade) — front wall, left of the door.
		for ( var i = 0; i < 4; i++ )
		{
			var x = -300f + i * 80f;
			slots.Add( new DisplaySlotDef
			{
				Index = slots.Count,
				Kind = DisplayKind.WallShelf,
				Position = new Vector3( x, -HalfD + 26f, 58f ),
				Yaw = 0f,
				Requires = UpgradeId.DisplayWall,
				BrowseSpot = new Vector3( x, -HalfD + 95f, 0 ),
			} );
		}

		// 12-15: Floor stands (upgrade) — two islands, two slots each.
		var standCenters = new[] { new Vector3( -170f, -100f, 0 ), new Vector3( 170f, -160f, 0 ) };
		foreach ( var c in standCenters )
		{
			for ( var i = 0; i < 2; i++ )
			{
				var off = (i == 0 ? -1f : 1f) * 34f;
				slots.Add( new DisplaySlotDef
				{
					Index = slots.Count,
					Kind = DisplayKind.FloorStand,
					Position = c + new Vector3( off, 0, 40f ),
					Yaw = c.x < 0 ? 90f : -90f,
					Requires = UpgradeId.DisplayFloor,
					BrowseSpot = c + new Vector3( 0, c.x < 0 ? 70f : -70f, 0 ),
				} );
			}
		}

		// 16-17: Premium case (upgrade) — left of the counter, glowing.
		for ( var i = 0; i < 2; i++ )
		{
			var x = -200f - i * 70f;
			slots.Add( new DisplaySlotDef
			{
				Index = slots.Count,
				Kind = DisplayKind.PremiumCase,
				Position = new Vector3( x, 40f, 50f ),
				Yaw = 180f,
				Requires = UpgradeId.PremiumCase,
				BrowseSpot = new Vector3( x, -40f, 0 ),
			} );
		}

		return slots.ToArray();
	}

	public static DisplaySlotDef Slot( int index ) =>
		index >= 0 && index < Slots.Length ? Slots[index] : null;

	public static bool SlotAvailable( int index, SaveData save )
	{
		var def = Slot( index );
		if ( def is null ) return false;
		return def.Requires is not { } req || save.OwnsUpgrade( req );
	}

	public static bool InBackroom( Vector3 pos ) => pos.y >= BackroomLine;
}
