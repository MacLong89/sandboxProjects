namespace Terraingen.Progression;

using Terraingen.Player;

/// <summary>Suppresses ambient wildlife spawns near freshly joined players.</summary>
public static class ThornsNewPlayerWildlifeGrace
{
	public const float GraceSeconds = 120f;

	sealed class GraceEntry
	{
		public GameObject PlayerRoot;
		public double ReadyAt;
	}

	static readonly Dictionary<Guid, GraceEntry> Entries = new( 8 );

	public static void HostRegisterPlayerReady( ThornsPlayerGameplay gameplay )
	{
		if ( gameplay is null || !gameplay.GameObject.IsValid() )
			return;

		Entries[gameplay.GameObject.Id] = new GraceEntry
		{
			PlayerRoot = gameplay.GameObject,
			ReadyAt = Time.Now
		};
	}

	public static bool IsWithinGrace( GameObject playerRoot )
	{
		if ( playerRoot is null || !playerRoot.IsValid() )
			return false;

		if ( !Entries.TryGetValue( playerRoot.Id, out var entry ) )
			return false;

		return Time.Now - entry.ReadyAt < GraceSeconds;
	}

	public static bool ShouldBlockSpawnNear( Vector3 anchor, float blockRadiusInches )
	{
		var radiusSq = blockRadiusInches * blockRadiusInches;
		foreach ( var entry in Entries.Values )
		{
			if ( Time.Now - entry.ReadyAt >= GraceSeconds )
				continue;

			if ( !entry.PlayerRoot.IsValid() )
				continue;

			var delta = anchor - entry.PlayerRoot.WorldPosition;
			delta.z = 0f;
			if ( delta.LengthSquared <= radiusSq )
				return true;
		}

		return false;
	}

	public static void HostPruneExpired()
	{
		if ( Entries.Count == 0 )
			return;

		List<Guid> stale = null;
		foreach ( var pair in Entries )
		{
			if ( Time.Now - pair.Value.ReadyAt >= GraceSeconds || !pair.Value.PlayerRoot.IsValid() )
				(stale ??= new List<Guid>( 4 )).Add( pair.Key );
		}

		if ( stale is null )
			return;

		foreach ( var id in stale )
			Entries.Remove( id );
	}
}
