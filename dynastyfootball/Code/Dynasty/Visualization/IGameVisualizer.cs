using Dynasty.Core.Enums;
using Dynasty.Domain.Simulation;

namespace Dynasty.Visualization;

/// <summary>
/// Visualization consumes pre-computed simulation events. Never influences outcomes.
/// </summary>
public interface IGameVisualizer
{
	GameVisualizationMode Mode { get; }
	void LoadReplay( IReadOnlyList<SimEventRecord> events );
	void Play();
	void Pause();
	void Stop();
	void SetPlaybackSpeed( float speed );
	SimEventRecord Tick();
	int EventCount { get; }
	bool IsPlaying { get; }
	int CurrentEventIndex { get; }
}

public interface IGameVisualizerFactory
{
	IGameVisualizer Create( GameVisualizationMode mode );
}
