namespace Terraingen.Player;



using Terraingen.Buildings;

using Terraingen.UI.Core;



/// <summary>Number keys and scroll-wheel slot actions for the gameplay hotbar.</summary>

public static class ThornsPlayerHotbarInput

{

	static readonly string[] SlotActions =

	{

		"Slot1", "Slot2", "Slot3", "Slot4", "Slot5", "Slot6", "Slot7", "Slot8"

	};



	public static void Tick()

	{

		if ( !Game.IsPlaying )

			return;



		if ( ThornsUiInputGate.BlocksHotbarInput )

			return;



		var gameplay = ThornsPlayerGameplay.Local;

		if ( gameplay is null )

			return;



		for ( var i = 0; i < SlotActions.Length; i++ )

		{

			if ( Input.Pressed( SlotActions[i] ) )

				gameplay.RequestHotbarSlot( i );

		}



		if ( Input.Pressed( "SlotPrev" ) )

			gameplay.RequestHotbarStep( -1 );



		if ( Input.Pressed( "SlotNext" ) )

			gameplay.RequestHotbarStep( 1 );

	}

}

