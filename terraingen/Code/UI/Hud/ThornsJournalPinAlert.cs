namespace Terraingen.UI.Hud;

using Terraingen.GameData;
using Terraingen.UI.Core;

/// <summary>Pulses the survival log pin icon five times on first HUD goal and when the pinned goal changes.</summary>
public static class ThornsJournalPinAlert
{
	const int FlashCount = 5;
	const float FlashPeakDuration = 0.2f;
	const float FlashGapDuration = 0.3f;

	static string _lastSeenGoalId = "";
	static bool _active;
	static float _elapsed;

	static float CycleDuration => FlashPeakDuration + FlashGapDuration;
	static float TotalDuration => FlashCount * CycleDuration;

	public static bool IsFlashing => _active;

	/// <summary>1 = bright peak, lower = dim between flashes.</summary>
	public static float Pulse01
	{
		get
		{
			if ( !_active )
				return 0f;

			var cycleIndex = (int)(_elapsed / CycleDuration);
			if ( cycleIndex >= FlashCount )
				return 0f;

			var t = _elapsed - cycleIndex * CycleDuration;
			return t < FlashPeakDuration ? 1f : 0.18f;
		}
	}

	public static void Cancel()
	{
		_active = false;
		_elapsed = 0;
		_lastSeenGoalId = "";
	}

	/// <summary>Call when the HUD survival log goal id may have changed (e.g. after Refresh).</summary>
	public static void NotifyHudGoal( string goalId )
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return;

		if ( string.IsNullOrWhiteSpace( goalId ) )
		{
			_lastSeenGoalId = "";
			return;
		}

		if ( string.Equals( goalId, _lastSeenGoalId, StringComparison.OrdinalIgnoreCase ) )
			return;

		_lastSeenGoalId = goalId;
		StartFlash();
	}

	static void StartFlash()
	{
		if ( ThornsMenuHost.IsOpen )
			return;

		_active = true;
		_elapsed = 0;
	}

	public static void Tick( float delta )
	{
		if ( !_active )
			return;

		_elapsed += delta;

		if ( _elapsed >= TotalDuration || ThornsMenuHost.IsOpen )
			_active = false;
	}
}
