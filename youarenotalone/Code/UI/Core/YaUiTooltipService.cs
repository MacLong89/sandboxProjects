namespace Sandbox;

/// <summary>Single active tooltip — prevents overlap, flicker, and off-screen spawn.</summary>
public sealed class YaUiTooltipService
{
	public static YaUiTooltipService Local { get; private set; }

	string _activeText = "";
	float _showDelayRemaining;
	float _visibleSecondsRemaining;
	bool _visible;

	const float ShowDelaySeconds = 0.18f;
	const float VisibleSeconds = 4f;

	public static void SetLocal( YaUiTooltipService service ) => Local = service;

	public void Request( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
		{
			Clear();
			return;
		}

		if ( text == _activeText && _visible )
			return;

		_activeText = text.Trim();
		_showDelayRemaining = ShowDelaySeconds;
		_visible = false;
	}

	public void Clear()
	{
		_activeText = "";
		_showDelayRemaining = 0f;
		_visibleSecondsRemaining = 0f;
		_visible = false;
	}

	public void Tick( float dt, bool allowTooltips )
	{
		if ( !allowTooltips || YaUiManager.Local is { AnyModalActive: true } )
		{
			Clear();
			return;
		}

		if ( string.IsNullOrWhiteSpace( _activeText ) )
			return;

		if ( !_visible )
		{
			_showDelayRemaining -= dt;
			if ( _showDelayRemaining <= 0f )
			{
				_visible = true;
				_visibleSecondsRemaining = VisibleSeconds;
			}

			return;
		}

		_visibleSecondsRemaining -= dt;
		if ( _visibleSecondsRemaining <= 0f )
			Clear();
	}

	public bool IsVisible => _visible;
	public string ActiveText => _visible ? _activeText : "";
}
