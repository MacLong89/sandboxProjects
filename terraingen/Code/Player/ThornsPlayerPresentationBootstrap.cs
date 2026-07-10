namespace Terraingen.Player;

using Sandbox;
using Terraingen.Buildings;

/// <summary>Hooks viewmodels + ADS FOV onto the stock player prefab camera rig.</summary>
public static class ThornsPlayerPresentationBootstrap
{
	public static void EnsureFirstPersonPresentation( GameObject player )
	{
		if ( player is null || !player.IsValid() )
			return;

		ThornsFpDebug.ApplyToWeaponResourceLoad();

		var locomotion = player.Components.Get<ThornsPlayerLocomotion>() ?? player.Components.Create<ThornsPlayerLocomotion>();
		locomotion.ConfigurePlayerController();

		ThornsPlayerFirstPersonRig.EnsurePresentationComponents( player );
		_ = player.Components.Get<ThornsFpPresentation>() ?? player.Components.Create<ThornsFpPresentation>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerWeaponCombat>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerWeaponCombat>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerBowCombat>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerBowCombat>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerTreeChopUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerTreeChopUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerResourceSalvageUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerResourceSalvageUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerBloomSeedUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerBloomSeedUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerContainerUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerContainerUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerCraftStationUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerCraftStationUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerCampfireUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerCampfireUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerWorkbenchUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerWorkbenchUse>();
		_ = player.Components.Get<Terraingen.Combat.ThornsPlayerResearchStationUse>() ?? player.Components.Create<Terraingen.Combat.ThornsPlayerResearchStationUse>();
		_ = player.Components.Get<ThornsFootstepAudio>() ?? player.Components.Create<ThornsFootstepAudio>();
		_ = player.Components.Get<ThornsPlayerBuildingController>() ?? player.Components.Create<ThornsPlayerBuildingController>();
		_ = player.Components.Get<ThornsTerrainWaterMoveMode>() ?? player.Components.Create<ThornsTerrainWaterMoveMode>();

		ThornsPlayerFirstPersonRig.ApplyLocalOwnerPresentation( player );
		ThornsCitizenRig.EnsureRemotePlayerThirdPersonPresentation( player );
	}

	/// <summary>First-person camera + locomotion only (no weapons / viewmodels).</summary>
	public static void EnsureLightingTestExploration( GameObject player )
	{
		if ( player is null || !player.IsValid() )
			return;

		var locomotion = player.Components.Get<ThornsPlayerLocomotion>() ?? player.Components.Create<ThornsPlayerLocomotion>();
		locomotion.ConfigurePlayerController();

		ThornsPlayerFirstPersonRig.EnsurePresentationComponents( player );
		DisableWeaponPresentation( player );
		_ = player.Components.Get<ThornsFootstepAudio>() ?? player.Components.Create<ThornsFootstepAudio>();
		_ = player.Components.Get<ThornsTerrainWaterMoveMode>() ?? player.Components.Create<ThornsTerrainWaterMoveMode>();

		ThornsPlayerFirstPersonRig.ApplyLocalOwnerPresentation( player );
		ThornsCitizenRig.EnsureRemotePlayerThirdPersonPresentation( player );
	}

	static void DisableWeaponPresentation( GameObject player )
	{
		foreach ( var fp in player.Components.GetAll<ThornsFpPresentation>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( fp is { IsValid: true } )
				fp.Enabled = false;
		}

		foreach ( var combat in player.Components.GetAll<Terraingen.Combat.ThornsPlayerWeaponCombat>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( combat is { IsValid: true } )
				combat.Enabled = false;
		}

		var rig = ThornsPlayerFirstPersonRig.ResolvePresentationCameraObject( player );
		if ( !rig.IsValid() )
			return;

		foreach ( var vmc in rig.Components.GetAll<ThornsViewModelController>( FindMode.EverythingInSelf ) )
		{
			if ( vmc is not { IsValid: true } )
				continue;

			vmc.ClearViewModel();
			vmc.Enabled = false;
		}
	}
}
