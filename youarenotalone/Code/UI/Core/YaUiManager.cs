using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Central UI authority: visibility, layering, focus, modal ownership, input context, and fade transitions.
/// No HUD panel may toggle its own Display independently — register here and drive via <see cref="Tick"/>.
/// </summary>
public sealed class YaUiManager
{
	public static YaUiManager Local { get; private set; }

	readonly Dictionary<YaUiSurfaceId, YaUiSurfaceBinding> _surfaces = new();
	readonly Dictionary<YaUiSurfaceId, float> _fadeAlpha = new();
	readonly Dictionary<YaUiSurfaceId, bool> _wasVisible = new();

	Panel _modalScrim;
	YaUiInputContext _activeInputContext = YaUiInputContext.Gameplay;
	bool _anyModalActive;

	public YaUiInputContext ActiveInputContext => _activeInputContext;
	public bool AnyModalActive => _anyModalActive;
	public YaUiPopupQueue Popups { get; } = new();

	public static void SetLocal( YaUiManager manager ) => Local = manager;

	public void SetModalScrim( Panel scrim ) => _modalScrim = scrim;

	public void Register( YaUiSurfaceBinding binding )
	{
		_surfaces[binding.Id] = binding;
		_fadeAlpha[binding.Id] = 0f;
		_wasVisible[binding.Id] = false;
	}

	public void Tick( YaUiFrameContext ctx, float dt )
	{
		var desired = new Dictionary<YaUiSurfaceId, bool>();
		foreach ( var (id, binding) in _surfaces )
		{
			var wants = binding.WantsVisible( ctx );
			if ( ctx.InCombat && binding.Request.SuppressWhenCombat && !YaUiCompatibility.IsModal( id ) )
				wants = false;

			desired[id] = wants;
		}

		ResolveConflicts( desired );

		var modalActive = false;
		var requiresMouse = ctx.RequiresMouse;
		var topInput = YaUiInputContext.Gameplay;

		foreach ( var (id, binding) in _surfaces )
		{
			var visible = desired.GetValueOrDefault( id, false );
			if ( !visible )
				continue;

			if ( YaUiCompatibility.IsModal( id ) )
			{
				modalActive = true;
				var ic = YaUiCompatibility.InputContextFor( id );
				if ( GetConflictPriority( ic ) >= GetConflictPriority( topInput ) )
					topInput = ic;
			}

			if ( binding.Request.RequiresMouse )
				requiresMouse = true;
		}

		_anyModalActive = modalActive;
		_activeInputContext = topInput;

		var suppressHud = false;
		foreach ( var (id, visible) in desired )
		{
			if ( !visible )
				continue;
			if ( YaUiCompatibility.BlocksGameplayHud( id ) )
				suppressHud = true;
		}

		if ( suppressHud )
		{
			ForceOff( desired, YaUiSurfaceId.HudCombat );
			ForceOff( desired, YaUiSurfaceId.HudTopObjective );
			ForceOff( desired, YaUiSurfaceId.HudTopLeftHints );
			ForceOff( desired, YaUiSurfaceId.HudCrosshair );
			ForceOff( desired, YaUiSurfaceId.PassiveParanoia );
			ForceOff( desired, YaUiSurfaceId.PassiveDamage );
		}

		if ( modalActive )
		{
			ForceOff( desired, YaUiSurfaceId.NotificationEventFeed );
			ForceOff( desired, YaUiSurfaceId.NotificationRoundStart );
			ForceOff( desired, YaUiSurfaceId.NotificationLobbyHint );
			ForceOff( desired, YaUiSurfaceId.NotificationFloatingStack );
			ForceOff( desired, YaUiSurfaceId.HudTopObjective );
			ForceOff( desired, YaUiSurfaceId.HudTopLeftHints );
		}

		if ( modalActive && _modalScrim != null )
		{
			_modalScrim.Style.Display = DisplayMode.Flex;
			_modalScrim.Style.ZIndex = YaUiLayerZ.ToZIndex( YaUiLayer.ModalScrim );
			_modalScrim.Style.PointerEvents = PointerEvents.None;
			_modalScrim.Style.Opacity = YaUiDesignTokens.ModalScrimOpacity;
		}
		else if ( _modalScrim != null )
		{
			_modalScrim.Style.Display = DisplayMode.None;
		}

		foreach ( var (id, binding) in _surfaces )
		{
			var visible = desired.GetValueOrDefault( id, false );
			ApplySurface( binding, visible, dt );
		}

		ctx.RequiresMouse = requiresMouse;
	}

	static int GetConflictPriority( YaUiInputContext ctx ) => ctx switch
	{
		YaUiInputContext.Gameplay => 0,
		YaUiInputContext.Scoreboard => 10,
		YaUiInputContext.Menu => 20,
		YaUiInputContext.Modal => 30,
		YaUiInputContext.Spectating => 40,
		_ => 0
	};

	static void ForceOff( Dictionary<YaUiSurfaceId, bool> desired, YaUiSurfaceId id )
	{
		if ( desired.ContainsKey( id ) )
			desired[id] = false;
	}

	void ResolveConflicts( Dictionary<YaUiSurfaceId, bool> desired )
	{
		var active = desired.Where( kv => kv.Value ).Select( kv => kv.Key ).ToList();
		for ( var i = 0; i < active.Count; i++ )
		{
			for ( var j = i + 1; j < active.Count; j++ )
			{
				var a = active[i];
				var b = active[j];
				if ( YaUiCompatibility.CanCoexist( a, b ) )
					continue;

				var pa = YaUiCompatibility.GetConflictPriority( a );
				var pb = YaUiCompatibility.GetConflictPriority( b );
				if ( pa >= pb )
					desired[b] = false;
				else
					desired[a] = false;
			}
		}
	}

	void ApplySurface( YaUiSurfaceBinding binding, bool visible, float dt )
	{
		var id = binding.Id;
		var panel = binding.Panel;
		if ( panel == null )
			return;

		var prev = _wasVisible.GetValueOrDefault( id, false );
		if ( prev != visible )
			binding.OnVisibilityChanged?.Invoke( visible );

		_wasVisible[id] = visible;

		var targetAlpha = visible ? 1f : 0f;
		var alpha = _fadeAlpha.GetValueOrDefault( id, 0f );
		var fadeSpeed = 1f / Math.Max( YaUiAnimation.FadeSeconds, 0.01f );
		alpha = visible
			? Math.Min( 1f, alpha + dt * fadeSpeed )
			: Math.Max( 0f, alpha - dt * fadeSpeed );
		_fadeAlpha[id] = alpha;

		var show = visible || alpha > 0.01f;
		panel.Style.Display = show ? DisplayMode.Flex : DisplayMode.None;
		panel.Style.ZIndex = YaUiLayerZ.ToZIndex( binding.Request.Layer );
		if ( binding.ManagesOpacity )
			panel.Style.Opacity = YaUiAnimation.EaseOut( alpha );
		panel.Style.PointerEvents = visible && YaUiCompatibility.IsModal( id )
			? PointerEvents.All
			: PointerEvents.None;
	}

	public bool IsVisible( YaUiSurfaceId id ) => _wasVisible.GetValueOrDefault( id, false );
}
