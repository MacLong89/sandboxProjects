namespace Sandbox;

/// <summary>Gameplay toast enqueue API — shell/presenter renders panels.</summary>
public interface IThornsToastBus
{
	void Push( string message, float durationSeconds, ThornsGameplayToastKind kind );
	void PushTameHudBanner( string title, string subtitle, float durationSeconds );
	void TickExpire( double now, Action<ThornsToastBusEntry> onExpired );
	void Clear( Action<ThornsToastBusEntry> onRemove );
	int Count { get; }

	string LevelUpBannerTitle { get; }
	string LevelUpBannerSubtitle { get; }
	double LevelUpBannerExpireAt { get; }

	string TameStunBannerTitle { get; }
	string TameStunBannerSubtitle { get; }
	double TameStunBannerExpireAt { get; }
}

public readonly struct ThornsToastBusEntry
{
	public readonly string Message;
	public readonly ThornsGameplayToastKind Kind;
	public readonly double ExpireAt;
	public readonly object UserData;

	public ThornsToastBusEntry( string message, ThornsGameplayToastKind kind, double expireAt, object userData = null )
	{
		Message = message;
		Kind = kind;
		ExpireAt = expireAt;
		UserData = userData;
	}
}
