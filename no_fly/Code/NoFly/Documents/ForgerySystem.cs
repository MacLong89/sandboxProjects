namespace NoFly;

public static class ForgerySystem
{
	public static List<DocumentFieldOption> GetOptionsForField( DocumentInstance doc, DocumentFieldType field )
	{
		var template = DocumentCatalog.GetTemplate( doc.TemplateId );
		var list = new List<DocumentFieldOption>();
		var catalog = DocumentCatalog.Options[field];
		var correct = doc.Values[field];

		foreach ( var opt in catalog )
		{
			var display = opt.DisplayValue;
			if ( string.IsNullOrEmpty( display ) || opt.IsCorrect )
				display = correct;

			if ( field == DocumentFieldType.Name && !opt.IsCorrect )
				display = DocumentCatalog.ApplyNameForge( correct, opt.DisplayValue );
			if ( field == DocumentFieldType.Date && !opt.IsCorrect && opt.DisplayValue is "DIGIT" or "REVERSE" )
				display = DocumentCatalog.ApplyDateForge( correct, opt.DisplayValue );
			if ( field == DocumentFieldType.PassportNumber && !opt.IsCorrect && opt.DisplayValue is "DIGIT" or "REVERSE" )
				display = DocumentCatalog.ApplyNumberForge( correct, opt.DisplayValue );
			if ( field == DocumentFieldType.Destination && opt.IsCorrect )
				display = correct;
			if ( field == DocumentFieldType.CountrySymbol && opt.IsCorrect )
				display = template.DefaultSymbol;
			if ( field == DocumentFieldType.SecuritySeal && opt.IsCorrect )
				display = template.DefaultSeal;
			if ( field == DocumentFieldType.BackgroundPattern && opt.IsCorrect )
				display = template.DefaultPattern;
			if ( field == DocumentFieldType.Photo && opt.IsCorrect )
				display = template.DefaultPhoto;

			list.Add( new DocumentFieldOption
			{
				Id = opt.Id,
				Label = opt.Label,
				DisplayValue = display,
				Difficulty = opt.Difficulty,
				IsCorrect = opt.IsCorrect
			} );
		}

		return list.Where( o => !o.IsCorrect || o.DisplayValue == correct )
			.GroupBy( o => o.DisplayValue )
			.Select( g => g.First() )
			.Take( 4 )
			.ToList();
	}

	public static void ApplyForgery( NoFlyPlayer smuggler, DocumentFieldType field, string newValue, DiscrepancyDifficulty difficulty )
	{
		if ( !Networking.IsHost || smuggler?.Document is null ) return;
		if ( smuggler.ForgeryComplete ) return;

		var doc = smuggler.Document;
		doc.OriginalValue = doc.Values[field];
		doc.ForgedValue = newValue;
		doc.ForgedField = field;
		doc.Difficulty = difficulty;
		doc.Values[field] = newValue;
		smuggler.Document = doc;
		smuggler.ForgeryComplete = true;
		smuggler.AddScore( 50, "deception" );
	}
}

public static class LuggageHideSystem
{
	public static void HideBehind( NoFlyPlayer smuggler, string itemId )
	{
		if ( !Networking.IsHost || smuggler?.Bag is null ) return;
		if ( smuggler.HideComplete ) return;

		var layout = LuggageCatalog.GetLayout( smuggler.Bag.LayoutId );
		var slot = layout.Slots.FirstOrDefault( s => s.ItemId == itemId );
		if ( slot is null ) return;

		var bag = smuggler.Bag;
		bag.HiddenBehindItemId = itemId;
		bag.ContrabandOffset = slot.Position + new Vector2( 8f, 6f );
		smuggler.Bag = bag;
		smuggler.HideComplete = true;
		smuggler.AddScore( 50, "deception" );
	}
}

public static class ObjectiveSystem
{
	public static void TryCompleteZone( NoFlyPlayer player, string zoneTag )
	{
		if ( player is null ) return;
		var list = player.Objectives;
		foreach ( var obj in list.Where( o => !o.Completed ).ToList() )
		{
			if ( obj.Id == "report_ok" ) continue;
			if ( obj.Id == "find_gate" )
			{
				var assigned = player.AssignedGate;
				if ( zoneTag == "gate" || (!string.IsNullOrEmpty( assigned ) && zoneTag == $"gate_{assigned}") )
					Complete( player, obj.Id );
				continue;
			}
			if ( obj.ZoneTag != zoneTag && obj.ZoneTag != "any" ) continue;
			Complete( player, obj.Id );
		}
	}

	public static void Complete( NoFlyPlayer player, PlayerObjective obj ) => Complete( player, obj?.Id );

	public static void Complete( NoFlyPlayer player, string objectiveId )
	{
		if ( player is null || string.IsNullOrEmpty( objectiveId ) ) return;
		var list = player.Objectives;
		var match = list.FirstOrDefault( o => o.Id == objectiveId );
		if ( match is null || match.Completed ) return;
		match.Completed = true;
		player.Objectives = list;
		player.AddScore( match.Score, "objectives" );
		player.ActivePrompt = $"Objective complete: {match.Label}";
	}
}
