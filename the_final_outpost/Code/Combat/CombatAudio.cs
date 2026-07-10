namespace FinalOutpost;

/// <summary>
/// Combat-only mix policy: throttles gunfire/impacts, varies pitch, and prioritizes readable accents.
/// Toggle <see cref="GameConstants.UseCombatAudioDirector"/> to revert to raw <see cref="Sfx.Play"/>.
/// </summary>
public static class CombatAudio
{
	public enum ImpactKind
	{
		Wall,
		Building,
		Core,
		BomberExplode,
		RecruitHit,
		RecruitKill
	}

	private static readonly List<double> _gunfireSlots = new();
	private static readonly List<double> _impactSlots = new();
	private static readonly List<double> _killSlots = new();
	private static readonly Dictionary<string, double> _targetCooldownUntil = new();

	private static double _lastGunfireTime;
	private static double _lastImpactTime;
	private static double _lastCoreImpactTime;

	public static void Reset()
	{
		_gunfireSlots.Clear();
		_impactSlots.Clear();
		_killSlots.Clear();
		_targetCooldownUntil.Clear();
		_lastGunfireTime = 0;
		_lastImpactTime = 0;
		_lastCoreImpactTime = 0;
	}

	public static void PlayGunfire( string path, string tag = null )
	{
		if ( !GameConstants.UseCombatAudioDirector )
		{
			Sfx.Play( path, tag );
			return;
		}

		var now = Time.Now;
		if ( now - _lastGunfireTime < GameConstants.CombatGunfireMinInterval )
			return;

		Prune( _gunfireSlots, now );
		if ( _gunfireSlots.Count >= GameConstants.CombatMaxGunfireVoices )
			return;

		var scale = GameConstants.CombatGunfireVolume
			* (path is Sfx.ShootShotgun or Sfx.Turret ? GameConstants.ShotgunVolumeScale : 1f);
		var pitch = Game.Random.Float( GameConstants.CombatGunfirePitchMin, GameConstants.CombatGunfirePitchMax );

		if ( !Sfx.TryPlay( path, scale, pitch, tag ?? "Gunfire" ) )
			return;

		_lastGunfireTime = now;
		_gunfireSlots.Add( now + GameConstants.CombatGunfireVoiceDuration );
	}

	public static void PlayImpact( ImpactKind kind, string targetKey, string tag = null )
	{
		if ( !GameConstants.UseCombatAudioDirector )
		{
			Sfx.Play( LegacyImpactSound( kind ), tag );
			return;
		}

		var now = Time.Now;
		var high = kind is ImpactKind.Core or ImpactKind.BomberExplode or ImpactKind.RecruitKill;

		if ( !high )
		{
			if ( now - _lastImpactTime < GameConstants.CombatImpactMinInterval )
				return;

			if ( !string.IsNullOrEmpty( targetKey )
			     && _targetCooldownUntil.TryGetValue( targetKey, out var until )
			     && now < until )
				return;
		}
		else if ( kind == ImpactKind.Core && now - _lastCoreImpactTime < GameConstants.CombatCoreImpactMinInterval )
		{
			return;
		}

		Prune( _impactSlots, now );
		var maxImpacts = high ? GameConstants.CombatMaxHighImpactVoices : GameConstants.CombatMaxImpactVoices;
		if ( _impactSlots.Count >= maxImpacts )
			return;

		var path = ImpactSound( kind );
		var scale = ImpactVolume( kind );
		var pitch = Game.Random.Float( GameConstants.CombatImpactPitchMin, GameConstants.CombatImpactPitchMax );

		if ( !Sfx.TryPlay( path, scale, pitch, tag ?? kind.ToString() ) )
			return;

		_lastImpactTime = now;
		if ( kind == ImpactKind.Core )
			_lastCoreImpactTime = now;

		if ( !string.IsNullOrEmpty( targetKey ) )
		{
			var cooldown = high ? GameConstants.CombatHighImpactTargetCooldown : GameConstants.CombatImpactTargetCooldown;
			_targetCooldownUntil[targetKey] = now + cooldown;
		}

		_impactSlots.Add( now + GameConstants.CombatImpactVoiceDuration );
	}

	public static void PlayZombieKill( string tag = null )
	{
		if ( !GameConstants.UseCombatAudioDirector )
			return;

		var now = Time.Now;
		Prune( _killSlots, now );
		if ( _killSlots.Count >= GameConstants.CombatMaxKillVoices )
			return;

		var pitch = Game.Random.Float( GameConstants.CombatKillPitchMin, GameConstants.CombatKillPitchMax );
		if ( !Sfx.TryPlay( Sfx.ZombieDeath, GameConstants.CombatKillVolume, pitch, tag ?? "ZombieKill" ) )
			return;

		_killSlots.Add( now + GameConstants.CombatKillVoiceDuration );
	}

	private static string ImpactSound( ImpactKind kind ) => kind switch
	{
		ImpactKind.RecruitHit => Sfx.ZombieHit,
		ImpactKind.RecruitKill => Sfx.ZombieDeath,
		_ => Sfx.WallHit
	};

	private static string LegacyImpactSound( ImpactKind kind ) => Sfx.WallHit;

	private static float ImpactVolume( ImpactKind kind ) => kind switch
	{
		ImpactKind.Core => GameConstants.CombatCoreImpactVolume,
		ImpactKind.BomberExplode => GameConstants.CombatBomberImpactVolume,
		ImpactKind.RecruitKill => GameConstants.CombatRecruitKillVolume,
		ImpactKind.RecruitHit => GameConstants.CombatRecruitHitVolume,
		ImpactKind.Building => GameConstants.CombatBuildingImpactVolume,
		ImpactKind.Wall => GameConstants.CombatWallImpactVolume,
		_ => GameConstants.CombatWallImpactVolume
	};

	private static void Prune( List<double> slots, double now )
	{
		for ( var i = slots.Count - 1; i >= 0; i-- )
		{
			if ( slots[i] <= now )
				slots.RemoveAt( i );
		}
	}
}
