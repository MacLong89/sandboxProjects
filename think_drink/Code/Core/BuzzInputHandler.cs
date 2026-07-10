namespace ThinkDrink;

/// <summary>Handles buzz-in input (Space) outside the UI panel so it always works.</summary>
public sealed class BuzzInputHandler : Component
{
	private const float BuzzerUseRange = 220f;
	private StudioBuzzerButton _hoveredBuzzer;

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor ) return;
		if ( ThinkDrink.UI.UIManager.BlocksGameplayInput ) return;
		if ( MatchManager.Instance?.Phase != MatchPhase.BuzzIn )
		{
			SetHoveredBuzzer( null );
			ThinkDrink.UI.UiState.SetBuzzerHover( false );
			return;
		}

		var hovered = TraceBuzzer();
		SetHoveredBuzzer( hovered );
		ThinkDrink.UI.UiState.SetBuzzerHover( hovered.IsValid() );

		if ( Input.Pressed( "Attack1" ) && TryPressPhysicalBuzzer() )
			return;

		if ( !Input.Pressed( "Buzz" ) )
			return;

		var local = ThinkDrinkPlayer.Local;
		if ( local is null || local.IsSpectator ) return;

		local.RequestBuzz();
	}

	private bool TryPressPhysicalBuzzer()
	{
		var localPlayer = ThinkDrinkPlayer.Local;
		if ( localPlayer is null || localPlayer.IsSpectator ) return false;

		var buzzer = TraceBuzzer();
		if ( !buzzer.IsValid() ) return false;

		buzzer.Press( localPlayer );
		return true;
	}

	private StudioBuzzerButton TraceBuzzer()
	{
		var pawn = FindLocalPawn();
		if ( pawn is null ) return null;

		var start = pawn.GetEyeWorldPosition();
		var end = start + pawn.GetEyeForward() * BuzzerUseRange;
		var trace = Scene.Trace.Ray( start, end )
			.WithTag( "buzzer" )
			.Run();

		return trace.GameObject?.Components.Get<StudioBuzzerButton>( FindMode.EverythingInSelf );
	}

	private void SetHoveredBuzzer( StudioBuzzerButton buzzer )
	{
		if ( _hoveredBuzzer == buzzer ) return;

		if ( _hoveredBuzzer.IsValid() )
			_hoveredBuzzer.SetHovered( false );

		_hoveredBuzzer = buzzer;

		if ( _hoveredBuzzer.IsValid() )
			_hoveredBuzzer.SetHovered( true );
	}

	private PlayerPawn FindLocalPawn()
	{
		foreach ( var pawn in Scene.GetAllComponents<PlayerPawn>() )
		{
			if ( pawn.IsValid() && pawn.IsLocalOwner )
				return pawn;
		}

		return null;
	}
}
