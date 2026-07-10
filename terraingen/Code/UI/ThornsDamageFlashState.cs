namespace Terraingen.UI;

/// <summary>Timed full-screen tint when the local player takes damage.</summary>
public static class ThornsDamageFlashState
{
	const double FlashDuration = 0.32;
	const double RecentDamageDuration = 1.0;
	const float PeakOpacity = 0.38f;

	static bool _flashActive;
	static double _flashUntil;
	static double _recentDamageUntil;
	static int _revision;
	static bool _wasVisible;

	public static int Revision => _revision;

	public static bool IsVisible => _flashActive && Time.Now < _flashUntil;

	public static bool WasRecentlyDamaged => Time.Now < _recentDamageUntil;

	/// <summary>0–1 opacity for the red overlay (fades out over the flash duration).</summary>
	public static float OverlayOpacity
	{
		get
		{
			if ( !IsVisible )
				return 0f;

			var t = (float)((_flashUntil - Time.Now) / FlashDuration);
			return PeakOpacity * Math.Clamp( t, 0f, 1f );
		}
	}

	public static void Reset()
	{
		_flashActive = false;
		_flashUntil = 0;
		_recentDamageUntil = 0;
		_wasVisible = false;
		_revision++;
	}

	public static void Pulse()
	{
		_flashActive = true;
		_flashUntil = Time.Now + FlashDuration;
		_recentDamageUntil = Time.Now + RecentDamageDuration;
		_revision++;
	}

	public static void Tick()
	{
		if ( _flashActive && Time.Now >= _flashUntil )
		{
			_flashActive = false;
			_flashUntil = 0;
		}

		var visible = IsVisible;
		if ( _wasVisible != visible )
		{
			_wasVisible = visible;
			_revision++;
		}
	}
}
