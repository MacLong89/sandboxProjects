namespace DeepDive;

/// <summary>Enterable mini-sub — faster travel, reduced O₂ drain, hull buffer, extra cargo.</summary>
public sealed class MiniSubVehicle : Component
{
	public float InteractRadius { get; set; } = 4f;
	public float HullMax { get; set; } = 80f;
	public float Hull { get; private set; } = 80f;
	public bool Occupied { get; private set; }

	private Vector3 _dockOrigin;
	private GameObject _visual;

	public void SetupAt( Vector3 worldPos )
	{
		_dockOrigin = worldPos;
		WorldPosition = worldPos;
		Hull = HullMax;
		Occupied = false;
		BuildVisual();
	}

	public bool TryToggle( DeepDiveGame game )
	{
		if ( game?.Diver is null ) return false;

		if ( Occupied )
		{
			Exit( game );
			return true;
		}

		if ( (game.Diver.WorldPosition - WorldPosition).Length > InteractRadius )
			return false;

		Enter( game );
		return true;
	}

	private void Enter( DeepDiveGame game )
	{
		Occupied = true;
		game.Diver.SetInVehicle( true );
		game.Run?.SetVehicleCargoBonus( 4 );
		game.ShowMessage( "Mini-sub boarded — E to exit", 1.5f );
		if ( _visual.IsValid() )
			_visual.Enabled = false;
	}

	private void Exit( DeepDiveGame game )
	{
		Occupied = false;
		game.Diver.SetInVehicle( false );
		game.Run?.SetVehicleCargoBonus( 0 );
		WorldPosition = game.Diver.WorldPosition.WithY( 0.25f );
		_dockOrigin = WorldPosition;
		if ( _visual.IsValid() )
			_visual.Enabled = true;
		game.ShowMessage( "Exited mini-sub", 1.2f );
	}

	public float AbsorbDamage( float amount )
	{
		if ( !Occupied || amount <= 0f ) return amount;
		var absorbed = MathF.Min( Hull, amount );
		Hull -= absorbed;
		var leftover = amount - absorbed;
		if ( Hull <= 0.01f )
		{
			var game = DeepDiveGame.Instance;
			if ( game is not null )
			{
				game.ShowMessage( "Sub hull breached!", 1.4f );
				Exit( game );
			}
		}
		return leftover;
	}

	protected override void OnUpdate()
	{
		var game = DeepDiveGame.Instance;
		if ( game is null || !game.State.IsDivingActive )
			return;

		if ( Occupied && game.Diver is not null )
		{
			WorldPosition = game.Diver.WorldPosition.WithY( 0.25f );
			return;
		}

		if ( game.Diver is not null
			&& (game.Diver.WorldPosition - WorldPosition).Length <= InteractRadius
			&& game.StatusMessageRemaining <= 0.05f )
		{
			game.ShowMessage( "E — board mini-sub", 0.85f );
		}
	}

	private void BuildVisual()
	{
		_visual = new GameObject( GameObject, true, "SubVisual" );
		_visual.LocalScale = MeshPrimitives.BoxScale( new Vector3( 4.5f, 0.7f, 2.2f ) );
		var mr = _visual.Components.Create<ModelRenderer>();
		mr.Model = MeshPrimitives.Box;
		mr.MaterialOverride = MeshPrimitives.Mat;
		mr.Tint = new Color( 0.2f, 0.55f, 0.65f );
	}
}
