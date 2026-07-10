using Dynasty.Core.Enums;
using Dynasty.Domain.Simulation;

namespace Dynasty.Visualization;

/// <summary>
/// Drives replay timing and dispatches events to mode-specific visualizers.
/// </summary>
public sealed class GameReplayController
{
	private readonly IGameVisualizerFactory _factory;
	private IGameVisualizer _visualizer;
	private float _accumulator;
	private float _speed = 1f;
	private const float SecondsPerEvent = 0.65f;

	public GameReplayController( IGameVisualizerFactory factory ) => _factory = factory;

	public IGameVisualizer Visualizer => _visualizer;

	public void Load( GameVisualizationMode mode, IReadOnlyList<SimEventRecord> events )
	{
		_events = events;
		_visualizer = _factory.Create( mode );
		_visualizer.LoadReplay( events );
		_accumulator = 0f;
	}

	IReadOnlyList<SimEventRecord> _events;

	public void Play() => _visualizer?.Play();
	public void Pause() => _visualizer?.Pause();
	public void Stop()
	{
		_visualizer?.Stop();
		_accumulator = 0f;
	}

	public void SetPlaybackSpeed( float speed ) => _speed = Math.Max( 0.25f, speed );

	public SimEventRecord Step()
	{
		if ( _visualizer == null || _visualizer.CurrentEventIndex >= _visualizer.EventCount )
			return null;

		return _visualizer.Tick();
	}

	public SimEventRecord Update( float deltaTime )
	{
		if ( _visualizer == null || !_visualizer.IsPlaying || _visualizer.EventCount == 0 )
			return null;

		if ( _visualizer.CurrentEventIndex >= _visualizer.EventCount )
		{
			_visualizer.Pause();
			return null;
		}

		_accumulator += deltaTime * _speed;
		SimEventRecord last = null;
		while ( _accumulator >= SecondsPerEvent && _visualizer.CurrentEventIndex < _visualizer.EventCount )
		{
			_accumulator -= SecondsPerEvent;
			last = _visualizer.Tick();
		}

		return last;
	}
}
