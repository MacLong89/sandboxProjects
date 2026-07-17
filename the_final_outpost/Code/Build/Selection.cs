namespace FinalOutpost;

public enum SelectActionKind { Primary, Normal, Warn }

public sealed class SelectAction
{
	public string Label { get; init; }
	public string Detail { get; init; }
	public bool Enabled { get; init; } = true;
	public SelectActionKind Kind { get; init; } = SelectActionKind.Normal;
	public Action Invoke { get; init; }
}

public sealed class StatLine
{
	public string Label { get; }
	public string Value { get; }
	public StatLine( string label, string value ) { Label = label; Value = value; }
}

/// <summary>Anything in the base the player can click to inspect and act on.</summary>
public interface ISelectable
{
	string Name { get; }
	string Icon { get; }
	string Subtitle { get; }
	bool HasHealth { get; }
	float Health { get; }
	float MaxHealth { get; }
	Vector3 WorldPos { get; }
	float SelectRadius { get; }
	bool IsAlive { get; }
	IReadOnlyList<StatLine> Stats { get; }
	IReadOnlyList<SelectAction> Actions { get; }
}

public static class SelectHelp
{
	public static string Cost( double v ) => GameConstants.FormatScrap( v ).Replace( " scrap", "" );
	public static bool CanAfford( double v ) => BuildPayment.CanAfford( GameCore.Instance, v );
	/// <summary>Scrap to spend on a repair — full cost if affordable, otherwise whatever remains.</summary>
	public static double AffordableRepairSpend( double fullCost )
	{
		if ( fullCost <= 0 ) return 0;
		var scrap = GameCore.Instance?.Wallet.Scrap ?? 0;
		if ( scrap <= 0 ) return 0;
		return Math.Min( fullCost, scrap );
	}
	public static bool CanStartRepair( double fullCost ) =>
		IsDay && AffordableRepairSpend( fullCost ) > 0;
	public static int Pct( float frac ) => (int)MathF.Round( Math.Clamp( frac, 0f, 1f ) * 100f );
	public static bool IsDay => GameCore.Instance?.Phase == GamePhase.Day;
}

/// <summary>Inspect / repair / reinforce a single perimeter wall segment.</summary>
public sealed class WallSelectable : ISelectable
{
	private readonly WallSegment _w;
	public WallSelectable( WallSegment w ) => _w = w;

	public string Name => "Perimeter Scaffold";
	public string Icon => "foundation";
	public string Subtitle => "Open timber & iron — fire through the gaps";
	public bool HasHealth => true;
	public float Health => _w.Health;
	public float MaxHealth => _w.MaxHealth;
	public Vector3 WorldPos => _w.Center;
	public float SelectRadius => 58f;
	public bool IsAlive => _w is not null && _w.Go.IsValid() && !_w.IsBroken;

	public IReadOnlyList<StatLine> Stats
	{
		get
		{
			var up = GameCore.Instance?.Upgrades;
			return new List<StatLine>
			{
				new( "Integrity", $"{SelectHelp.Pct( _w.HealthFraction )}%" ),
				new( "Armor Level", $"{up?.Level( UpgradeId.WallArmor ) ?? 0}" ),
			};
		}
	}

	public IReadOnlyList<SelectAction> Actions
	{
		get
		{
			var core = GameCore.Instance;
			var up = core?.Upgrades;
			var list = new List<SelectAction>();

			var rm = RepairManager.Instance;
			if ( rm?.IsScheduled( _w ) == true )
			{
				list.Add( new SelectAction
				{
					Label = $"Repairing ({SelectHelp.Pct( rm.ProgressFor( _w ) )}%)",
					Enabled = false,
					Invoke = () => { }
				} );
			}
			else if ( _w.Health < _w.MaxHealth )
			{
				var repairCost = (_w.MaxHealth - _w.Health) * (float)GameConstants.RepairCostPerHp;
				var spend = SelectHelp.AffordableRepairSpend( repairCost );
				list.Add( new SelectAction
				{
					Label = spend > 0 && spend + 0.001 < repairCost ? "Partial Repair" : "Repair",
					Detail = SelectHelp.Cost( spend > 0 ? spend : repairCost ),
					Enabled = SelectHelp.CanStartRepair( repairCost ),
					Invoke = () => BuildManager.Instance?.TryRepairWall( _w )
				} );
			}

			var maxed = up?.IsMaxed( UpgradeId.WallArmor ) ?? true;
			var cost = up?.NextCost( UpgradeId.WallArmor ) ?? double.PositiveInfinity;
			list.Add( new SelectAction
			{
				Label = maxed ? "Armor Maxed" : "Reinforce All Walls",
				Detail = maxed ? null : SelectHelp.Cost( cost ),
				Kind = SelectActionKind.Primary,
				Enabled = !maxed && SelectHelp.CanAfford( cost ),
				Invoke = () => core?.BuyUpgrade( UpgradeId.WallArmor )
			} );

			list.Add( new SelectAction
			{
				Label = "Tear Down",
				Detail = "Opens a gap",
				Kind = SelectActionKind.Warn,
				Enabled = true,
				Invoke = () => OutpostManager.Instance?.RemoveWall( _w )
			} );

			return list;
		}
	}
}

/// <summary>Inspect / repair / fortify the command post.</summary>
public sealed class CoreSelectable : ISelectable
{
	private readonly OutpostManager _o;
	public CoreSelectable( OutpostManager o ) => _o = o;

	public string Name => "Command Post";
	public string Icon => "shield";
	public string Subtitle => "Losing this ends the run";
	public bool HasHealth => true;
	public float Health => _o.CoreHealth;
	public float MaxHealth => _o.CoreMaxHealth;
	public Vector3 WorldPos => _o.CorePosition;
	public float SelectRadius => 100f;
	public bool IsAlive => _o is not null && _o.CoreMaxHealth > 0f;

	public IReadOnlyList<StatLine> Stats
	{
		get
		{
			var up = GameCore.Instance?.Upgrades;
			var frac = _o.CoreMaxHealth <= 0f ? 0f : _o.CoreHealth / _o.CoreMaxHealth;
			var list = new List<StatLine>
			{
				new( "Integrity", $"{SelectHelp.Pct( frac )}%" ),
			};
			if ( GameCore.Instance?.IsCure == true )
				list.Add( new StatLine( "Scrap income", $"+{CureConstants.CommandPostScrapPerSec:0.##}/s" ) );
				list.Add( new StatLine( "Colony upkeep", $"-{ColonyEconomy.ScrapUpkeepPerSec():0.##}/s" ) );
			list.Add( new StatLine( "Fortify Level", $"{up?.Level( UpgradeId.FortifyCore ) ?? 0}" ) );
			return list;
		}
	}

	public IReadOnlyList<SelectAction> Actions
	{
		get
		{
			var core = GameCore.Instance;
			var up = core?.Upgrades;
			var list = new List<SelectAction>();

			var rm = RepairManager.Instance;
			if ( rm?.IsScheduledCore() == true )
			{
				list.Add( new SelectAction
				{
					Label = $"Repairing ({SelectHelp.Pct( rm.ProgressForCore() )}%)",
					Enabled = false,
					Invoke = () => { }
				} );
			}
			else if ( _o.CoreHealth < _o.CoreMaxHealth )
			{
				var repairCost = (_o.CoreMaxHealth - _o.CoreHealth) * (float)GameConstants.RepairCostPerHp;
				var spend = SelectHelp.AffordableRepairSpend( repairCost );
				list.Add( new SelectAction
				{
					Label = spend > 0 && spend + 0.001 < repairCost ? "Partial Repair" : "Repair",
					Detail = SelectHelp.Cost( spend > 0 ? spend : repairCost ),
					Enabled = SelectHelp.CanStartRepair( repairCost ),
					Invoke = () => BuildManager.Instance?.TryRepairCore()
				} );
			}

			var maxed = up?.IsMaxed( UpgradeId.FortifyCore ) ?? true;
			var cost = up?.NextCost( UpgradeId.FortifyCore ) ?? double.PositiveInfinity;
			list.Add( new SelectAction
			{
				Label = maxed ? "Fortify Maxed" : "Fortify Command",
				Detail = maxed ? null : SelectHelp.Cost( cost ),
				Kind = SelectActionKind.Primary,
				Enabled = !maxed && SelectHelp.CanAfford( cost ),
				Invoke = () => core?.BuyUpgrade( UpgradeId.FortifyCore )
			} );

			return list;
		}
	}
}

/// <summary>Inspect / claim a surrounding land plot and staff it with foragers.</summary>
public sealed class PlotSelectable : ISelectable
{
	private readonly int _x;
	private readonly int _y;
	public PlotSelectable( int x, int y ) { _x = x; _y = y; }

	private ResourceKind Kind => PlotGrid.HarvestResourceAt( _x, _y );
	private PlotKind Feature => PlotGrid.FeatureKindAt( _x, _y );
	private bool Owned => PlotManager.Instance?.IsOwned( _x, _y ) == true;
	private bool Buyable => PlotManager.Instance?.IsBuyable( _x, _y ) == true;
	private bool Cleared => PlotManager.Instance?.IsCleared( _x, _y ) == true;
	private bool IsCiv => Feature == PlotKind.NeutralCiv;
	private bool IsRival => RivalCivManager.IsRivalOwned( GameCore.Instance?.Save, _x, _y );
	private BossKind Boss => PlotWorldRolls.BossAt( _x, _y );
	private PlotBoostKind Boost => PlotWorldRolls.BoostAt( _x, _y );

	public string Name
	{
		get
		{
			if ( Boss != BossKind.None && !Cleared )
				return PlotWorldRolls.GetBoss( Boss ).Name;
			if ( Boost != PlotBoostKind.None && !Cleared && !PlotBoosts.IsClaimed( GameCore.Instance?.Save, _x, _y ) )
				return PlotWorldRolls.GetBoost( Boost ).Name;
			if ( RivalCivManager.IsSeedPlot( _x, _y ) && !Owned )
				return "Rival Command Post";
			if ( IsCiv && Owned ) return PlotFeatureCatalog.Get( Feature ).Name;
			if ( Cleared ) return "Cleared Plot";
			if ( Owned ) return $"{ResourceInfo.Name( Kind )} Plot";
			if ( Feature != PlotKind.Standard ) return PlotFeatureCatalog.Get( Feature ).Name;
			return "Unclaimed Plot";
		}
	}

	public string Icon
	{
		get
		{
			if ( Boss != BossKind.None && !Cleared ) return PlotWorldRolls.GetBoss( Boss ).Icon;
			if ( Boost != PlotBoostKind.None && !Cleared ) return PlotWorldRolls.GetBoost( Boost ).Icon;
			if ( RivalCivManager.IsSeedPlot( _x, _y ) && !Owned ) return "flag";
			if ( Feature != PlotKind.Standard && !Cleared ) return PlotFeatureCatalog.Get( Feature ).Icon;
			if ( Cleared ) return "grid_view";
			return ResourceInfo.Icon( Kind );
		}
	}

	public string Subtitle
	{
		get
		{
			if ( IsRival && !Owned )
				return "Rival territory — claim at double cost";
			if ( RivalCivManager.IsSeedPlot( _x, _y ) && !Owned )
				return "Enemy colony — expands each season";
			if ( Boss != BossKind.None && !Cleared )
				return PlotWorldRolls.GetBoss( Boss ).Description;
			if ( Boost != PlotBoostKind.None && !Cleared )
				return PlotWorldRolls.GetBoost( Boost ).Description;
			if ( IsCiv && Owned )
			{
				var save = GameCore.Instance?.Save;
				if ( PlotCivActions.IsRaided( save, _x, _y ) ) return "Raided colony";
				if ( PlotCivActions.IsAllied( save, _x, _y ) ) return "Allied colony";
				return "Neighboring colony";
			}
			if ( Cleared ) return "Buildable land";
			if ( Owned ) return "Claimed territory";
			if ( Feature != PlotKind.Standard ) return PlotFeatureCatalog.Get( Feature ).Description;
			return $"Ring {PlotGrid.Ring( _x, _y )} · frontier";
		}
	}
	public bool HasHealth => false;
	public float Health => 0f;
	public float MaxHealth => 0f;
	public Vector3 WorldPos => PlotGrid.CenterWorld( _x, _y );
	public float SelectRadius => GameConstants.PlotSize * 0.5f - 30f;
	public bool IsAlive => PlotGrid.InGrid( _x, _y ) && !PlotGrid.IsHome( _x, _y );

	public IReadOnlyList<StatLine> Stats
	{
		get
		{
			var wm = WorkerManager.Instance;

			if ( Cleared )
			{
				return new List<StatLine>
				{
					new( "Land", "Cleared" ),
					new( "Status", "Ready to build" ),
				};
			}

			var list = new List<StatLine> { new( "Resource", ResourceInfo.Name( Kind ) ) };

			if ( Owned )
			{
				var foragers = wm?.CountForagersOn( _x, _y ) ?? 0;
				var perMin = foragers * GameConstants.ForagerHarvestPerSec * 60f;
				var clearPct = SelectHelp.Pct( PlotManager.Instance?.ClearFraction( _x, _y ) ?? 0f );
				list.Add( new StatLine( "Foragers", $"{foragers}" ) );
				list.Add( new StatLine( "Yield", $"{perMin:0}/min" ) );
				list.Add( new StatLine( "Cleared", $"{clearPct}%" ) );
			}
			else
			{
				var mult = RivalCivManager.InvasionCostMult( GameCore.Instance?.Save, _x, _y );
				var cost = PlotGrid.BuyCostEffective( _x, _y ) * mult;
				list.Add( new StatLine( "Claim Cost", SelectHelp.Cost( cost ) ) );
				if ( mult > 1 ) list.Add( new StatLine( "Note", "Rival invasion (2×)" ) );
			}

			return list;
		}
	}

	public IReadOnlyList<SelectAction> Actions
	{
		get
		{
			var list = new List<SelectAction>();
			var wm = WorkerManager.Instance;

			if ( !Owned )
			{
				var mult = RivalCivManager.InvasionCostMult( GameCore.Instance?.Save, _x, _y );
				var cost = PlotGrid.BuyCostEffective( _x, _y ) * mult;
				list.Add( new SelectAction
				{
					Label = Buyable ? (mult > 1 ? "Invade Plot" : "Claim Plot") : "Not Reachable",
					Detail = Buyable ? SelectHelp.Cost( cost ) : null,
					Kind = SelectActionKind.Primary,
					Enabled = Buyable && SelectHelp.CanAfford( cost ),
					Invoke = () => PlotManager.Instance?.TryBuyPlot( _x, _y )
				} );
				return list;
			}

			// Foragers both harvest and clear the land. Once cleared there's nothing left to gather,
			// so we only offer to recall any leftover workers off the (now buildable) plot.
			if ( !Cleared && Kind != ResourceKind.None )
			{
				var hireCost = WorkerInfo.HireCost( WorkerRole.Forager );
				var full = (wm?.Count ?? 0) >= GameConstants.MaxWorkers;
				var foragerUnlocked = NightUnlocks.IsWorkerUnlocked( GameCore.Instance?.Save, WorkerRole.Forager );
				// AUDIT FIX M3: Cure used Survival "Night N" wording for locked foragers.
				var foragerLockLabel = GameCore.Instance?.IsCure == true
					? $"Forager ({CureUnlocks.WorkerUnlockLabel( GameCore.Instance?.Save, WorkerRole.Forager )})"
					: $"Forager (Night {WorkerInfo.UnlockNight( WorkerRole.Forager )})";
				list.Add( new SelectAction
				{
					Label = !foragerUnlocked ? foragerLockLabel : full ? "Worker Cap Reached" : "Hire Forager",
					Detail = full || !foragerUnlocked ? null : SelectHelp.Cost( hireCost ),
					Kind = SelectActionKind.Primary,
					Enabled = foragerUnlocked && !full && SelectHelp.CanAfford( hireCost ),
					Invoke = () => wm?.TryHireForager( _x, _y )
				} );
			}

			if ( (wm?.CountForagersOn( _x, _y ) ?? 0) > 0 )
			{
				list.Add( new SelectAction
				{
					Label = "Recall Forager",
					Detail = $"+{SelectHelp.Cost( WorkerInfo.HireCost( WorkerRole.Forager ) * GameConstants.SellRefundFraction )}",
					Kind = SelectActionKind.Warn,
					Invoke = () => wm?.RecallForager( _x, _y )
				} );
			}

			if ( GameCore.Instance?.IsCure == true && Owned && PlotCivActions.CanInteract( GameCore.Instance.Save, _x, _y ) )
			{
				list.Add( new SelectAction
				{
					Label = "Trade (20 Food)",
					Detail = "+Scrap",
					Kind = SelectActionKind.Primary,
					Enabled = GameCore.Instance.Resources.Get( ResourceKind.Food ) >= 20,
					Invoke = () => PlotCivActions.TryTrade( GameCore.Instance, _x, _y )
				} );

				if ( !PlotCivActions.IsAllied( GameCore.Instance.Save, _x, _y ) )
				{
					list.Add( new SelectAction
					{
						Label = "Form Alliance",
						Detail = "+Knowledge, +Food",
						Kind = SelectActionKind.Normal,
						Invoke = () => PlotCivActions.TryAlly( GameCore.Instance, _x, _y )
					} );
				}

				list.Add( new SelectAction
				{
					Label = "Raid Colony",
					Detail = "+Supplies, +Scrap · +Sickness",
					Kind = SelectActionKind.Warn,
					Invoke = () => PlotCivActions.TryRaid( GameCore.Instance, _x, _y )
				} );
			}

			return list;
		}
	}
}

/// <summary>Inspect / dismiss a hired non-combat worker.</summary>
public sealed class WorkerSelectable : ISelectable
{
	private readonly WorkerManager.WorkerUnit _u;
	public WorkerSelectable( WorkerManager.WorkerUnit u ) => _u = u;

	public bool Wraps( WorkerManager.WorkerUnit unit ) => ReferenceEquals( _u, unit );

	public WorkerManager.WorkerUnit TryGetUnit() => IsAlive ? _u : null;

	public string Name => WorkerInfo.Name( _u.Role );
	public string Icon => WorkerInfo.Icon( _u.Role );
	public string Subtitle => "Worker";
	public bool HasHealth => false;
	public float Health => 0f;
	public float MaxHealth => 0f;
	public Vector3 WorldPos => _u.WorldPos;
	public float SelectRadius => 32f;
	public bool IsAlive => _u is not null && _u.Go.IsValid() && WorkerManager.Instance?.Units.Contains( _u ) == true;

	public IReadOnlyList<StatLine> Stats
	{
		get
		{
			var list = new List<StatLine>();
			if ( _u.Role == WorkerRole.Forager )
			{
				list.Add( new StatLine( "Assignment", _u.HasPlot ? $"{ResourceInfo.Name( _u.PlotResource )} Plot" : "Unassigned" ) );
				list.Add( new StatLine( "Status", _u.IsWorking ? "Harvesting" : "Idle" ) );
				if ( GameCore.Instance?.IsCure == true && GameCore.Instance.Phase == GamePhase.Day )
					list.Add( new StatLine( "Orders", "Click plot to gather · click ground to move" ) );
			}
			else
			{
				list.Add( new StatLine( "Status", _u.IsWorking ? "Working" : "Idle" ) );
			}

			return list;
		}
	}

	public string Description => WorkerInfo.Blurb( _u.Role );

	public IReadOnlyList<SelectAction> Actions => new List<SelectAction>
	{
		new()
		{
			Label = "Dismiss",
			Detail = $"+{SelectHelp.Cost( WorkerInfo.HireCost( _u.Role ) * GameConstants.SellRefundFraction )}",
			Kind = SelectActionKind.Warn,
			Invoke = () => WorkerManager.Instance?.Dismiss( _u )
		}
	};
}

/// <summary>Inspect / train / dismiss a single recruited defender.</summary>
public sealed class DefenderSelectable : ISelectable
{
	private readonly DefenderManager.DefenderUnit _u;
	public DefenderSelectable( DefenderManager.DefenderUnit u ) => _u = u;

	/// <summary>True when this selectable wraps the given recruit (used to clear selection on dismiss).</summary>
	public bool Wraps( DefenderManager.DefenderUnit unit ) => ReferenceEquals( _u, unit );

	public DefenderManager.DefenderUnit TryGetUnit() => IsAlive ? _u : null;

	private RecruitWeaponDef Def => RecruitWeapons.Get( _u.Type );

	public string Name => Def.Name;
	public string Icon => Def.Icon;
	public string Subtitle => $"Recruit · {Def.ShortName}";
	public bool HasHealth => true;
	public float Health => _u.Health;
	public float MaxHealth => _u.MaxHealth;
	public Vector3 WorldPos => _u.WorldPos;
	public float SelectRadius => 32f;
	public bool IsAlive => _u is not null && _u.Go.IsValid() && DefenderManager.Instance?.Units.Contains( _u ) == true;

	public IReadOnlyList<StatLine> Stats
	{
		get
		{
			var mgr = DefenderManager.Instance;
			var def = Def;
			var trainLevel = mgr?.TrainLevelOf( _u.Type ) ?? 0;
			var volley = def.VolleyDamage( trainLevel );
			var dps = def.Dps( trainLevel );
			return new List<StatLine>
			{
				new( "Weapon", def.ShortName ),
				new( "Training", $"Lv {trainLevel}" ),
				new( "Health", $"{_u.Health:0} / {_u.MaxHealth:0}" ),
				new( "Damage", $"{volley:0}{(def.Pellets > 1 ? $" ({def.Pellets}x)" : "")}" ),
				new( "Range", $"{def.Range:0}" ),
				new( "DPS", $"{dps:0}" ),
				new( "Squad", $"{mgr?.Count ?? 0} / {BuildManager.Instance?.RecruitCapacity ?? 0}" ),
				new( "Orders", GameCore.Instance?.Phase == GamePhase.Night
					? "Auto-defending · use Command Recruits to override"
					: GameCore.Instance?.IsCure == true && GameCore.Instance.Phase == GamePhase.Day
						? "Click ground to move · attack nearby threats"
						: "Auto-defends during night threats" ),
			};
		}
	}

	public IReadOnlyList<SelectAction> Actions
	{
		get
		{
			var mgr = DefenderManager.Instance;
			var type = _u.Type;
			var cost = mgr?.TrainCost( type ) ?? double.PositiveInfinity;
			var level = mgr?.TrainLevelOf( type ) ?? 0;
			return new List<SelectAction>
			{
				new()
				{
					Label = $"Train {Def.ShortName} (Lv {level})",
					Detail = SelectHelp.Cost( cost ),
					Kind = SelectActionKind.Primary,
					Enabled = SelectHelp.CanAfford( cost ),
					Invoke = () => mgr?.TryTrain( type )
				},
				new()
				{
					Label = "Recruit More",
					Kind = SelectActionKind.Normal,
					Enabled = (BuildManager.Instance?.BarracksCount ?? 0) > 0
						&& (mgr?.Count ?? 0) < (BuildManager.Instance?.RecruitCapacity ?? 0),
					Invoke = () => GameCore.Instance?.OpenRecruit()
				},
				new()
				{
					Label = "Dismiss",
					Detail = $"+{SelectHelp.Cost( Def.RecruitCost * GameConstants.SellRefundFraction )}",
					Kind = SelectActionKind.Warn,
					Enabled = true,
					Invoke = () => mgr?.Dismiss( _u )
				},
			};
		}
	}
}
