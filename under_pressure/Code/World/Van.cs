namespace UnderPressure;

/// <summary>
/// The player's work van, parked at the job site. It re-parks whenever a new job loads and
/// rebuilds its geometry whenever the Van Class upgrade tier changes, so buying a van tier
/// visibly swaps the vehicle in the world.
/// </summary>
public sealed class Van : Component
{
	private const float InteractRange = 240f;

	private int _tier = -1;
	private int _lastGeneration = -1;
	private List<GameObject> _parts = new();

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null )
			return;

		if ( core.Jobs.LoadGeneration != _lastGeneration )
		{
			_lastGeneration = core.Jobs.LoadGeneration;
			ParkAtJob( core );
		}

		var tier = core.Upgrades.VanTier;
		if ( tier != _tier )
			Rebuild( tier );

		UpdateInteraction( core );
	}

	/// <summary>
	/// Focus the van when the player looks at it in reach; a click opens the van locker
	/// (swap tools, and depart once the job is done).
	/// </summary>
	private void UpdateInteraction( GameCore core )
	{
		var focused = false;

		if ( !core.IsUiBlocking )
		{
			var player = PressurePlayer.Instance;
			if ( player.IsValid() )
			{
				var eye = player.EyePosition;
				var tr = Scene.Trace
					.Ray( eye, eye + player.EyeForward * InteractRange )
					.IgnoreGameObjectHierarchy( player.GameObject )
					.Run();

				focused = tr.Hit && IsPartOfVan( tr.GameObject );
			}
		}

		core.VanFocused = focused;

		if ( focused && Input.Pressed( "Attack2" ) )
			core.OpenVanMenu();
	}

	private bool IsPartOfVan( GameObject go )
	{
		while ( go.IsValid() )
		{
			if ( go == GameObject ) return true;
			go = go.Parent;
		}

		return false;
	}

	private void ParkAtJob( GameCore core )
	{
		var yaw = core.Jobs.SpawnYaw;
		var rot = Rotation.FromYaw( yaw );

		// Park behind and to the side of the spawn, in profile, clear of the work area.
		WorldPosition = core.Jobs.SpawnPosition + rot.Backward * 170f + rot.Left * 130f;
		WorldRotation = Rotation.FromYaw( yaw + 90f );
	}

	private void Rebuild( int tier )
	{
		_tier = tier;

		foreach ( var part in _parts )
			part?.Destroy();

		_parts = Scenery.BuildVan( GameObject, tier );
	}
}
