namespace PawnShop;

public enum CustomerState
{
	Entering,
	Queuing,
	AtCounter,
	Negotiating,
	Browsing,
	GoingToCounter,
	CheckingOut,
	Leaving,
}

/// <summary>
/// A customer in the shop: waypoint walking, queueing, browsing, and hooks into the
/// negotiation system. Movement is simple straight-line walking on the open floor.
/// </summary>
public sealed class CustomerActor : Component
{
	public CustomerProfile Profile { get; private set; }
	public CustomerState State { get; private set; } = CustomerState.Entering;

	/// <summary>Queue index (0 = at counter position). -1 when not queued.</summary>
	public int QueueIndex { get; set; } = -1;

	public bool IsAtCounter => State == CustomerState.AtCounter;
	public bool IsLeaving => State == CustomerState.Leaving;
	public bool IsBusy => State is CustomerState.Negotiating or CustomerState.CheckingOut;

	private readonly List<Vector3> _path = new();
	private float _speed = 95f;
	private float _waitTimer;
	private float _queuePatience;
	private HumanoidVisual _visual;
	private Interactable _zone;

	// Browsing
	private int _browseVisits;
	private ItemInstance _targetItem;

	private GameManager Game => GameManager.Instance;
	private CustomerManager Manager => Game?.Customers;

	public void Setup( CustomerProfile profile )
	{
		Profile = profile;
		_speed = Sandbox.Game.Random.Float( 85f, 110f );
		_queuePatience = GameConstants.QueuePatienceSeconds * (0.6f + profile.Archetype.Patience * 0.8f);
		if ( Game?.Save.OwnsUpgrade( UpgradeId.BetterCounter ) == true )
			_queuePatience *= 1.25f;

		WorldPosition = ShopLayout.DoorOutside + new Vector3( Sandbox.Game.Random.Float( -30f, 30f ), -20f, 0 );

		_visual = Components.Create<HumanoidVisual>();
		_visual.Setup( profile );

		var zoneGo = new GameObject( GameObject, true, "InteractZone" );
		_zone = zoneGo.Components.Create<Interactable>();
		_zone.Kind = InteractKind.Customer;
		_zone.Customer = this;
		_zone.HalfExtents = new Vector3( 30, 30, 60 );

		Sfx.PlayAt( Sfx.DoorBell, ShopLayout.DoorInside );

		// First move: through the door.
		if ( profile.Intent == CustomerIntent.Buy )
			StartBrowsing();
		else
			GoQueue();
	}

	// ==================================================================== Movement

	private void SetPath( Vector3 target, params Vector3[] via )
	{
		_path.Clear();
		foreach ( var v in via ) _path.Add( v.WithZ( 0 ) );
		_path.Add( target.WithZ( 0 ) );
	}

	private bool MoveAlongPath()
	{
		if ( _path.Count == 0 ) return true;

		var target = _path[0];
		var to = target - WorldPosition.WithZ( 0 );
		var dist = to.Length;

		if ( dist < 6f )
		{
			_path.RemoveAt( 0 );
			return _path.Count == 0;
		}

		var dir = to.Normal;
		WorldPosition += dir * Math.Min( _speed * Time.Delta, dist );
		WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( dir, Vector3.Up ), Time.Delta * 8f );
		return false;
	}

	private void FaceCounter()
	{
		WorldRotation = Rotation.Lerp( WorldRotation, Rotation.FromYaw( 90f ), Time.Delta * 6f );
	}

	protected override void OnUpdate()
	{
		_visual?.SetMoodColor( Profile?.Mood ?? 0.7f );

		switch ( State )
		{
			case CustomerState.Entering:
			case CustomerState.Queuing:
				TickQueue();
				break;
			case CustomerState.AtCounter:
				TickAtCounter();
				break;
			case CustomerState.Negotiating:
				FaceCounter();
				break;
			case CustomerState.Browsing:
				TickBrowsing();
				break;
			case CustomerState.GoingToCounter:
				TickGoingToCounter();
				break;
			case CustomerState.CheckingOut:
				TickCheckout();
				break;
			case CustomerState.Leaving:
				if ( MoveAlongPath() )
					Despawn();
				break;
		}
	}

	// ==================================================================== Queue flow (sell/pawn/redeem)

	private void GoQueue()
	{
		State = CustomerState.Entering;
		QueueIndex = Manager.ClaimQueueSpot( this );
		if ( QueueIndex < 0 )
		{
			// Line's full — walk right back out.
			Leave( annoyed: true );
			return;
		}
		SetPath( QueueTarget(), ShopLayout.DoorInside );
	}

	private Vector3 QueueTarget() => QueueIndex == 0
		? ShopLayout.CounterCustomerSpot
		: ShopLayout.QueueSpots[Math.Clamp( QueueIndex - 1, 0, ShopLayout.QueueSpots.Length - 1 )];

	private void TickQueue()
	{
		// The line may have moved us up.
		var arrived = MoveAlongPath();

		if ( arrived )
		{
			if ( QueueIndex == 0 )
			{
				State = CustomerState.AtCounter;
				UiState.Bump();
			}
			else
			{
				State = CustomerState.Queuing;
				FaceCounter();
			}
		}

		TickQueuePatience();
	}

	private void TickAtCounter()
	{
		FaceCounter();
		TickQueuePatience();
	}

	private void TickQueuePatience()
	{
		_queuePatience -= Time.Delta;
		if ( _queuePatience > 0f ) return;

		// Fed up with waiting.
		Profile.Mood -= 0.3f;
		Game.Reputation.Add( -1f );
		Game.Toast( $"{Profile.Name} got tired of waiting and left.", "hourglass_disabled" );
		Leave( annoyed: true );
	}

	/// <summary>Queue moved — walk to the new spot.</summary>
	public void OnQueueAdvanced()
	{
		if ( State is CustomerState.Queuing or CustomerState.Entering or CustomerState.AtCounter )
		{
			State = State == CustomerState.AtCounter ? CustomerState.AtCounter : CustomerState.Queuing;
			SetPath( QueueTarget() );
			if ( QueueIndex == 0 )
				State = CustomerState.Entering; // walk up, becomes AtCounter on arrival
		}
	}

	/// <summary>Player served us — negotiation begins.</summary>
	public void BeginNegotiation()
	{
		// Buyer whose target vanished while they queued: apologize and go.
		if ( Profile.Intent == CustomerIntent.Buy
			&& (_targetItem is null || _targetItem.Location != ItemLocation.OnDisplay || _targetItem.NotForSale) )
		{
			Game.Toast( $"{Profile.Name} wanted something that's no longer available.", "remove_shopping_cart" );
			Leave( annoyed: false );
			return;
		}

		State = CustomerState.Negotiating;
		_zone.Enabled = false;

		Game.Shop.SetCounterItem( Profile.Intent is CustomerIntent.Sell or CustomerIntent.Pawn ? Profile.Item : null );

		var negotiation = Game.Negotiation;
		negotiation.OnFinished = OnNegotiationFinished;

		switch ( Profile.Intent )
		{
			case CustomerIntent.Sell: negotiation.StartSell( Profile ); break;
			case CustomerIntent.Pawn: negotiation.StartPawn( Profile ); break;
			case CustomerIntent.Redeem: negotiation.StartRedeem( Profile ); break;
			case CustomerIntent.Buy: negotiation.StartBuyerOffer( Profile, _targetItem ); break;
		}

		Game.Tutorial.Notify( TutorialTrigger.StartedNegotiation );
	}

	private void OnNegotiationFinished( NegotiationResult result )
	{
		Game.Shop.SetCounterItem( null );

		var annoyed = result == NegotiationResult.CustomerLeft
			|| (result == NegotiationResult.Rejected && Profile.Mood < 0.4f);
		Leave( annoyed );
	}

	// ==================================================================== Browsing flow (buyers)

	private void StartBrowsing()
	{
		State = CustomerState.Browsing;
		_browseVisits = 0;
		PickBrowseTarget();
	}

	private void PickBrowseTarget()
	{
		var displayed = Game.Inventory.OnDisplay.Where( i => !i.NotForSale ).ToList();
		if ( displayed.Count == 0 )
		{
			// Nothing to look at.
			SetPath( ShopLayout.DoorInside + new Vector3( Sandbox.Game.Random.Float( -80, 80 ), Sandbox.Game.Random.Float( 40, 120 ), 0 ), ShopLayout.DoorInside );
			_waitTimer = Sandbox.Game.Random.Float( 2f, 4f );
			_targetItem = null;
			return;
		}

		// Prefer favorite categories.
		var favorites = Profile.Named?.Favorites ?? Array.Empty<ItemCategory>();
		var preferred = displayed.Where( i => favorites.Contains( i.Def?.Category ?? default ) ).ToList();
		var pool = preferred.Count > 0 && Sandbox.Game.Random.Float() < 0.7f ? preferred : displayed;
		_targetItem = pool[Sandbox.Game.Random.Int( 0, pool.Count - 1 )];

		var slot = ShopLayout.Slot( _targetItem.DisplaySlot );
		var browseSpot = slot?.BrowseSpot ?? ShopLayout.DoorInside;
		SetPath( browseSpot, ShopLayout.DoorInside );
		_waitTimer = Sandbox.Game.Random.Float( 3f, 6f );
		if ( Game.Save.OwnsUpgrade( UpgradeId.Lighting ) )
			_waitTimer += 1.5f;
	}

	private void TickBrowsing()
	{
		if ( !MoveAlongPath() )
			return;

		// Look at the shelf.
		if ( _targetItem is not null )
		{
			var slot = ShopLayout.Slot( _targetItem.DisplaySlot );
			if ( slot is not null )
			{
				var dir = (slot.Position.WithZ( 0 ) - WorldPosition.WithZ( 0 )).Normal;
				if ( dir.Length > 0.1f )
					WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( dir, Vector3.Up ), Time.Delta * 5f );
			}
		}

		_waitTimer -= Time.Delta;
		if ( _waitTimer > 0f ) return;

		_browseVisits++;

		// Item may have been sold/stowed while we were walking.
		if ( _targetItem is null || _targetItem.Location != ItemLocation.OnDisplay )
		{
			if ( _browseVisits >= 3 ) { Leave( annoyed: false ); return; }
			PickBrowseTarget();
			return;
		}

		DecideOnItem();
	}

	private void DecideOnItem()
	{
		var item = _targetItem;
		var price = Math.Max( 1, item.SalePrice );

		// Theft attempt? Only shady types, only occasionally.
		var thiefly = Profile.Archetype.Honesty < 0.35f;
		if ( thiefly && Sandbox.Game.Random.Float() < 0.35f * Game.Events.TheftMult )
		{
			Game.AttemptTheft( Profile );
			Leave( annoyed: false, hurried: true );
			return;
		}

		var perceived = ItemValue.TrueValue( item, Game ) * Sandbox.Game.Random.Float( 0.85f, 1.15f );
		perceived *= Game.Reputation.BuyerConfidence;

		if ( price > Profile.Budget && price * 0.75f > Profile.Budget )
		{
			// Way out of budget — keep browsing or bail.
			if ( _browseVisits >= 3 ) { Leave( annoyed: false ); return; }
			PickBrowseTarget();
			return;
		}

		if ( price <= perceived && price <= Profile.Budget && Sandbox.Game.Random.Float() < 0.75f )
		{
			// Fair sticker — head to the till and buy it outright.
			State = CustomerState.GoingToCounter;
			SetPath( ShopLayout.CounterCustomerSpot + new Vector3( Sandbox.Game.Random.Float( -20, 20 ), 0, 0 ) );
			return;
		}

		// Overpriced (or they're hagglers) — queue up to make an offer.
		var wantsToHaggle = Profile.Archetype.Id is Archetype.BargainHunter or Archetype.Collector or Archetype.WealthyBuyer
			|| price > perceived;
		if ( wantsToHaggle && Sandbox.Game.Random.Float() < 0.8f )
		{
			QueueIndex = Manager.ClaimQueueSpot( this );
			if ( QueueIndex < 0 ) { Leave( annoyed: false ); return; }
			State = CustomerState.Entering;
			SetPath( QueueTarget() );
			return;
		}

		if ( _browseVisits >= 3 ) { Leave( annoyed: false ); return; }
		PickBrowseTarget();
	}

	private void TickGoingToCounter()
	{
		if ( !MoveAlongPath() ) return;

		// Sticker purchase — short checkout moment.
		State = CustomerState.CheckingOut;
		_waitTimer = 1.6f;
		FaceCounter();
	}

	private void TickCheckout()
	{
		FaceCounter();
		_waitTimer -= Time.Delta;
		if ( _waitTimer > 0f ) return;

		if ( _targetItem is not null && _targetItem.Location == ItemLocation.OnDisplay && !_targetItem.NotForSale )
		{
			Game.CompleteStickerSale( Profile, _targetItem );
			Profile.Mood = 0.9f;
		}

		Leave( annoyed: false );
	}

	// ==================================================================== Leaving

	public void Leave( bool annoyed, bool hurried = false )
	{
		if ( State == CustomerState.Leaving ) return;

		Manager.ReleaseQueueSpot( this );
		State = CustomerState.Leaving;
		if ( _zone.IsValid() ) _zone.Enabled = false;
		if ( hurried ) _speed *= 1.6f;
		if ( annoyed ) Profile.Mood = Math.Min( Profile.Mood, 0.25f );

		SetPath( ShopLayout.DoorOutside + new Vector3( Sandbox.Game.Random.Float( -120f, 120f ), -60f, 0 ), ShopLayout.DoorInside );
		UiState.Bump();
	}

	private void Despawn()
	{
		Manager?.OnActorDespawned( this );
		GameObject.Destroy();
	}
}
