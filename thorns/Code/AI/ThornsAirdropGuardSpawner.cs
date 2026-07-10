namespace Sandbox;

/// <summary>Runtime airdrop perimeter guards — citizen presentation + bandit AI, wildlife XP, guard loot.</summary>
public static class ThornsAirdropGuardSpawner
{
	/// <summary>Host-only: spawn a small ring of M4 bandits around a supply drop.</summary>
	public static void HostSpawnGuardsAroundSupplyDrop( Scene scene, Vector3 dropCenterWorld, Random rnd )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( scene is null || !scene.IsValid() )
			return;

		ThornsGameManager.EnsureThornsBanditDirectorForScene( scene );

		var count = 3 + rnd.Next( 3 );
		for ( var i = 0; i < count; i++ )
		{
			var ang = (float)( rnd.NextDouble() * Math.PI * 2.0 );
			var rad = 300f + (float)rnd.NextDouble() * 220f;
			var flat = dropCenterWorld.WithZ( 0 )
			             + new Vector3( MathF.Cos( ang ) * rad, MathF.Sin( ang ) * rad, 0f );

			var start = flat + Vector3.Up * 520f;
			var tr = ThornsTraceUtility.RunRay( scene, new Ray( start, Vector3.Down ), 1400f, ThornsTraceProfile.AirdropGroundSnapDown, null );

			var spawnPos = tr.Hit
				? tr.HitPosition + tr.Normal * 2f
				: dropCenterWorld + Vector3.Up * 18f;

			var cfg = ThornsNpcHumanBanditSpawn.AirdropGuard( dropCenterWorld );
			ThornsNpcHumanBanditSpawn.HostSpawnM4Citizen( scene, spawnPos, rnd, cfg );
		}
	}
}
