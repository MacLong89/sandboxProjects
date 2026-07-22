namespace NoFly;

public sealed class RoleInfo
{
	public RoleType Role { get; init; }
	public TeamType Team { get; init; }
	public string DisplayName { get; init; }
	public string ShortObjective { get; init; }
	public string[] Tips { get; init; }
	public string Icon { get; init; }
	public bool IsHiddenAsPassenger { get; init; }

	public Color Color => RoleColors.ForRole( Role );
}

public static class RoleCatalog
{
	public static readonly Dictionary<RoleType, RoleInfo> All = new()
	{
		[RoleType.RegularPassenger] = new RoleInfo
		{
			Role = RoleType.RegularPassenger,
			Team = TeamType.Passenger,
			DisplayName = "Regular Passenger",
			ShortObjective = "Pass security, finish your tasks, and board your flight.",
			Tips = new[]
			{
				"Complete your personal objectives for bonus score.",
				"Report truly suspicious behavior — false alarms hurt.",
				"Don't miss Final Call!"
			},
			Icon = "luggage"
		},
		[RoleType.Smuggler] = new RoleInfo
		{
			Role = RoleType.Smuggler,
			Team = TeamType.Passenger,
			DisplayName = "Smuggler",
			ShortObjective = "Forge your document, hide contraband, and board before arrest.",
			Tips = new[]
			{
				"Alter exactly one document field.",
				"Hide your item so part of it stays visible.",
				"Blend with NPC crowds if exposed."
			},
			Icon = "vpn_key"
		},
		[RoleType.UndercoverAgent] = new RoleInfo
		{
			Role = RoleType.UndercoverAgent,
			Team = TeamType.Tsa,
			DisplayName = "Undercover Agent",
			ShortObjective = "Use incomplete clues to find the Smuggler without blowing your cover.",
			Tips = new[]
			{
				"You look like a passenger — act like one.",
				"Mark one suspect and alert TSA.",
				"A wrong arrest exposes you."
			},
			Icon = "visibility",
			IsHiddenAsPassenger = true
		},
		[RoleType.DocumentAgent] = new RoleInfo
		{
			Role = RoleType.DocumentAgent,
			Team = TeamType.Tsa,
			DisplayName = "Document Agent",
			ShortObjective = "Compare documents, spot forgeries, keep the queue moving.",
			Tips = new[]
			{
				"Click the wrong field before rejecting.",
				"Don't reject blindly — false rejections cost score.",
				"Call Security when you're sure."
			},
			Icon = "badge"
		},
		[RoleType.ScannerAgent] = new RoleInfo
		{
			Role = RoleType.ScannerAgent,
			Team = TeamType.Tsa,
			DisplayName = "Scanner Agent",
			ShortObjective = "Find hidden contraband in bag scans and clear legal bags fast.",
			Tips = new[]
			{
				"Click the suspicious silhouette, then Search.",
				"Unnecessary searches delay the line.",
				"Call Security on clear finds."
			},
			Icon = "radar"
		},
		[RoleType.SecurityOfficer] = new RoleInfo
		{
			Role = RoleType.SecurityOfficer,
			Team = TeamType.Tsa,
			DisplayName = "Security Officer",
			ShortObjective = "Respond to flags, patrol, and arrest the Smuggler.",
			Tips = new[]
			{
				"Watch your alert tablet.",
				"Unjustified arrests are heavily penalized.",
				"Chase hard once the alarm sounds."
			},
			Icon = "local_police"
		}
	};

	public static RoleInfo Get( RoleType role ) => All.TryGetValue( role, out var info ) ? info : All[RoleType.RegularPassenger];

	public static TeamType VisibleTeam( RoleType role )
	{
		var info = Get( role );
		return info.IsHiddenAsPassenger ? TeamType.Passenger : info.Team;
	}
}
