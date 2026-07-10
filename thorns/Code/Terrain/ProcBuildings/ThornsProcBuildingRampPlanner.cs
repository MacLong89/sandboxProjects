using System.Collections.Generic;

namespace Sandbox;

/// <summary>Assigns per-storey ramps so upper-floor shaft/headroom stays clear (no stacked ramp anchors).</summary>
public static class ThornsProcBuildingRampPlanner
{
	static readonly ThornsProcRampDirection[] DirCycle =
	[
		ThornsProcRampDirection.North,
		ThornsProcRampDirection.East,
		ThornsProcRampDirection.South,
		ThornsProcRampDirection.West
	];

	/// <summary>ASCII settlement layouts — fixed switchback on 3×3, generic search on larger footprints.</summary>
	public static List<ThornsProcRampSpec>[] BuildSettlementRampsByStory( int stories, int widthCells, int depthCells )
	{
		stories = Math.Clamp( stories, 1, 8 );
		var w = Math.Clamp( widthCells, 2, 9 );
		var d = Math.Clamp( depthCells, 2, 9 );

		if ( w == 3 && d == 3 && stories >= 2 )
			return BuildCompact3x3SwitchbackRampsByStory( stories );

		return BuildSwitchbackRampsByStory( stories, w, d );
	}

	/// <summary>
	/// 3×3 only has one interior cell (1,1) — never anchor a ramp there or the next storey has nowhere to go.
	/// F0 ramp SE (2,0) rising west; F1 ramp NE (2,2) rising west (canonical ASCII / corner furniture).
	/// </summary>
	public static List<ThornsProcRampSpec>[] BuildCompact3x3SwitchbackRampsByStory( int stories )
	{
		stories = Math.Clamp( stories, 1, 8 );
		var byStory = new List<ThornsProcRampSpec>[stories];
		for ( var i = 0; i < stories; i++ )
			byStory[i] = new List<ThornsProcRampSpec>( 1 );

		if ( stories <= 1 )
			return byStory;

		byStory[0].Add( new ThornsProcRampSpec
		{
			Story = 0,
			X = 2,
			Y = 0,
			Direction = ThornsProcRampDirection.West
		} );

		if ( stories >= 3 )
		{
			byStory[1].Add( new ThornsProcRampSpec
			{
				Story = 1,
				X = 2,
				Y = 2,
				Direction = ThornsProcRampDirection.West
			} );
		}

		return byStory;
	}

	public static List<ThornsProcRampSpec>[] BuildSwitchbackRampsByStory( int stories, int widthCells, int depthCells )
	{
		stories = Math.Clamp( stories, 1, 8 );
		var w = Math.Clamp( widthCells, 2, 9 );
		var d = Math.Clamp( depthCells, 2, 9 );

		var byStory = new List<ThornsProcRampSpec>[stories];
		for ( var i = 0; i < stories; i++ )
			byStory[i] = new List<ThornsProcRampSpec>( 1 );

		if ( stories <= 1 || w < 3 || d < 3 )
			return byStory;

		var opening = new bool[stories * w * d];
		var hintX = 1;
		var hintY = 1;

		for ( var s = 0; s < stories - 1; s++ )
		{
			if ( !TryPlaceStoryRamp( opening, w, d, stories, s, ref hintX, ref hintY, out var ramp )
			     && !TryPlaceStoryRampBruteForce( opening, w, d, stories, s, out ramp ) )
			{
				Log.Warning(
					$"[Thorns RampPlanner] No valid ramp for story {s} on {w}x{d}x{stories} — layout may fail validation." );
				continue;
			}

			byStory[s].Add( ramp );
			ThornsProcTileRampHeadroom.ApplyRequiredOpenings(
				opening,
				w,
				d,
				new List<ThornsProcRampSpec>( 1 ) { ramp } );

			ThornsProcTileRampHeadroom.GetRiseDelta( ramp.Direction, out var riseDx, out var riseDy );
			hintX = Math.Clamp( ramp.X + riseDx, 1, w - 2 );
			hintY = Math.Clamp( ramp.Y + riseDy, 1, d - 2 );
		}

		return byStory;
	}

	public static List<ThornsProcRampSpec> BuildFlatRampList( List<ThornsProcRampSpec>[] byStory )
	{
		var flat = new List<ThornsProcRampSpec>( 8 );
		if ( byStory is null )
			return flat;

		for ( var s = 0; s < byStory.Length; s++ )
		{
			if ( byStory[s] is null )
				continue;

			flat.AddRange( byStory[s] );
		}

		return flat;
	}

	static bool TryPlaceStoryRampBruteForce(
		bool[] opening,
		int w,
		int d,
		int stories,
		int story,
		out ThornsProcRampSpec ramp )
	{
		ramp = default;
		for ( var y = 1; y < d - 1; y++ )
		for ( var x = 1; x < w - 1; x++ )
		{
			foreach ( var dir in DirCycle )
			{
				if ( !CanPlaceRamp( opening, w, d, stories, story, x, y, dir ) )
					continue;

				ramp = new ThornsProcRampSpec
				{
					Story = story,
					X = x,
					Y = y,
					Direction = dir
				};
				return true;
			}
		}

		return false;
	}

	static bool TryPlaceStoryRamp(
		bool[] opening,
		int w,
		int d,
		int stories,
		int story,
		ref int hintX,
		ref int hintY,
		out ThornsProcRampSpec ramp )
	{
		ramp = default;
		var maxRadius = Math.Max( w, d );

		for ( var radius = 0; radius <= maxRadius; radius++ )
		{
			for ( var ox = -radius; ox <= radius; ox++ )
			for ( var oy = -radius; oy <= radius; oy++ )
			{
				var cx = Math.Clamp( hintX + ox, 1, w - 2 );
				var cy = Math.Clamp( hintY + oy, 1, d - 2 );

				foreach ( var dir in DirCycle )
				{
					if ( !CanPlaceRamp( opening, w, d, stories, story, cx, cy, dir ) )
						continue;

					ramp = new ThornsProcRampSpec
					{
						Story = story,
						X = cx,
						Y = cy,
						Direction = dir
					};
					hintX = cx;
					hintY = cy;
					return true;
				}
			}
		}

		return false;
	}

	static bool CanPlaceRamp(
		bool[] opening,
		int w,
		int d,
		int stories,
		int story,
		int x,
		int y,
		ThornsProcRampDirection dir )
	{
		if ( IsOpening( opening, w, d, story, x, y ) )
			return false;

		// 3×3: only interior tile — reserve for headroom, never a ramp anchor when another storey needs a ramp.
		if ( w == 3 && d == 3 && stories >= 3 && story < stories - 2 && x == 1 && y == 1 )
			return false;

		if ( story + 1 >= stories )
			return true;

		ThornsProcTileRampHeadroom.GetRiseDelta( dir, out var riseDx, out var riseDy );
		var headX = x + riseDx;
		var headY = y + riseDy;
		if ( headX < 0 || headX >= w || headY < 0 || headY >= d )
			return false;

		return true;
	}

	static bool IsOpening( bool[] opening, int w, int d, int story, int x, int y ) =>
		opening[story * w * d + y * w + x];

}
