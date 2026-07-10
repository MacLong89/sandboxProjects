namespace Sandbox;

/// <summary>
/// Host-driven journal goals — parallel progress per definition row; XP when a row completes.
/// Legacy <see cref="ActiveMilestoneIndex"/> / <see cref="ActiveProgress"/> are migrated into
/// <see cref="MilestoneProgressPacked"/> on first host mutation.
/// </summary>
[Title( "Thorns — Milestones (player)" )]
[Category( "Thorns" )]
[Icon( "flag" )]
[Order( 47 )]
public sealed class ThornsPlayerMilestones : Component
{
	/// <summary>Serialized parallel progress (replicated; preferred source of truth once non-empty).</summary>
	[Sync( SyncFlags.FromHost )] public string MilestoneProgressPacked { get; set; } = "";

	/// <summary>
	/// Legacy linear chain (disk + pre-packed migration only). Not replicated — clients use <see cref="MilestoneProgressPacked"/> after host materializes
	/// (see <see cref="HostMaterializePackedFromLegacyIfNeeded"/> on restore / first mutation).
	/// </summary>
	public int ActiveMilestoneIndex { get; set; }

	/// <summary>Legacy progress on the active row; not replicated (see <see cref="ActiveMilestoneIndex"/>).</summary>
	public int ActiveProgress { get; set; }

	public bool MilestoneChainFinished
	{
		get
		{
			var pr = GetGoalProgressSnapshot();
			for ( var i = 0; i < pr.Length; i++ )
			{
				if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) )
					continue;
				if ( pr[i] < def.TargetValue )
					return false;
			}

			return ThornsMilestoneDefinitions.Count > 0;
		}
	}

	public int[] GetGoalProgressSnapshot()
	{
		var n = ThornsMilestoneDefinitions.Count;
		if ( !string.IsNullOrEmpty( MilestoneProgressPacked ) )
			return ThornsMilestoneProgressCodec.ParsePacked( MilestoneProgressPacked, n );

		return BuildLegacyProgressArray( n );
	}

	public int ClientFirstIncompleteGoalIndex()
	{
		var pr = GetGoalProgressSnapshot();
		for ( var i = 0; i < pr.Length; i++ )
		{
			if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) )
				continue;
			if ( pr[i] < def.TargetValue )
				return i;
		}

		return -1;
	}

	public int ClientCompletedGoalsCount()
	{
		var pr = GetGoalProgressSnapshot();
		var c = 0;
		for ( var i = 0; i < pr.Length; i++ )
		{
			if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) )
				continue;
			if ( pr[i] >= def.TargetValue )
				c++;
		}

		return c;
	}

	public bool ClientIsGoalComplete( int index )
	{
		if ( !ThornsMilestoneDefinitions.TryGet( index, out var def ) )
			return false;

		var pr = GetGoalProgressSnapshot();
		if ( index < 0 || index >= pr.Length )
			return false;

		return pr[index] >= def.TargetValue;
	}

	int[] BuildLegacyProgressArray( int n )
	{
		var arr = new int[n];
		if ( n <= 0 )
			return arr;

		if ( ActiveMilestoneIndex >= n )
		{
			for ( var i = 0; i < n; i++ )
			{
				if ( ThornsMilestoneDefinitions.TryGet( i, out var d ) )
					arr[i] = d.TargetValue;
			}

			return arr;
		}

		for ( var i = 0; i < n; i++ )
		{
			if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) )
				break;

			if ( i < ActiveMilestoneIndex )
				arr[i] = def.TargetValue;
			else if ( i == ActiveMilestoneIndex )
				arr[i] = Math.Clamp( ActiveProgress, 0, def.TargetValue );
			else
				arr[i] = 0;
		}

		return arr;
	}

	/// <summary>Host: fills <see cref="MilestoneProgressPacked"/> from legacy indices when packed is still empty (JIP restore / old saves).</summary>
	public void HostMaterializePackedFromLegacyIfNeeded()
	{
		if ( !Networking.IsHost )
			return;

		if ( !string.IsNullOrEmpty( MilestoneProgressPacked ) )
			return;

		MilestoneProgressPacked =
			ThornsMilestoneProgressCodec.Serialize( BuildLegacyProgressArray( ThornsMilestoneDefinitions.Count ) );
	}

	void HostWriteProgressArray( int[] arr )
	{
		if ( !Networking.IsHost || arr is null )
			return;

		MilestoneProgressPacked = ThornsMilestoneProgressCodec.Serialize( arr );
	}

	void HostApplyDelta( int goalIndex, int delta, ThornsMilestoneDefinition def )
	{
		if ( !Networking.IsHost || delta <= 0 )
			return;

		HostMaterializePackedFromLegacyIfNeeded();
		var n = ThornsMilestoneDefinitions.Count;
		var arr = ThornsMilestoneProgressCodec.ParsePacked( MilestoneProgressPacked, n );
		if ( goalIndex < 0 || goalIndex >= arr.Length )
			return;

		var oldP = arr[goalIndex];
		if ( oldP >= def.TargetValue )
			return;

		var newP = Math.Min( def.TargetValue, oldP + delta );
		arr[goalIndex] = newP;
		HostWriteProgressArray( arr );

		if ( oldP < def.TargetValue && newP >= def.TargetValue )
		{
			var vitals = Components.Get<ThornsVitals>();
			if ( vitals.IsValid() && def.RewardXp > 0 )
				vitals.AddXp( def.RewardXp );

			Log.Info(
				$"[Thorns] Journal goal complete id={def.Id} rewardXp={def.RewardXp} pawn='{GameObject.Name}'" );
		}
	}

	/// <summary>Host-only: resource quantity accepted into inventory (harvest / craft / loot) for <see cref="ThornsItemType.Resource"/>.</summary>
	public void HostRecordResourceCollected( string itemId, int quantity )
	{
		if ( !Networking.IsHost || quantity <= 0 )
			return;

		HostMaterializePackedFromLegacyIfNeeded();
		var remaining = quantity;
		var n = ThornsMilestoneDefinitions.Count;

		for ( var pass = 0; pass < n && remaining > 0; pass++ )
		{
			for ( var i = 0; i < n && remaining > 0; i++ )
			{
				if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) || def.Kind != ThornsMilestoneKind.Collect )
					continue;

				if ( string.IsNullOrEmpty( def.CollectItemId )
				     || !string.Equals( def.CollectItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
					continue;

				var arr = ThornsMilestoneProgressCodec.ParsePacked( MilestoneProgressPacked, n );
				if ( arr[i] >= def.TargetValue )
					continue;

				var need = def.TargetValue - arr[i];
				var take = Math.Min( remaining, need );
				if ( take <= 0 )
					continue;

				HostApplyDelta( i, take, def );
				remaining -= take;
			}
		}
	}

	/// <summary>Host-only: successful structure spawn from building controller.</summary>
	public void HostRecordStructurePlaced( string structureDefId )
	{
		if ( !Networking.IsHost )
			return;

		HostMaterializePackedFromLegacyIfNeeded();
		var n = ThornsMilestoneDefinitions.Count;

		for ( var i = 0; i < n; i++ )
		{
			if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) || def.Kind != ThornsMilestoneKind.Build )
				continue;

			var arr = ThornsMilestoneProgressCodec.ParsePacked( MilestoneProgressPacked, n );
			if ( arr[i] >= def.TargetValue )
				continue;

			if ( !string.IsNullOrEmpty( def.BuildStructureDefId )
			     && !string.Equals( def.BuildStructureDefId, structureDefId, StringComparison.OrdinalIgnoreCase ) )
				continue;

			HostApplyDelta( i, 1, def );
		}
	}

	/// <summary>Host-only: lethal blow — killer attribution from <see cref="DamageContext"/>.</summary>
	public void HostRecordKill( GameObject victimRoot )
	{
		if ( !Networking.IsHost || !victimRoot.IsValid() )
			return;

		HostMaterializePackedFromLegacyIfNeeded();
		var n = ThornsMilestoneDefinitions.Count;

		for ( var i = 0; i < n; i++ )
		{
			if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) || def.Kind != ThornsMilestoneKind.Kill )
				continue;

			if ( !VictimMatchesKillFilter( victimRoot, def.KillFilter ) )
				continue;

			var arr = ThornsMilestoneProgressCodec.ParsePacked( MilestoneProgressPacked, n );
			if ( arr[i] >= def.TargetValue )
				continue;

			HostApplyDelta( i, 1, def );
		}
	}

	/// <summary>Host-only: call from tame subsystem when a tame succeeds (no client shortcut).</summary>
	public void HostRecordTameCompleted( int count = 1 )
	{
		if ( !Networking.IsHost || count <= 0 )
			return;

		HostMaterializePackedFromLegacyIfNeeded();
		var remaining = count;
		var n = ThornsMilestoneDefinitions.Count;

		while ( remaining > 0 )
		{
			var progressed = false;
			for ( var i = 0; i < n && remaining > 0; i++ )
			{
				if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) || def.Kind != ThornsMilestoneKind.Tame )
					continue;

				var arr = ThornsMilestoneProgressCodec.ParsePacked( MilestoneProgressPacked, n );
				if ( arr[i] >= def.TargetValue )
					continue;

				var need = def.TargetValue - arr[i];
				var take = Math.Min( remaining, need );
				HostApplyDelta( i, take, def );
				remaining -= take;
				progressed = true;
				if ( remaining <= 0 )
					break;
			}

			if ( !progressed )
				break;
		}
	}

	/// <summary>Host-only: one-shot journal events (Tab opened, crate looted, etc.).</summary>
	public void HostRecordEvent( string eventToken, int count = 1 )
	{
		if ( !Networking.IsHost || string.IsNullOrWhiteSpace( eventToken ) || count <= 0 )
			return;

		HostMaterializePackedFromLegacyIfNeeded();
		var remaining = count;
		var n = ThornsMilestoneDefinitions.Count;

		while ( remaining > 0 )
		{
			var progressed = false;
			for ( var i = 0; i < n && remaining > 0; i++ )
			{
				if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) || def.Kind != ThornsMilestoneKind.Event )
					continue;

				if ( string.IsNullOrEmpty( def.EventToken )
				     || !string.Equals( def.EventToken, eventToken, StringComparison.OrdinalIgnoreCase ) )
					continue;

				var arr = ThornsMilestoneProgressCodec.ParsePacked( MilestoneProgressPacked, n );
				if ( arr[i] >= def.TargetValue )
					continue;

				var need = def.TargetValue - arr[i];
				var take = Math.Min( remaining, need );
				HostApplyDelta( i, take, def );
				remaining -= take;
				progressed = true;
				if ( remaining <= 0 )
					break;
			}

			if ( !progressed )
				break;
		}
	}

	/// <summary>Owner client → host for UI-driven milestones (Tab menu, etc.).</summary>
	[Rpc.Host]
	public void RpcHostRecordClientEvent( string eventToken )
	{
		if ( string.IsNullOrWhiteSpace( eventToken ) )
			return;

		if ( !ThornsPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		HostRecordEvent( eventToken );
	}

	/// <summary>Record an event from owner UI or host gameplay code.</summary>
	public void ClientOrHostRecordEvent( string eventToken )
	{
		if ( !Game.IsPlaying || string.IsNullOrWhiteSpace( eventToken ) )
			return;

		if ( Networking.IsHost )
			HostRecordEvent( eventToken );
		else
			RpcHostRecordClientEvent( eventToken );
	}

	/// <summary>Host-only: validated craft at bench (one successful recipe resolution).</summary>
	public void HostRecordRecipeCrafted( string recipeId )
	{
		if ( !Networking.IsHost || string.IsNullOrWhiteSpace( recipeId ) )
			return;

		HostMaterializePackedFromLegacyIfNeeded();
		var n = ThornsMilestoneDefinitions.Count;

		for ( var i = 0; i < n; i++ )
		{
			if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) || def.Kind != ThornsMilestoneKind.Craft )
				continue;

			if ( string.IsNullOrEmpty( def.CraftRecipeId )
			     || !string.Equals( def.CraftRecipeId, recipeId, StringComparison.OrdinalIgnoreCase ) )
				continue;

			var arr = ThornsMilestoneProgressCodec.ParsePacked( MilestoneProgressPacked, n );
			if ( arr[i] >= def.TargetValue )
				continue;

			HostApplyDelta( i, 1, def );
		}
	}

	static bool VictimMatchesKillFilter( GameObject victimRoot, ThornsMilestoneKillFilter filter )
	{
		var hasPlayerPawn = victimRoot.Components.Get<ThornsPawn>( FindMode.EnabledInSelf ).IsValid();
		var hasBandit = victimRoot.Components.Get<ThornsBanditBrain>( FindMode.EnabledInSelf ).IsValid();
		var hasWildlife =
			victimRoot.Components.GetInAncestorsOrSelf<ThornsWildlifeIdentity>( true ).IsValid();

		return filter switch
		{
			ThornsMilestoneKillFilter.Bandit => hasBandit,
			ThornsMilestoneKillFilter.NonPlayer => !hasPlayerPawn,
			ThornsMilestoneKillFilter.Any => true,
			ThornsMilestoneKillFilter.Wildlife => !hasPlayerPawn && !hasBandit && hasWildlife,
			_ => false
		};
	}
}
