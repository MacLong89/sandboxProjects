namespace Terraingen.Player;

using Terraingen.GameData;
using Terraingen.UI;

/// <summary>Throttled vitals replication for remote player observers.</summary>
public sealed class ThornsPlayerVitalsNetwork
{
	const float SyncIntervalSeconds = 0.125f;

	ThornsVitalsSnapshotDto _lastSent = new();
	double _nextSyncTime;
	bool _dirty = true;

	public void MarkDirty() => _dirty = true;

	public void HostPush( ThornsPlayerGameplay gameplay, ThornsVitalsSnapshotDto current, bool force )
	{
		if ( gameplay is null || !gameplay.IsValid() || gameplay.IsLocalPlayer() )
			return;

		if ( !force && !_dirty && Time.Now < _nextSyncTime )
			return;

		if ( !force && VitalsEqual( _lastSent, current ) )
		{
			_nextSyncTime = Time.Now + SyncIntervalSeconds;
			return;
		}

		_lastSent = Clone( current );
		_dirty = false;
		_nextSyncTime = Time.Now + SyncIntervalSeconds;
		gameplay.RpcSyncVitalsToOwner( Json.Serialize( current ) );
	}

	public static bool VitalsEqual( ThornsVitalsSnapshotDto a, ThornsVitalsSnapshotDto b )
	{
		if ( a is null || b is null )
			return false;

		// Bars display whole numbers — ignore sub-unit churn that was republishing every frame.
		const float displayEpsilon = 0.5f;

		return MathF.Abs( a.Health - b.Health ) < displayEpsilon
		       && MathF.Abs( a.MaxHealth - b.MaxHealth ) < displayEpsilon
		       && MathF.Abs( a.Food - b.Food ) < displayEpsilon
		       && MathF.Abs( a.MaxFood - b.MaxFood ) < displayEpsilon
		       && MathF.Abs( a.Water - b.Water ) < displayEpsilon
		       && MathF.Abs( a.MaxWater - b.MaxWater ) < displayEpsilon
		       && MathF.Abs( a.Stamina - b.Stamina ) < displayEpsilon
		       && MathF.Abs( a.MaxStamina - b.MaxStamina ) < displayEpsilon
		       && a.ShowHealth == b.ShowHealth
		       && a.ShowStamina == b.ShowStamina
		       && a.ShowFood == b.ShowFood
		       && a.ShowWater == b.ShowWater
		       && a.ShowTemperature == b.ShowTemperature
		       && a.HasCampfireWarmth == b.HasCampfireWarmth;
	}

	static ThornsVitalsSnapshotDto Clone( ThornsVitalsSnapshotDto v ) => new()
	{
		Health = v.Health,
		MaxHealth = v.MaxHealth,
		Stamina = v.Stamina,
		MaxStamina = v.MaxStamina,
		Food = v.Food,
		MaxFood = v.MaxFood,
		Water = v.Water,
		MaxWater = v.MaxWater,
		TemperatureC = v.TemperatureC,
		ShowHealth = v.ShowHealth,
		ShowStamina = v.ShowStamina,
		ShowFood = v.ShowFood,
		ShowWater = v.ShowWater,
		ShowTemperature = v.ShowTemperature,
		HasCampfireWarmth = v.HasCampfireWarmth
	};
}
