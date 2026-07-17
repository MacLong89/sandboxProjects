namespace DeepDive;

/// <summary>
/// Temporary per-dive stats. Reset at the start of every dive.
/// </summary>
public sealed class DiveRunState
{
	public HaulInventory Haul { get; } = new();

	public float DiveStartTime { get; private set; }
	public float DiveDurationSeconds { get; private set; }
	public float CurrentDepthMeters { get; private set; }
	public float MaxDepthMeters { get; private set; }
	public float OxygenUsedSeconds { get; private set; }
	public float DamageTaken { get; private set; }
	public float PhotoBonusValue { get; private set; }
	public float BankedHaulValue { get; private set; }
	public int VehicleCargoBonus { get; private set; }
	public bool ObjectiveRewardPending { get; set; }
	public bool DiveSuccess { get; private set; }
	public bool DiveFailed { get; private set; }
	public DiveFailureReason FailureReason { get; private set; }
	public bool BrokeDepthRecord { get; private set; }
	public bool Active { get; private set; }

	private readonly HashSet<string> _photographed = new();
	private readonly HashSet<string> _scannedLoot = new();

	public void Begin( int haulCapacity )
	{
		DiveStartTime = Time.Now;
		DiveDurationSeconds = 0f;
		CurrentDepthMeters = 0f;
		MaxDepthMeters = 0f;
		OxygenUsedSeconds = 0f;
		DamageTaken = 0f;
		PhotoBonusValue = 0f;
		BankedHaulValue = 0f;
		VehicleCargoBonus = 0;
		ObjectiveRewardPending = false;
		DiveSuccess = false;
		DiveFailed = false;
		FailureReason = DiveFailureReason.None;
		BrokeDepthRecord = false;
		Active = true;
		_photographed.Clear();
		_scannedLoot.Clear();
		Haul.Reset( haulCapacity );
	}

	public bool TryMarkPhotographed( string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) ) return false;
		return _photographed.Add( id );
	}

	public bool IsScanned( string id ) =>
		!string.IsNullOrWhiteSpace( id ) && _scannedLoot.Contains( id );

	public void MarkScanned( string id )
	{
		if ( !string.IsNullOrWhiteSpace( id ) )
			_scannedLoot.Add( id );
	}

	public void BankHaulValue( float value )
	{
		if ( !Active || value <= 0f ) return;
		BankedHaulValue += value;
	}

	public void SetVehicleCargoBonus( int bonus )
	{
		VehicleCargoBonus = Math.Max( 0, bonus );
		Haul.SetCapacityBonus( VehicleCargoBonus );
	}

	public void Tick( float dt )
	{
		if ( !Active ) return;
		DiveDurationSeconds += dt;
	}

	public void SetDepth( float depthMeters )
	{
		if ( !Active ) return;
		CurrentDepthMeters = MathF.Max( 0f, depthMeters );
		MaxDepthMeters = MathF.Max( MaxDepthMeters, CurrentDepthMeters );
	}

	public void AddOxygenUsed( float seconds )
	{
		if ( !Active || seconds <= 0f ) return;
		OxygenUsedSeconds += seconds;
	}

	public void AddDamage( float amount )
	{
		if ( !Active || amount <= 0f ) return;
		DamageTaken += amount;
	}

	public void AddPhotoBonus( float amount )
	{
		if ( !Active || amount <= 0f ) return;
		PhotoBonusValue += amount;
	}

	public void MarkRecordBroken() => BrokeDepthRecord = true;

	public void CompleteSuccess()
	{
		if ( !Active ) return;
		Active = false;
		DiveSuccess = true;
		DiveFailed = false;
		FailureReason = DiveFailureReason.None;
	}

	public void CompleteFailure( DiveFailureReason reason )
	{
		if ( !Active ) return;
		Active = false;
		DiveSuccess = false;
		DiveFailed = true;
		FailureReason = reason == DiveFailureReason.None ? DiveFailureReason.Unknown : reason;
	}

	public void Clear()
	{
		Active = false;
		DiveSuccess = false;
		DiveFailed = false;
		FailureReason = DiveFailureReason.None;
		BrokeDepthRecord = false;
		CurrentDepthMeters = 0f;
		MaxDepthMeters = 0f;
		OxygenUsedSeconds = 0f;
		DamageTaken = 0f;
		PhotoBonusValue = 0f;
		BankedHaulValue = 0f;
		VehicleCargoBonus = 0;
		ObjectiveRewardPending = false;
		DiveDurationSeconds = 0f;
		_photographed.Clear();
		_scannedLoot.Clear();
		Haul.Clear();
	}
}
