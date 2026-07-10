namespace Dynasty.UI.Management;

using System.Linq;
using Dynasty.UI.ViewModels;

/// <summary>
/// Central UI orchestrator. Every open/close, layer, focus, notification, and tooltip request flows through here.
/// </summary>
public sealed class DynastyUiManager
{
	public static DynastyUiManager Instance { get; } = new();

	public const int MaxVisibleNotifications = 4;
	public const int NotificationBatchWindowMs = 1200;
	public const int TooltipShowDelayMs = 350;

	public event Action StateChanged;

	readonly Dictionary<UiWindowType, UiWindowInstance> _windows = new();
	readonly List<DynastyNotification> _notifications = new();
	readonly Dictionary<string, DynastyNotificationBatch> _notificationBatches = new();
	readonly Queue<UiRequest> _deferredRequests = new();
	readonly HashSet<string> _recentNotificationKeys = new();

	DynastyTooltipRequest _tooltip;
	UiGameplayPhase _gameplayPhase = UiGameplayPhase.FranchiseManagement;
	int _stackCounter;
	int _changeToken;

	public int ChangeToken => _changeToken;
	public UiGameplayPhase GameplayPhase => _gameplayPhase;
	public IReadOnlyList<DynastyNotification> Notifications => _notifications;
	public DynastyTooltipRequest Tooltip => _tooltip;

	public bool HasModalOpen => _windows.Values.Any( w => w.Definition.IsModal );
	public bool ShouldDimBackground => _windows.Values.Any( w => w.Definition.IsModal && w.Definition.SuppressesHud );
	public bool IsHudInputBlocked => _windows.Values.Any( w => w.Definition.IsModal && w.Definition.BlocksHudInput );
	public bool ShouldShowTooltips => _windows.Values.All( w => w.Definition.AllowsTooltips );
	public bool ShouldShowNotifications => _windows.Values.All( w => w.Definition.AllowsNotifications );

	public UiWindowInstance TopmostWindow =>
		_windows.Values
			.OrderByDescending( w => (int)w.Definition.Priority )
			.ThenByDescending( w => w.StackOrder )
			.FirstOrDefault();

	public bool IsWindowOpen( UiWindowType type ) => _windows.ContainsKey( type );

	public T GetPayload<T>( UiWindowType type ) where T : class
		=> _windows.TryGetValue( type, out var window ) ? window.Payload as T : null;

	public int GetZIndex( DynastyUiPriority priority ) => (int)priority;

	public int GetWindowZIndex( UiWindowType type )
	{
		if ( !_windows.TryGetValue( type, out var window ) )
			return GetZIndex( DynastyUiPriority.Hud );

		return GetZIndex( window.Definition.Priority ) + Math.Min( window.StackOrder, 9 );
	}

	public UiRequestResult ProcessRequest( UiRequest request )
	{
		if ( request == null )
			return UiRequestResult.Invalid;

		var result = request.Action switch
		{
			UiRequestAction.Open => OpenWindow( request ),
			UiRequestAction.Close => CloseWindow( request.Window ),
			UiRequestAction.CloseTopmost => CloseTopmost() ? UiRequestResult.Success : UiRequestResult.NotOpen,
			UiRequestAction.CloseAll => CloseAllWindows(),
			_ => UiRequestResult.Invalid
		};

		DynastyUiInputManager.Instance.UpdateFromManager( this );
		return result;
	}

	public void SetGameplayPhase( UiGameplayPhase phase )
	{
		if ( _gameplayPhase == phase )
			return;

		_gameplayPhase = phase;
		FlushDeferredRequests();
		Bump();
	}

	public bool CloseTopmost()
	{
		var top = TopmostWindow;
		if ( top == null || !top.Definition.DismissOnEscape )
			return false;

		CloseWindow( top.Type, invokeDismiss: true );
		return true;
	}

	public void EnqueueNotification( string message, bool isError = false, int durationMs = 5000, string category = null )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		if ( !ShouldShowNotifications && !isError )
			return;

		var batchKey = $"{isError}:{category ?? message}";
		if ( TryBatchNotification( batchKey, message, isError, durationMs ) )
		{
			Bump();
			return;
		}

		var dedupeKey = $"{isError}:{message}";
		if ( _recentNotificationKeys.Contains( dedupeKey ) )
			return;

		_recentNotificationKeys.Add( dedupeKey );
		_ = ClearNotificationKeyLater( dedupeKey );

		PushNotification( message, isError, durationMs );
		Bump();
	}

	public void ShowTooltip( DynastyTooltipRequest request )
	{
		if ( !ShouldShowTooltips )
		{
			HideTooltip();
			return;
		}

		if ( request == null || string.IsNullOrWhiteSpace( request.Text ) )
		{
			HideTooltip();
			return;
		}

		_tooltip = request;
		Bump();
	}

	public void HideTooltip()
	{
		if ( _tooltip == null )
			return;

		_tooltip = null;
		Bump();
	}

	public void DismissNotification( Guid id )
	{
		for ( var i = _notifications.Count - 1; i >= 0; i-- )
		{
			if ( _notifications[i].Id != id )
				continue;

			_notifications.RemoveAt( i );
			Bump();
			return;
		}
	}

	public DynastyUiInputContext GetInputContext() => DynastyUiInputManager.Instance.ActiveContext;

	UiRequestResult OpenWindow( UiRequest request )
	{
		var def = UiWindowRegistry.Get( request.Window );
		if ( def == null )
			return UiRequestResult.Invalid;

		if ( !request.Force && ShouldDeferOpen( request.Window ) )
		{
			_deferredRequests.Enqueue( request );
			return UiRequestResult.DeferredByGameplay;
		}

		if ( _windows.ContainsKey( request.Window ) )
		{
			_windows[request.Window].Payload = request.Payload;
			Bump();
			return UiRequestResult.AlreadyOpen;
		}

		CloseIncompatibleWindows( request.Window );

		_stackCounter++;
		_windows[request.Window] = new UiWindowInstance
		{
			Type = request.Window,
			Definition = def,
			Payload = request.Payload,
			StackOrder = _stackCounter
		};

		HideTooltip();
		Bump();
		return UiRequestResult.Success;
	}

	UiRequestResult CloseWindow( UiWindowType type, bool invokeDismiss = false )
	{
		if ( !_windows.TryGetValue( type, out var window ) )
			return UiRequestResult.NotOpen;

		if ( invokeDismiss )
			window.OnDismiss?.Invoke();

		_windows.Remove( type );
		Bump();
		return UiRequestResult.Success;
	}

	UiRequestResult CloseAllWindows()
	{
		if ( _windows.Count == 0 )
			return UiRequestResult.NotOpen;

		_windows.Clear();
		HideTooltip();
		Bump();
		return UiRequestResult.Success;
	}

	void CloseIncompatibleWindows( UiWindowType incoming )
	{
		var toClose = _windows.Keys.Where( open => UiCompatibility.ShouldCloseForIncoming( open, incoming ) ).ToList();
		foreach ( var type in toClose )
			CloseWindow( type, invokeDismiss: true );
	}

	bool ShouldDeferOpen( UiWindowType type )
	{
		if ( _gameplayPhase == UiGameplayPhase.Celebration && type != UiWindowType.PostGameCelebration && type != UiWindowType.DraftCeremony )
			return true;

		return false;
	}

	void FlushDeferredRequests()
	{
		while ( _deferredRequests.Count > 0 )
		{
			var req = _deferredRequests.Dequeue();
			if ( OpenWindow( req ) == UiRequestResult.DeferredByGameplay )
				break;
		}
	}

	bool TryBatchNotification( string batchKey, string message, bool isError, int durationMs )
	{
		if ( isError )
			return false;

		if ( !_notificationBatches.TryGetValue( batchKey, out var batch ) )
		{
			_notificationBatches[batchKey] = new DynastyNotificationBatch
			{
				Key = batchKey,
				FirstMessage = message,
				Count = 1,
				LastUpdatedUtc = DateTime.UtcNow,
				DurationMs = durationMs
			};
			_ = FinalizeBatchLater( batchKey );
			return false;
		}

		batch.Count++;
		batch.LastUpdatedUtc = DateTime.UtcNow;
		return true;
	}

	async Task FinalizeBatchLater( string batchKey )
	{
		await Task.Delay( NotificationBatchWindowMs );
		if ( !_notificationBatches.Remove( batchKey, out var batch ) )
			return;

		var text = batch.Count > 1 ? $"{batch.FirstMessage} (×{batch.Count})" : batch.FirstMessage;
		PushNotification( text, false, batch.DurationMs );
		Bump();
	}

	void PushNotification( string message, bool isError, int durationMs )
	{
		while ( _notifications.Count >= MaxVisibleNotifications )
			_notifications.RemoveAt( 0 );

		_notifications.Add( new DynastyNotification
		{
			Message = message,
			IsError = isError,
			DurationMs = durationMs
		} );
	}

	async Task ClearNotificationKeyLater( string key )
	{
		await Task.Delay( 1500 );
		_recentNotificationKeys.Remove( key );
	}

	void Bump()
	{
		_changeToken++;
		StateChanged?.Invoke();
	}

	sealed class DynastyNotificationBatch
	{
		public string Key { get; init; }
		public string FirstMessage { get; init; }
		public int Count { get; set; }
		public DateTime LastUpdatedUtc { get; set; }
		public int DurationMs { get; init; }
	}
}

/// <summary>
/// Convenience facade for UI requests from Razor components.
/// </summary>
public static class DynastyUi
{
	public static DynastyUiManager Manager => DynastyUiManager.Instance;

	public static UiRequestResult Request( UiRequest uiRequest ) => Manager.ProcessRequest( uiRequest );

	public static bool IsOpen( UiWindowType type ) => Manager.IsWindowOpen( type );

	public static T Payload<T>( UiWindowType type ) where T : class => Manager.GetPayload<T>( type );
}
