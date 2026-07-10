namespace Sandbox;

/// <summary>Host-visible diagnostics for resource strikes (wildlife logs stay minimal).</summary>
public static class ThornsResourceHarvestLog
{
	public static void Strike(
		Guid nodeId,
		ThornsResourceKind kind,
		float hpBefore,
		float hpAfter,
		string itemId,
		int qtyGranted )
	{
		Log.Info(
			$"[Thorns Harvest] strike node={nodeId:D} kind={kind} hp {hpBefore:F1}→{hpAfter:F1} +{qtyGranted} {itemId}" );
	}

	public static void Depleted( Guid nodeId, ThornsResourceKind kind ) =>
		Log.Info( $"[Thorns Harvest] depleted node={nodeId:D} kind={kind} (removed)" );
}
