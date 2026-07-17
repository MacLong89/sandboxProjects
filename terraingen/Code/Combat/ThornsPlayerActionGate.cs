namespace Terraingen.Combat;

using Terraingen.Player;
using Terraingen.UI.Core;

/// <summary>
/// AUDIT FIX (2026-07): Shared gates for player combat / use actions.
/// <para>
/// History: Combat components read raw <c>Input.Pressed("Attack1")</c> and never checked
/// death or UI overlay state. Inventory/stations used <c>HostIsDead()</c>, so a dead (or
/// menu-open) player could still fire RPCs and apply damage/ammo changes on the host.
/// </para>
/// <para>
/// Revert guide: If combat feels over-blocked after a UI change, start here — then check
/// callers of <see cref="BlocksLocalWorldActions"/> / <see cref="BlocksHostWorldActions"/>.
/// </para>
/// </summary>
public static class ThornsPlayerActionGate
{
	/// <summary>
	/// Local-owner early-out before sending fire/use intents or playing attack presentation.
	/// Includes UI overlays (inventory, containers, etc.) because menus also consume Attack1.
	/// </summary>
	public static bool BlocksLocalWorldActions( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() )
			return true;

		// UI overlays: inventory drag also listens for Attack1 — combat must not share that press.
		if ( ThornsUiInputGate.BlocksGameplayInput )
			return true;

		if ( IsDeadPawn( pawnRoot ) )
			return true;

		return false;
	}

	/// <summary>
	/// Host-authoritative reject for fire / combat mutations.
	/// Death is host-synced; UI-open is NOT replicated — local gate must stop the RPC,
	/// and host still refuses if the pawn is dead (exploit / race protection).
	/// </summary>
	public static bool BlocksHostWorldActions( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() )
			return true;

		return IsDeadPawn( pawnRoot );
	}

	/// <summary>True when health says the pawn is dead or HP is effectively zero.</summary>
	public static bool IsDeadPawn( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() )
			return true;

		var health = pawnRoot.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelf );
		if ( !health.IsValid() )
			return false;

		return !health.IsAlive || health.IsDeadState;
	}

	/// <summary>
	/// Spawn-protection window after respawn (host remaining seconds).
	/// Uses float Relative rather than TimeUntil-as-bool so intent stays obvious when debugging.
	/// </summary>
	public static bool HostHasSpawnDamageProtection( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() )
			return false;

		var health = pawnRoot.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelf );
		return health.IsValid() && health.HostHasSpawnDamageProtection;
	}
}
