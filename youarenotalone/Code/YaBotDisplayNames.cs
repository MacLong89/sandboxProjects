using System.Collections.Generic;

namespace Sandbox;

/// <summary>Assigns unique faux player handles to practice bots.</summary>
public static class YaBotDisplayNames
{
	static readonly string[] Handles =
	[
		"Ashwalker", "NovaStrike", "RiftHunter", "SilentVex", "CrimsonWolf",
		"IronMoth", "GhostLedger", "HexPulse", "NightCartographer", "ZeroKelvin",
		"DustRazor", "PaleSignal", "VantaScout", "CopperHive", "BlackTide",
		"StaticSaint", "GlassRevenant", "HollowIndex", "MireRunner", "ColdVector",
		"EmberQuiet", "ShadeCourier", "RustOracle", "IvoryFang", "DriftNomad",
		"ObsidianKite", "LumenJackal", "FrostCipher", "VelvetRook", "GrimArcade",
		"QuartzViper", "SableWarden", "TarnishedAce", "UmberGhost", "WraithPilot"
	];

	static readonly string[] Prefixes = [ "Neon", "Static", "Pale", "Iron", "Ghost", "Ash", "Cold", "Rift", "Dust", "Night" ];
	static readonly string[] Suffixes = [ "Warden", "Scout", "Fang", "Pulse", "Walker", "Index", "Nomad", "Oracle", "Pilot", "Vector" ];

	static readonly HashSet<string> _reserved = new( StringComparer.Ordinal );

	public static string Reserve()
	{
		foreach ( var handle in Handles )
		{
			if ( _reserved.Add( handle ) )
				return handle;
		}

		for ( var attempt = 0; attempt < 64; attempt++ )
		{
			var generated = $"{Prefixes[Random.Shared.Int( 0, Prefixes.Length - 1 )]}{Suffixes[Random.Shared.Int( 0, Suffixes.Length - 1 )]}{Random.Shared.Int( 10, 99 )}";
			if ( _reserved.Add( generated ) )
				return generated;
		}

		var fallback = $"Hunter{Random.Shared.Int( 1000, 9999 )}";
		_reserved.Add( fallback );
		return fallback;
	}

	public static void Release( string name )
	{
		if ( string.IsNullOrWhiteSpace( name ) )
			return;

		_reserved.Remove( name.Trim() );
	}

	public static void ClearAll() => _reserved.Clear();
}
