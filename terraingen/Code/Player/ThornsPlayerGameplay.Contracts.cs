namespace Terraingen.Player;

using Terraingen.Multiplayer;
using Terraingen.Progression;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed partial class ThornsPlayerGameplay
{
	ThornsSurvivorContractsSnapshotDto _survivorContracts = new();

	public ThornsSurvivorContractsSnapshotDto HostPeekSurvivorContracts() => _survivorContracts;

	public void HostApplySurvivorContractsSnapshot( ThornsSurvivorContractsSnapshotDto contracts )
	{
		if ( contracts is null )
			return;

		_survivorContracts = contracts;
		_survivorContracts.Daily ??= new ThornsDailyContractDto();
		_survivorContracts.Weekly ??= new ThornsWeeklyContractDto();
		_survivorContracts.Weekly.GoalIds ??= new List<string>();
	}

	string _sessionLastKillerDisplayName = "";

	public void HostSetSessionLastKiller( string killerDisplayName ) =>
		_sessionLastKillerDisplayName = killerDisplayName?.Trim() ?? "";

	public string HostGetSessionLastKillerDisplayName() => _sessionLastKillerDisplayName;

	public void HostNotifyContractsChanged()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline )
			return;

		PushSnapshotToOwnerClient();
		UiRevisionBus.Publish( UiRevisionChannel.Journal );
	}
}
