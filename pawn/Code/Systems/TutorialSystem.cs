namespace PawnShop;

public enum TutorialTrigger
{
	None,
	OpenedShop,
	StartedNegotiation,
	OpenedInspection,
	FoundDefect,
	MadeOffer,
	BoughtItem,
	CleanedItem,
	PricedItem,
	DisplayedItem,
	SoldItem,
	SawSummary,
}

/// <summary>
/// Contextual first-day tutorial. A linear chain of prompts driven by what the
/// player actually does. Skippable at any time.
/// </summary>
public sealed class TutorialSystem
{
	private readonly SaveData _save;
	private int _dismissedStep = -1;

	public TutorialSystem( SaveData save )
	{
		_save = save;
	}

	public bool Done => _save.TutorialDone;
	public int Step => _save.TutorialStep;
	public int DismissedStep => _dismissedStep;

	private static readonly (TutorialTrigger Advance, string Title, string Text, string Icon)[] Steps =
	{
		(TutorialTrigger.OpenedShop, "Morning prep", "Welcome to Brass Buck! Before the rush — polish the COUNTER, water the PLANT, or sweep dirt on the floor (look for mess, press E). Then open at the FRONT DOOR.", "storefront"),
		(TutorialTrigger.StartedNegotiation, "Serve customers", "A customer is coming in. When they reach the counter, walk up and press E to serve them.", "person"),
		(TutorialTrigger.OpenedInspection, "Inspect first", "Before talking money, click INSPECT ITEM to look the item over.", "search"),
		(TutorialTrigger.FoundDefect, "Check the item", "Click the inspection points to examine the item. Pick the right tool for each spot.", "touch_app"),
		(TutorialTrigger.MadeOffer, "Make an offer", "Good. Now set your offer with the slider and press MAKE OFFER. Aim below the estimate to profit.", "payments"),
		(TutorialTrigger.BoughtItem, "Close the deal", "Haggle until you land a deal (or walk away — rejecting is always allowed).", "handshake"),
		(TutorialTrigger.CleanedItem, "Backroom work", "Bought items go to the BACKROOM behind the counter. Walk back there, pick one up (E), or open the workbench to CLEAN it.", "cleaning_services"),
		(TutorialTrigger.DisplayedItem, "Stock the shelf", "Carry an item to a front shelf and press E to DISPLAY it — or set a price in TAB and click Display.", "sell"),
		(TutorialTrigger.SoldItem, "Wait for buyers", "Buyers browse the shelves. Make sure something's priced and displayed, then wait for a sale!", "storefront"),
		(TutorialTrigger.SawSummary, "You're set", "Nice work! Keep dealing until closing time (or close up early at the door). This is your shop now.", "celebration"),
	};

	public bool ShouldShow => !Done && Step < Steps.Length && _dismissedStep != Step;
	public string CurrentTitle => Done || Step >= Steps.Length ? null : Steps[Step].Title;
	public string CurrentText => Done || Step >= Steps.Length ? null : Steps[Step].Text;
	public string CurrentIcon => Done || Step >= Steps.Length ? "school" : Steps[Step].Icon;

	public void Notify( TutorialTrigger trigger )
	{
		if ( Done || Step >= Steps.Length ) return;

		// Fast-forward: if the player skipped ahead (e.g. displayed an item that never
		// needed cleaning), jump past the intermediate steps instead of getting stuck.
		for ( var i = Step; i < Steps.Length; i++ )
		{
			if ( Steps[i].Advance != trigger ) continue;
			_save.TutorialStep = i + 1;
			if ( _save.TutorialStep >= Steps.Length )
				_save.TutorialDone = true;
			UiState.Bump();
			return;
		}
	}

	public void Dismiss()
	{
		if ( Done || Step >= Steps.Length ) return;
		_dismissedStep = Step;
		UiState.Bump();
	}

	public void Skip()
	{
		_save.TutorialDone = true;
		_dismissedStep = -1;
		UiState.Bump();
	}

	public void ToggleHidden()
	{
		if ( Done )
		{
			_save.TutorialDone = false;
			_dismissedStep = -1;
		}
		else
		{
			Skip();
		}

		UiState.Bump();
	}
}
