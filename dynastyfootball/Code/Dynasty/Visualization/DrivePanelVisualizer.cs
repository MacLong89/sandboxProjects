using Dynasty.Core.Enums;
using Dynasty.Domain.Simulation;

namespace Dynasty.Visualization;

/// <summary>
/// Option B: drive visualization panel with field position tracking.
/// </summary>
public sealed class DrivePanelVisualizer : IGameVisualizer
{
	private IReadOnlyList<SimEventRecord> _events;
	private int _index;
	private bool _playing;

	public GameVisualizationMode Mode => GameVisualizationMode.DrivePanel;
	public bool IsPlaying => _playing;
	public int CurrentEventIndex => _index;
	public int EventCount => _events?.Count ?? 0;

	public event Action<SimEventRecord> OnEventDisplayed;

	public void LoadReplay( IReadOnlyList<SimEventRecord> events )
	{
		_events = events;
		_index = 0;
		_playing = false;
	}

	public void Play() => _playing = true;
	public void Pause() => _playing = false;
	public void Stop() { _playing = false; _index = 0; }
	public void SetPlaybackSpeed( float speed ) { }

	public SimEventRecord Tick()
	{
		if ( _events == null || _index >= _events.Count )
			return null;

		var ev = _events[_index++];
		OnEventDisplayed?.Invoke( ev );
		return ev;
	}
}
