namespace Terraingen.Victory;

using Terraingen.Player;

/// <summary>Gameplay hooks report progress sources into the victory manager (host-only).</summary>
public static class ThornsVictoryBridge
{
	public static void Report( ThornsPlayerGameplay gameplay, string sourceKey, int amount = 1 )
	{
		if ( gameplay is null || !gameplay.IsValid || amount <= 0 )
			return;

		ThornsVictoryManager.EnsureInstance()?.HostReportSource( gameplay.AccountKey, sourceKey, amount );
	}

	public static void ReportAccount( string accountKey, string sourceKey, int amount = 1 )
	{
		if ( string.IsNullOrWhiteSpace( accountKey ) || amount <= 0 )
			return;

		ThornsVictoryManager.EnsureInstance()?.HostReportSource( accountKey, sourceKey, amount );
	}
}
