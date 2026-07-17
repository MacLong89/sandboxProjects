namespace DeepDive;

/// <summary>Mid-dive diving bell — refill oxygen and bank haul value safely.</summary>
public sealed class DivingBell : Component
{
	public string CheckpointId { get; private set; }
	public float InteractRadius { get; set; } = 4.5f;
	public float DepthMeters { get; private set; }

	private bool _activatedThisDive;
	private TimeUntil _hintReady;

	public void Setup( string id, float depthMeters )
	{
		CheckpointId = id;
		DepthMeters = depthMeters;
		_hintReady = 0f;

		var go = new GameObject( GameObject, true, "BellVisual" );
		go.LocalScale = MeshPrimitives.BoxScale( new Vector3( 3.2f, 0.6f, 4.5f ) );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = new Color( 0.35f, 0.55f, 0.7f );
	}

	public bool TryActivate( DeepDiveGame game )
	{
		if ( game is null || !game.State.IsDivingActive )
			return false;

		var diver = game.Diver;
		if ( diver is null ) return false;
		if ( (diver.WorldPosition - WorldPosition).Length > InteractRadius )
			return false;

		game.Oxygen?.ResetToFull();
		game.Health?.ResetToFull( game.Balance.MaxHealth );

		var banked = 0f;
		if ( game.Run?.Haul.ItemCount > 0 )
		{
			banked = game.Run.Haul.TotalValue;
			game.Run.BankHaulValue( banked );
			game.Run.Haul.Clear();
		}

		_activatedThisDive = true;
		game.Progression.DiscoverCheckpoint( CheckpointId );

		var msg = banked > 0.5f
			? $"Bell secure — banked ${banked:0} · O₂ refilled"
			: "Diving bell — O₂ refilled";
		game.ShowMessage( msg, 1.8f );
		game.DiveLog?.AddEntry( $"Checkpoint {CheckpointId}", "/ui/icons/map_objective.png", known: true );
		return true;
	}

	protected override void OnUpdate()
	{
		var game = DeepDiveGame.Instance;
		if ( game is null || !game.State.IsDivingActive ) return;
		var diver = game.Diver;
		if ( diver is null ) return;
		if ( (diver.WorldPosition - WorldPosition).Length > InteractRadius + 2f ) return;

		if ( !_activatedThisDive && _hintReady )
		{
			_hintReady = 2.5f;
			game.ShowMessage( "E — use diving bell", 0.9f );
		}
	}
}
