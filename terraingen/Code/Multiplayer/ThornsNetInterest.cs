namespace Terraingen.Multiplayer;

using Sandbox.Network;

/// <summary>Radius-filtered [Rpc.Broadcast] delivery for world events.</summary>
public static class ThornsNetInterest
{
	public const float WorldEventRadius = 4500f;
	public const float WorldEventRadiusSq = WorldEventRadius * WorldEventRadius;

	public static bool ConnectionNearWorldPoint( Connection connection, Vector3 worldPosition, float radiusSq = WorldEventRadiusSq )
	{
		if ( connection is null )
			return true;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return true;

		foreach ( var session in scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() || session.OwnerConnection?.Id != connection.Id )
				continue;

			var root = session.GameObject;
			if ( !root.IsValid() )
				return true;

			var delta = root.WorldPosition.WithZ( 0f ) - worldPosition.WithZ( 0f );
			return delta.LengthSquared <= radiusSq;
		}

		return true;
	}

	/// <summary>Invoke a broadcast RPC only for connections near <paramref name="worldPosition"/>.</summary>
	public static void HostBroadcastNear( Vector3 worldPosition, Action invokeBroadcastRpc, float radiusSq = WorldEventRadiusSq )
	{
		if ( invokeBroadcastRpc is null )
			return;

		if ( !Networking.IsActive )
			return;

		using ( Rpc.FilterInclude( c => ConnectionNearWorldPoint( c, worldPosition, radiusSq ) ) )
		{
			invokeBroadcastRpc();
		}
	}
}
