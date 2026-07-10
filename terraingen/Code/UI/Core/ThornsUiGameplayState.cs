namespace Terraingen.UI.Core;

using Terraingen;
using Terraingen.Combat;
using Terraingen.Player;

/// <summary>Gameplay state drives UI priority — lower priority systems defer during combat, death, etc.</summary>
public static class ThornsUiGameplayState
{
	public static bool IsInCombat => ThornsDamageFlashState.WasRecentlyDamaged;

	public static bool IsDead
	{
		get
		{
			var player = ThornsPlayerGameplay.Local;
			if ( !player.IsValid() )
				return false;

			var health = player.Components.Get<ThornsPlayerHealth>();
			return health.IsValid() && ( !health.IsAlive || health.IsDeadState );
		}
	}

	public static bool ShouldDeferTransientFeedback =>
		IsInCombat || IsDead || ThornsUiManager.IsModalOpen;

	public static bool ShouldHideTutorial =>
		IsInCombat || IsDead || ThornsMenuHost.IsOpen;

	public static bool ShouldSuppressAchievements =>
		IsInCombat;

	public static bool AllowsInventoryAccess =>
		!IsDead;
}
