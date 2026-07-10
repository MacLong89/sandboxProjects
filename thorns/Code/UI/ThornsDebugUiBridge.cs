namespace Sandbox;

/// <summary>
/// Owner-local bridge for debug UI: requests server-validated death crate snapshots (THORNS doc — no client-authoritative crate contents).
/// </summary>
[Title( "Thorns — Debug UI Bridge" )]
[Category( "Thorns" )]
[Icon( "terminal" )]
[Order( 55 )]
public sealed class ThornsDebugUiBridge : Component
{
	public Guid LastCrateId { get; private set; }

	/// <summary>Tab-separated snapshot lines from host; non-authoritative mirror.</summary>
	public string LastCrateSnapshotText { get; private set; } = "";

	public int CrateSnapshotRevision { get; private set; }

	[Rpc.Host]
	public void RequestDeathCrateSnapshotForUi( Guid crateId )
	{
		Log.Info( $"[Thorns] UI: crate snapshot request crate={crateId}" );

		if ( !Networking.IsHost )
			return;

		if ( !ValidateRpcCallerOwnsPawn() )
		{
			Log.Warning( "[Thorns] UI crate snapshot rejected: caller" );
			return;
		}

		if ( crateId == Guid.Empty || !ThornsDeathCrate.ActiveById.TryGetValue( crateId, out var crate ) || !crate.IsValid() )
		{
			Log.Warning( "[Thorns] UI crate snapshot rejected: crate not found" );
			ClientReceiveDeathCrateSnapshot( crateId, "" );
			return;
		}

		if ( Rpc.Caller is null || !crate.HostValidateCallerForUiSnapshot( Rpc.Caller ) )
		{
			Log.Warning( "[Thorns] UI crate snapshot rejected: caller not alive" );
			ClientReceiveDeathCrateSnapshot( crateId, "" );
			return;
		}

		var text = crate.HostFormatUiSnapshotText();
		ClientReceiveDeathCrateSnapshot( crateId, text );
	}

	[Rpc.Owner]
	void ClientReceiveDeathCrateSnapshot( Guid crateId, string text )
	{
		LastCrateId = crateId;
		LastCrateSnapshotText = text ?? "";
		CrateSnapshotRevision++;
		Log.Info( $"[Thorns] UI received crate snapshot (mirror rev={CrateSnapshotRevision}, bytes={LastCrateSnapshotText.Length})" );
	}

	bool ValidateRpcCallerOwnsPawn() => ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject );
}
