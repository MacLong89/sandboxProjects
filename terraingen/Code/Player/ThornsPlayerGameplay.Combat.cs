namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.UI;

/// <summary>Combat feedback hooks (extracted from gameplay god-class).</summary>
public sealed partial class ThornsPlayerGameplay
{
	public void HostNotifyDamaged()
	{
		_healthRevealTimer = 8f;
		_vitalsNetwork.MarkDirty();
		HostRefreshVitals( forceShowHealth: true );
		PushDamageFlashToOwner();
	}

	void PushDamageFlashToOwner()
	{
		if ( IsLocalPlayer() )
			ThornsDamageFlashState.Pulse();
		else if ( Networking.IsActive )
			RpcOwnerDamageFlash();
	}

	[Rpc.Owner]
	void RpcOwnerDamageFlash()
	{
		ThornsDamageFlashState.Pulse();
	}

	/// <summary>Red crosshair flash when a shot or gather strike connects (owner client only).</summary>
	public void PushCrosshairHitFeedbackToOwner( float damage = 0f, bool killed = false )
	{
		if ( IsLocalPlayer() )
			ApplyCrosshairHitFeedback( damage, killed );
		else if ( Networking.IsActive )
			RpcOwnerCrosshairHitFeedback( damage, killed );
	}

	[Rpc.Owner]
	void RpcOwnerCrosshairHitFeedback( float damage, bool killed )
	{
		ApplyCrosshairHitFeedback( damage, killed );
	}

	static void ApplyCrosshairHitFeedback( float damage, bool killed )
	{
		ThornsCombatFeedbackHost.Instance?.EnsureFeedbackHud();
		ThornsHitmarkerState.ReportHit( damage, killed );
	}

	public void HostNotifyWildlifeKill()
	{
		ThornsMilestoneTracker.OnKill( this, "wildlife" );
		HostGrantXp( XpWildlifeKill );
	}

	public void HostNotifyBanditKill()
	{
		ThornsMilestoneTracker.OnKill( this, "bandit" );
		HostGrantXp( XpBanditKill );
	}
}
