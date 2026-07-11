namespace Terraingen.Player;

using Sandbox;

/// <summary>
/// Builds a walkable player when the stock <c>player controller</c> template is not mounted.
/// Keeps host/join playable in editor and unpublished builds without a bundled prefab.
/// </summary>
public static class ThornsPlayerSpawnBootstrap
{
	public static GameObject TryResolveSpawnTemplate( GameObject assignedPrefab, string prefabPath )
	{
		if ( assignedPrefab.IsValid() )
			return assignedPrefab;

		if ( string.IsNullOrWhiteSpace( prefabPath ) )
			return null;

		var prefab = GameObject.GetPrefab( prefabPath );
		return prefab.IsValid() ? prefab : null;
	}

	public static GameObject CreatePlayerRoot( string displayName )
	{
		var playerGo = new GameObject( true, displayName );
		playerGo.Tags.Add( "player" );

		_ = playerGo.Components.Create<ThornsPlayerLocomotion>();

		var viewGo = new GameObject( true, ThornsPlayerFirstPersonRig.ViewChildName );
		viewGo.SetParent( playerGo );
		viewGo.LocalPosition = new Vector3( 0f, 0f, ThornsPlayerFirstPersonRig.DefaultEyeOffsetZ );
		viewGo.LocalRotation = Rotation.Identity;
		viewGo.LocalScale = Vector3.One;
		_ = viewGo.Components.Create<CameraComponent>();

		ThornsCitizenRig.SetupCitizenBody( playerGo );

		Log.Info( "[Thorns] Code-built player hierarchy (player controller prefab not found)." );
		return playerGo;
	}

	public static GameObject SpawnPlayerRoot(
		GameObject assignedPrefab,
		string prefabPath,
		Transform transform,
		string displayName )
	{
		var template = TryResolveSpawnTemplate( assignedPrefab, prefabPath );
		if ( template.IsValid() )
			return template.Clone( transform, name: displayName );

		var root = CreatePlayerRoot( displayName );
		root.WorldTransform = transform;
		return root;
	}
}
