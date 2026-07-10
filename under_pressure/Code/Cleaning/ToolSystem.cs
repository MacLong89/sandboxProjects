namespace UnderPressure;

/// <summary>The cleaning tools the player can carry in the van and swap between.</summary>
public enum ToolType
{
	PressureWasher,
	ScrubBrush,
	Squeegee,
	Gun,
}

/// <summary>Static description of a tool: how it reaches, whether it sprays water, and how
/// hard it scrubs. The pressure washer defers its radius/power to the upgrade system; the
/// hand tools use the fixed values here.</summary>
public sealed class ToolDef
{
	public ToolType Type { get; init; }
	public string Name { get; init; }
	public string Icon { get; init; }
	public string Blurb { get; init; }

	/// <summary>Signature color for this tool, used to color-link pests to the tool that beats
	/// them (world beacons + HUD).</summary>
	public Color Tint { get; init; } = Color.White;

	/// <summary>Short label of what this tool is for (shown in the van locker).</summary>
	public string Cleans { get; init; }

	/// <summary>Looping sound played while this tool is in use (M1 held).</summary>
	public string UseSound { get; init; }

	/// <summary>Effective reach of the tool in units.</summary>
	public float Range { get; init; }

	/// <summary>Sprayers drain the water tank and draw a long jet; hand tools do neither.</summary>
	public bool UsesWater { get; init; }

	/// <summary>Hand tools tire the player out: they drain the stamina meter instead of water.</summary>
	public bool UsesStamina { get; init; }

	/// <summary>Hand tools scrub a square contact patch; the washer's jet is round.</summary>
	public bool SquareBrush { get; init; }

	/// <summary>Brush footprint for hand tools (sprayers use the Nozzle upgrade instead).</summary>
	public float Radius { get; init; } = 16f;

	/// <summary>Scrub strength for hand tools (sprayers use the Pressure upgrade instead).</summary>
	public float Power { get; init; } = 2.0f;

	/// <summary>One-time purchase price in the van shop. Zero means it's a starter tool that's
	/// owned from the beginning.</summary>
	public double Cost { get; init; }

	/// <summary>Hidden in the van until the hitman briefing unlocks the black-market line.</summary>
	public bool RequiresHitmanUnlock { get; init; }

	public bool IsStarter => Cost <= 0;
}

/// <summary>The full set of tools in the game.</summary>
public static class ToolCatalog
{
	public static readonly IReadOnlyList<ToolDef> All = new List<ToolDef>
	{
		new()
		{
			Type = ToolType.PressureWasher,
			Name = "Pressure Washer",
			Icon = "shower",
			Blurb = "High-pressure jet. Long reach, drains the water tank.",
			Cleans = "Driveways, walls & concrete",
			Range = GameConstants.SprayRange,
			UsesWater = true,
			Tint = new Color( 0f, 0.72f, 1f ),
			UseSound = "sounds/pressure_washer.sound",
		},
		new()
		{
			Type = ToolType.ScrubBrush,
			Name = "Scrub Brush",
			Icon = "cleaning_services",
			Blurb = "Stiff bristles for caked-on moss. No water needed.",
			Cleans = "Mossy fences & timber",
			Range = 150f,
			UsesWater = false,
			UsesStamina = true,
			SquareBrush = true,
			Radius = 18f,
			Power = 2.4f,
			Cost = 900,
			Tint = new Color( 0.98f, 0.68f, 0.24f ),
			UseSound = "sounds/brush.sound",
		},
		new()
		{
			Type = ToolType.Squeegee,
			Name = "Squeegee",
			Icon = "wash",
			Blurb = "Streak-free wipe for glass. Close range, no water.",
			Cleans = "Windows & glass",
			Range = 170f,
			UsesWater = false,
			UsesStamina = true,
			SquareBrush = true,
			Radius = 22f,
			Power = 2.8f,
			Cost = 2400,
			Tint = new Color( 0.30f, 0.88f, 0.80f ),
			UseSound = "sounds/squeegee.sound",
		},
		new()
		{
			Type = ToolType.Gun,
			Name = "9mm Pistol",
			Icon = "gps_fixed",
			Blurb = "Classified hardware. For contract work only — then wash the scene.",
			Cleans = "Contract targets & blood evidence",
			Range = GameConstants.GunRange,
			UsesWater = false,
			Radius = GameConstants.GunRadius,
			Power = GameConstants.GunPower,
			Cost = 15000,
			RequiresHitmanUnlock = true,
			Tint = new Color( 0.22f, 0.24f, 0.28f ),
			UseSound = "sounds/button.sound",
		},
	};

	public static ToolDef Get( ToolType type ) => All.First( t => t.Type == type );
}

/// <summary>Owns which tool is equipped (persisted). Every tool is carried in the van from
/// the start; the challenge is equipping the right one for a job's surfaces.</summary>
public sealed class ToolSystem
{
	private readonly SaveData _save;

	public ToolSystem( SaveData save )
	{
		_save = save;
		Equipped = Parse( save.EquippedTool );
	}

	public ToolType Equipped { get; private set; }
	public ToolDef EquippedDef => ToolCatalog.Get( Equipped );

	/// <summary>Starter tools are always owned; others must be bought from the van shop.</summary>
	public bool IsOwned( ToolType type )
	{
		var def = ToolCatalog.Get( type );
		return def.IsStarter || _save.OwnedTools.Contains( type.ToString() );
	}

	/// <summary>Kept for existing call sites: ownership is the unlock condition.</summary>
	public bool IsUnlocked( ToolType type ) => IsOwned( type );

	/// <summary>Buy a tool with wallet cash. Returns false if already owned or unaffordable.</summary>
	public bool TryBuy( ToolType type, PlayerWallet wallet )
	{
		if ( IsOwned( type ) ) return false;
		var def = ToolCatalog.Get( type );
		if ( !wallet.TrySpend( def.Cost ) ) return false;

		_save.OwnedTools.Add( type.ToString() );
		return true;
	}

	/// <summary>Equip a tool; ignored if the player doesn't own it yet.</summary>
	public void Equip( ToolType type )
	{
		if ( !IsOwned( type ) ) return;
		Equipped = type;
		_save.EquippedTool = type.ToString();
	}

	private static ToolType Parse( string value )
		=> Enum.TryParse<ToolType>( value, out var t ) ? t : ToolType.PressureWasher;
}
