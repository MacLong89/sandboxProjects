namespace Sandbox;

/// <summary>
/// Player identity + visibility on the <b>network root</b> (same <see cref="GameObject"/> as <see cref="YaPawnMovement"/>).
/// </summary>
[Title( "Thorns — Pawn" )]
[Category( "Thorns" )]
[Icon( "accessibility" )]
public sealed class YaPawn : Component, Component.INetworkSpawn
{
	const string WorldModelChildName = "Body";

	/// <summary>True when this pawn is owned by the local client connection (your player).</summary>
	public bool IsLocal { get; private set; }

	/// <summary>Connection that owns the networked root (same as session owner).</summary>
	public Connection OwnerConnection { get; private set; }

	/// <summary>Published when local pawn is ready (once per client).</summary>
	public static YaPawn Local { get; private set; }

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
		var pawn = component.GameObject.Components.GetInAncestorsOrSelf<YaPawn>( true );
		return pawn.IsValid() && IsLocalConnectionPawnRoot( pawn.GameObject );
	}

	/// <summary>
	/// Host-only RPC guard. On a listen server, <see cref="Rpc.Calling"/> may be false when the host handles an incoming client RPC,
	/// but <see cref="Rpc.Caller"/> still identifies the remote connection — do not fall back to <see cref="Connection.Local"/> in that case.
	/// </summary>
	public static bool ValidateHostRpcCallerOwnsPawnRoot( GameObject pawnNetworkRoot )
	{
		if ( pawnNetworkRoot is null || !pawnNetworkRoot.IsValid() )
			return false;
		var ownerId = pawnNetworkRoot.Network.OwnerId;
		if ( Rpc.Caller is not null )
			return Rpc.Caller.Id == ownerId;
		var lc = Connection.Local;
		return lc is not null && lc.Id == ownerId;
	}

	bool _lateVisualApplied;

	/// <summary>World-body alpha for <see cref="YaPlayerRole.Alone"/> during <see cref="YaGameState.InRound"/> (third-person; local FP body stays hidden).</summary>
	[Property] public float AloneWorldModelAlpha { get; set; } = 0.08f;

	public void OnNetworkSpawn( Connection owner )
	{
		OwnerConnection = owner ?? Connection.Find( GameObject.Network.OwnerId );

		var local = Connection.Local;
		IsLocal = local is not null && OwnerConnection is not null && OwnerConnection.Id == local.Id;

		var ownerId = GameObject.Network.OwnerId;

		if ( IsLocal )
		{
			Local = this;
			Log.Info( $"[YA] Local pawn detected: {GameObject.Name}, OwnerId={ownerId}, IsProxy={IsProxy}" );
		}
		else
		{
			Log.Info( $"[YA] Pawn spawned (non-local): {GameObject.Name}, owner id={ownerId}, IsProxy={IsProxy}" );
		}

		TryApplyWorldVisual( logIfMissing: true );
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
			model.Model = Model.Load( YaCitizenPaths.CitizenVmdl );
	}

	void TryApplyWorldVisual( bool logIfMissing )
	{
		EnsureWorldModelAsset();

		var model = FindWorldModelRenderer();
		if ( !model.IsValid() )
		{
			if ( logIfMissing )
				Log.Warning( $"[YA] Pawn '{GameObject.Name}' has no ModelRenderer under pawn — host should run {nameof( YaGameManager.EnsurePawnWorldModel )} before NetworkSpawn; other players will not see this pawn." );

			return;
		}

		_lateVisualApplied = true;

		var hideOwnBody = IsLocalConnectionPawnRoot( GameObject );

		var gs = YaHudMatchSnapshot.TryGameState( GameObject.Scene );
		var inRound = gs is { IsValid: true, CurrentState: YaGameState.InRound };
		var roleCmp = GameObject.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var role = roleCmp.IsValid() ? roleCmp.Role : YaPlayerRole.Unassigned;
		var aloneMech = GameObject.Components.Get<YaAloneMechanics>( FindMode.EnabledInSelf );
		var mimicActive = aloneMech.IsValid() && aloneMech.MimicPresentationActive;
		// Lobby / intermission / unassigned: never ghost. In-round + Alone only: faint silhouette (unless Mimic).
		var aloneGhost = inRound && role == YaPlayerRole.Alone && !mimicActive;

		if ( hideOwnBody )
		{
			model.Enabled = false;
			return;
		}

		var alphaAlone = Math.Clamp( AloneWorldModelAlpha, 0.02f, 0.35f );

		foreach ( var mr in GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !mr.IsValid() || IsUnderLocalViewHierarchy( mr.GameObject ) )
				continue;

			mr.Enabled = true;

			if ( aloneGhost )
			{
				// Faint silhouette — only while replicated state is InRound and role is Alone.
				mr.Tint = new Color( 0.72f, 0.74f, 0.78f, alphaAlone );
			}
			else if ( mimicActive && aloneMech.IsValid() )
			{
				if ( aloneMech.MimicVisualCopyConnectionId != default )
				{
					var bytes = aloneMech.MimicVisualCopyConnectionId.ToByteArray();
					mr.Tint = new Color( bytes[0] / 255f, bytes[1] / 255f, bytes[2] / 255f, 1f );
				}
				else
					mr.Tint = YaHudTheme.MimicGenericHunterTint;
			}
			else if ( OwnerConnection is not null )
			{
				var bytes = OwnerConnection.Id.ToByteArray();
				mr.Tint = new Color( bytes[0] / 255f, bytes[1] / 255f, bytes[2] / 255f, 1f );
			}
			else
			{
				// Between rounds or before owner replicates — force full opacity (avoids stale ghost tint).
				mr.Tint = Color.White;
			}
		}
	}

	static bool IsUnderLocalViewHierarchy( GameObject go )
	{
		while ( go.IsValid() )
		{
			if ( go.Name == "View" )
				return true;
			go = go.Parent;
		}

		return false;
	}

	protected override void OnDestroy()
	{
		if ( Local == this )
			Local = null;
	}
}
