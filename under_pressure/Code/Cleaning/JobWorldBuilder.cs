namespace UnderPressure;

/// <summary>
/// Shared geometry builder for a job site — world, props, decor, and cleanable panels.
/// Used by both the playable campaign and the level viewer.
/// </summary>
public static class JobWorldBuilder
{
	public static void BuildEnvironment( GameObject root, JobDef job, int jobIndex )
	{
		WorldMapBuilder.Build( root, job, jobIndex );
		BuildProps( root, job );
		BuildDecor( root, job );
	}

	/// <summary>Returns created surfaces. Callers can hook earn/resoil callbacks as needed.</summary>
	public static List<CleanableSurface> BuildPanels( GameObject root, JobDef job )
	{
		var surfaces = new List<CleanableSurface>();

		foreach ( var panel in job.Panels )
		{
			var go = new GameObject( root, true, "Panel" );
			go.WorldPosition = DepthLayers.LiftPanel( panel.Position, panel.Rotation );
			go.WorldRotation = panel.Rotation.ToRotation();

			var cleanMat = panel.Surface switch
			{
				CleanSurface.Wood => GameMaterials.Wood,
				CleanSurface.Glass => GameMaterials.Metal,
				_ => GameMaterials.Concrete,
			};

			var surface = go.Components.Create<CleanableSurface>();
			surface.Setup( panel.Width, panel.Height, panel.CellSize, panel.Clean, cleanMat, panel.Stages(),
				panel.Graffiti, panel.Secrets, panel.Shape, panel.GrimePattern );
			surfaces.Add( surface );
		}

		return surfaces;
	}

	private static void BuildProps( GameObject root, JobDef job )
	{
		foreach ( var prop in job.Props )
		{
			var go = new GameObject( root, true, "Prop" );
			go.WorldPosition = DepthLayers.LiftProp( prop.Position, prop.Rotation, prop.Size );
			go.WorldRotation = prop.Rotation.ToRotation();
			go.LocalScale = MeshPrimitives.BoxScale( prop.Size );
			var mr = go.Components.Create<ModelRenderer>();
			mr.Model = MeshPrimitives.Box;
			mr.MaterialOverride = GameMaterials.Concrete;
			mr.Tint = prop.Color;
		}
	}

	private static void BuildDecor( GameObject root, JobDef job )
	{
		foreach ( var decor in job.Decor )
			Scenery.Build( root, decor );
	}
}
