namespace Deep;

public enum MinimapBlipKind
{
	Player,
	Threat,
	Loot,
	Objective
}

public readonly struct MinimapBlip
{
	public MinimapBlipKind Kind { get; init; }
	/// <summary>0-1 within the local map window (0.5 = center).</summary>
	public float Nx { get; init; }
	public float Ny { get; init; }
}

/// <summary>Runtime dive tools: hotbar selection, Attack1 use, status effects.</summary>
public sealed class ToolSystem : Component
{
	public HotbarInventory Hotbar { get; } = new();

	public float ScannerRevealSeconds { get; private set; }
	public float DroneRevealSeconds { get; private set; }
	public float LureDistractSeconds { get; private set; }
	public float SubBurstSeconds { get; private set; }
	public float FinSpeedMultiplier => Hotbar.SelectedTool?.Kind == ToolKind.Fins ? 1.12f : 1f;
	public float SubSpeedMultiplier => SubBurstSeconds > 0f ? 1.65f : 1f;

	public bool ScannerActive => ScannerRevealSeconds > 0f;
	public bool DroneActive => DroneRevealSeconds > 0f;
	public bool LureActive => LureDistractSeconds > 0f;

	public float ScanRadius => 28f + (DeepGame.Instance?.Balance.ScannerRangeBonus ?? 0f);
	public float HarpoonRange => 9f;
	public float HarpoonPickRadius => 3.5f;
	public float CameraRange => 10f;

	public void ClearDiveEffects()
	{
		ScannerRevealSeconds = 0f;
		DroneRevealSeconds = 0f;
		LureDistractSeconds = 0f;
		SubBurstSeconds = 0f;
	}

	public void ClearEffects()
	{
		ScannerRevealSeconds = 0f;
		DroneRevealSeconds = 0f;
		LureDistractSeconds = 0f;
		SubBurstSeconds = 0f;
	}

	protected override void OnUpdate()
	{
		var game = DeepGame.Instance;
		if ( game is null ) return;

		TickEffects();

		if ( !game.State.IsDivingActive )
			return;

		HandleHotbarInput( game );

		if ( Input.Pressed( "Attack1" ) )
			TryUseSelected( game );
	}

	private void TickEffects()
	{
		var dt = Time.Delta;
		if ( ScannerRevealSeconds > 0f ) ScannerRevealSeconds = MathF.Max( 0f, ScannerRevealSeconds - dt );
		if ( DroneRevealSeconds > 0f ) DroneRevealSeconds = MathF.Max( 0f, DroneRevealSeconds - dt );
		if ( LureDistractSeconds > 0f ) LureDistractSeconds = MathF.Max( 0f, LureDistractSeconds - dt );
		if ( SubBurstSeconds > 0f ) SubBurstSeconds = MathF.Max( 0f, SubBurstSeconds - dt );
	}

	private void HandleHotbarInput( DeepGame game )
	{
		var loadout = game.Loadout;
		for ( var i = 0; i < Hotbar.SlotCount; i++ )
		{
			if ( !Input.Pressed( $"Slot{i + 1}" ) )
				continue;

			if ( loadout is not null && !loadout.IsUnlocked( i ) )
			{
				game.ShowMessage( "Buy this tool at the Diver Hub", 1.1f );
				return;
			}

			Hotbar.Select( i );
			var def = Hotbar.SelectedTool;
			if ( def is not null )
				game.ShowMessage( def.DisplayName, 0.9f );
			return;
		}

		if ( Input.Pressed( "SlotNext" ) )
		{
			Hotbar.SelectNext( loadout );
			var def = Hotbar.SelectedTool;
			if ( def is not null )
				game.ShowMessage( def.DisplayName, 0.7f );
		}
		else if ( Input.Pressed( "SlotPrev" ) )
		{
			Hotbar.SelectPrev( loadout );
			var def = Hotbar.SelectedTool;
			if ( def is not null )
				game.ShowMessage( def.DisplayName, 0.7f );
		}
	}

	public bool TryUseSelected( DeepGame game )
	{
		var def = Hotbar.SelectedTool;
		if ( def is null ) return false;

		if ( def.Kind == ToolKind.Fins )
		{
			game.ShowMessage( "Fins equipped — swim faster", 1.0f );
			return true;
		}

		if ( def.Kind == ToolKind.Submersible )
		{
			UseSub( game );
			return true;
		}

		if ( !Hotbar.CanUseSelected() )
		{
			if ( Hotbar.IsOnCooldown( Hotbar.SelectedIndex ) )
				game.ShowMessage( "Tool recharging...", 0.8f );
			else
				game.ShowMessage( "No charges left", 0.9f );
			return false;
		}

		var ok = def.Kind switch
		{
			ToolKind.Harpoon => UseHarpoon( game ),
			ToolKind.Scanner => UseScanner( game ),
			ToolKind.Camera => UseCamera( game ),
			ToolKind.OxygenTank => UseOxygen( game ),
			ToolKind.Drone => UseDrone( game ),
			ToolKind.Lure => UseLure( game ),
			_ => false
		};

		if ( !ok ) return false;

		Hotbar.TryConsumeSelected();
		game.Objectives?.NotifyToolUsed( def.Kind );
		return true;
	}

	private bool UseHarpoon( DeepGame game )
	{
		var diver = game.Diver;
		if ( diver is null ) return false;

		if ( !DivePointer.TryGetPlayPlanePoint( out var clickWorld ) )
		{
			game.ShowMessage( "Click a creature to harpoon", 0.9f );
			return false;
		}

		var target = PickHazardAtPoint( game, clickWorld );
		if ( target is null )
		{
			// Locked vault salvage with harpoon selected + click near vault.
			if ( TrySalvageLockedAtPoint( game, clickWorld ) )
				return true;

			game.ShowMessage( "Click a creature to harpoon", 0.9f );
			return false;
		}

		var diverDist = (target.WorldPosition - diver.WorldPosition).Length;
		if ( diverDist > HarpoonRange )
		{
			game.ShowMessage( "Too far — swim closer", 1.0f );
			return false;
		}

		var kind = target.Kind;
		var targetPos = target.WorldPosition;
		var creature = CreatureCatalog.FromHazard( kind );
		diver.Animator?.PlayHarpoon( targetPos );

		HarpoonSpearFx.Launch( diver.WorldPosition, targetPos, () =>
		{
			if ( target.IsValid() )
				target.GameObject.Destroy();
		} );

		if ( creature is not null )
			game.Progression.RegisterCreature( creature.Id );

		game.ShowMessage( $"Harpooned {kind}!", 1.1f );
		game.DiveLog?.AddEntry( $"Harpooned {kind}", "/ui/icons/tool_harpoon.png", known: true );
		return true;
	}

	private bool TrySalvageLockedAtPoint( DeepGame game, Vector3 clickWorld )
	{
		var diver = game.Diver;
		if ( diver is null ) return false;

		CollectiblePickup best = null;
		var bestDist = HarpoonPickRadius;
		foreach ( var c in game.Collectibles?.ActivePickups ?? Array.Empty<CollectiblePickup>() )
		{
			if ( c is null || !c.IsValid() || c.Collected || c.Definition is null ) continue;
			if ( c.Definition.RequiredTool != ToolKind.Harpoon ) continue;
			if ( c.Definition.RequiresScan && !c.Revealed ) continue;

			var planar = new Vector3( c.WorldPosition.x, 0f, c.WorldPosition.z );
			var click = new Vector3( clickWorld.x, 0f, clickWorld.z );
			var dist = (planar - click).Length;
			if ( dist < bestDist )
			{
				bestDist = dist;
				best = c;
			}
		}

		if ( best is null ) return false;
		if ( (best.WorldPosition - diver.WorldPosition).Length > HarpoonRange )
		{
			game.ShowMessage( "Too far — swim closer", 1.0f );
			return false;
		}

		// Force pickup path by temporarily clearing required tool gate via direct add.
		var def = best.Definition;
		if ( !game.Run.Haul.CanFit( def ) )
		{
			game.ShowMessage( "Bag full!", 1.0f );
			return false;
		}

		if ( !game.Run.Haul.TryAdd( def ) )
			return false;

		best.GameObject.Destroy();
		game.Progression.RegisterDiscovery( def.Id );
		game.ShowMessage( $"Breached {def.DisplayName}!", 1.3f );
		return true;
	}

	private HazardContact PickHazardAtPoint( DeepGame game, Vector3 clickWorld )
	{
		HazardContact best = null;
		var bestDist = HarpoonPickRadius;

		foreach ( var h in game.Hazards?.ActiveHazards ?? Array.Empty<HazardContact>() )
		{
			if ( h is null || !h.IsValid() ) continue;
			if ( LureActive && h.Kind is HazardKind.Jellyfish or HazardKind.Puffer )
				continue;

			var planar = new Vector3( h.WorldPosition.x, 0f, h.WorldPosition.z );
			var click = new Vector3( clickWorld.x, 0f, clickWorld.z );
			var dist = (planar - click).Length;
			if ( dist < bestDist )
			{
				bestDist = dist;
				best = h;
			}
		}

		return best;
	}

	private bool UseScanner( DeepGame game )
	{
		ScannerRevealSeconds = 12f;
		var loot = 0;
		var threats = 0;
		var diver = game.Diver;
		if ( diver is null ) return false;

		foreach ( var c in game.Collectibles?.ActivePickups ?? Array.Empty<CollectiblePickup>() )
		{
			if ( c is null || !c.IsValid() || c.Collected ) continue;
			if ( (c.WorldPosition - diver.WorldPosition).Length > ScanRadius ) continue;
			loot++;
			if ( c.Definition?.RequiresScan == true )
			{
				c.Reveal();
				game.Run.MarkScanned( c.ScanKey );
			}
		}

		foreach ( var h in game.Hazards?.ActiveHazards ?? Array.Empty<HazardContact>() )
		{
			if ( h is null || !h.IsValid() ) continue;
			if ( (h.WorldPosition - diver.WorldPosition).Length > ScanRadius ) continue;
			threats++;
			var creature = CreatureCatalog.FromHazard( h.Kind );
			if ( creature is not null )
				game.Progression.RegisterCreature( creature.Id );
		}

		foreach ( var story in game.Stories?.ActiveStories ?? Array.Empty<StoryPickup>() )
		{
			if ( story is null || !story.IsValid() || story.Collected ) continue;
			if ( (story.WorldPosition - diver.WorldPosition).Length > ScanRadius * 0.5f ) continue;
			if ( story.Definition?.RequiredTool == ToolKind.Scanner )
				story.TryInteract( game );
		}

		game.ShowMessage( $"Scan: {loot} loot · {threats} threats", 1.6f );
		return true;
	}

	private bool UseCamera( DeepGame game )
	{
		var diver = game.Diver;
		if ( diver is null ) return false;

		CollectiblePickup best = null;
		var bestDist = CameraRange;
		foreach ( var c in game.Collectibles?.ActivePickups ?? Array.Empty<CollectiblePickup>() )
		{
			if ( c is null || !c.IsValid() || c.Collected || c.Definition is null ) continue;
			var dist = (c.WorldPosition - diver.WorldPosition).Length;
			if ( dist < bestDist )
			{
				bestDist = dist;
				best = c;
			}
		}

		if ( best is null )
		{
			game.ShowMessage( "Nothing to photograph", 1.0f );
			return false;
		}

		var def = best.Definition;
		if ( !game.Run.TryMarkPhotographed( best.ScanKey ) )
		{
			game.ShowMessage( "Already photographed", 1.0f );
			return false;
		}

		var bonus = MathF.Max( 8f, def.BaseValue * 0.35f );
		game.Run.AddPhotoBonus( bonus );
		game.Progression.RegisterDiscovery( def.Id );
		game.DiveLog?.AddEntry( def.DisplayName, IconForCollectible( def ), known: true );
		game.ShowMessage( $"Photo +${(int)bonus} — {def.DisplayName}", 1.5f );
		game.Objectives?.NotifyPhoto( def.Id );
		return true;
	}

	private bool UseOxygen( DeepGame game )
	{
		var o2 = game.Oxygen;
		if ( o2 is null ) return false;
		if ( o2.Fraction >= 0.98f )
		{
			game.ShowMessage( "Oxygen already full", 0.9f );
			return false;
		}

		o2.RestoreFraction( 0.45f );
		game.ShowMessage( "Oxygen restored", 1.2f );
		return true;
	}

	private bool UseDrone( DeepGame game )
	{
		DroneRevealSeconds = 16f;
		ScannerRevealSeconds = MathF.Max( ScannerRevealSeconds, 6f );
		game.ShowMessage( "Scout drone deployed", 1.3f );
		return true;
	}

	private bool UseLure( DeepGame game )
	{
		LureDistractSeconds = 10f;
		game.ShowMessage( "Wildlife distracted", 1.2f );
		return true;
	}

	private bool UseSub( DeepGame game )
	{
		if ( game.Vehicles?.TryInteract( game ) == true )
			return true;

		if ( game.Vehicles?.HasSubUnlocked( game ) == true )
		{
			game.ShowMessage( "Swim to the mini-sub near the boat", 1.4f );
			return false;
		}

		SubBurstSeconds = 4.5f;
		game.Boost?.RestoreFraction( 0.55f );
		game.ShowMessage( "Submersible burst!", 1.2f );
		return true;
	}

	private static int CountNearbyLoot( DeepGame game, float radius )
	{
		var diver = game.Diver;
		if ( diver is null ) return 0;
		var n = 0;
		foreach ( var c in game.Collectibles?.ActivePickups ?? Array.Empty<CollectiblePickup>() )
		{
			if ( c is null || !c.IsValid() || c.Collected ) continue;
			if ( (c.WorldPosition - diver.WorldPosition).Length <= radius ) n++;
		}
		return n;
	}

	private static int CountNearbyThreats( DeepGame game, float radius )
	{
		var diver = game.Diver;
		if ( diver is null ) return 0;
		var n = 0;
		foreach ( var h in game.Hazards?.ActiveHazards ?? Array.Empty<HazardContact>() )
		{
			if ( h is null || !h.IsValid() ) continue;
			if ( (h.WorldPosition - diver.WorldPosition).Length <= radius ) n++;
		}
		return n;
	}

	public IReadOnlyList<MinimapBlip> BuildMinimapBlips( DeepGame game, float mapRadiusMeters = 40f )
	{
		var list = new List<MinimapBlip>( 16 );
		var diver = game.Diver;
		if ( diver is null ) return list;

		list.Add( new MinimapBlip { Kind = MinimapBlipKind.Player, Nx = 0.5f, Ny = 0.5f } );

		var reveal = ScannerActive || DroneActive;
		var origin = diver.WorldPosition;
		var halfW = game.Balance.HorizontalHalfWidth;
		var alwaysThreatRadius = mapRadiusMeters * 0.55f;

		foreach ( var h in game.Hazards?.ActiveHazards ?? Array.Empty<HazardContact>() )
		{
			if ( h is null || !h.IsValid() ) continue;
			var range = reveal ? mapRadiusMeters : alwaysThreatRadius;
			if ( !TryProject( origin, h.WorldPosition, range, halfW, out var nx, out var ny ) )
				continue;
			list.Add( new MinimapBlip { Kind = MinimapBlipKind.Threat, Nx = nx, Ny = ny } );
		}

		if ( reveal )
		{
			foreach ( var c in game.Collectibles?.ActivePickups ?? Array.Empty<CollectiblePickup>() )
			{
				if ( c is null || !c.IsValid() || c.Collected ) continue;
				if ( !TryProject( origin, c.WorldPosition, mapRadiusMeters, halfW, out var nx, out var ny ) )
					continue;
				list.Add( new MinimapBlip { Kind = MinimapBlipKind.Loot, Nx = nx, Ny = ny } );
			}
		}

		var objDepth = game.Objectives?.TargetDepthMeters ?? 0f;
		if ( objDepth > 0f )
		{
			var objPos = new Vector3( origin.x, 0f, game.Balance.WorldZFromDepth( objDepth ) );
			if ( TryProject( origin, objPos, mapRadiusMeters, halfW, out var ox, out var oy ) )
				list.Add( new MinimapBlip { Kind = MinimapBlipKind.Objective, Nx = ox, Ny = oy } );
		}

		return list;
	}

	private static bool TryProject( Vector3 origin, Vector3 world, float radius, float halfW, out float nx, out float ny )
	{
		nx = 0.5f;
		ny = 0.5f;
		var dx = world.x - origin.x;
		var dz = world.z - origin.z;
		var dist = MathF.Sqrt( dx * dx + dz * dz );
		if ( dist > radius ) return false;

		nx = 0.5f + (dx / (radius * 2f));
		// Deeper = higher on map (screen Y up is shallower in side-view ocean; map shows depth down)
		ny = 0.5f - (dz / (radius * 2f));
		nx = Math.Clamp( nx, 0.06f, 0.94f );
		ny = Math.Clamp( ny, 0.06f, 0.94f );
		_ = halfW;
		return true;
	}

	private static string IconForCollectible( CollectibleDefinition def )
	{
		if ( !string.IsNullOrEmpty( def.TexturePath ) )
			return "/" + def.TexturePath.Replace( '\\', '/' );
		return "/ui/icons/map_loot.png";
	}
}
