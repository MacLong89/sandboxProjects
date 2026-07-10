namespace Terraingen.World;

using Terraingen.AI;
using Terraingen.Combat;
using Terraingen.Multiplayer;

/// <summary>Spawns veteran defenders around supply drops — contested loot tension.</summary>
public static class ThornsAirdropGarrison
{
	const int DefenderCount = 3;
	const float DefenderRingRadius = 240f;

	static readonly Dictionary<int, List<GameObject>> GarrisonByAirdropId = new();

	public static void HostSpawnDefenders( Scene scene, int airdropId, Vector3 anchor )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || scene is null || !scene.IsValid() || airdropId <= 0 )
			return;

		HostReleaseGarrison( airdropId );

		var roots = new List<GameObject>( DefenderCount );
		var groupId = unchecked( (int)HashCode.Combine( airdropId, 0xA1BD0F ) );
		var cfg = ThornsNpcHumanBanditSpawn.AirdropDefender( anchor );

		for ( var i = 0; i < DefenderCount; i++ )
		{
			var angle = i * (360f / DefenderCount);
			var offset = new Vector3(
				MathF.Cos( angle * MathF.PI / 180f ) * DefenderRingRadius,
				MathF.Sin( angle * MathF.PI / 180f ) * DefenderRingRadius,
				0f );
			var spawnPos = anchor + offset;
			ThornsBanditBrain.HostTryResolveSpawnClearOfBanditPeers( ref spawnPos );

			var root = ThornsNpcHumanBanditSpawn.HostSpawnM4Citizen( scene, spawnPos, Game.Random, cfg, groupId, i, DefenderCount );
			if ( !root.IsValid() )
				continue;

			if ( !root.Tags.Has( "airdrop_defender" ) )
				root.Tags.Add( "airdrop_defender" );

			roots.Add( root );
		}

		if ( roots.Count > 0 )
			GarrisonByAirdropId[airdropId] = roots;
	}

	public static void HostReleaseGarrison( int airdropId )
	{
		if ( airdropId <= 0 || !GarrisonByAirdropId.TryGetValue( airdropId, out var roots ) )
			return;

		for ( var i = 0; i < roots.Count; i++ )
		{
			var root = roots[i];
			if ( root.IsValid() )
				root.Destroy();
		}

		GarrisonByAirdropId.Remove( airdropId );
	}

	public static void HostClearAll()
	{
		foreach ( var pair in GarrisonByAirdropId )
			HostReleaseGarrison( pair.Key );

		GarrisonByAirdropId.Clear();
	}
}
