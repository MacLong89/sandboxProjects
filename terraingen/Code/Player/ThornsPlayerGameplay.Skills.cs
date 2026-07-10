namespace Terraingen.Player;

using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;

/// <summary>Skill progression and UI sync (extracted module).</summary>
public sealed partial class ThornsPlayerGameplay
{
	void HostRebuildSkills()
	{
		_skills = new ThornsSkillsSnapshotDto
		{
			ActiveCategory = ThornsSkillCategory.Persistence,
			PlayerLevel = _playerLevel,
			TotalXp = _totalXp,
			SelectedSkillId = "hydration"
		};
		foreach ( var skill in ThornsDefinitionRegistry.AllSkills.Values )
			_skills.Ranks.Add( new ThornsSkillRankDto { SkillId = skill.Id, Rank = 0 } );

		HostRecalculateUpgradePoints();
	}

	public void RequestSkillUnlock( string skillId )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcSkillUnlock( skillId );
		else
			HostSkillUnlock( skillId );
	}

	[Rpc.Host]
	void RpcSkillUnlock( string skillId )
	{
		if ( !ValidateCaller() )
			return;

		HostSkillUnlock( skillId );
	}

	void HostEnsureSkillsRanks()
	{
		_skills ??= new ThornsSkillsSnapshotDto();
		_skills.Ranks ??= new List<ThornsSkillRankDto>();
	}

	public void HostSkillUnlock( string skillId )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		HostEnsureSkillsRanks();

		var def = ThornsDefinitionRegistry.GetSkill( skillId );
		if ( def is null )
			return;

		var rank = _skills.Ranks.FirstOrDefault( r => r.SkillId == skillId );
		if ( rank is null )
			return;

		if ( rank.Rank >= def.MaxRank )
			return;

		if ( !string.IsNullOrWhiteSpace( def.PrerequisiteSkillId ) )
		{
			var pre = _skills.Ranks.FirstOrDefault( r =>
				string.Equals( r.SkillId, def.PrerequisiteSkillId, StringComparison.OrdinalIgnoreCase ) );
			if ( (pre?.Rank ?? 0) < 1 )
				return;
		}

		var cost = ThornsUpgradeBalance.NextPurchaseCost( def, rank.Rank );
		if ( _skills.AvailablePoints < cost )
			return;

		rank.Rank++;
		_skills.SpentPoints += cost;
		HostRecalculateUpgradePoints();
		HostApplySurvivalCaps();
		PlayOwnerSfx( ThornsGameplaySfx.SkillUpgrade );
		PushSkillsToOwner();
		HostRefreshVitals( forceShowHealth: false );
	}

	void HostRecalculateUpgradePoints()
	{
		HostEnsureSkillsRanks();
		_skills.PlayerLevel = _playerLevel;
		_skills.TotalXp = _totalXp;
		_skills.AvailablePoints = ThornsUpgradeBalance.TotalPointsForLevel( _playerLevel ) - _skills.SpentPoints;
	}

	void PushSkillsToOwner()
	{
		if ( !CanPushOwnerRpcs() )
			return;

		if ( !Networking.IsActive )
		{
			ThornsUiClientState.ApplyPartialSkills( _skills );
			return;
		}

		RpcSyncSkillsJson( Json.Serialize( _skills ) );
	}

	[Rpc.Owner]
	void RpcSyncSkillsJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsSkillsSnapshotDto skills ) )
			return;

		ThornsUiClientState.ApplyPartialSkills( skills );
	}

	public void SetSkillsUiState( ThornsSkillCategory? category, string selectedSkillId )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
		{
			RpcSetSkillsUiState( category.HasValue ? (byte)category.Value : byte.MaxValue, selectedSkillId ?? "" );
			return;
		}

		HostSetSkillsUiState( category, selectedSkillId );
	}

	[Rpc.Host]
	void RpcSetSkillsUiState( byte categoryByte, string selectedSkillId )
	{
		if ( !ValidateCaller() )
			return;

		var category = categoryByte == byte.MaxValue ? (ThornsSkillCategory?)null : (ThornsSkillCategory)categoryByte;
		HostSetSkillsUiState( category, selectedSkillId );
	}

	void HostSetSkillsUiState( ThornsSkillCategory? category, string selectedSkillId )
	{
		if ( category.HasValue )
			_skills.ActiveCategory = category.Value;
		if ( !string.IsNullOrEmpty( selectedSkillId ) )
			_skills.SelectedSkillId = selectedSkillId;
		PushSkillsToOwner();
	}
}
