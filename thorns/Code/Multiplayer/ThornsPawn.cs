namespace Sandbox;

/// <summary>
/// Player identity + visibility on the <b>network root</b> (same <see cref="GameObject"/> as <see cref="ThornsPawnMovement"/>).
/// </summary>
[Title( "Thorns — Pawn" )]
[Category( "Thorns" )]
[Icon( "accessibility" )]
public sealed class ThornsPawn : Component, Component.INetworkSpawn
{
	const string WorldModelChildName = "Body";

	/// <summary>True when this pawn is owned by the local client connection (your player).</summary>
	public bool IsLocal { get; private set; }

	/// <summary>Connection that owns the networked root (same as session owner).</summary>
	public Connection OwnerConnection { get; private set; }

	/// <summary>Published when local pawn is ready (once per client).</summary>
	public static ThornsPawn Local { get; private set; }

	/// <summary>
	/// True when <paramref name="pawnRoot"/> is owned by <see cref="Connection.Local"/>.
	/// Prefer this over comparing to <see cref="Local"/> for input: <see cref="Local"/> is assigned in <see cref="OnNetworkSpawn"/>,
	/// which can run after other components' <c>OnUpdate</c> in the same frame (joining clients would miss keys until then).
	/// </summary>
	public static bool IsLocalConnectionPawnRoot( GameObject pawnRoot )
	{
		var lc = Connection.Local;
		return lc is not null && pawnRoot.IsValid() && pawnRoot.Network.OwnerId == lc.Id;
	}

	/// <summary>
	/// True when <paramref name="component"/> lives under a pawn whose network owner is <see cref="Connection.Local"/>.
	/// Prefer over <see cref="Component.IsProxy"/> for gameplay input: joining clients often still have <c>IsProxy == true</c> on owned pawns,
	/// which disables Update paths while UI-driven actions (e.g. build toolbar clicks) continue to work.
	/// </summary>
	public static bool IsLocalConnectionOwner( Component component )
	{
		if ( component is null || !component.IsValid() )
			return false;
		var pawn = component.GameObject.Components.GetInAncestorsOrSelf<ThornsPawn>( true );
		return pawn.IsValid() && IsLocalConnectionPawnRoot( pawn.GameObject );
	}

	/// <summary>
	/// Host-only RPC guard. On a listen server, <see cref="Rpc.Calling"/> may be false when the host handles an incoming client RPC,
	/// but <see cref="Rpc.Caller"/> still identifies the remote connection — do not fall back to <see cref="Connection.Local"/> in that case.
	/// </summary>
	public static bool ValidateHostRpcCallerOwnsPawnRoot( GameObject pawnNetworkRoot )
	{
		if ( !pawnNetworkRoot.IsValid() )
			return false;
		var ownerId = pawnNetworkRoot.Network.OwnerId;
		if ( Rpc.Caller is not null )
			return Rpc.Caller.Id == ownerId;
		var lc = Connection.Local;
		return lc is not null && lc.Id == ownerId;
	}

	bool _lateVisualApplied;

	public void OnNetworkSpawn( Connection owner )
	{
		OwnerConnection = owner ?? Connection.Find( GameObject.Network.OwnerId );

		var local = Connection.Local;
		IsLocal = local is not null && OwnerConnection is not null && OwnerConnection.Id == local.Id;

		var ownerId = GameObject.Network.OwnerId;

		if ( IsLocal )
		{
			Local = this;
			ThornsWorldBootGate.BeginLocalBoot();
			ThornsGameManager.EnsureThornsCollisionDebugDriver( GameObject );
			ThornsToolMeleeCombat.ClientResetPrimaryStrikeCadence();
			Components.Get<ThornsHotbarEquipment>()?.ClientTryBootstrapEquipmentFromObservers();
			Log.Info( $"[Thorns] Local pawn detected: {GameObject.Name}, OwnerId={ownerId}, IsProxy={IsProxy}" );
		}
		else
		{
			Log.Info( $"[Thorns] Pawn spawned (non-local): {GameObject.Name}, owner id={ownerId}, IsProxy={IsProxy}" );
		}

		TryApplyWorldVisual( logIfMissing: true );
		ThornsPawnConnectionIndex.Register( this );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		// Joining clients can see stale IsLocal until OnNetworkSpawn; owner id is authoritative for hiding own Body.
		TryApplyWorldVisual( logIfMissing: false );
	}

	protected override void OnStart()
	{
		_ = LateApplyWorldVisual();
		if ( Game.IsPlaying )
			ThornsPawnConnectionIndex.Register( this );
	}

	async Task LateApplyWorldVisual()
	{
		await Task.DelayRealtimeSeconds( 0.05f );
		TryApplyWorldVisual( logIfMissing: !_lateVisualApplied );
	}

	/// <summary>Prefer <see cref="WorldModelChildName"/>; otherwise any ModelRenderer under this pawn (replication can lag behind <see cref="GameObject.Children"/>).</summary>
	ModelRenderer FindWorldModelRenderer()
	{
		foreach ( var mr in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !mr.IsValid() )
				continue;
			if ( mr.GameObject.Name == WorldModelChildName )
				return mr;
		}

		foreach ( var mr in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( mr.IsValid() )
				return mr;
		}

		return default;
	}

	void EnsureWorldModelAsset()
	{
		var model = FindWorldModelRenderer();
		if ( !model.IsValid() )
			return;

		if ( !model.Model.IsValid() )
			model.Model = Model.Load( ThornsCitizenPaths.CitizenVmdl );
	}

	void TryApplyWorldVisual( bool logIfMissing )
	{
		EnsureWorldModelAsset();

		var model = FindWorldModelRenderer();
		if ( !model.IsValid() )
		{
			if ( logIfMissing )
				Log.Warning( $"[Thorns] Pawn '{GameObject.Name}' has no ModelRenderer under pawn — host should run {nameof( ThornsGameManager.EnsurePawnWorldModel )} before NetworkSpawn; other players will not see this pawn." );

			return;
		}

		_lateVisualApplied = true;

		var hideOwnBody = IsLocalConnectionPawnRoot( GameObject );

		if ( hideOwnBody )
			model.Enabled = false;
		else
		{
			model.Enabled = true;
			if ( OwnerConnection is not null )
			{
				var bytes = OwnerConnection.Id.ToByteArray();
				model.Tint = new Color( bytes[0] / 255f, bytes[1] / 255f, bytes[2] / 255f, 1f );
			}
		}
	}

	protected override void OnDestroy()
	{
		ThornsPawnConnectionIndex.Unregister( this );
		if ( Local == this )
			Local = null;
	}
}
