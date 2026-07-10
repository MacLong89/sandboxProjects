namespace Sandbox;

public static class ThornsHotTipEvaluator
{
	public static bool TryPick(
		ThornsHotTipContext ctx,
		ThornsHotTipMemory memory,
		double now,
		out ThornsHotTipDefinition picked )
	{
		picked = default;
		ThornsHotTipRule? best = null;
		var bestPri = int.MinValue;

		foreach ( var rule in ThornsHotTipRegistry.AllRules )
		{
			var def = rule.Definition;

			if ( def.NewPlayerEnhanced && !ctx.NewPlayerWindow )
				continue;

			if ( def.MinLookSeconds > 0.001f )
			{
				var lookOk = def.Id switch
				{
					ThornsHotTipIds.PunchTrees or ThornsHotTipIds.PunchRocks or ThornsHotTipIds.NeedPickaxeOre
						=> ctx.LookResourceSeconds >= def.MinLookSeconds,
					ThornsHotTipIds.LootCrateE => ctx.LookLootCrateSeconds >= def.MinLookSeconds,
					ThornsHotTipIds.WeakenBeforeTame or ThornsHotTipIds.HoldETame
						=> ctx.LookTameWildlifeSeconds >= def.MinLookSeconds,
					_ => true
				};

				if ( !lookOk )
					continue;
			}

			if ( !rule.Condition( ctx ) )
				continue;

			if ( def.Priority <= bestPri )
				continue;

			bestPri = def.Priority;
			best = rule;
		}

		if ( best is null )
			return false;

		picked = best.Value.Definition;
		return memory.TryBeginShow( picked, now );
	}
}
