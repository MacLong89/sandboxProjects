namespace Sandbox;

public enum AimboxGameMode
{
	TeamDeathmatch,
	FreeForAll,
	Duel,
	Survival,
	Range,
	AimLevel1,
	AimLevel2,
	AimLevel3,
	AimLevel4,
	AimLevel5,
	AimLevel6
}

public enum AimboxSessionPhase
{
	Playing,
	Intermission,
	Starting
}

public enum AimboxTeam
{
	None,
	Red,
	Blue
}

public enum AimboxWeaponId
{
	M4A1,
	Mp5,
	Usp,
	M700,
	SpaghelliM4,
	M9Bayonet,
	Trenchknife,
	Crowbar,
	HeGrenade,
	FlashGrenade,
	SmokeGrenade,
	DecoyGrenade,
	IncendiaryGrenade,
	Bow,

	AssaultRifle = M4A1,
	Smg = Mp5,
	Pistol = Usp,
	SniperRifle = M700,
	Shotgun = SpaghelliM4,
	Knife = M9Bayonet
}

public enum AimboxAttachmentId
{
	HoloSight,
	RangedSight,
	RaisedRedDot,
	ExtendedMag,
	ForegripStraight,
	ForegripAngled,
	Flashlight,
	Suppressor
}

public enum AimboxAdsSightMode
{
	None,
	IronSight,
	RedDot,
	SniperScope
}

public enum AimboxMedalId
{
	FirstBlood,
	Headshot,
	Longshot,
	Revenge,
	DoubleKill,
	TripleKill,
	Bloodthirsty
}

public enum AimboxRankTier
{
	Bronze,
	Silver,
	Gold,
	Platinum,
	Diamond,
	Master
}

public enum AimboxPerkId
{
	None,
	Lightweight,
	StoppingPower,
	Scavenger,
	SleightOfHand,
	Marathon,
	Ninja
}

public enum AimboxKillstreakId
{
	None,
	Uav,
	CarePackage,
	PredatorMissile,
	CounterUav,
	SentryGun
}

public enum AimboxMetaScreen
{
	None,
	MainMenu,
	ModeSelect,
	CreateClass,
	Barracks,
	Armory,
	Challenges,
	Scoreboard,
	PostMatch
}

public enum AimboxCreateClassCategory
{
	Primary,
	Secondary,
	Perks,
	Killstreaks,
	Attachments
}
