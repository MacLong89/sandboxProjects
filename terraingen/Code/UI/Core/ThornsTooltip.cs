namespace Terraingen.UI.Core;

using Sandbox.UI;

/// <summary>Single-instance tooltip with edge detection, delay, and smooth positioning.</summary>
public static class ThornsTooltip
{
	const float ShowDelaySeconds = 0.35f;
	const float HideDelaySeconds = 0.05f;
	const int OffsetPx = 14;
	const int MaxWidthPx = 320;

	static Panel _host;
	static Panel _bubble;
	static Label _text;
	static Panel _currentTarget;
	static string _pendingText;
	static TimeSince _hoverSince;
	static TimeSince _leaveSince;
	static bool _visible;

	public static void EnsureHost( Panel root )
	{
		if ( _host is not null && _host.IsValid )
			return;

		if ( root is null || !root.IsValid )
			return;

		_host = ThornsUiFactory.AddPanel( root, "thorns-tooltip-host" );
		_host.Style.Position = PositionMode.Absolute;
		_host.Style.Left = Length.Pixels( 0 );
		_host.Style.Top = Length.Pixels( 0 );
		_host.Style.Width = Length.Percent( 100 );
		_host.Style.Height = Length.Percent( 100 );
		_host.Style.PointerEvents = PointerEvents.None;
		ThornsUiLayer.Apply( _host, ThornsUiPriority.Tooltip );

		_bubble = ThornsUiFactory.AddPanel( _host, "thorns-tooltip-bubble thorns-hud-glass" );
		_bubble.Style.Position = PositionMode.Absolute;
		_bubble.Style.MaxWidth = Length.Pixels( MaxWidthPx );
		_bubble.Style.Padding = Length.Pixels( 8 );
		_bubble.Style.Display = DisplayMode.None;
		_bubble.Style.Opacity = 0f;
		_bubble.Style.PointerEvents = PointerEvents.None;
		_bubble.Style.FlexShrink = 0;

		_text = ThornsUiFactory.AddPassiveLabel( _bubble, "", "thorns-muted" );
		_text.Style.WhiteSpace = WhiteSpace.Normal;
	}

	public static void Attach( Panel target, string text )
	{
		if ( target is null || !target.IsValid || string.IsNullOrWhiteSpace( text ) )
			return;

		target.SetAttribute( "data-tooltip", text );
		target.AddEventListener( "onmouseover", e => OnTargetEnter( e.Target, text ) );
		target.AddEventListener( "onmouseout", e => OnTargetLeave( e.Target ) );
	}

	public static void Tick()
	{
		if ( _host is null || !_host.IsValid )
			return;

		if ( _currentTarget is not null && !_currentTarget.IsValid )
			HideImmediate();

		if ( !_visible && _currentTarget is not null )
		{
			if ( _hoverSince >= ShowDelaySeconds )
				Show();
		}
		else if ( _visible && _currentTarget is null && _leaveSince >= HideDelaySeconds )
		{
			HideImmediate();
		}

		if ( _visible )
			Reposition();
	}

	static void OnTargetEnter( Panel target, string text )
	{
		if ( target is null || !target.IsValid )
			return;

		_currentTarget = target;
		_pendingText = text;
		_hoverSince = 0f;
		_leaveSince = 0f;

		if ( _text.IsValid )
			_text.Text = text;
	}

	static void OnTargetLeave( Panel target )
	{
		if ( _currentTarget != target )
			return;

		_currentTarget = null;
		_leaveSince = 0f;
	}

	static void Show()
	{
		if ( _bubble is null || !_bubble.IsValid )
			return;

		_visible = true;
		_bubble.Style.Display = DisplayMode.Flex;
		_bubble.Style.Opacity = 1f;
		Reposition();
	}

	static void HideImmediate()
	{
		_visible = false;
		_currentTarget = null;
		_pendingText = null;

		if ( _bubble is not null && _bubble.IsValid )
		{
			_bubble.Style.Display = DisplayMode.None;
			_bubble.Style.Opacity = 0f;
		}
	}

	static void Reposition()
	{
		if ( _bubble is null || !_bubble.IsValid || _currentTarget is null || !_currentTarget.IsValid )
			return;

		var mouse = Mouse.Position;
		var vw = ThornsHudSafeZones.ViewportWidth;
		var vh = ThornsHudSafeZones.ViewportHeight;

		var left = mouse.x + OffsetPx;
		var top = mouse.y + OffsetPx;

		// Flip when near screen edges.
		if ( left + MaxWidthPx > vw - ThornsHudSafeZones.EdgeInset )
			left = mouse.x - MaxWidthPx - OffsetPx;

		if ( top + 80 > vh - ThornsHudSafeZones.EdgeInset )
			top = mouse.y - 80 - OffsetPx;

		left = Math.Clamp( left, ThornsHudSafeZones.EdgeInset, vw - MaxWidthPx - ThornsHudSafeZones.EdgeInset );
		top = Math.Clamp( top, ThornsHudSafeZones.EdgeInset, vh - 40 - ThornsHudSafeZones.EdgeInset );

		_bubble.Style.Left = Length.Pixels( left );
		_bubble.Style.Top = Length.Pixels( top );
	}
}
