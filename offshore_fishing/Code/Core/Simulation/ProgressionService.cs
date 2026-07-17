namespace OffshoreFishing.Core;

public sealed class ProgressionService
{
	private readonly GameContent _content;
	private readonly List<IDomainEvent> _events = new();

	public ProgressionService( GameContent content )
	{
		_content = content;
	}

	public IReadOnlyList<IDomainEvent> DrainEvents()
	{
		var copy = _events.ToList();
		_events.Clear();
		return copy;
	}

	public void EnsureActiveObjective( GameState state )
	{
		if ( !string.IsNullOrEmpty( state.ActiveObjectiveId ) ) return;
		var next = _content.Objectives
			.Where( o => !state.CompletedObjectiveIds.Contains( o.Id ) )
			.OrderBy( o => o.SortOrder )
			.FirstOrDefault();
		if ( next == null ) return;
		state.ActiveObjectiveId = next.Id;
		state.ActiveObjectiveProgress = 0;
		_events.Add( new ObjectiveUpdatedEvent
		{
			ObjectiveId = next.Id,
			Progress = 0,
			Target = next.TargetCount,
			Completed = false
		} );
	}

	public void OnFishCaught( GameState state, CaughtFish fish, bool isNew )
	{
		Bump( state, ObjectiveType.CatchCount, null, 1 );
		Bump( state, ObjectiveType.CatchSpecies, fish.FishId, 1 );
		if ( isNew )
			Bump( state, ObjectiveType.DiscoverSpecies, fish.FishId, 1 );
	}

	public void OnGoldEarned( GameState state, int amount )
	{
		if ( amount > 0 )
			Bump( state, ObjectiveType.EarnGold, null, amount );
	}

	public void OnDistance( GameState state )
	{
		Bump( state, ObjectiveType.ReachDistance, null, (int)state.FarthestDistanceM, absolute: true );
	}

	public void OnItemBought( GameState state, string itemId )
	{
		Bump( state, ObjectiveType.BuyItem, itemId, 1 );
	}

	public void OnHired( GameState state, string hiredId )
	{
		Bump( state, ObjectiveType.HireBoat, hiredId, 1 );
	}

	private void Bump( GameState state, ObjectiveType type, string targetId, int amount, bool absolute = false )
	{
		if ( string.IsNullOrEmpty( state.ActiveObjectiveId ) ) return;
		var obj = _content.Objectives.FirstOrDefault( o => o.Id == state.ActiveObjectiveId );
		if ( obj == null || obj.Type != type ) return;
		if ( !string.IsNullOrEmpty( obj.TargetId ) && obj.TargetId != targetId && type != ObjectiveType.ReachDistance )
			return;

		if ( absolute )
			state.ActiveObjectiveProgress = Math.Max( state.ActiveObjectiveProgress, amount );
		else
			state.ActiveObjectiveProgress += amount;

		var done = state.ActiveObjectiveProgress >= obj.TargetCount;
		_events.Add( new ObjectiveUpdatedEvent
		{
			ObjectiveId = obj.Id,
			Progress = Math.Min( state.ActiveObjectiveProgress, obj.TargetCount ),
			Target = obj.TargetCount,
			Completed = done
		} );

		if ( !done ) return;

		state.CompletedObjectiveIds.Add( obj.Id );
		if ( obj.RewardGold > 0 )
		{
			state.Gold += obj.RewardGold;
			state.TotalGoldEarned += obj.RewardGold;
			_events.Add( new GoldChangedEvent { Gold = state.Gold, Delta = obj.RewardGold } );
		}

		if ( !string.IsNullOrEmpty( obj.RewardItemId ) )
			state.AddItem( obj.RewardItemId, 1 );

		if ( !string.IsNullOrEmpty( obj.UnlockZoneId ) )
			UnlockZone( state, obj.UnlockZoneId );

		state.ActiveObjectiveId = null;
		state.ActiveObjectiveProgress = 0;
		EnsureActiveObjective( state );

		if ( state.UnlockedZoneIds.Contains( "trench" ) && state.FishLog.Count >= 28 )
		{
			state.EndingReached = true;
			_events.Add( new NotificationEvent { Text = "You reached the Midnight Trench. True angler!" } );
		}
	}

	public void UnlockZone( GameState state, string zoneId )
	{
		if ( state.UnlockedZoneIds.Contains( zoneId ) ) return;
		state.UnlockedZoneIds.Add( zoneId );
		_events.Add( new ZoneUnlockedEvent { ZoneId = zoneId } );
		_events.Add( new NotificationEvent { Text = $"New waters unlocked: {_content.GetZone( zoneId ).Name}" } );
	}

	public void CheckDistanceUnlocks( GameState state )
	{
		foreach ( var zone in _content.Zones.OrderBy( z => z.UnlockOrder ) )
		{
			if ( state.UnlockedZoneIds.Contains( zone.Id ) ) continue;
			var boat = _content.GetBoat( state.OwnedBoatId );
			if ( boat.Tier >= zone.RequiredBoatTier && boat.MaxRangeM >= zone.RequiredRangeM && state.FarthestDistanceM >= zone.MinDistanceM * 0.9f )
				UnlockZone( state, zone.Id );
		}
	}
}
