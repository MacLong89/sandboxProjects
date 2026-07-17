namespace Offshore;

/// <summary>
/// Empty equipped boat moored at the pier tip. Uses that boat's dock sprite.
/// Hidden while boarded — the player avatar becomes the fisherman-in-boat sprite.
/// </summary>
public sealed class DockBoatVisuals : Component
{
	public static DockBoatVisuals Instance { get; private set; }

	private GameObject _boatGo;
	private SpriteRenderer _boat;
	private string _shownBoatId = "";
	private string _shownSpritePath = "";

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
		BoatSystem.EquippedBoatChanged -= Refresh;
	}

	protected override void OnStart()
	{
		Instance = this;
		BoatSystem.EquippedBoatChanged += Refresh;
		Log.Info( "[Offshore Boat] DockBoatVisuals OnStart" );
		Refresh();
	}

	protected override void OnUpdate()
	{
		var game = OffshoreGameController.Instance;
		if ( game is null )
			return;

		if ( game.Player?.Mode == AnglerController.LocomotionMode.InBoat )
		{
			SetVisible( false );
			return;
		}

		var boat = BoatSystem.Equipped( game.Progression );
		var id = boat?.Id ?? "";
		var path = boat?.DockSpritePath ?? "";
		if ( !string.Equals( id, _shownBoatId, StringComparison.OrdinalIgnoreCase )
		     || !string.Equals( path, _shownSpritePath, StringComparison.OrdinalIgnoreCase ) )
			Refresh();
		else
			SetVisible( boat is not null );
	}

	public void Refresh()
	{
		var game = OffshoreGameController.Instance;
		var boat = BoatSystem.Equipped( game?.Progression );
		BoatCatalog.NormalizeSpritePaths( boat );
		_shownBoatId = boat?.Id ?? "";
		_shownSpritePath = boat?.DockSpritePath ?? "";

		if ( boat is null )
		{
			Log.Info(
				$"[Offshore Boat] DOCK refresh: no equipped boat " +
				$"(equippedId='{game?.Progression?.EquippedBoatId}' owned={game?.Progression?.OwnedBoatIds?.Count ?? 0})" );
			SetVisible( false );
			return;
		}

		EnsureBoatObject();
		var path = boat.DockSpritePath;
		var hasTex = OffshoreSprites.HasTexture( path );
		var tex = OffshoreSprites.Load( path );
		var size = OffshoreSprites.BoatWorldSize( path, tex, worldHeight: OffshoreConstants.BoatWorldHeight );
		OffshoreSprites.ApplyBoatSprite( _boat, _boatGo, tex, size, flipHorizontal: false );
		_boatGo.WorldPosition = new Vector3(
			OffshoreConstants.BoatMooringX,
			OffshoreConstants.FisherPlaneY + 0.35f,
			OffshoreConstants.BoatMooringZ );

		var visible = game?.Player?.Mode != AnglerController.LocomotionMode.InBoat;
		SetVisible( visible );
		Log.Info(
			$"[Offshore Boat] DOCK id={boat.Id} path='{path}' hasTex={hasTex} size={size} visible={visible}" );
	}

	private void EnsureBoatObject()
	{
		if ( _boatGo is not null && _boatGo.IsValid() )
			return;

		_boatGo = new GameObject( true, "DockMooredBoat" );
		_boat = _boatGo.Components.Create<SpriteRenderer>();
		OffshoreSprites.ConfigureBoatRenderer( _boat );
		Log.Info( "[Offshore Boat] Created DockMooredBoat GameObject" );
	}

	private void SetVisible( bool visible )
	{
		if ( _boatGo is null || !_boatGo.IsValid() )
			return;
		if ( _boatGo.Enabled == visible )
			return;
		_boatGo.Enabled = visible;
	}
}

