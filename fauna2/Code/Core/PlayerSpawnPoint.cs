namespace Fauna2;

/// <summary>Where zookeepers spawn — restored save position, else entrance, else plot center.</summary>
public static class PlayerSpawnPoint
{
	private static Vector3? _restoredPosition;

	public static void SetRestoredPosition( Vector3 position ) =>
		_restoredPosition = position;

	public static void ClearRestoredPosition() =>
		_restoredPosition = null;

	public static void ConsumeRestoredPosition() =>
		_restoredPosition = null;

	public static Vector3 GetSpawnPosition()
	{
		if ( _restoredPosition.HasValue )
			return _restoredPosition.Value.WithZ( WalkHeight );

		var entrance = PathNetwork.Entrance;
		if ( entrance.IsValid() )
			return ExitNearEntrance( entrance );

		var plots = PlotSystem.Instance;
		if ( plots.IsValid() && plots.OwnedPlots.Count > 0 )
			return PlotSystem.PlotCenter( 0, 0 ).WithZ( WalkHeight );

		return Vector3.Zero.WithZ( WalkHeight );
	}

	private static Vector3 ExitNearEntrance( PlaceableComponent entrance )
	{
		var pos = entrance.GameObject.WorldPosition;
		var path = PathNetwork.GetConnectedPaths()
			.OrderBy( p => p.GameObject.WorldPosition.Distance( pos ) )
			.FirstOrDefault();

		if ( path is not null )
		{
			var inward = (path.GameObject.WorldPosition - pos).WithZ( 0 ).Normal;
			return (pos + inward * 48f).WithZ( WalkHeight );
		}

		return (pos + entrance.GameObject.WorldRotation.Forward * 48f).WithZ( WalkHeight );
	}

	public static float WalkHeight => 2f;
}
