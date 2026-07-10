namespace Terraingen.UI;

using Sandbox;
using Sandbox.UI;
using Terraingen.GameData;

/// <summary>Shared colors, legend copy, and layout helpers for world map + minimap markers.</summary>
public static class ThornsMapMarkerStyle
{
	static readonly ThornsMapMarkerKind[] LegendOrder =
	{
		ThornsMapMarkerKind.You,
		ThornsMapMarkerKind.GuildMember,
		ThornsMapMarkerKind.Metropolis,
		ThornsMapMarkerKind.City,
		ThornsMapMarkerKind.Town,
		ThornsMapMarkerKind.Suburb,
		ThornsMapMarkerKind.RuralPoi,
		ThornsMapMarkerKind.MilitaryPoi,
		ThornsMapMarkerKind.CabinSite,
		ThornsMapMarkerKind.Farmstead,
		ThornsMapMarkerKind.NpcGuildOutpost,
		ThornsMapMarkerKind.Settlement,
		ThornsMapMarkerKind.Home,
		ThornsMapMarkerKind.Goal,
		ThornsMapMarkerKind.CustomWaypoint,
		ThornsMapMarkerKind.Boss,
		ThornsMapMarkerKind.Airdrop,
		ThornsMapMarkerKind.BloomSeed,
		ThornsMapMarkerKind.SpecialEvent,
		ThornsMapMarkerKind.LastDeath
	};

	static readonly ThornsMapMarkerKind[] PoiLegendOrder =
	{
		ThornsMapMarkerKind.Metropolis,
		ThornsMapMarkerKind.City,
		ThornsMapMarkerKind.Town,
		ThornsMapMarkerKind.Suburb,
		ThornsMapMarkerKind.RuralPoi,
		ThornsMapMarkerKind.MilitaryPoi,
		ThornsMapMarkerKind.CabinSite,
		ThornsMapMarkerKind.Farmstead
	};

	public static Color GetColor( ThornsMapMarkerKind kind ) => kind switch
	{
		ThornsMapMarkerKind.You => new Color( 0.12f, 0.86f, 0.24f ),
		ThornsMapMarkerKind.GuildMember => new Color( 0.38f, 0.88f, 0.42f ),
		ThornsMapMarkerKind.Metropolis => new Color( 0.9f, 0.44f, 0.96f ),
		ThornsMapMarkerKind.City => new Color( 0.36f, 0.58f, 0.95f ),
		ThornsMapMarkerKind.Town => new Color( 0.95f, 0.72f, 0.32f ),
		ThornsMapMarkerKind.Suburb => new Color( 0.54f, 0.78f, 0.48f ),
		ThornsMapMarkerKind.RuralPoi => new Color( 0.62f, 0.74f, 0.42f ),
		ThornsMapMarkerKind.MilitaryPoi => new Color( 0.82f, 0.28f, 0.32f ),
		ThornsMapMarkerKind.CabinSite => new Color( 0.74f, 0.52f, 0.31f ),
		ThornsMapMarkerKind.Farmstead => new Color( 0.79f, 0.62f, 0.36f ),
		ThornsMapMarkerKind.NpcGuildOutpost => new Color( 0.82f, 0.28f, 0.32f ),
		ThornsMapMarkerKind.Settlement => new Color( 0.86f, 0.64f, 0.34f ),
		ThornsMapMarkerKind.Home => new Color( 0.32f, 0.72f, 0.95f ),
		ThornsMapMarkerKind.Goal => new Color( 1f, 0.92f, 0.08f ),
		ThornsMapMarkerKind.CustomWaypoint => ThornsTheme.Accent,
		ThornsMapMarkerKind.Boss => new Color( 0.95f, 0.24f, 0.2f ),
		ThornsMapMarkerKind.Airdrop => new Color( 0.48f, 0.82f, 0.38f ),
		ThornsMapMarkerKind.BloomSeed => new Color( 0.22f, 0.62f, 1f ),
		ThornsMapMarkerKind.SpecialEvent => new Color( 0.72f, 0.42f, 0.95f ),
		ThornsMapMarkerKind.LastDeath => new Color( 1f, 0.12f, 0.08f ),
		_ => ThornsTheme.TextSecondary
	};

	public static string GetCssKindClass( ThornsMapMarkerKind kind ) =>
		$"map-marker-{kind.ToString().ToLowerInvariant()}";

	public static string GetIconPath( ThornsMapMarkerKind kind ) =>
		ThornsIconRegistry.MapMarker( kind );

	/// <summary>Minimap draws live player position via the blip, not a marker dot.</summary>
	public static bool UseLivePlayerBlip( ThornsMapMarkerKind kind ) =>
		kind == ThornsMapMarkerKind.You;

	public static string GetLegendTitle( ThornsMapMarkerKind kind ) => kind switch
	{
		ThornsMapMarkerKind.You => "You",
		ThornsMapMarkerKind.GuildMember => "Guild member",
		ThornsMapMarkerKind.Metropolis => "Metropolis",
		ThornsMapMarkerKind.City => "City",
		ThornsMapMarkerKind.Town => "Town",
		ThornsMapMarkerKind.Suburb => "Suburb",
		ThornsMapMarkerKind.RuralPoi => "Rural POI",
		ThornsMapMarkerKind.MilitaryPoi => "Military POI",
		ThornsMapMarkerKind.CabinSite => "Cabin site",
		ThornsMapMarkerKind.Farmstead => "Farmstead",
		ThornsMapMarkerKind.NpcGuildOutpost => "NPC guild outpost",
		ThornsMapMarkerKind.Settlement => "Settlement",
		ThornsMapMarkerKind.Home => "Home",
		ThornsMapMarkerKind.Goal => "Goal",
		ThornsMapMarkerKind.CustomWaypoint => "Waypoint",
		ThornsMapMarkerKind.Boss => "Boss lair",
		ThornsMapMarkerKind.Airdrop => "Airdrop",
		ThornsMapMarkerKind.BloomSeed => "Bloom Seed",
		ThornsMapMarkerKind.SpecialEvent => "Special event",
		ThornsMapMarkerKind.LastDeath => "Last death",
		_ => kind.ToString()
	};

	public static string GetLegendDescription( ThornsMapMarkerKind kind ) => kind switch
	{
		ThornsMapMarkerKind.You => "Your current position on the world map.",
		ThornsMapMarkerKind.GuildMember => "Other players in your guild, live positions.",
		ThornsMapMarkerKind.Metropolis => "Dense urban loot: towers, offices, stores, warehouses, factories, and radio outposts.",
		ThornsMapMarkerKind.City => "Urban POI with apartments, towers, offices, stores, factories, warehouses, and houses.",
		ThornsMapMarkerKind.Town => "Mid-size generated POI with homes, apartments, offices, stores, warehouses, and factories.",
		ThornsMapMarkerKind.Suburb => "Residential POI built mostly from houses, apartments, and stores.",
		ThornsMapMarkerKind.RuralPoi => "Small rural POI with houses, cabins, and barns.",
		ThornsMapMarkerKind.MilitaryPoi => "Military complex and radio outpost loot.",
		ThornsMapMarkerKind.CabinSite => "Solo or paired cabin loot site.",
		ThornsMapMarkerKind.Farmstead => "Solo or paired barn/cabin loot site.",
		ThornsMapMarkerKind.NpcGuildOutpost => "Rival NPC guild territory — destroy the core to claim it.",
		ThornsMapMarkerKind.Settlement => "Named settlements and safe zones.",
		ThornsMapMarkerKind.Home => "Your home or respawn anchor.",
		ThornsMapMarkerKind.Goal => "Active journey goal.",
		ThornsMapMarkerKind.CustomWaypoint => "Waypoint you placed (compass goal).",
		ThornsMapMarkerKind.Boss => "High-threat boss encounter.",
		ThornsMapMarkerKind.Airdrop => "Supply drop or loot event.",
		ThornsMapMarkerKind.BloomSeed => "Timed Bloom infection node. Purify it for +5% Purification path progress.",
		ThornsMapMarkerKind.SpecialEvent => "Timed world event.",
		ThornsMapMarkerKind.LastDeath => "Where you last died.",
		_ => ""
	};

	public static string GetCompactLegendDescription( ThornsMapMarkerKind kind ) => kind switch
	{
		ThornsMapMarkerKind.Metropolis => "15-20: towers, offices, stores, industry, radio.",
		ThornsMapMarkerKind.City => "12-15: towers, apartments, offices, homes, industry.",
		ThornsMapMarkerKind.Town => "12-15: homes, apartments, stores, offices, industry.",
		ThornsMapMarkerKind.Suburb => "10-12: houses, apartments, stores.",
		ThornsMapMarkerKind.RuralPoi => "3-7: houses, cabins, barns.",
		ThornsMapMarkerKind.MilitaryPoi => "3-5: military complexes, radio outposts.",
		ThornsMapMarkerKind.CabinSite => "1-2: solo or paired cabins.",
		ThornsMapMarkerKind.Farmstead => "1-2: barns, cabins, rural homes.",
		_ => GetLegendDescription( kind )
	};

	public static bool IsPoiKind( ThornsMapMarkerKind kind ) =>
		PoiLegendOrder.Contains( kind );

	public static IEnumerable<ThornsMapMarkerKind> GetPoiLegendKinds()
	{
		foreach ( var kind in PoiLegendOrder )
			yield return kind;
	}

	/// <summary>World-event markers always shown in the map legend so players know what to look for.</summary>
	public static IEnumerable<ThornsMapMarkerKind> GetAlwaysVisibleLegendKinds()
	{
		yield return ThornsMapMarkerKind.BloomSeed;
		yield return ThornsMapMarkerKind.Airdrop;
	}

	/// <summary>Classic map mockup legend order and labels.</summary>
	public static IEnumerable<ThornsMapMarkerKind> GetClassicLegendKinds()
	{
		yield return ThornsMapMarkerKind.Settlement;
		yield return ThornsMapMarkerKind.Boss;
		yield return ThornsMapMarkerKind.Airdrop;
		yield return ThornsMapMarkerKind.SpecialEvent;
		yield return ThornsMapMarkerKind.NpcGuildOutpost;
		yield return ThornsMapMarkerKind.GuildMember;
		yield return ThornsMapMarkerKind.You;
		yield return ThornsMapMarkerKind.LastDeath;
	}

	public static bool IsWorldEventLegendKind( ThornsMapMarkerKind kind ) =>
		kind is ThornsMapMarkerKind.BloomSeed
			or ThornsMapMarkerKind.Airdrop
			or ThornsMapMarkerKind.Boss
			or ThornsMapMarkerKind.SpecialEvent;

	public static bool IsPlayerLegendKind( ThornsMapMarkerKind kind ) =>
		kind is ThornsMapMarkerKind.You
			or ThornsMapMarkerKind.CustomWaypoint
			or ThornsMapMarkerKind.GuildMember
			or ThornsMapMarkerKind.LastDeath;

	public static bool IsPoiLegendKind( ThornsMapMarkerKind kind ) =>
		kind is ThornsMapMarkerKind.Home
			or ThornsMapMarkerKind.Town
			or ThornsMapMarkerKind.Settlement
			or ThornsMapMarkerKind.NpcGuildOutpost
			or ThornsMapMarkerKind.CabinSite
			or ThornsMapMarkerKind.Farmstead
			or ThornsMapMarkerKind.Metropolis
			or ThornsMapMarkerKind.City
			or ThornsMapMarkerKind.Suburb
			or ThornsMapMarkerKind.RuralPoi
			or ThornsMapMarkerKind.MilitaryPoi
			or ThornsMapMarkerKind.Goal;

	public static string GetClassicLegendTitle( ThornsMapMarkerKind kind ) => kind switch
	{
		ThornsMapMarkerKind.You => "YOU",
		ThornsMapMarkerKind.Settlement => "SETTLEMENT",
		ThornsMapMarkerKind.Town => "TOWN / SETTLEMENT",
		ThornsMapMarkerKind.Boss => "BOSS FIGHT",
		ThornsMapMarkerKind.Airdrop => "AIRDROP",
		ThornsMapMarkerKind.SpecialEvent => "SPECIAL EVENT",
		ThornsMapMarkerKind.NpcGuildOutpost => "GUILD OUTPOST",
		ThornsMapMarkerKind.GuildMember => "GUILD MEMBER",
		ThornsMapMarkerKind.LastDeath => "LAST DEATH",
		ThornsMapMarkerKind.CustomWaypoint => "WAYPOINT",
		ThornsMapMarkerKind.Goal => "RUINS / GOAL",
		ThornsMapMarkerKind.Home => "YOUR CAMP",
		ThornsMapMarkerKind.CabinSite => "LANDMARK",
		ThornsMapMarkerKind.BloomSeed => "RESOURCE NODE",
		_ => GetLegendTitle( kind ).ToUpperInvariant()
	};

	public static void WorldToMap01( ThornsMapSnapshotDto snap, float worldX, float worldY, out float u, out float v ) =>
		ThornsMapProjection.WorldToMap01( snap, worldX, worldY, out u, out v );

	public static void ApplyMarkerPosition( Panel panel, float u, float v, bool clampToEdge = false )
	{
		if ( clampToEdge )
		{
			u = Math.Clamp( u, 0.04f, 0.96f );
			v = Math.Clamp( v, 0.04f, 0.96f );
		}

		panel.Style.Left = Length.Fraction( u );
		panel.Style.Top = Length.Fraction( v );
	}

	public static void StyleMapMarkerPanel( Panel panel, ThornsMapMarkerKind kind )
	{
		panel.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0f );
		panel.AddClass( "map-marker" );
		panel.AddClass( GetCssKindClass( kind ) );

		if ( UsesCustomGlyph( kind ) )
		{
			BuildCustomMarkerGlyph( panel, kind );
			return;
		}

		ThornsIconCache.ApplyToPanel( panel, GetIconPath( kind ) );
	}

	public static bool UsesCustomGlyph( ThornsMapMarkerKind kind ) =>
		kind == ThornsMapMarkerKind.LastDeath;

	public static void BuildCustomMarkerGlyph( Panel glyph, ThornsMapMarkerKind kind )
	{
		if ( kind != ThornsMapMarkerKind.LastDeath )
			return;

		glyph.Style.BackgroundImage = null;
		ThornsUiFactory.AddPanel( glyph, "map-marker-lastdeath-bar" );
		ThornsUiFactory.AddPanel( glyph, "map-marker-lastdeath-bar map-marker-lastdeath-bar-alt" );
	}

	public const string PlayerArrowIconPath = "ui/map/player_arrow.png";

	public const float MinimapPlayerBlipSizePx = 14f;
	public const float WorldMapPlayerBlipSizePx = 32f;
	public const float PlayerBlipSizePx = MinimapPlayerBlipSizePx;
	public static Color PlayerBlipColor => new Color( 0.12f, 0.86f, 0.24f );

	/// <summary>World yaw → map rotation (map up = world +Y, arrow asset points north/up).</summary>
	public static bool TryResolvePlayerMapFacingDegrees( GameObject playerRoot, out float degrees )
	{
		degrees = 0f;
		if ( !playerRoot.IsValid() )
			return false;

		var controller = playerRoot.Components.Get<PlayerController>();
		var forward = controller.IsValid()
			? controller.EyeAngles.ToRotation().Forward
			: playerRoot.WorldRotation.Forward;

		forward = forward.WithZ( 0f );
		if ( forward.LengthSquared < 0.0001f )
			return false;

		forward = forward.Normal;
		degrees = MathF.Atan2( forward.x, forward.y ) * (180f / MathF.PI );
		return true;
	}

	public static void ApplyPlayerBlipRotation( Panel panel, float degrees )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.Style.TransformOriginX = Length.Fraction( 0.5f );
		panel.Style.TransformOriginY = Length.Fraction( 0.5f );

		var transform = new PanelTransform();
		transform.AddRotation( 0f, 0f, degrees );
		panel.Style.Transform = transform;
	}

	public static void StylePlayerBlipPanel( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( "map-player-blip-arrow" );
		panel.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0f );
		panel.Style.BorderWidth = Length.Pixels( 0 );
		ThornsIconCache.ApplyToPanel( panel, PlayerArrowIconPath, addSlotIconClass: false );
		ApplyPlayerBlipRotation( panel, 0f );
	}

	/// <summary>Legend row icon — PNG glyphs for POIs; green north arrow for the local player.</summary>
	public static void StyleLegendIconPanel( Panel panel, ThornsMapMarkerKind kind )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.AddClass( GetCssKindClass( kind ) );

		if ( kind == ThornsMapMarkerKind.You )
		{
			panel.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0f );
			ThornsIconCache.ApplyToPanel( panel, PlayerArrowIconPath, addSlotIconClass: false );
			ApplyPlayerBlipRotation( panel, 0f );
			return;
		}

		if ( kind == ThornsMapMarkerKind.LastDeath )
		{
			panel.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0f );
			BuildCustomMarkerGlyph( panel, kind );
			return;
		}

		panel.Style.BackgroundColor = new Color( 0f, 0f, 0f, 0f );
		ThornsIconCache.ApplyToPanel( panel, GetIconPath( kind ), addSlotIconClass: false );
	}

	public static IEnumerable<ThornsMapMarkerKind> GetLegendKinds( IReadOnlyList<ThornsMapMarkerDto> markers, bool includeYou )
	{
		var present = markers is not null
			? markers.Select( m => m.Kind ).ToHashSet()
			: new HashSet<ThornsMapMarkerKind>();

		if ( includeYou )
			present.Add( ThornsMapMarkerKind.You );

		foreach ( var kind in LegendOrder )
		{
			if ( present.Contains( kind ) )
				yield return kind;
		}
	}
}
