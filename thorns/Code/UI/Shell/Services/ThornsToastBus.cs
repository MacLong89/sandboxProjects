namespace Sandbox;

/// <summary>Central toast ownership — loot, combat, level-up, economy, tame banners.</summary>
public sealed class ThornsToastBus : IThornsToastBus
{
	const int MaxToastCount = 8;

	readonly List<ThornsToastBusEntry> _entries = new();

	IThornsHudPresenter _presenter;

	public int Count => _entries.Count;

	public string LevelUpBannerTitle { get; private set; } = "";
	public string LevelUpBannerSubtitle { get; private set; } = "";
	public double LevelUpBannerExpireAt { get; private set; }

	public string TameStunBannerTitle { get; private set; } = "";
	public string TameStunBannerSubtitle { get; private set; } = "";
	public double TameStunBannerExpireAt { get; private set; }

	public void BindPresenter( IThornsHudPresenter presenter ) => _presenter = presenter;

	public static void HostPushForPawnRoot(
		GameObject pawnRoot,
		string message,
		float durationSeconds = 3.2f,
		ThornsGameplayToastKind kind = ThornsGameplayToastKind.Positive )
	{
		if ( !Networking.IsHost || pawnRoot is null || !pawnRoot.IsValid() )
			return;

		var shell = pawnRoot.Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		shell.RpcReceiveGameplayToast( message ?? "", durationSeconds, (int)kind );
	}

	public static void HostPushTameStunBannerForPawnRoot(
		GameObject pawnRoot,
		string title,
		string subtitle,
		float durationSeconds = 4.2f )
	{
		if ( !Networking.IsHost || pawnRoot is null || !pawnRoot.IsValid() )
			return;

		var shell = pawnRoot.Components.Get<ThornsGameShell>();
		if ( !shell.IsValid() )
			return;

		shell.RpcReceiveTameStunBanner( title ?? "", subtitle ?? "", durationSeconds );
	}

	public static void HostPushLootPickupToast( ThornsInventory inv, string itemId, int qty, string subtitle )
	{
		if ( !Networking.IsHost || inv is null || !inv.IsValid() || qty <= 0 || string.IsNullOrWhiteSpace( itemId ) )
			return;

		var nm = ThornsItemRegistry.TryGet( itemId, out var def ) ? def.DisplayName : itemId;
		var sub = string.IsNullOrWhiteSpace( subtitle ) ? "Loot secured." : subtitle.Trim();
		HostPushForPawnRoot( inv.GameObject, $"+{qty} {nm}\n{sub}", 3f, ThornsGameplayToastKind.Loot );
	}

	public void ReceiveFromNetwork( string message, float durationSeconds, ThornsGameplayToastKind kind ) =>
		Push( message, durationSeconds, kind );

	public void ReceiveTameStunBannerFromNetwork( string title, string subtitle, float durationSeconds ) =>
		PushTameHudBanner( title, subtitle, durationSeconds );

	public void Push( string message, float durationSeconds, ThornsGameplayToastKind kind )
	{
		if ( _presenter is not { IsLocalOwned: true } || string.IsNullOrWhiteSpace( message ) )
			return;

		if ( kind == ThornsGameplayToastKind.LevelUp )
		{
			ApplyLevelUpCenterBannerFromMessage( message.Trim(), durationSeconds );
			return;
		}

		_presenter.EnsureGameplayOverlayPanels();

		while ( _entries.Count >= MaxToastCount )
		{
			var oldest = _entries[0];
			_entries.RemoveAt( 0 );
			_presenter.OnToastRemoved( oldest );
		}

		var body = message.Trim();
		if ( kind == ThornsGameplayToastKind.Hint )
			body = ThornsInteractionPromptText.Format( body );

		var entry = new ThornsToastBusEntry( body, kind, Time.Now + Math.Max( 0.35f, durationSeconds ) );
		entry = _presenter.OnToastEnqueued( entry );
		_entries.Add( entry );
	}

	public void PushTameHudBanner( string title, string subtitle, float durationSeconds )
	{
		if ( _presenter is not { IsLocalOwned: true } )
			return;

		TameStunBannerTitle = ThornsInteractionPromptText.Format( title ?? "" );
		TameStunBannerSubtitle = ThornsInteractionPromptText.Format( subtitle ?? "" );
		TameStunBannerExpireAt = Time.Now + Math.Max( 0.5, durationSeconds );
	}

	public void TickExpire( double now, Action<ThornsToastBusEntry> onExpired )
	{
		for ( var i = _entries.Count - 1; i >= 0; i-- )
		{
			if ( now < _entries[i].ExpireAt )
				continue;

			var entry = _entries[i];
			_entries.RemoveAt( i );
			onExpired?.Invoke( entry );
			_presenter?.OnToastRemoved( entry );
		}
	}

	public void Clear( Action<ThornsToastBusEntry> onRemove )
	{
		foreach ( var entry in _entries )
			onRemove?.Invoke( entry );

		_entries.Clear();
		TameStunBannerTitle = "";
		TameStunBannerSubtitle = "";
		TameStunBannerExpireAt = 0;
		LevelUpBannerTitle = "";
		LevelUpBannerSubtitle = "";
		LevelUpBannerExpireAt = 0;
	}

	void ApplyLevelUpCenterBannerFromMessage( string message, float durationSeconds )
	{
		var normalized = message.Replace( "\r\n", "\n" );
		var idx = normalized.IndexOf( '\n' );
		if ( idx < 0 )
		{
			LevelUpBannerTitle = ThornsInteractionPromptText.Format( normalized.Trim() );
			LevelUpBannerSubtitle = "";
		}
		else
		{
			LevelUpBannerTitle = ThornsInteractionPromptText.Format( normalized[..idx].Trim() );
			LevelUpBannerSubtitle = ThornsInteractionPromptText.Format( normalized[( idx + 1 )..].Trim() );
		}

		LevelUpBannerExpireAt = Time.Now + Math.Max( 0.35f, durationSeconds );
	}

	public static string CssClassForKind( ThornsGameplayToastKind kind ) =>
		kind switch
		{
			ThornsGameplayToastKind.Combat => "thorns-toast thorns-toast--combat",
			ThornsGameplayToastKind.Loot => "thorns-toast thorns-toast--loot",
			ThornsGameplayToastKind.Hint => "thorns-toast thorns-toast--hint",
			ThornsGameplayToastKind.Economy => "thorns-toast thorns-toast--economy",
			ThornsGameplayToastKind.LevelUp => "thorns-toast thorns-toast--levelup",
			_ => "thorns-toast thorns-toast--positive"
		};
}
