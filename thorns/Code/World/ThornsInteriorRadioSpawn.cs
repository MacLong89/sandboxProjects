namespace Sandbox;

/// <summary>Radio prop for procedural building interiors (5th storey when ≥5 stories); hosts a <see cref="ThornsRadioStation"/> for the radio shop.</summary>
public static class ThornsInteriorRadioSpawn
{
	/// <summary>Lift above floor walk height — radio mesh pivot sits low on <c>models/placeables/radio.vmdl</c>.</summary>
	public const float InteriorRadioSpawnUpOffset = 30f;

	public static void TrySpawnInteriorStation( Scene scene, Vector3 worldPosition, Rotation worldRotation ) =>
		TrySpawnInMetalBuilding( scene, worldPosition, worldRotation );

	/// <summary>Legacy name — prefer <see cref="TrySpawnInteriorStation"/>.</summary>
	public static void TrySpawnInMetalBuilding( Scene scene, Vector3 worldPosition, Rotation worldRotation )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var go = new GameObject( true, "ThornsInteriorRadio" );
		go.WorldPosition = worldPosition + worldRotation * Vector3.Up * InteriorRadioSpawnUpOffset;
		go.WorldRotation = worldRotation;
		go.Tags.Add( "thorns_interior_radio" );

		var vis = new GameObject( true, "RadioVisual" );
		vis.SetParent( go );
		var visScale = ThornsBuildingVisuals.RadioPlaceableLocalScale;
		vis.LocalScale = visScale;
		vis.LocalRotation = Rotation.Identity;
		vis.LocalPosition = Vector3.Up * ThornsBuildingVisuals.PlaceableKitVisualLocalUpOffset( visScale );

		var mr = vis.Components.Create<ModelRenderer>();
		var mdl = ThornsBuildingVisuals.PlaceableRadioWorldModel();
		mr.Model = mdl;
		mr.Tint = Color.White;
		ThornsModelMaterialUvScale.ApplyForScaledModel(
			mr,
			vis,
			mdl,
			"models/placeables/radio.vmdl",
			ThornsBuildingVisuals.PlaceableRadioBaseColorMaterial() );

		var hullUniform = MathF.Max( visScale.x, MathF.Max( visScale.y, visScale.z ) );
		ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysics( go, mdl, hullExtentScale: hullUniform );

		// Shop anchor — same interaction model as world radio masts (<see cref="ThornsRadioShopInteractor"/>).
		var station = go.Components.Create<ThornsRadioStation>();
		station.InteractionRadius = ThornsBuildingVisuals.PlaceableInteractionUseRange;

		if ( Networking.IsActive && Networking.IsHost )
		{
			var opts = new NetworkSpawnOptions
			{
				Owner = Connection.Host,
				OrphanedMode = NetworkOrphaned.Host
			};

			ThornsNetworkReplication.SetSubtreeNetworkModeObject( go );
			if ( !go.NetworkSpawn( opts ) )
				Log.Warning( "[Thorns] Interior radio NetworkSpawn failed." );
		}
	}
}
