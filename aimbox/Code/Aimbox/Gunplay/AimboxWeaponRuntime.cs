namespace Sandbox;

/// <summary>Runtime ammo/reload state for one weapon definition + attachment set.</summary>
public sealed class AimboxWeaponRuntime
{
	public AimboxWeaponDefinition Definition { get; }
	public IReadOnlyCollection<AimboxAttachmentId> Attachments { get; }

	readonly HashSet<AimboxAttachmentId> _attachmentSet;

	public int Ammo { get; private set; }
	public int Reserve { get; private set; }
	public bool IsReloading { get; private set; }
	public bool ReloadStartedThisTick { get; private set; }
	public int AmmoBeforeShellReload { get; private set; }
	public bool UsesPerShellReload => Definition.Id == AimboxWeaponId.SpaghelliM4;

	float _cooldown;
	float _reloadRemaining;

	static bool HasInfiniteAmmo =>
		AimboxAimModeRules.IsAimMode( AimboxGame.Instance?.Match.Mode ?? default );

	public float PerkReloadMultiplier { get; set; } = 1f;
	public float PerkPresentationSpeedMultiplier { get; set; } = 1f;

	public bool IsBowWeapon => Definition.IsBow;

	public AimboxWeaponRuntime( AimboxWeaponDefinition definition, IReadOnlyList<AimboxAttachmentId> attachments )
	{
		Definition = definition;
		Attachments = attachments ?? [];
		_attachmentSet = new HashSet<AimboxAttachmentId>( Attachments );
		if ( Definition.IsBow )
		{
			Ammo = 0;
			Reserve = definition.ReserveAmmo;
		}
		else
		{
			Ammo = EffectiveMagazineSize;
			Reserve = definition.ReserveAmmo;
		}
	}

	public int EffectiveMagazineSize =>
		Math.Max( 1, (int)MathF.Round( Definition.MagazineSize * AimboxAttachmentModifiers.MagazineSizeMultiplier( _attachmentSet ) ) );

	public float EffectiveReloadSeconds =>
		Definition.ReloadSeconds * PerkReloadMultiplier;

	public float EffectiveShellReloadSeconds =>
		UsesPerShellReload ? Math.Min( 0.52f, EffectiveReloadSeconds ) : EffectiveReloadSeconds;

	public float EffectiveSpread =>
		Definition.SpreadDegrees * AimboxAttachmentModifiers.SpreadMultiplier( _attachmentSet );

	public float EffectiveRecoilKickMultiplier => AimboxAttachmentModifiers.RecoilKickMultiplier( _attachmentSet );

	public float EffectiveAdsPresentationSpeedMultiplier =>
		AimboxAttachmentModifiers.AdsPresentationSpeedMultiplier( _attachmentSet );

	public bool HasSuppressor => AimboxAttachmentModifiers.HasSuppressor( _attachmentSet );

	public float NoiseLoudnessMultiplier => AimboxAttachmentModifiers.NoiseLoudnessMultiplier( _attachmentSet );

	public void Update( float delta )
	{
		ReloadStartedThisTick = false;
		_cooldown = MathF.Max( 0f, _cooldown - delta );

		if ( HasInfiniteAmmo )
		{
			IsReloading = false;
			_reloadRemaining = 0f;
			return;
		}

		if ( !IsReloading )
			return;

		_reloadRemaining -= delta;
		if ( _reloadRemaining > 0f )
			return;

		if ( UsesPerShellReload )
			CompleteShellReloadCycle();
		else
			CompleteBulkReload();
	}

	void CompleteBulkReload()
	{
		var needed = EffectiveMagazineSize - Ammo;
		var taken = Math.Min( needed, Reserve );
		Ammo += taken;
		Reserve -= taken;
		IsReloading = false;
	}

	void CompleteShellReloadCycle()
	{
		if ( Reserve <= 0 || Ammo >= EffectiveMagazineSize )
		{
			IsReloading = false;
			return;
		}

		Ammo++;
		Reserve--;

		if ( Ammo < EffectiveMagazineSize && Reserve > 0 )
			BeginShellReloadCycle( Ammo );
		else
			IsReloading = false;
	}

	void BeginShellReloadCycle( int ammoBeforeShell )
	{
		IsReloading = true;
		ReloadStartedThisTick = true;
		AmmoBeforeShellReload = ammoBeforeShell;
		_reloadRemaining = EffectiveShellReloadSeconds;
	}

	public bool TryNockArrow()
	{
		if ( !IsBowWeapon || IsReloading || Ammo >= EffectiveMagazineSize || Reserve <= 0 )
			return false;

		Ammo++;
		Reserve--;
		return true;
	}

	public bool TryConsumeShot()
	{
		if ( IsReloading || _cooldown > 0f )
			return false;

		if ( Definition.IsMelee )
		{
			_cooldown = Definition.FireDelay;
			return true;
		}

		if ( HasInfiniteAmmo )
		{
			_cooldown = Definition.FireDelay;
			return true;
		}

		if ( Ammo <= 0 )
		{
			if ( !IsBowWeapon )
				StartReload();
			return false;
		}

		Ammo--;
		_cooldown = Definition.FireDelay;
		if ( Ammo == 0 && !IsBowWeapon )
			StartReload();
		return true;
	}

	public bool StartReload()
	{
		if ( HasInfiniteAmmo || Definition.IsMelee || IsBowWeapon || IsReloading || Ammo >= EffectiveMagazineSize || Reserve <= 0 )
			return false;

		IsReloading = true;
		ReloadStartedThisTick = true;
		AmmoBeforeShellReload = Ammo;
		_reloadRemaining = UsesPerShellReload ? EffectiveShellReloadSeconds : EffectiveReloadSeconds;
		return true;
	}

	public void RefillAmmo()
	{
		if ( Definition.IsMelee )
			return;

		Ammo = EffectiveMagazineSize;
		Reserve = Definition.ReserveAmmo;
		IsReloading = false;
		_reloadRemaining = 0f;
	}
}

/// <summary>Slot-based weapon loadout owned by the local player.</summary>
public sealed class AimboxWeaponInventory
{
	readonly List<AimboxWeaponRuntime> _slots = [];

	public IReadOnlyList<AimboxWeaponRuntime> Slots => _slots;

	public AimboxWeaponRuntime GetById( AimboxWeaponId id ) =>
		_slots.FirstOrDefault( x => x?.Definition.Id == id ) ?? _slots.FirstOrDefault();

	public void ApplyLoadout( AimboxLoadoutData loadout, AimboxPlayerData data, AimboxPerkRuntime perks )
	{
		_slots.Clear();

		var primary = AimboxUnlockService.ResolveWeapon( data, loadout.PrimaryWeapon, AimboxMw2Catalog.PrimaryWeapons );
		var secondary = AimboxUnlockService.ResolveWeapon( data, loadout.SecondaryWeapon, AimboxMw2Catalog.SecondaryWeapons );

		if ( AimboxUnlockService.ShouldEquipPrimaryInMatch( data, primary ) )
			AddWeaponSlot( primary, loadout, data, perks );

		AddWeaponSlot( secondary, loadout, data, perks );
		AddWeaponSlot( AimboxWeaponId.M9Bayonet, loadout, data, perks );
	}

	void AddWeaponSlot( AimboxWeaponId id, AimboxLoadoutData loadout, AimboxPlayerData data, AimboxPerkRuntime perks )
	{
		if ( !AimboxUnlockService.CanEquipWeaponInMatch( data, id ) )
			return;

		var attachments = AimboxUnlockService.SanitizeAttachments(
			data,
			id,
			loadout.Attachments.GetValueOrDefault( id ) ?? [] );

		var weaponData = data.GetWeapon( id );
		weaponData.EquippedAttachments = new HashSet<AimboxAttachmentId>( attachments );
		var runtime = new AimboxWeaponRuntime( AimboxWeapons.Get( id ), attachments )
		{
			PerkReloadMultiplier = perks.ReloadMultiplier,
			PerkPresentationSpeedMultiplier = perks.PresentationSpeedMultiplier
		};
		_slots.Add( runtime );
	}

	public AimboxWeaponRuntime GetSlot( int slot ) =>
		slot >= 0 && slot < _slots.Count ? _slots[slot] : null;

	public void ApplySingleWeapon( AimboxWeaponId weaponId ) =>
		ApplySingleWeapon( weaponId, [] );

	public void ApplySingleWeapon( AimboxWeaponId weaponId, IReadOnlyList<AimboxAttachmentId> attachments )
	{
		var list = attachments ?? [];
		AimboxAttachmentPipelineDebug.Reg(
			$"Inventory.ApplySingleWeapon weapon={weaponId} attachments=[{AimboxAttachmentPipelineDebug.FormatList( list )}]" );
		_slots.Clear();
		_slots.Add( new AimboxWeaponRuntime( AimboxWeapons.Get( weaponId ), list ) );
	}

	/// <summary>
	/// Host-authority mirror: add a runtime for <paramref name="weaponId"/> without clearing
	/// other slots (preserves ammo/cooldown so clients cannot reset by alternating weapon IDs).
	/// </summary>
	public AimboxWeaponRuntime GetOrAddHostMirrorSlot( AimboxWeaponId weaponId )
	{
		for ( var i = 0; i < _slots.Count; i++ )
		{
			var slot = _slots[i];
			if ( slot is not null && slot.Definition.Id == weaponId )
				return slot;
		}

		var runtime = new AimboxWeaponRuntime( AimboxWeapons.Get( weaponId ), [] );
		_slots.Add( runtime );
		AimboxAttachmentPipelineDebug.Reg(
			$"Inventory.GetOrAddHostMirrorSlot weapon={weaponId} slots={_slots.Count}" );
		return runtime;
	}

	public bool TryReplaceSlotAttachments( AimboxWeaponId weaponId, IReadOnlyList<AimboxAttachmentId> attachments )
	{
		var list = attachments ?? [];
		for ( var i = 0; i < _slots.Count; i++ )
		{
			var slot = _slots[i];
			if ( slot?.Definition.Id != weaponId )
				continue;

			AimboxAttachmentPipelineDebug.Reg(
				$"Inventory.TryReplaceSlotAttachments slot={i + 1} weapon={weaponId} attachments=[{AimboxAttachmentPipelineDebug.FormatList( list )}]" );
			_slots[i] = new AimboxWeaponRuntime( slot.Definition, list )
			{
				PerkReloadMultiplier = slot.PerkReloadMultiplier,
				PerkPresentationSpeedMultiplier = slot.PerkPresentationSpeedMultiplier
			};
			return true;
		}

		AimboxAttachmentPipelineDebug.Reg(
			$"Inventory.TryReplaceSlotAttachments weapon={weaponId} not found in {_slots.Count} slot(s)." );
		return false;
	}

	/// <summary>Temporary test kit: slot 1 M4, 2 shotgun, 3 sniper, 4 USP.</summary>
	public void ApplyDebugSandboxLoadout( AimboxPerkRuntime perks )
	{
		_slots.Clear();

		AddDebugSlot( AimboxWeaponId.M4A1,
			[AimboxAttachmentId.RaisedRedDot, AimboxAttachmentId.ForegripAngled, AimboxAttachmentId.ExtendedMag],
			perks );
		AddDebugSlot( AimboxWeaponId.SpaghelliM4, [], perks );
		AddDebugSlot( AimboxWeaponId.M700, [], perks );
		AddDebugSlot( AimboxWeaponId.Usp, [], perks );
	}

	void AddDebugSlot( AimboxWeaponId id, IReadOnlyList<AimboxAttachmentId> attachments, AimboxPerkRuntime perks )
	{
		_slots.Add( new AimboxWeaponRuntime( AimboxWeapons.Get( id ), attachments ?? [] )
		{
			PerkReloadMultiplier = perks.ReloadMultiplier,
			PerkPresentationSpeedMultiplier = perks.PresentationSpeedMultiplier
		} );
	}
}
