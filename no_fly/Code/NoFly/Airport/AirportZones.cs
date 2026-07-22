namespace NoFly;

public sealed class InteractableMarker : Component, Component.IPressable
{
	[Property] public InteractionKind Kind { get; set; }
	[Property] public string ZoneTag { get; set; }
	[Property] public string Prompt { get; set; } = "Use";

	public bool CanPress( IPressable.Event e )
	{
		var player = e.Source?.Components.Get<NoFlyPlayer>();
		return player.IsValid() && !player.IsArrested;
	}

	public bool Press( IPressable.Event e )
	{
		var player = e.Source?.Components.Get<NoFlyPlayer>();
		if ( !player.IsValid() ) return false;
		player.Components.Get<PlayerInteractor>()?.HandleInteraction( this );
		return true;
	}

	public IPressable.Tooltip GetTooltip( IPressable.Event e ) => new( Prompt, "use", null );
}

public sealed class ObjectiveZone : Component
{
	[Property] public string ZoneTag { get; set; }
	[Property] public float Radius { get; set; } = 80f;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		foreach ( var player in Scene.GetAllComponents<NoFlyPlayer>() )
		{
			if ( !player.NeedsSecurityCheck ) continue;
			if ( Vector3.DistanceBetween( WorldPosition, player.WorldPosition ) > Radius ) continue;
			ObjectiveSystem.TryCompleteZone( player, ZoneTag );
		}
	}
}

public sealed class RestrictedZone : Component
{
	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		var game = NoFlyGame.Instance;
		if ( game is null || !game.IsPlaying ) return;

		foreach ( var player in Scene.GetAllComponents<NoFlyPlayer>() )
		{
			if ( player.Role is RoleType.SecurityOfficer or RoleType.DocumentAgent or RoleType.ScannerAgent ) continue;
			if ( Vector3.DistanceBetween( WorldPosition, player.WorldPosition ) > 90f ) continue;

			game.AddAlert( new SecurityAlert
			{
				Type = AlertType.RestrictedArea,
				Message = $"{player.DisplayName} entered restricted staff area",
				TargetPlayerId = player.PlayerId,
				Position = player.WorldPosition
			} );

			if ( player.Role == RoleType.Smuggler && !player.Exposed )
				game.StartChase( player, "restricted shortcut" );
		}
	}
}

public sealed class BoardingZone : Component
{
	protected override void OnUpdate()
	{
		// Handled via interactable; keep component for scene lookup.
	}
}
