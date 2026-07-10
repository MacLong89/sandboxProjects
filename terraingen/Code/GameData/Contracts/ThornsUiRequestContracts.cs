namespace Terraingen.GameData;

public enum ThornsMoveItemMode : byte
{
	Move,
	SplitHalf,
	SplitAmount,
	Swap,
	QuickTransfer,
	DoubleClickTransfer
}

public sealed class ThornsMoveItemRequest
{
	public ThornsContainerKind FromContainer { get; set; }
	public int FromIndex { get; set; }
	public ThornsContainerKind ToContainer { get; set; }
	public int ToIndex { get; set; }
	public ThornsMoveItemMode Mode { get; set; }
	public int SplitAmount { get; set; }
	public bool ShiftHeld { get; set; }
	public string WorldContainerKey { get; set; } = "";
}

public sealed class ThornsCraftRequest
{
	public string RecipeId { get; set; } = "";
	public int Quantity { get; set; } = 1;
}

public sealed class ThornsEquipRequest
{
	public ThornsContainerKind FromContainer { get; set; }
	public int FromIndex { get; set; }
}

public sealed class ThornsEquipAttachmentRequest
{
	public ThornsContainerKind WeaponContainer { get; set; }
	public int WeaponIndex { get; set; }
	public int AttachmentSlotIndex { get; set; } = -1;
	public ThornsContainerKind FromContainer { get; set; }
	public int FromIndex { get; set; }
	public bool Unequip { get; set; }
}

public sealed class ThornsSkillUnlockRequest
{
	public string SkillId { get; set; } = "";
}

public sealed class ThornsTameCommandRequest
{
	public Guid TameEntityId { get; set; }
	public ThornsTameCommand Command { get; set; }
}

public sealed class ThornsTameRenameRequest
{
	public Guid TameEntityId { get; set; }
	public string DisplayName { get; set; } = "";
}

public sealed class ThornsTameFeedRequest
{
	public Guid TameEntityId { get; set; }
}

public sealed class ThornsTameStatUpgradeRequest
{
	public Guid TameEntityId { get; set; }
	public string StatKey { get; set; } = "";
}

public sealed class ThornsTameBreedRequest
{
	public Guid ParentAEntityId { get; set; }
	public Guid ParentBEntityId { get; set; }
}

public sealed class ThornsGuildCreateRequest
{
	public string GuildName { get; set; } = "";
}

public sealed class ThornsGuildJoinRequest
{
	public string GuildId { get; set; } = "";
}

public sealed class ThornsGuildKickRequest
{
	public string TargetAccountKey { get; set; } = "";
}

public sealed class ThornsGuildPromotionRequest
{
	public string TargetAccountKey { get; set; } = "";
	public string NewRank { get; set; } = "";
}

public sealed class ThornsGuildInviteRequest
{
	public string TargetAccountKey { get; set; } = "";
}
