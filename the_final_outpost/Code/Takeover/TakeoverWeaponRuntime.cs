namespace FinalOutpost;

/// <summary>Per-weapon ammo, reload, and fire cooldown (aimbox-style).</summary>
public sealed class TakeoverWeaponRuntime
{
	public TakeoverWeaponDef Definition { get; }
	public int Ammo { get; private set; }
	public int Reserve { get; private set; }
	public bool IsReloading { get; private set; }
	public float ReloadProgress { get; private set; }

	private float _cooldown;
	private float _shellTimer;

	public TakeoverWeaponRuntime( TakeoverWeaponDef def )
	{
		Definition = def ?? throw new ArgumentNullException( nameof( def ) );
		Ammo = def.MagazineSize;
		Reserve = def.ReserveAmmo;
	}

	public void Update( float dt )
	{
		if ( _cooldown > 0f )
			_cooldown = MathF.Max( 0f, _cooldown - dt );

		if ( !IsReloading ) return;

		if ( Definition.UsesShellReload )
		{
			_shellTimer -= dt;
			if ( _shellTimer > 0f ) return;

			if ( Ammo < Definition.MagazineSize && Reserve > 0 )
			{
				Ammo++;
				Reserve--;
				_shellTimer = MathF.Min( 0.52f, Definition.ReloadSeconds / MathF.Max( 1, Definition.MagazineSize ) );
				if ( Ammo >= Definition.MagazineSize || Reserve <= 0 )
					IsReloading = false;
				return;
			}

			IsReloading = false;
			return;
		}

		ReloadProgress += dt;
		if ( ReloadProgress < Definition.ReloadSeconds ) return;

		var need = Definition.MagazineSize - Ammo;
		var take = Math.Min( need, Reserve );
		Ammo += take;
		Reserve -= take;
		IsReloading = false;
		ReloadProgress = 0f;

		if ( Reserve <= 0 && Ammo <= 0 )
			Reserve = Definition.ReserveAmmo;
	}

	public bool TryConsumeShot() => TryConsumeShot( out _ );

	public bool TryConsumeShot( out bool startedReload )
	{
		startedReload = false;
		if ( IsReloading || _cooldown > 0f ) return false;
		if ( Ammo <= 0 )
		{
			startedReload = TryStartReload();
			return false;
		}

		Ammo--;
		_cooldown = Definition.FireDelay;
		if ( Ammo <= 0 )
			startedReload = TryStartReload();
		return true;
	}

	public bool TryStartReload()
	{
		if ( IsReloading ) return false;
		if ( Ammo >= Definition.MagazineSize ) return false;
		if ( Reserve <= 0 )
		{
			Reserve = Definition.ReserveAmmo;
			if ( Reserve <= 0 ) return false;
		}

		IsReloading = true;
		ReloadProgress = 0f;
		if ( Definition.UsesShellReload )
			_shellTimer = MathF.Min( 0.52f, Definition.ReloadSeconds / MathF.Max( 1, Definition.MagazineSize ) );
		return true;
	}

	public void StartReload() => TryStartReload();

	public void CancelReload()
	{
		IsReloading = false;
		ReloadProgress = 0f;
	}
}
