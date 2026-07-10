namespace Terraingen.Core;

using Terraingen.Combat;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Throttled player root / account / id lookups — avoids repeated scene scans in AI hot paths.</summary>
public static class ThornsPlayerRootCache
{
	static readonly List<GameObject> Roots = new( 16 );
	static readonly Dictionary<string, GameObject> ByAccountKey = new( 16 );
	static readonly Dictionary<Guid, GameObject> ByObjectId = new( 16 );

	static Scene _scene;
	static TimeUntil _nextRefresh;

	public static IReadOnlyList<GameObject> RootsReadOnly => Roots;

	public static void RefreshIfStale( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		if ( _scene == scene && _nextRefresh )
			return;

		Refresh( scene );
	}

	public static void Refresh( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		_nextRefresh = 0.5f;
		_scene = scene;
		Roots.Clear();
		ByAccountKey.Clear();
		ByObjectId.Clear();

		foreach ( var session in scene.GetAllComponents<ThornsPlayerSession>() )
		{
			if ( !session.IsValid() || !session.GameObject.IsValid() )
				continue;

			var root = session.GameObject;
			ByObjectId[root.Id] = root;

			if ( !string.IsNullOrEmpty( session.HostPersistenceAccountKey ) )
				ByAccountKey[session.HostPersistenceAccountKey] = root;
		}

		foreach ( var controller in scene.GetAllComponents<PlayerController>() )
		{
			if ( !controller.IsValid() || !controller.GameObject.IsValid() )
				continue;

			var root = controller.GameObject;
			if ( !Roots.Contains( root ) )
				Roots.Add( root );

			ByObjectId[root.Id] = root;

			var health = root.Components.Get<ThornsPlayerHealth>( FindMode.EnabledInSelf );
			if ( health is not null && health.IsValid() && !health.IsAlive )
				continue;

			var accountKey = ThornsPersistenceIdentity.GetStableAccountKey( root );
			if ( !string.IsNullOrEmpty( accountKey ) )
				ByAccountKey[accountKey] = root;
		}
	}

	public static GameObject TryGetByAccountKey( Scene scene, string accountKey )
	{
		if ( string.IsNullOrEmpty( accountKey ) )
			return null;

		RefreshIfStale( scene );
		return ByAccountKey.TryGetValue( accountKey, out var root ) && root.IsValid() ? root : null;
	}

	public static GameObject TryGetByObjectId( Scene scene, Guid objectId )
	{
		if ( objectId == Guid.Empty )
			return null;

		RefreshIfStale( scene );
		return ByObjectId.TryGetValue( objectId, out var root ) && root.IsValid() ? root : null;
	}
}
