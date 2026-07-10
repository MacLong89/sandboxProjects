namespace Fauna2;

/// <summary>Paths to supplied 1024×1024 sprites in Assets/models/.</summary>
public static class SuppliedSpriteManifest
{
	public const string ModelsRoot = "models/";
	public const string AnimalModelsRoot = ModelsRoot + "animals/";

	public const string PlayerSpritePath = ModelsRoot + "player_sprite.png";
	public const string GuestSpritePath = ModelsRoot + "guest_sprite.png";
	public const string GuestBoy1SpritePath = ModelsRoot + "guest_boy_1.png";
	public const string GuestBoy2SpritePath = ModelsRoot + "guest_boy_2.png";
	public const string GuestGirl1SpritePath = ModelsRoot + "guest_girl_1.png";

	public static readonly string[] GuestVariantSpritePaths =
	[
		GuestSpritePath,
		GuestBoy1SpritePath,
		GuestBoy2SpritePath,
		GuestGirl1SpritePath,
	];
	public const string EntrancePath = ModelsRoot + "entrance.png";
	public const string FenceHPath = ModelsRoot + "fence_h.png";
	public const string FenceVPath = ModelsRoot + "fence_v.png";
	public const string FenceTopLeftPath = ModelsRoot + "fence_top_left.png";
	public const string FenceTopRightPath = ModelsRoot + "fence_top_right.png";
	public const string FenceBottomLeftPath = ModelsRoot + "fence_bottom_left.png";
	public const string FenceBottomRightPath = ModelsRoot + "fence_bottom_right.png";
	public const string FencePostPath = ModelsRoot + "fence_post.png";

	public static bool TryGetSuppliedPropPath( string name, out string path )
	{
		path = name switch
		{
			"fence_h" => FenceHPath,
			"fence_v" => FenceVPath,
			"fence_top_left" => FenceTopLeftPath,
			"fence_top_right" => FenceTopRightPath,
			"fence_bottom_left" => FenceBottomLeftPath,
			"fence_bottom_right" => FenceBottomRightPath,
			"fence_post" => FencePostPath,
			"oak_tree" or "tree" => OakTreePath,
			"aspen_tree" => AspenTreePath,
			"pine_tree" or "pine" => PineTreePath,
			"bush" => BushPath,
			"cactus" => CactusPath,
			"rock" => RockPath,
			"pond" => PondPath,
			"entrance" => EntrancePath,
			"cafe" => CafePath,
			"cafeteria" => CafeteriaPath,
			"restaurant" => RestaurantPath,
			"restroom" => RestroomPath,
			"shop" => ShopPath,
			"food_stand" => FoodStandPath,
			"kiosk" => KioskPath,
			"playground" => PlaygroundPath,
			_ => null,
		};

		if ( path is null )
			return false;

		if ( name is "shop" or "kiosk"
			&& !FileSystem.Mounted.FileExists( path )
			&& FileSystem.Mounted.FileExists( KioskPath ) )
			path = KioskPath;

		return true;
	}
	public const string OakTreePath = ModelsRoot + "oak_tree.png";
	public const string AspenTreePath = ModelsRoot + "aspen_tree.png";
	public const string PineTreePath = ModelsRoot + "pine_tree.png";
	public const string BushPath = ModelsRoot + "bush.png";
	public const string CactusPath = ModelsRoot + "cactus.png";
	public const string RockPath = ModelsRoot + "rock.png";
	public const string PondPath = ModelsRoot + "pond.png";
	public const string CafePath = ModelsRoot + "cafe.png";
	public const string CafeteriaPath = ModelsRoot + "cafeteria.png";
	public const string RestaurantPath = ModelsRoot + "restaurant.png";
	public const string RestroomPath = ModelsRoot + "restroom.png";
	public const string ShopPath = ModelsRoot + "shop.png";
	public const string FoodStandPath = ModelsRoot + "food_stand.png";
	public const string KioskPath = ModelsRoot + "kiosk.png";
	public const string PlaygroundPath = ModelsRoot + "playground.png";

	public const string RabbitPath = AnimalModelsRoot + "rabbit.png";
	public const string SquirrelPath = AnimalModelsRoot + "squirrel.png";
	public const string FoxPath = AnimalModelsRoot + "fox.png";
	public const string DeerPath = AnimalModelsRoot + "deer.png";
	public const string WolfPath = AnimalModelsRoot + "wolf.png";
	public const string BlackBearPath = AnimalModelsRoot + "black_bear.png";
	public const string MoosePath = AnimalModelsRoot + "moose.png";
	public const string AlligatorPath = AnimalModelsRoot + "alligator.png";

	public const string Summary = "fauna2/models sprites";

	public const string AnimationsRoot = ModelsRoot + "animations/";

	public static string PlayerAnimationDir( PlayerFacing facing ) =>
		AnimationsRoot + $"player/{facing.ToKey()}/";

	/// <summary>Per-variant walk/idle frames under models/animations/ (guest_sprite → guest/ for legacy art).</summary>
	public static string GuestAnimationDir( int variantIndex = 0 )
	{
		var paths = GuestVariantSpritePaths;
		var index = Math.Clamp( variantIndex, 0, paths.Length - 1 );
		var stem = System.IO.Path.GetFileNameWithoutExtension( paths[index] );
		var folder = stem.Equals( "guest_sprite", StringComparison.OrdinalIgnoreCase ) ? "guest" : stem;
		return AnimationsRoot + $"{folder}/";
	}

	public static string AnimalAnimationDir( string stem ) =>
		AnimationsRoot + $"animals/{stem}/";
}
