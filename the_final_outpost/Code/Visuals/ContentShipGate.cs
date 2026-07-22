namespace FinalOutpost;

/// <summary>
/// Boot-time checklist of every asset path players need in a published build.
/// Logs missing project-packaged content loudly; cloud weapon packages are mounted
/// by <see cref="WeaponModelLoader"/>.
/// </summary>
public static class ContentShipGate
{
	public static readonly string[] Materials =
	{
		"materials/fo_grass.vmat",
		"materials/fo_stone.vmat",
		"materials/fo_wood.vmat",
		"materials/fo_roof.vmat",
		"materials/fo_brick.vmat",
		"materials/fo_metal.vmat",
		"materials/fo_plaster.vmat",
		"materials/fo_thatch.vmat",
		"materials/fo_crops.vmat",
		"materials/fo_awning.vmat",
		"materials/fo_slate.vmat",
	};

	public static readonly string[] Sounds =
	{
		Sfx.Shoot,
		Sfx.ShootPistol,
		Sfx.ShootSmg,
		Sfx.ShootShotgun,
		Sfx.ShootSniper,
		Sfx.Turret,
		Sfx.ZombieHit,
		Sfx.ZombieDeath,
		Sfx.WallHit,
		Sfx.WaveStart,
		Sfx.WaveClear,
		Sfx.Purchase,
		Sfx.GameOver,
		Sfx.UiClick,
		AmbiencePlayer.AmbienceSound,
		"sounds/fo_combat.sound",
		"sounds/fo_combat2.sound",
		TakeoverSfx.M4Fire,
		TakeoverSfx.ShotgunFire,
		TakeoverSfx.M4Reload,
		TakeoverSfx.ShotgunReload,
		TakeoverSfx.GunDeploy,
		TakeoverSfx.FoPistol,
		TakeoverSfx.FoSmg,
		TakeoverSfx.FoRifle,
		TakeoverSfx.FoShotgun,
		TakeoverSfx.FoSniper,
	};

	public static readonly string[] UiArt =
	{
		UiIcons.Brand,
		"ui/build_gun_tower.png",
		"ui/build_cannon.png",
		"ui/build_long_range.png",
		"ui/build_wall.png",
		"ui/build_barracks.png",
		"ui/build_lab.png",
	};

	/// <summary>Engine / package models that should resolve after mount (not project Assets/).</summary>
	public static readonly string[] EngineModels =
	{
		CharacterModel.CitizenVmdl,
		TakeoverWeaponCatalog.ArmsPath,
	};

	/// <returns>Number of missing project assets (0 = clean).</returns>
	public static int Verify()
	{
		var missing = 0;

		foreach ( var path in Materials )
		{
			var mat = AssetSafe.Material( path );
			if ( mat is null || !mat.IsValid() || mat == MeshPrimitives.Mat )
			{
				Log.Warning( $"[FinalOutpost][Ship] Missing material '{path}' — buildings will fall back to flat tint." );
				missing++;
			}
		}

		foreach ( var path in Sounds )
		{
			if ( !SoundResolves( path ) )
			{
				Log.Warning( $"[FinalOutpost][Ship] Missing sound '{path}' — that cue will be silent for players." );
				missing++;
			}
		}

		foreach ( var path in UiArt )
		{
			if ( !TextureResolves( path ) )
			{
				Log.Warning( $"[FinalOutpost][Ship] Missing UI art '{path}' — HUD falls back to glyphs." );
				missing++;
			}
		}

		foreach ( var path in EngineModels )
		{
			var model = AssetSafe.Model( path );
			if ( model is null )
			{
				Log.Warning( $"[FinalOutpost][Ship] Engine model unavailable '{path}' — placeholder until remount." );
				missing++;
			}
		}

		if ( missing == 0 )
			Log.Info( $"[FinalOutpost][Ship] Content gate OK — {Materials.Length} mats, {Sounds.Length} sounds, {UiArt.Length} UI, {EngineModels.Length} engine models." );
		else
			Log.Warning( $"[FinalOutpost][Ship] Content gate found {missing} missing asset(s). Publish/install will look incomplete until fixed." );

		return missing;
	}

	/// <summary>Warm materials so first building spawn does not hitch, then run the gate.</summary>
	public static void WarmAndVerify()
	{
		_ = StylizedMaterials.Grass;
		_ = StylizedMaterials.Stone;
		_ = StylizedMaterials.Wood;
		_ = StylizedMaterials.Roof;
		_ = StylizedMaterials.Brick;
		_ = StylizedMaterials.Metal;
		_ = StylizedMaterials.Plaster;
		_ = StylizedMaterials.Thatch;
		_ = StylizedMaterials.Crops;
		_ = StylizedMaterials.Awning;
		_ = StylizedMaterials.Slate;

		Verify();
	}

	static bool SoundResolves( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		try
		{
			// Silent probe — do not Sound.Play (would audible-spam every cue at boot).
			var ev = ResourceLibrary.Get<SoundEvent>( path );
			return ev is not null;
		}
		catch
		{
			return false;
		}
	}

	static bool TextureResolves( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		try
		{
			var tex = Texture.Load( path );
			return tex is not null && tex.IsValid();
		}
		catch
		{
			try
			{
				var tex = Texture.LoadFromFileSystem( path, FileSystem.Mounted, warnOnMissing: false );
				return tex is not null && tex.IsValid();
			}
			catch
			{
				return false;
			}
		}
	}
}
