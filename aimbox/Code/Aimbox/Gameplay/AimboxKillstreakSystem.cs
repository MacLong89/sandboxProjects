namespace Sandbox;

public sealed class AimboxKillstreakRuntime
{
	public AimboxKillstreakId Id { get; init; }
	public int KillsRequired { get; init; }
}

public sealed class AimboxKillstreakSystem
{
	readonly Dictionary<string, List<AimboxKillstreakRuntime>> _earned = new();
	readonly Dictionary<string, TimeUntil> _activeUav = new();

	static bool HasAccountId( string accountId ) => !string.IsNullOrEmpty( accountId );

	public IReadOnlyList<AimboxKillstreakRuntime> GetEarned( string accountId )
	{
		if ( !HasAccountId( accountId ) || !_earned.TryGetValue( accountId, out var earned ) )
			return [];

		return earned;
	}

	public bool IsUavActive( string accountId ) =>
		HasAccountId( accountId ) && _activeUav.TryGetValue( accountId, out var until ) && until;

	public void ClearLifeStreaks( string accountId )
	{
		if ( !HasAccountId( accountId ) )
			return;

		_earned.Remove( accountId );
	}

	public void EvaluateKill( AimboxPlayerController player )
	{
		var loadout = AimboxGame.Instance?.Loadouts.GetActiveLoadout( player.Data );
		if ( loadout is null )
			return;

		foreach ( var id in new[] { loadout.Killstreak1, loadout.Killstreak2, loadout.Killstreak3 } )
		{
			if ( !AimboxMw2Catalog.IsKillstreakImplemented( id ) )
				continue;

			if ( !AimboxMw2Catalog.TryGetKillstreak( id, out var def ) )
				continue;

			if ( !AimboxUnlockService.IsKillstreakUnlocked( player.Data, id ) )
				continue;

			if ( player.KillStreak != def.KillThreshold )
				continue;

			if ( !_earned.TryGetValue( player.AccountId, out var list ) )
				list = [];

			if ( HasEarnedKillstreak( list, id ) )
				continue;

			list.Add( new AimboxKillstreakRuntime { Id = id, KillsRequired = def.KillThreshold } );
			_earned[player.AccountId] = list;
			player.NotifyKillstreakReady( def.Name );
		}
	}

	public bool TryActivate( AimboxPlayerController player )
	{
		if ( !_earned.TryGetValue( player.AccountId, out var list ) || list.Count == 0 )
			return false;

		var next = list[0];
		list.RemoveAt( 0 );
		if ( list.Count == 0 )
			_earned.Remove( player.AccountId );

		Activate( player, next.Id );
		return true;
	}

	void Activate( AimboxPlayerController player, AimboxKillstreakId id )
	{
		switch ( id )
		{
			case AimboxKillstreakId.Uav:
				_activeUav[player.AccountId] = 30f;
				player.NotifyKillstreakUsed( "UAV Online" );
				break;
			case AimboxKillstreakId.CarePackage:
				SpawnCarePackage( player );
				player.NotifyKillstreakUsed( "Care Package Incoming" );
				break;
			case AimboxKillstreakId.PredatorMissile:
				FirePredatorMissile( player );
				player.NotifyKillstreakUsed( "Predator Missile Away" );
				break;
			default:
				if ( AimboxMw2Catalog.TryGetKillstreak( id, out var def ) )
					player.NotifyKillstreakUsed( def.Name );
				break;
		}
	}

	void SpawnCarePackage( AimboxPlayerController player )
	{
		var unlocked = new List<AimboxWeaponId>();
		foreach ( var candidate in AimboxMw2Catalog.PrimaryWeapons )
		{
			if ( AimboxUnlockService.IsWeaponUnlocked( player.Data, candidate ) )
				unlocked.Add( candidate );
		}

		if ( unlocked.Count == 0 )
			return;

		var selectedWeapon = unlocked[Random.Shared.Next( unlocked.Count )];
		player.RefillWeapon( selectedWeapon );
	}

	static bool HasEarnedKillstreak( List<AimboxKillstreakRuntime> list, AimboxKillstreakId id )
	{
		foreach ( var earned in list )
		{
			if ( earned.Id == id )
				return true;
		}

		return false;
	}

	void FirePredatorMissile( AimboxPlayerController player )
	{
		var scene = player.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var origin = player.EyePosition + player.AimForward * 1200f + Vector3.Up * 800f;
		var end = origin + Vector3.Down * 2000f;
		var tr = scene.Trace.Ray( origin, end ).Run();
		var impact = tr.Hit ? tr.EndPosition : end;

		var damage = AimboxGame.Instance?.Damage;
		foreach ( var actor in AimboxGame.Instance.GetAllCombatActors() )
		{
			if ( actor == player || !actor.IsAlive )
				continue;

			var distance = actor.WorldPosition.Distance( impact );
			if ( distance > 220f )
				continue;

			// AUDIT FIX M1 (2026-07-13): used to call actor.TakeDamage directly, skipping
			// AimboxDamageSystem FF / aim-mode / human damage-mul rules. Keep HeGrenade weapon id.
			if ( damage is not null )
			{
				damage.ApplyDamage(
					player,
					actor,
					AimboxWeaponId.HeGrenade,
					180f,
					headshot: false,
					distance,
					allowSelfDamage: false );
			}
			else
			{
				// Fallback if game wiring is mid-teardown — prefer never taking this branch.
				actor.TakeDamage( player, AimboxWeaponId.HeGrenade, 180f, false, distance );
			}
		}
	}

	public List<IAimboxCombatActor> GetUavRevealedActors( string accountId )
	{
		var actors = new List<IAimboxCombatActor>();
		if ( !IsUavActive( accountId ) )
			return actors;

		foreach ( var actor in AimboxGame.Instance.GetAllCombatActors() )
		{
			if ( actor.IsAlive )
				actors.Add( actor );
		}

		return actors;
	}
}
