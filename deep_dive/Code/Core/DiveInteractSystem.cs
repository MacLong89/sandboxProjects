namespace DeepDive;

/// <summary>Unified dive interactions: bells, sub, story fragments.</summary>
public sealed class DiveInteractSystem : Component
{
	protected override void OnUpdate()
	{
		var game = DeepDiveGame.Instance;
		if ( game is null || !game.State.IsDivingActive )
			return;

		if ( !Input.Pressed( "Use" ) )
			return;

		if ( game.Vehicles?.TryInteract( game ) == true )
			return;

		var diverPos = game.Diver?.WorldPosition ?? default;
		var bestBell = (DivingBell)null;
		var bestBellDist = 5f;
		foreach ( var bell in game.Checkpoints?.ActiveBells ?? Array.Empty<DivingBell>() )
		{
			if ( bell is null || !bell.IsValid() ) continue;
			var d = (bell.WorldPosition - diverPos).Length;
			if ( d < bestBellDist )
			{
				bestBellDist = d;
				bestBell = bell;
			}
		}

		if ( bestBell is not null && bestBell.TryActivate( game ) )
			return;

		var bestStory = (StoryPickup)null;
		var bestStoryDist = 4f;
		foreach ( var story in game.Stories?.ActiveStories ?? Array.Empty<StoryPickup>() )
		{
			if ( story is null || !story.IsValid() || story.Collected ) continue;
			var d = (story.WorldPosition - diverPos).Length;
			if ( d < bestStoryDist )
			{
				bestStoryDist = d;
				bestStory = story;
			}
		}

		bestStory?.TryInteract( game );
	}
}
