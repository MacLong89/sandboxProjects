namespace RunGun;

public static class Sfx
{
	public const string Shoot = "sounds/rg_shoot.sound";
	public const string GateHit = "sounds/rg_gate_hit.sound";
	public const string GatePass = "sounds/rg_gate_pass.sound";
	public const string EnemyKill = "sounds/rg_enemy_kill.sound";
	public const string Hurt = "sounds/rg_hurt.sound";
	public const string Death = "sounds/rg_death.sound";
	public const string Purchase = "sounds/rg_purchase.sound";
	public const string Crit = "sounds/rg_crit.sound";
	public const string Overdrive = "sounds/rg_overdrive.sound";
	public const string Combo = "sounds/rg_combo.sound";
	public const string Boss = "sounds/rg_boss.sound";

	private static readonly HashSet<string> _missing = new();

	public static void Play( string path )
	{
		if ( string.IsNullOrEmpty( path ) || _missing.Contains( path ) )
			return;

		try
		{
			var handle = Sound.Play( path );
			if ( handle is null )
				_missing.Add( path );
		}
		catch
		{
			_missing.Add( path );
		}
	}
}
