namespace Terraingen.Combat.Attachments;



using Terraingen.GameData;

using Terraingen.Player;



/// <summary>Pre-roll compatible attachments onto world-loot weapon stacks.</summary>

public static class ThornsWeaponAttachmentRoll

{

	public const float DefaultAttachmentRollChance = 0.35f;



	public static void RollOntoStack( ref ThornsItemStack stack, Random rng, float rollChanceMul = 1f )

	{

		if ( stack.IsEmpty || rng is null )

			return;



		if ( !ThornsItemRegistry.TryGet( stack.ItemId, out var def ) || def.Category != ThornsItemCategory.Weapon )

			return;



		var combatId = ThornsInventoryWeaponState.ResolveCombatId( def, stack.ItemId );

		if ( !ThornsAttachmentCatalog.SupportsAttachments( combatId ) )

			return;



		if ( ThornsWeaponAttachmentState.CountEquipped( stack ) > 0 )

			return;



		var chance = DefaultAttachmentRollChance * Math.Max( 0f, rollChanceMul );

		if ( rng.NextSingle() > chance )

			return;



		var compatible = ThornsAttachmentCatalog.GetCompatibleAttachments( combatId );

		if ( compatible.Count == 0 )

			return;



		var maxRoll = ResolveMaxAttachmentCount( combatId, rng );

		var picked = new List<ThornsAttachmentId>( maxRoll );

		for ( var i = 0; i < maxRoll; i++ )

		{

			if ( !TryPickWeighted( combatId, compatible, picked, rng, out var attachment ) )

				break;



			picked.Add( attachment );

		}



		if ( picked.Count == 0 )

			return;



		var itemIds = picked.Select( ThornsAttachmentItemIds.ToItemId ).Where( id => !string.IsNullOrEmpty( id ) );

		ThornsWeaponAttachmentState.SetAttachmentItemIds( ref stack, itemIds, combatId );

		ThornsInventoryWeaponState.ClampLoadedAmmoToClip( ref stack, combatId );

	}



	static int ResolveMaxAttachmentCount( string combatId, Random rng )

	{

		var normalized = ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatId );

		return normalized switch

		{

			"m4" => rng.Next( 1, 3 ),

			"sniper" => 1,

			"mp5" or "usp" or "shotgun" => 1,

			_ => 1

		};

	}



	static bool TryPickWeighted(

		string combatId,

		IReadOnlyList<ThornsAttachmentId> compatible,

		IReadOnlyList<ThornsAttachmentId> alreadyPicked,

		Random rng,

		out ThornsAttachmentId attachment )

	{

		attachment = default;

		var pool = compatible.Where( a => !alreadyPicked.Contains( a ) ).ToList();

		if ( pool.Count == 0 )

			return false;



		var weights = pool.Select( a => WeightFor( combatId, a ) ).ToArray();

		var total = weights.Sum();

		if ( total <= 0.0001f )

		{

			attachment = pool[rng.Next( pool.Count )];

			return true;

		}



		var roll = rng.NextSingle() * total;

		for ( var i = 0; i < pool.Count; i++ )

		{

			roll -= weights[i];

			if ( roll > 0f )

				continue;



			attachment = pool[i];

			return true;

		}



		attachment = pool[^1];

		return true;

	}



	static float WeightFor( string combatId, ThornsAttachmentId attachment )

	{

		var normalized = ThornsAttachmentCatalog.NormalizeCombatWeaponId( combatId );

		return normalized switch

		{

			"m4" => attachment switch

			{

				ThornsAttachmentId.HoloSight => 3f,

				ThornsAttachmentId.RaisedRedDot => 2f,

				ThornsAttachmentId.Suppressor => 2.5f,

				ThornsAttachmentId.ExtendedMag => 2f,

				ThornsAttachmentId.ForegripAngled => 1.5f,

				_ => 1f

			},

			"sniper" => attachment switch

			{

				ThornsAttachmentId.RangedSight => 4f,

				ThornsAttachmentId.Suppressor => 1.5f,

				_ => 1f

			},

			"mp5" or "usp" or "shotgun" => attachment == ThornsAttachmentId.Suppressor ? 4f : 1f,

			_ => 1f

		};

	}



	public static string RollLooseAttachmentItemId( Random rng )

	{

		var pool = ThornsAttachmentCatalog.StandardAttachments;

		var attachment = pool[rng.Next( pool.Length )];

		return ThornsAttachmentItemIds.ToItemId( attachment );

	}

}

