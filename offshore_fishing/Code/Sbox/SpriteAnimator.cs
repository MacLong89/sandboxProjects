namespace Sandbox;

/// <summary>Simple sheet playback for procedural/generated animation strips.</summary>
[Title( "Sprite Animator" )]
public sealed class SpriteAnimator : Component
{
	[Property] public string SheetPath { get; set; }
	[Property] public int FrameWidth { get; set; } = 16;
	[Property] public int FrameHeight { get; set; } = 24;
	[Property] public int FrameCount { get; set; } = 4;
	[Property] public float FramesPerSecond { get; set; } = 8f;
	[Property] public bool Loop { get; set; } = true;

	private SpriteRenderer _renderer;
	private Texture _sheet;
	private float _time;
	private int _frame;

	protected override void OnStart()
	{
		_renderer = Components.Get<SpriteRenderer>();
		if ( !string.IsNullOrEmpty( SheetPath ) )
			_sheet = Texture.Load( SheetPath );
	}

	protected override void OnUpdate()
	{
		if ( _renderer == null || FrameCount <= 0 ) return;
		_time += Time.Delta * FramesPerSecond;
		if ( _time >= 1f )
		{
			_time -= 1f;
			_frame++;
			if ( _frame >= FrameCount )
				_frame = Loop ? 0 : FrameCount - 1;
		}

		// Prefer full-sheet sprite with frame index when available.
		if ( _sheet != null && _sheet.IsValid() )
		{
			_renderer.Texture = _sheet;
			_renderer.CurrentFrameIndex = _frame;
		}
	}

	public void Play( string sheetPath, int frames, float fps = 8f )
	{
		SheetPath = sheetPath;
		FrameCount = frames;
		FramesPerSecond = fps;
		_sheet = Texture.Load( sheetPath );
		_frame = 0;
		_time = 0;
	}
}
