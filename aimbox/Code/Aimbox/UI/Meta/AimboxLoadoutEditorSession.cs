namespace Sandbox;

/// <summary>
/// Shared loadout editing state and mutations for meta UI screens.
/// </summary>
public sealed class AimboxLoadoutEditorSession
{
	public static AimboxLoadoutEditorSession Shared { get; } = new();

	public int EditingIndex { get; private set; }
	public AimboxLoadoutData CurrentLoadout { get; private set; }
	public AimboxLoadoutSlotKind ActiveSlot { get; set; } = AimboxLoadoutSlotKind.Primary;
	public AimboxLoadoutWeaponCategory ActiveCategory { get; set; } = AimboxLoadoutWeaponCategory.AssaultRifles;
	public int ActivePerkSlot { get; set; } = 1;
	public int ActiveKillstreakSlot { get; set; } = 1;
	public bool EditMode { get; set; }

	AimboxPlayerController Player => FindPlayer();

	AimboxPlayerData PlayerData => Player?.Data;
	AimboxGame Game => AimboxGame.Instance;

	public void ResetFromPlayer()
	{
		EditingIndex = PlayerData?.ActiveLoadoutIndex ?? 0;
		RefreshLoadout();
		SyncAllLoadoutsFromValidation();
		ActiveCategory = ResolveDefaultCategory();
		ActiveSlot = AimboxLoadoutSlotKind.Primary;
	}

	public void SelectLoadoutSlot( AimboxLoadoutSlotKind slot )
	{
		ActiveSlot = slot;
		ActiveCategory = slot switch
		{
			AimboxLoadoutSlotKind.Secondary => AimboxLoadoutWeaponCategory.Secondaries,
			AimboxLoadoutSlotKind.Primary => CategoryForCurrentPrimary(),
			_ => ActiveCategory
		};
	}

	public void SelectLoadout( int index )
	{
		if ( PlayerData is null || index < 0 || index >= PlayerData.Loadouts.Count )
			return;

		EditingIndex = index;
		RefreshLoadout();
		ActiveCategory = CategoryForCurrentPrimary();
	}

	public void SelectCategory( AimboxLoadoutWeaponCategory category )
	{
		ActiveCategory = category;
		ActiveSlot = AimboxLoadoutSlotKind.Primary;
	}

	public void SetPrimary( AimboxWeaponId id )
	{
		if ( CurrentLoadout is null || PlayerData is null || !AimboxUnlockService.IsWeaponUnlocked( PlayerData, id ) )
			return;

		CurrentLoadout.PrimaryWeapon = id;
	}

	public void SetSecondary( AimboxWeaponId id )
	{
		if ( CurrentLoadout is null || PlayerData is null || !AimboxUnlockService.IsWeaponUnlocked( PlayerData, id ) )
			return;

		CurrentLoadout.SecondaryWeapon = id;
	}

	public void EquipWeapon( AimboxWeaponId id )
	{
		if ( ActiveSlot == AimboxLoadoutSlotKind.Secondary
		    || AimboxLoadoutUiHelpers.CategoryForWeapon( id ) == AimboxLoadoutWeaponCategory.Secondaries )
		{
			SetSecondary( id );
			return;
		}

		SetPrimary( id );
	}

	public void EquipGrenade( string grenadeId )
	{
		if ( CurrentLoadout is null || PlayerData is null || string.IsNullOrWhiteSpace( grenadeId ) )
			return;

		var weaponId = AimboxLoadoutUiHelpers.GrenadeWeaponId( grenadeId );
		if ( !AimboxUnlockService.IsWeaponUnlocked( PlayerData, weaponId ) )
			return;

		switch ( ActiveSlot )
		{
			case AimboxLoadoutSlotKind.Lethal when AimboxLoadoutUiHelpers.IsLethalGrenadeId( grenadeId ):
				CurrentLoadout.LethalGrenade = grenadeId.Trim();
				break;
			case AimboxLoadoutSlotKind.Tactical when AimboxLoadoutUiHelpers.IsTacticalGrenadeId( grenadeId ):
				CurrentLoadout.TacticalGrenade = grenadeId.Trim();
				break;
		}
	}

	public bool IsGrenadeUnlocked( string grenadeId ) =>
		PlayerData is not null
		&& AimboxUnlockService.IsWeaponUnlocked( PlayerData, AimboxLoadoutUiHelpers.GrenadeWeaponId( grenadeId ) );

	public bool IsGrenadeEquipped( string grenadeId )
	{
		if ( CurrentLoadout is null || string.IsNullOrWhiteSpace( grenadeId ) )
			return false;

		return ActiveSlot switch
		{
			AimboxLoadoutSlotKind.Lethal => CurrentLoadout.LethalGrenade.Equals( grenadeId.Trim(), StringComparison.OrdinalIgnoreCase ),
			AimboxLoadoutSlotKind.Tactical => CurrentLoadout.TacticalGrenade.Equals( grenadeId.Trim(), StringComparison.OrdinalIgnoreCase ),
			_ => false
		};
	}

	public void EquipPerk( AimboxPerkId id )
	{
		if ( CurrentLoadout is null || PlayerData is null || !AimboxUnlockService.IsPerkUnlocked( PlayerData, id ) )
			return;

		if ( CurrentLoadout.Perk1 == id ) { CurrentLoadout.Perk1 = AimboxPerkId.None; return; }
		if ( CurrentLoadout.Perk2 == id ) { CurrentLoadout.Perk2 = AimboxPerkId.None; return; }
		if ( CurrentLoadout.Perk3 == id ) { CurrentLoadout.Perk3 = AimboxPerkId.None; return; }

		switch ( ActivePerkSlot )
		{
			case 1: CurrentLoadout.Perk1 = id; break;
			case 2: CurrentLoadout.Perk2 = id; break;
			default: CurrentLoadout.Perk3 = id; break;
		}
	}

	public void EquipKillstreak( AimboxKillstreakId id )
	{
		if ( CurrentLoadout is null || PlayerData is null || !AimboxMw2Catalog.IsKillstreakImplemented( id ) || !AimboxUnlockService.IsKillstreakUnlocked( PlayerData, id ) )
			return;

		if ( CurrentLoadout.Killstreak1 == id ) { CurrentLoadout.Killstreak1 = AimboxKillstreakId.None; return; }
		if ( CurrentLoadout.Killstreak2 == id ) { CurrentLoadout.Killstreak2 = AimboxKillstreakId.None; return; }
		if ( CurrentLoadout.Killstreak3 == id ) { CurrentLoadout.Killstreak3 = AimboxKillstreakId.None; return; }

		switch ( ActiveKillstreakSlot )
		{
			case 1: CurrentLoadout.Killstreak1 = id; break;
			case 2: CurrentLoadout.Killstreak2 = id; break;
			default: CurrentLoadout.Killstreak3 = id; break;
		}
	}

	public void ToggleAttachment( AimboxWeaponId weapon, AimboxAttachmentId attachment )
	{
		if ( CurrentLoadout is null || PlayerData is null )
			return;

		if ( !AimboxAttachmentCatalog.IsCompatible( weapon, attachment ) )
			return;

		var weaponData = PlayerData.GetWeapon( weapon );
		if ( !AimboxUnlockService.IsAttachmentUnlocked( weapon, weaponData, attachment ) )
			return;

		if ( !CurrentLoadout.Attachments.TryGetValue( weapon, out var list ) )
		{
			list = [];
			CurrentLoadout.Attachments[weapon] = list;
		}

		if ( list.Contains( attachment ) )
		{
			list.Remove( attachment );
			PushRuntimeAttachmentsIfEquipped( weapon );
			return;
		}

		if ( AimboxAttachmentCatalog.IsSight( attachment ) )
		{
			RemoveSightAttachments( list );
		}
		else if ( AimboxAttachmentCatalog.IsForegrip( attachment ) )
		{
			RemoveForegripAttachments( list );
		}

		list.Add( attachment );
		PushRuntimeAttachmentsIfEquipped( weapon );
	}

	public void SaveLoadout()
	{
		if ( PlayerData is null || CurrentLoadout is null || Game is null )
			return;

		Game.Loadouts.SaveLoadout( PlayerData, CurrentLoadout, EditingIndex );
		Game.QueueSave( PlayerData );

		if ( Game.Phase == AimboxSessionPhase.Playing && IsEditingLoadoutEquipped() )
			Player?.Respawn();
	}

	public void EquipLoadout()
	{
		if ( PlayerData is null || CurrentLoadout is null || Game is null || IsEditingLoadoutEquipped() )
			return;

		Game.Loadouts.SaveLoadout( PlayerData, CurrentLoadout, EditingIndex );
		PlayerData.ActiveLoadoutIndex = EditingIndex;
		Game.QueueSave( PlayerData );

		if ( Game.Phase == AimboxSessionPhase.Playing )
			Player?.Respawn();
	}

	public bool IsEditingLoadoutEquipped() =>
		PlayerData is not null && EditingIndex == PlayerData.ActiveLoadoutIndex;

	public bool IsWeaponUnlocked( AimboxWeaponId id ) =>
		PlayerData is not null && AimboxUnlockService.IsWeaponUnlocked( PlayerData, id );

	public bool IsPerkUnlocked( AimboxPerkId id ) =>
		PlayerData is not null && AimboxUnlockService.IsPerkUnlocked( PlayerData, id );

	public bool IsKillstreakUnlocked( AimboxKillstreakId id ) =>
		PlayerData is not null && AimboxUnlockService.IsKillstreakUnlocked( PlayerData, id );

	public bool IsAttachmentEquipped( AimboxWeaponId weapon, AimboxAttachmentId attachment )
	{
		if ( CurrentLoadout is null || !CurrentLoadout.Attachments.TryGetValue( weapon, out var attachments ) )
			return false;

		return attachments.Contains( attachment );
	}

	public bool IsAttachmentUnlocked( AimboxWeaponId weapon, AimboxAttachmentId attachment )
	{
		if ( PlayerData is null )
			return false;

		return AimboxUnlockService.IsAttachmentUnlocked( weapon, PlayerData.GetWeapon( weapon ), attachment );
	}

	public bool IsPerkEquipped( AimboxPerkId id )
	{
		if ( CurrentLoadout is null || id == AimboxPerkId.None )
			return false;

		return CurrentLoadout.Perk1 == id || CurrentLoadout.Perk2 == id || CurrentLoadout.Perk3 == id;
	}

	public bool IsKillstreakEquipped( AimboxKillstreakId id )
	{
		if ( CurrentLoadout is null || id == AimboxKillstreakId.None )
			return false;

		return CurrentLoadout.Killstreak1 == id || CurrentLoadout.Killstreak2 == id || CurrentLoadout.Killstreak3 == id;
	}

	public IEnumerable<AimboxAttachmentId> EquippedAttachments( AimboxWeaponId weapon )
	{
		if ( CurrentLoadout is null || !CurrentLoadout.Attachments.TryGetValue( weapon, out var attachments ) )
			return [];

		return attachments;
	}

	public int BuildHash()
	{
		if ( CurrentLoadout is null )
			return HashCode.Combine( EditingIndex, ActiveSlot, ActiveCategory, ActivePerkSlot, ActiveKillstreakSlot, EditMode );

		return HashCode.Combine(
			HashCode.Combine( EditingIndex, ActiveSlot, ActiveCategory, ActivePerkSlot, ActiveKillstreakSlot, EditMode, CurrentLoadout.PrimaryWeapon, CurrentLoadout.SecondaryWeapon ),
			HashCode.Combine(
				CurrentLoadout.LethalGrenade,
				CurrentLoadout.TacticalGrenade,
				CurrentLoadout.Perk1,
				CurrentLoadout.Perk2,
				CurrentLoadout.Perk3,
				CurrentLoadout.Killstreak1,
				CurrentLoadout.Killstreak2,
				CurrentLoadout.Killstreak3 ),
			LoadoutAttachmentHash() );
	}

	int LoadoutAttachmentHash()
	{
		if ( CurrentLoadout is null )
			return 0;

		var hash = new HashCode();
		var keys = new List<AimboxWeaponId>();
		foreach ( var pair in CurrentLoadout.Attachments )
			keys.Add( pair.Key );

		keys.Sort( ( a, b ) => a.CompareTo( b ) );

		foreach ( var key in keys )
		{
			hash.Add( key );
			if ( !CurrentLoadout.Attachments.TryGetValue( key, out var attachments ) )
				continue;

			foreach ( var attachment in attachments )
				hash.Add( attachment );
		}

		return hash.ToHashCode();
	}

	AimboxPlayerController FindPlayer()
	{
		var players = AimboxGame.Instance?.Players;
		if ( players is null )
			return null;

		AimboxPlayerController fallback = null;
		foreach ( var player in players )
		{
			fallback ??= player;
			if ( !player.IsProxy )
				return player;
		}

		return fallback;
	}

	static void RemoveSightAttachments( List<AimboxAttachmentId> attachments )
	{
		for ( var i = attachments.Count - 1; i >= 0; i-- )
		{
			if ( AimboxAttachmentCatalog.IsSight( attachments[i] ) )
				attachments.RemoveAt( i );
		}
	}

	static void RemoveForegripAttachments( List<AimboxAttachmentId> attachments )
	{
		for ( var i = attachments.Count - 1; i >= 0; i-- )
		{
			if ( AimboxAttachmentCatalog.IsForegrip( attachments[i] ) )
				attachments.RemoveAt( i );
		}
	}

	void RefreshLoadout()
	{
		if ( PlayerData is null )
		{
			CurrentLoadout = null;
			return;
		}

		var source = PlayerData.Loadouts[EditingIndex];
		var validated = Game?.Loadouts.ValidateLoadout( PlayerData, source ) ?? source;
		if ( validated.PrimaryWeapon != source.PrimaryWeapon || validated.SecondaryWeapon != source.SecondaryWeapon )
			Game?.Loadouts.SaveLoadout( PlayerData, validated, EditingIndex );

		CurrentLoadout = CloneLoadout( validated );
	}

	void SyncAllLoadoutsFromValidation()
	{
		if ( PlayerData is null || Game is null )
			return;

		var changed = false;
		for ( var i = 0; i < PlayerData.Loadouts.Count; i++ )
		{
			var source = PlayerData.Loadouts[i];
			var validated = Game.Loadouts.ValidateLoadout( PlayerData, source );
			if ( validated.PrimaryWeapon == source.PrimaryWeapon
				&& validated.SecondaryWeapon == source.SecondaryWeapon
				&& validated.Perk1 == source.Perk1
				&& validated.Perk2 == source.Perk2
				&& validated.Perk3 == source.Perk3
				&& validated.Killstreak1 == source.Killstreak1
				&& validated.Killstreak2 == source.Killstreak2
				&& validated.Killstreak3 == source.Killstreak3 )
				continue;

			Game.Loadouts.SaveLoadout( PlayerData, validated, i );
			changed = true;
		}

		if ( changed )
			Game.PlayerDataService.SavePlayer( PlayerData );
	}

	static AimboxLoadoutData CloneLoadout( AimboxLoadoutData source )
	{
		var clone = new AimboxLoadoutData
		{
			Name = source.Name,
			PrimaryWeapon = source.PrimaryWeapon,
			SecondaryWeapon = source.SecondaryWeapon,
			LethalGrenade = source.LethalGrenade,
			TacticalGrenade = source.TacticalGrenade,
			Perk1 = source.Perk1,
			Perk2 = source.Perk2,
			Perk3 = source.Perk3,
			Killstreak1 = source.Killstreak1,
			Killstreak2 = source.Killstreak2,
			Killstreak3 = source.Killstreak3
		};

		foreach ( var pair in source.Attachments )
		{
			var attachments = new List<AimboxAttachmentId>();
			foreach ( var attachment in pair.Value )
				attachments.Add( attachment );

			clone.Attachments[pair.Key] = attachments;
		}

		return clone;
	}

	void PushRuntimeAttachmentsIfEquipped( AimboxWeaponId weapon )
	{
		if ( Player is null || CurrentLoadout is null )
			return;

		if ( !CurrentLoadout.Attachments.TryGetValue( weapon, out var attachments ) )
			attachments = [];

		Player.TryApplyAttachmentsToWeapon( weapon, attachments );
	}

	AimboxLoadoutWeaponCategory ResolveDefaultCategory()
	{
		if ( CurrentLoadout is null )
			return AimboxLoadoutWeaponCategory.AssaultRifles;

		return CategoryForCurrentPrimary();
	}

	AimboxLoadoutWeaponCategory CategoryForCurrentPrimary() =>
		CurrentLoadout is null
			? AimboxLoadoutWeaponCategory.AssaultRifles
			: AimboxLoadoutUiHelpers.CategoryForWeapon( CurrentLoadout.PrimaryWeapon );
}
