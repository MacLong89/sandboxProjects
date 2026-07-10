namespace Sandbox;

/// <summary>
/// Asset paths and copy for the boot main menu. Drop files under the matching <c>Assets/</c> folders (see tooltips).
/// </summary>
public static class ThornsMainMenuPresentation
{
	/// <summary>Menu tracks — one is picked at random when the boot menu loads (<c>Assets/sounds/ThornsMusic_*.mp3</c>).</summary>
	public static readonly string[] MenuMusicSoundPaths =
	[
		"sounds/thorns_music_blood_compass.sound",
		"sounds/thorns_music_dusty_banjo.sound"
	];

	/// <summary>Optional low bed under music — leave empty so only score tracks play on the boot menu.</summary>
	public const string AmbienceSoundPath = "";

	/// <summary>Short whoosh when the menu appears — <c>Assets/ui/main_menu/menu_sting.mp3</c>.</summary>
	public const string BootStingSoundPath = "ui/main_menu/menu_sting.sound";

	/// <summary>
	/// Full-screen hero stills (Ken Burns slideshow). Add PNGs under <c>Assets/textures/ui/main_menu/</c>:
	/// <c>hero_01.png</c>, <c>hero_02.png</c>, <c>hero_03.png</c>.
	/// </summary>
	public static readonly string[] DefaultHeroTexturePaths =
	[
		"textures/ui/main_menu/hero_01.png",
		"textures/ui/main_menu/hero_02.png",
		"textures/ui/main_menu/hero_03.png",
		"Assets/textures/ui/main_menu/hero_01.png",
		"Assets/textures/ui/main_menu/hero_02.png",
		"Assets/textures/ui/main_menu/hero_03.png"
	];

	/// <summary>Optional full-bleed backdrop if you skip hero slides — <c>textures/ui/main_menu/backdrop.png</c>.</summary>
	public const string BackdropTexturePath = "textures/ui/main_menu/backdrop.png";

	/// <summary>Subtle film grain overlay — <c>textures/ui/main_menu/grain.png</c> (tileable, low-contrast).</summary>
	public const string GrainTexturePath = "textures/ui/main_menu/grain.png";

	/// <summary>Small logo mark above the title — <c>textures/ui/main_menu/logo.png</c>.</summary>
	public const string LogoTexturePath = "textures/ui/main_menu/logo.png";

	public static readonly string[] RotatingTaglines =
	[
		"Survive the bloom.",
		"Build. Hunt. Tame.",
		"Procedural frontiers.",
		"Raid the thorns."
	];

	public static readonly string[] RotatingTips =
	[
		"Host a world — your friends join from the browser.",
		"Craft kits and furnish proc-building interiors.",
		"Tame wildlife for travel and combat perks.",
		"Story mode is on the roadmap — multiplayer is live."
	];
}
