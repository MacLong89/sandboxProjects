namespace RunGun;

public enum BuildStat
{
	Squad,
	Damage,
	FireRate,
	Multishot,
	Pierce,
	CritChance,
	Shield,
	Heal,
	CoinMult,
}

public enum GateOp { Add, Mult }

/// <summary>Presentation helpers for gate types and build HUD labels.</summary>
public static class BuildStatPresentation
{
	public static Color ColorFor( BuildStat stat ) => stat switch
	{
		BuildStat.Squad => new Color( 0.4f, 1f, 0.5f ),
		BuildStat.Damage => new Color( 1f, 0.45f, 0.35f ),
		BuildStat.FireRate => new Color( 0.35f, 0.85f, 1f ),
		BuildStat.Multishot => new Color( 1f, 0.82f, 0.3f ),
		BuildStat.Pierce => new Color( 0.55f, 0.95f, 0.55f ),
		BuildStat.CritChance => new Color( 1f, 0.35f, 0.75f ),
		BuildStat.Shield => new Color( 0.45f, 0.7f, 1f ),
		BuildStat.Heal => new Color( 0.4f, 1f, 0.55f ),
		BuildStat.CoinMult => new Color( 1f, 0.85f, 0.25f ),
		_ => Color.White,
	};

	public static string IconFor( BuildStat stat ) => stat switch
	{
		BuildStat.Squad => "groups",
		BuildStat.Damage => "whatshot",
		BuildStat.FireRate => "bolt",
		BuildStat.Multishot => "flare",
		BuildStat.Pierce => "arrow_forward",
		BuildStat.CritChance => "stars",
		BuildStat.Shield => "shield",
		BuildStat.Heal => "favorite",
		BuildStat.CoinMult => "payments",
		_ => "help",
	};

	public static string ShortName( BuildStat stat ) => stat switch
	{
		BuildStat.Squad => "MOB",
		BuildStat.Damage => "DMG",
		BuildStat.FireRate => "RATE",
		BuildStat.Multishot => "SHOTS",
		BuildStat.Pierce => "PIERCE",
		BuildStat.CritChance => "CRIT",
		BuildStat.Shield => "SHIELD",
		BuildStat.Heal => "HEAL",
		BuildStat.CoinMult => "COINS",
		_ => "???",
	};

	/// <summary>Just the value portion, e.g. "+12", "x1.4", "+8%". Kept short so it never clips.</summary>
	public static string FormatGateValue( BuildStat stat, GateOp op, float value )
	{
		if ( stat == BuildStat.Squad && op == GateOp.Add && value < 0f )
			return $"-{(int)MathF.Abs( value )}";

		return stat switch
		{
			BuildStat.Squad => op == GateOp.Add ? $"+{(int)value}" : $"x{value:0.0}",
			BuildStat.Damage => op == GateOp.Add ? $"+{(int)value}" : $"x{value:0.0}",
			BuildStat.FireRate => op == GateOp.Add ? $"+{value:0.1}" : $"x{value:0.0}",
			BuildStat.Multishot => $"+{(int)value}",
			BuildStat.Pierce => $"+{(int)value}",
			BuildStat.CritChance => $"+{value * 100f:0}%",
			BuildStat.Shield => $"+{(int)value}",
			BuildStat.Heal => $"+{(int)value}",
			BuildStat.CoinMult => op == GateOp.Add ? $"+{value:0.1}" : $"x{value:0.0}",
			_ => "?"
		};
	}

	public static string FormatGateLabel( BuildStat stat, GateOp op, float value )
	{
		if ( stat == BuildStat.Squad && op == GateOp.Add && value < 0f )
			return $"-{(int)MathF.Abs( value )} MOB";

		return stat switch
		{
			BuildStat.Squad => op == GateOp.Add ? $"+{(int)value} MOB" : $"x{value:0.0} MOB",
			BuildStat.Damage => op == GateOp.Add ? $"+{(int)value} DMG" : $"x{value:0.0} DMG",
			BuildStat.FireRate => op == GateOp.Add ? $"+{value:0.1} RATE" : $"x{value:0.0} RATE",
			BuildStat.Multishot => $"+{(int)value} SHOT",
			BuildStat.Pierce => $"+{(int)value} PIERCE",
			BuildStat.CritChance => $"+{value * 100f:0}% CRIT",
			BuildStat.Shield => $"+{(int)value} SHIELD",
			BuildStat.Heal => $"+{(int)value} HP",
			BuildStat.CoinMult => op == GateOp.Add ? $"+{value:0.1} COINS" : $"x{value:0.0} COINS",
			_ => "?"
		};
	}

	public static Color ColorForGate( BuildStat stat, GateOp op, float value )
	{
		if ( stat == BuildStat.Squad && op == GateOp.Add && value < 0f )
			return new Color( 1f, 0.2f, 0.18f );
		return ColorFor( stat );
	}
}
