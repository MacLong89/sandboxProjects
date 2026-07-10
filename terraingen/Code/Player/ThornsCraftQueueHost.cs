namespace Terraingen.Player;

using Terraingen.GameData;

public sealed class ThornsCraftQueueEntry
{
	public string EntryId { get; set; } = Guid.NewGuid().ToString( "N" );
	public string RecipeId { get; set; } = "";
	public int QuantityRemaining { get; set; }
	public float SecondsRemaining { get; set; }
}

/// <summary>Server-side timed craft queue.</summary>
public sealed class ThornsCraftQueueHost
{
	readonly List<ThornsCraftQueueEntry> _queue = new();

	public IReadOnlyList<ThornsCraftQueueEntry> Entries => _queue;

	public void Enqueue( ThornsRecipeDefinition recipe, int quantity )
	{
		if ( recipe is null || quantity <= 0 )
			return;

		ThornsCraftQueueEntry existing = null;
		for ( var i = 0; i < _queue.Count; i++ )
		{
			if ( _queue[i].RecipeId == recipe.Id )
			{
				existing = _queue[i];
				break;
			}
		}

		if ( existing is not null )
		{
			existing.QuantityRemaining += quantity;
			return;
		}

		_queue.Add( new ThornsCraftQueueEntry
		{
			RecipeId = recipe.Id,
			QuantityRemaining = quantity,
			SecondsRemaining = recipe.CraftSeconds
		} );
	}

	public bool Cancel( string entryId )
	{
		var idx = _queue.FindIndex( e => e.EntryId == entryId );
		if ( idx < 0 )
			return false;

		_queue.RemoveAt( idx );
		return true;
	}

	/// <summary>Returns recipe id if a craft just completed.</summary>
	public string Tick( float delta, out bool queueChanged )
	{
		queueChanged = false;
		if ( _queue.Count == 0 )
			return null;

		var head = _queue[0];
		head.SecondsRemaining -= delta;
		if ( head.SecondsRemaining > 0f )
			return null;

		var recipeId = head.RecipeId;
		head.QuantityRemaining--;
		if ( head.QuantityRemaining > 0 )
			head.SecondsRemaining = ThornsDefinitionRegistry.GetRecipe( recipeId )?.CraftSeconds ?? 5f;
		else
			_queue.RemoveAt( 0 );

		queueChanged = true;
		return recipeId;
	}

	public ThornsCraftSnapshotDto ToSnapshot( ThornsCraftStationKind nearest )
	{
		return new ThornsCraftSnapshotDto
		{
			NearestStation = nearest,
			Queue = _queue.Select( e => new ThornsCraftQueueEntryDto
			{
				EntryId = e.EntryId,
				RecipeId = e.RecipeId,
				QuantityRemaining = e.QuantityRemaining,
				SecondsRemaining = e.SecondsRemaining,
				OutputItemId = ThornsDefinitionRegistry.GetRecipe( e.RecipeId )?.OutputItemId ?? ""
			} ).ToList()
		};
	}

	public void ApplySnapshot( ThornsCraftSnapshotDto dto )
	{
		_queue.Clear();
		if ( dto?.Queue is null )
			return;

		foreach ( var entry in dto.Queue )
		{
			if ( entry is null || string.IsNullOrWhiteSpace( entry.RecipeId ) || entry.QuantityRemaining <= 0 )
				continue;

			_queue.Add( new ThornsCraftQueueEntry
			{
				EntryId = string.IsNullOrWhiteSpace( entry.EntryId ) ? Guid.NewGuid().ToString( "N" ) : entry.EntryId,
				RecipeId = entry.RecipeId,
				QuantityRemaining = entry.QuantityRemaining,
				SecondsRemaining = Math.Max( 0f, entry.SecondsRemaining )
			} );
		}
	}
}
