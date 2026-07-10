namespace Terraingen.UI.Core;

using Sandbox.UI;

/// <summary>
/// Central UI manager — registers active windows, enforces layering hierarchy,
/// manages focus/escape stack, compatibility, and prevents conflicting UI states.
/// </summary>
public static class ThornsUiManager
{
	public enum UiContext
	{
		Gameplay,
		MainMenu
	}

	sealed class UiWindowEntry
	{
		public string Id;
		public ThornsUiWindowKind Kind;
		public ThornsUiPriority Priority;
		public UiContext Context;
		public bool CapturesInput;
		public bool BlocksGameplay;
		public bool IsModal;
		public Panel Panel;
		public Action OnEscape;
		public Action OnConflictClose;
	}

	static readonly Dictionary<string, UiWindowEntry> Windows = new( StringComparer.OrdinalIgnoreCase );
	static readonly List<string> FocusStack = new();
	static UiContext _activeContext = UiContext.Gameplay;

	public static UiContext ActiveContext => _activeContext;

	public static bool HasOpenWindows => Windows.Count > 0;

	public static IEnumerable<ThornsUiWindowKind> OpenWindowKinds =>
		Windows.Values.Select( w => w.Kind );

	public static bool BlocksGameplayInput =>
		Windows.Values.Any( w => w.BlocksGameplay && w.Context == UiContext.Gameplay );

	public static bool CapturesPointerInput =>
		Windows.Values.Any( w => w.CapturesInput );

	public static bool IsModalOpen =>
		Windows.Values.Any( w => w.IsModal );

	public static bool SuppressesHud =>
		Windows.Values.Any( w => w.Context == UiContext.Gameplay && ThornsUiCompatibility.SuppressesHud( w.Kind ) );

	public static ThornsUiPriority TopPriority
	{
		get
		{
			if ( Windows.Count == 0 )
				return ThornsUiPriority.PassiveOverlay;

			return Windows.Values.Max( w => w.Priority );
		}
	}

	public static bool IsOpen( string id ) =>
		!string.IsNullOrWhiteSpace( id ) && Windows.ContainsKey( id );

	public static void SetContext( UiContext context ) => _activeContext = context;

	public static void Reset( UiContext? context = null )
	{
		Windows.Clear();
		FocusStack.Clear();
		if ( context.HasValue )
			_activeContext = context.Value;
	}

	/// <summary>Data-driven open request — checks compatibility before registering.</summary>
	public static bool Request( in ThornsUiRequest request )
	{
		if ( string.IsNullOrWhiteSpace( request.Id ) )
			return false;

		if ( !ThornsUiCompatibility.CanOpen( request.Kind ) )
		{
			CloseIncompatible( request.Kind );
			if ( !ThornsUiCompatibility.CanOpen( request.Kind ) )
				return false;
		}

		Register(
			request.Id,
			request.Priority,
			request.Panel,
			request.CapturesInput,
			request.BlocksGameplay,
			request.IsModal,
			request.OnEscape,
			request.OnConflictClose,
			request.Context,
			request.Kind );

		return true;
	}

	public static void Close( string id )
	{
		if ( !IsOpen( id ) )
			return;

		if ( Windows.TryGetValue( id, out var entry ) && entry.OnEscape is not null )
		{
			try { entry.OnEscape.Invoke(); }
			catch ( Exception e ) { Log.Warning( e, $"[Thorns UI] Close handler failed for '{id}'." ); }
			return;
		}

		Unregister( id );
	}

	/// <summary>Register or update an open UI window. Closes conflicting lower-priority windows.</summary>
	public static void Register(
		string id,
		ThornsUiPriority priority,
		Panel panel = null,
		bool capturesInput = true,
		bool blocksGameplay = true,
		bool isModal = false,
		Action onEscape = null,
		Action onConflictClose = null,
		UiContext? context = null,
		ThornsUiWindowKind kind = ThornsUiWindowKind.GameplayModal )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return;

		var ctx = context ?? _activeContext;

		// Close windows that occupy the same interaction layer at equal or lower priority.
		foreach ( var existing in Windows.Values.ToArray() )
		{
			if ( existing.Context != ctx )
				continue;

			if ( existing.Id == id )
				continue;

			if ( !ThornsUiCompatibility.CanCoexist( kind, existing.Kind ) )
			{
				try { existing.OnConflictClose?.Invoke(); }
				catch ( Exception e ) { Log.Warning( e, $"[Thorns UI] Conflict close failed for '{existing.Id}'." ); }
				Unregister( existing.Id );
				continue;
			}

			if ( existing.Priority >= priority && (existing.CapturesInput || capturesInput) )
			{
				try { existing.OnConflictClose?.Invoke(); }
				catch ( Exception e ) { Log.Warning( e, $"[Thorns UI] Conflict close failed for '{existing.Id}'." ); }
				Unregister( existing.Id );
			}
		}

		// Update existing registration (fixes stale panel reference after UI rebuild).
		if ( Windows.TryGetValue( id, out var prior ) )
		{
			if ( prior.Panel is not null && prior.Panel.IsValid )
				prior.Panel.SetClass( "thorns-ui-active", false );

			prior.Kind = kind;
			prior.Priority = priority;
			prior.CapturesInput = capturesInput;
			prior.BlocksGameplay = blocksGameplay;
			prior.IsModal = isModal;
			prior.Panel = panel ?? prior.Panel;
			prior.OnEscape = onEscape ?? prior.OnEscape;
			prior.OnConflictClose = onConflictClose ?? prior.OnConflictClose;

			if ( prior.Panel is not null && prior.Panel.IsValid )
				ApplyWindowSurface( prior.Panel, priority, isModal );

			FocusStack.Remove( id );
			FocusStack.Add( id );
			return;
		}

		Windows[id] = new UiWindowEntry
		{
			Id = id,
			Kind = kind,
			Priority = priority,
			Context = ctx,
			CapturesInput = capturesInput,
			BlocksGameplay = blocksGameplay,
			IsModal = isModal,
			Panel = panel,
			OnEscape = onEscape,
			OnConflictClose = onConflictClose
		};

		if ( panel is not null && panel.IsValid )
			ApplyWindowSurface( panel, priority, isModal );

		FocusStack.Remove( id );
		FocusStack.Add( id );
	}

	public static void Unregister( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return;

		if ( Windows.TryGetValue( id, out var entry ) && entry.Panel is not null && entry.Panel.IsValid )
			entry.Panel.SetClass( "thorns-ui-active", false );

		Windows.Remove( id );
		FocusStack.Remove( id );
	}

	/// <summary>Handle Cancel/Menu/Escape — closes only the topmost focus entry.</summary>
	public static bool TryHandleCancel( UiContext context )
	{
		for ( var i = FocusStack.Count - 1; i >= 0; i-- )
		{
			var id = FocusStack[i];
			if ( !Windows.TryGetValue( id, out var entry ) || entry.Context != context )
				continue;

			if ( entry.OnEscape is not null )
			{
				try { entry.OnEscape.Invoke(); }
				catch ( Exception e ) { Log.Warning( e, $"[Thorns UI] Escape handler failed for '{id}'." ); }
				return true;
			}

			Unregister( id );
			return true;
		}

		return false;
	}

	/// <summary>Hide or dim HUD beneath active modal/fullscreen layers.</summary>
	public static void ApplyFocusDimming( Panel hudLayer )
	{
		if ( hudLayer is null || !hudLayer.IsValid )
			return;

		var suppress = SuppressesHud || TopPriority >= ThornsUiPriority.FullscreenMenu;
		var dim = !suppress && IsModalOpen;

		hudLayer.SetClass( "thorns-ui-suppressed", suppress );
		hudLayer.SetClass( "thorns-ui-dimmed", dim );
	}

	static void CloseIncompatible( ThornsUiWindowKind incoming )
	{
		foreach ( var existing in Windows.Values.ToArray() )
		{
			if ( ThornsUiCompatibility.CanCoexist( incoming, existing.Kind ) )
				continue;

			try { existing.OnConflictClose?.Invoke(); }
			catch ( Exception e ) { Log.Warning( e, $"[Thorns UI] Auto-close failed for '{existing.Id}'." ); }

			if ( existing.OnEscape is null )
				Unregister( existing.Id );
		}
	}

	static void ApplyWindowSurface( Panel panel, ThornsUiPriority priority, bool isModal )
	{
		if ( isModal )
			ThornsUiLayer.ApplyModalSurface( panel, priority );
		else
			ThornsUiLayer.Apply( panel, priority );

		panel.SetClass( "thorns-ui-active", true );
	}

	/// <summary>Smart offset for draggable/spawned windows — cascade from previous position.</summary>
	public static (int left, int top) NextWindowPosition( string windowKind, int width, int height )
	{
		_spawnCounts.TryGetValue( windowKind ?? "default", out var count );
		_spawnCounts[windowKind ?? "default"] = count + 1;

		const int step = 28;
		const int margin = 48;
		var maxLeft = Math.Max( margin, ThornsHudSafeZones.ViewportWidth - width - margin );
		var maxTop = Math.Max( margin, ThornsHudSafeZones.ViewportHeight - height - margin );
		var left = margin + (count * step ) % Math.Max( 1, maxLeft - margin );
		var top = margin + (count * step ) % Math.Max( 1, maxTop - margin );
		return (left, top);
	}

	static readonly Dictionary<string, int> _spawnCounts = new( StringComparer.OrdinalIgnoreCase );
}
