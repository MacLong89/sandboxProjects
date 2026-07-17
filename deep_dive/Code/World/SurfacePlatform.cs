namespace DeepDive;

/// <summary>Fishing boat floating at the surface — dive entry / exit point.</summary>
public sealed class SurfacePlatform : Component
{
	protected override void OnStart()
	{
		var balance = DeepDiveGame.Instance?.Balance ?? BalanceConfig.Defaults;
		WorldPosition = new Vector3( balance.SurfaceSpawnX, 0f, balance.SurfaceZ + 0.2f );
		DeepDiveSprites.SpawnBoat( GameObject );
	}
}
