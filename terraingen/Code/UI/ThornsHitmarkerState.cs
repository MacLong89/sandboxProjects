namespace Terraingen.UI;

/// <summary>Timed hit feedback for the gameplay crosshair (no damage numbers).</summary>
public static class ThornsHitmarkerState
{
	const double HitFlashDuration = 0.25;

	static bool _flashActive;
	static double _hitFlashUntil;
	static int _revision;
	static bool _wasHitFlashVisible;

	public static bool IsHitFlashVisible => _flashActive && Time.Now < _hitFlashUntil;

	/// <summary>Legacy name kept for older callers.</summary>
	public static bool IsVisible => IsHitFlashVisible;
	public static int Revision => _revision;

	/// <summary>Clears stale flash state after hotload or when gameplay session restarts.</summary>
	public static void Reset()
	{
		_flashActive = false;
		_hitFlashUntil = 0;
		_wasHitFlashVisible = false;
		_revision++;
	}

	public static void ReportHit( float damage, bool killed )
	{
		_ = damage;
		_ = killed;
		_flashActive = true;
		_hitFlashUntil = Time.Now + HitFlashDuration;
		_revision++;
	}

	public static void Pulse() => ReportHit( 0f, false );

	/// <summary>Keeps HUD panels updating when timed visibility expires.</summary>
	public static void Tick()
	{
		if ( _flashActive && Time.Now >= _hitFlashUntil )
		{
			_flashActive = false;
			_hitFlashUntil = 0;
		}

		var hitFlashVisible = IsHitFlashVisible;

		if ( _wasHitFlashVisible != hitFlashVisible )
		{
			_wasHitFlashVisible = hitFlashVisible;
			_revision++;
		}
	}
}
