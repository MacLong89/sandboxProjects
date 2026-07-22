namespace SceneLab;

/// <summary>Shared hollow-room kit for house archetypes. Rooms abut; never interpenetrate.</summary>
public static class HouseShell
{
	private const float RadToDeg = 57.29578f;

	public const float WallT = 12f;
	public const float DoorW = CityScale.DoorW;
	public const float DoorH = CityScale.DoorH;
	public const float PassW = CityScale.DoorW;
	public const float FloorT = 6f;

	/// <summary>Front-wall glazed opening (centers in room space; Z absolute).</summary>
	public readonly struct FrontWindow
	{
		public readonly float Cy;
		public readonly float Z;
		public readonly float W;
		public readonly float H;
		public readonly bool Shutters;

		public FrontWindow( float cy, float z, float w, float h, bool shutters = false )
		{
			Cy = cy;
			Z = z;
			W = w;
			H = h;
			Shutters = shutters;
		}
	}

	/// <summary>
	/// Abut an attached room on ±Y of the host without overlapping volumes.
	/// Attached room should omit its host-facing wall.
	/// </summary>
	public static float AbutY( float hostCy, float hostW, float attachW, float sideSign )
		=> hostCy + sideSign * (hostW * 0.5f + attachW * 0.5f);

	public static void Foundation( GameObject root, float depth, float width, float foundH )
	{
		// Bury 1 unit into the lot so the pad meets the foundation with no air gap.
		var buried = foundH + Depth.Step;
		KitBox.CollidingBox( root, "Foundation",
			new Vector3( 0f, 0f, buried * 0.5f - Depth.Step ),
			new Vector3( depth + 20f, width + 20f, buried ),
			Palette.HouseFoundation );
	}

	public static void HollowRoom(
		GameObject root,
		string prefix,
		float cx,
		float cy,
		float depth,
		float width,
		float wallBase,
		float h,
		Color wallC,
		Color floorC,
		float? openSideSign = null,
		float openPassW = 0f,
		float openPassX = 0f,
		float? openOtherSideSign = null,
		float openOtherPassW = 0f,
		float openOtherPassX = 0f,
		bool frontDoor = false,
		float doorW = DoorW,
		float doorH = DoorH,
		bool openFront = false,
		float? omitSideSign = null,
		bool skipCeiling = false,
		float? stairWellX = null,
		float? stairWellY = null,
		float stairWellD = 0f,
		float stairWellW = 0f,
		bool wellInFloor = false,
		bool? wellInCeiling = null,
		FrontWindow[] frontWindows = null,
		Color? windowTrim = null )
	{
		var t = WallT;
		var floorT = FloorT;
		var ceilT = MathF.Max( 5f, t * 0.45f );
		var halfD = depth * 0.5f;
		var halfW = width * 0.5f;
		var zMid = wallBase + Depth.CenterLift( h );
		var wellY = stairWellY ?? cy;
		var hasWell = stairWellX.HasValue && stairWellD > t && stairWellW > t;
		// Legacy: a well defaulted to ceiling unless wellInFloor. Middle floors need both.
		var cutFloor = wellInFloor && hasWell;
		var cutCeil = (wellInCeiling ?? (hasWell && !wellInFloor)) && hasWell;

		if ( cutFloor )
		{
			SplitSlab( root, prefix + "Floor", cx, cy, depth, width, wallBase + Depth.CenterLift( floorT ), floorT, t,
				stairWellX.Value, wellY, stairWellD, stairWellW, floorC );
		}
		else
		{
			KitBox.CollidingBox( root, prefix + "Floor",
				new Vector3( cx, cy, wallBase + Depth.CenterLift( floorT ) ),
				new Vector3( depth - t * 0.5f, width - t * 0.5f, floorT ),
				floorC );
		}

		if ( !skipCeiling )
		{
			if ( cutCeil )
			{
				SplitSlab( root, prefix + "Ceil", cx, cy, depth, width, wallBase + h - Depth.CenterLift( ceilT ), ceilT, t,
					stairWellX.Value, wellY, stairWellD, stairWellW, Palette.HouseCeiling );
			}
			else
			{
				KitBox.CollidingBox( root, prefix + "Ceil",
					new Vector3( cx, cy, wallBase + h - Depth.CenterLift( ceilT ) ),
					new Vector3( depth - t, width - t, ceilT ),
					Palette.HouseCeiling );
			}
		}

		KitBox.CollidingBox( root, prefix + "Back",
			new Vector3( cx - halfD + t * 0.5f, cy, zMid ),
			new Vector3( t, width, h ),
			wallC );

		BuildFront( root, prefix, cx, cy, halfD, halfW, zMid, wallBase, h, t, wallC,
			frontDoor, doorW, doorH, openFront, frontWindows, windowTrim ?? KitBox.Solid( wallC, 1.1f ) );

		BuildSide( root, prefix + "SideP", cx, cy, halfD, halfW, zMid, depth, h, t, wallC, +1f,
			openSideSign, openPassW, openPassX, openOtherSideSign, openOtherPassW, openOtherPassX, omitSideSign );
		BuildSide( root, prefix + "SideN", cx, cy, halfD, halfW, zMid, depth, h, t, wallC, -1f,
			openSideSign, openPassW, openPassX, openOtherSideSign, openOtherPassW, openOtherPassX, omitSideSign );
	}

	private static float SafeClamp( float v, float min, float max )
	{
		if ( min > max )
			return (min + max) * 0.5f;
		if ( v < min )
			return min;
		if ( v > max )
			return max;
		return v;
	}

	private static void SplitSlab(
		GameObject root,
		string name,
		float cx,
		float cy,
		float depth,
		float width,
		float z,
		float thick,
		float t,
		float wellX,
		float wellY,
		float wellD,
		float wellW,
		Color color )
	{
		var xMin = cx - depth * 0.5f + t * 0.5f;
		var xMax = cx + depth * 0.5f - t * 0.5f;
		var yMin = cy - width * 0.5f + t * 0.5f;
		var yMax = cy + width * 0.5f - t * 0.5f;
		var spanX = xMax - xMin;
		var spanY = yMax - yMin;

		// Stair wells can outgrow a room — shrink before clamping (avoids min>max).
		wellD = MathF.Min( wellD, MathF.Max( 40f, spanX - 8f ) );
		wellW = MathF.Min( wellW, MathF.Max( 40f, spanY - 8f ) );
		wellX = SafeClamp( wellX, xMin + wellD * 0.5f, xMax - wellD * 0.5f );
		wellY = SafeClamp( wellY, yMin + wellW * 0.5f, yMax - wellW * 0.5f );
		var gapLo = wellX - wellD * 0.5f;
		var gapHi = wellX + wellD * 0.5f;

		var leftD = gapLo - xMin;
		var rightD = xMax - gapHi;
		if ( leftD > 4f )
		{
			KitBox.CollidingBox( root, name,
				new Vector3( xMin + leftD * 0.5f, cy, z ),
				new Vector3( leftD, spanY, thick ),
				color );
		}

		if ( rightD > 4f )
		{
			KitBox.CollidingBox( root, name,
				new Vector3( xMax - rightD * 0.5f, cy, z ),
				new Vector3( rightD, spanY, thick ),
				color );
		}

		var below = (wellY - wellW * 0.5f) - yMin;
		var above = yMax - (wellY + wellW * 0.5f);
		if ( below > 4f )
		{
			KitBox.CollidingBox( root, name,
				new Vector3( wellX, yMin + below * 0.5f, z ),
				new Vector3( wellD, below, thick ),
				color );
		}

		if ( above > 4f )
		{
			KitBox.CollidingBox( root, name,
				new Vector3( wellX, yMax - above * 0.5f, z ),
				new Vector3( wellD, above, thick ),
				color );
		}
	}

	private readonly struct OpeningRect
	{
		public readonly float Y0, Y1, Z0, Z1;
		public readonly bool Glaze;
		public readonly bool Shutters;
		public float Cy => (Y0 + Y1) * 0.5f;
		public float Zc => (Z0 + Z1) * 0.5f;
		public float W => Y1 - Y0;
		public float H => Z1 - Z0;

		public OpeningRect( float y0, float y1, float z0, float z1, bool glaze, bool shutters = false )
		{
			Y0 = y0;
			Y1 = y1;
			Z0 = z0;
			Z1 = z1;
			Glaze = glaze;
			Shutters = shutters;
		}
	}

	private static void BuildFront(
		GameObject root,
		string prefix,
		float cx,
		float cy,
		float halfD,
		float halfW,
		float zMid,
		float wallBase,
		float h,
		float t,
		Color wallC,
		bool frontDoor,
		float doorW,
		float doorH,
		bool openFront,
		FrontWindow[] frontWindows,
		Color trimC )
	{
		if ( openFront )
		{
			foreach ( var sy in new[] { -1f, 1f } )
			{
				var jamW = MathF.Max( t * 1.2f, halfW * 0.16f );
				KitBox.CollidingBox( root, prefix + "FrontJam",
					new Vector3( cx + halfD - t * 0.5f, cy + sy * (halfW - jamW * 0.5f), zMid ),
					new Vector3( t, jamW, h ),
					wallC );
			}

			var lintH = MathF.Max( 14f, h * 0.12f );
			KitBox.CollidingBox( root, prefix + "FrontLint",
				new Vector3( cx + halfD - t * 0.5f, cy, wallBase + h - Depth.CenterLift( lintH ) ),
				new Vector3( t, halfW * 2f - t * 2f, lintH ),
				wallC );
			return;
		}

		var openings = new List<OpeningRect>();
		if ( frontDoor )
		{
			openings.Add( new OpeningRect(
				cy - doorW * 0.5f, cy + doorW * 0.5f,
				wallBase, wallBase + doorH,
				glaze: false ) );
		}

		if ( frontWindows is { Length: > 0 } )
		{
			foreach ( var w in frontWindows )
			{
				openings.Add( new OpeningRect(
					w.Cy - w.W * 0.5f, w.Cy + w.W * 0.5f,
					w.Z - w.H * 0.5f, w.Z + w.H * 0.5f,
					glaze: true, shutters: w.Shutters ) );
			}
		}

		var faceX = cx + halfD - t * 0.5f;
		var yMin = cy - halfW;
		var yMax = cy + halfW;
		var zMin = wallBase;
		var zMax = wallBase + h;

		if ( openings.Count == 0 )
		{
			KitBox.CollidingBox( root, prefix + "Front",
				new Vector3( faceX, cy, zMid ),
				new Vector3( t, halfW * 2f, h ),
				wallC );
			return;
		}

		FillFaceAroundOpenings( root, prefix + "Front", faceX, yMin, yMax, zMin, zMax, t, wallC, openings );

		foreach ( var o in openings )
		{
			if ( !o.Glaze )
				continue;
			PlaceWindowGlass( root, faceX + t * 0.5f, o.Cy, o.Zc, o.W, o.H, trimC, o.Shutters );
		}
	}

	private static void FillFaceAroundOpenings(
		GameObject root,
		string name,
		float faceX,
		float yMin,
		float yMax,
		float zMin,
		float zMax,
		float t,
		Color wallC,
		List<OpeningRect> openings )
	{
		var zs = new List<float> { zMin, zMax };
		foreach ( var o in openings )
		{
			zs.Add( SafeClamp( o.Z0, zMin, zMax ) );
			zs.Add( SafeClamp( o.Z1, zMin, zMax ) );
		}

		zs.Sort();
		DedupSorted( zs, 1.5f );

		for ( var i = 0; i < zs.Count - 1; i++ )
		{
			var a = zs[i];
			var b = zs[i + 1];
			var bandH = b - a;
			if ( bandH < 3f )
				continue;

			var zc = (a + b) * 0.5f;
			var blocked = new List<(float y0, float y1)>();
			foreach ( var o in openings )
			{
				if ( o.Z1 <= a + 0.5f || o.Z0 >= b - 0.5f )
					continue;
				blocked.Add( (SafeClamp( o.Y0, yMin, yMax ), SafeClamp( o.Y1, yMin, yMax )) );
			}

			blocked.Sort( ( u, v ) => u.y0.CompareTo( v.y0 ) );
			MergeIntervals( blocked );

			var cursor = yMin;
			foreach ( var (y0, y1) in blocked )
			{
				var span = y0 - cursor;
				if ( span > 3f )
				{
					KitBox.CollidingBox( root, name,
						new Vector3( faceX, cursor + span * 0.5f, zc ),
						new Vector3( t, span, bandH ),
						wallC );
				}

				cursor = MathF.Max( cursor, y1 );
			}

			var trail = yMax - cursor;
			if ( trail > 3f )
			{
				KitBox.CollidingBox( root, name,
					new Vector3( faceX, cursor + trail * 0.5f, zc ),
					new Vector3( t, trail, bandH ),
					wallC );
			}
		}
	}

	private static void DedupSorted( List<float> values, float eps )
	{
		for ( var i = values.Count - 1; i > 0; i-- )
		{
			if ( values[i] - values[i - 1] < eps )
				values.RemoveAt( i );
		}
	}

	private static void MergeIntervals( List<(float y0, float y1)> intervals )
	{
		if ( intervals.Count <= 1 )
			return;
		var write = 0;
		for ( var read = 1; read < intervals.Count; read++ )
		{
			var cur = intervals[write];
			var next = intervals[read];
			if ( next.y0 <= cur.y1 + 1.5f )
			{
				intervals[write] = (cur.y0, MathF.Max( cur.y1, next.y1 ));
			}
			else
			{
				write++;
				intervals[write] = next;
			}
		}

		if ( write + 1 < intervals.Count )
			intervals.RemoveRange( write + 1, intervals.Count - write - 1 );
	}

	private static void PlaceWindowGlass( GameObject root, float faceOuterX, float cy, float z, float winW, float winH, Color trimC, bool shutters )
	{
		var glassT = MathF.Max( 3f, WallT * 0.35f );
		var rim = 5f;
		var fx = faceOuterX + Depth.Step * 0.5f;
		// Rim strips only — never a full opaque plate behind the glass.
		KitBox.Box( root, "WinSill",
			new Vector3( fx, cy, z - winH * 0.5f - rim * 0.5f ),
			new Vector3( 4f, winW + rim * 2f, rim ),
			trimC );
		KitBox.Box( root, "WinHead",
			new Vector3( fx, cy, z + winH * 0.5f + rim * 0.5f ),
			new Vector3( 4f, winW + rim * 2f, rim ),
			trimC );
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "WinJamb",
				new Vector3( fx, cy + sy * (winW * 0.5f + rim * 0.5f), z ),
				new Vector3( 4f, rim, winH ),
				trimC );
		}

		KitBox.Box( root, "WinGlass",
			new Vector3( faceOuterX - WallT * 0.25f, cy, z ),
			new Vector3( glassT, winW * 0.96f, winH * 0.96f ),
			Palette.WindowGlass,
			opaque: false );
		if ( !shutters )
			return;
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "Shutter",
				new Vector3( faceOuterX + Depth.Step, cy + sy * (winW * 0.55f), z ),
				new Vector3( 4f, 12f, winH * 0.85f ),
				Palette.HouseDoor );
		}
	}

	private static void BuildSide(
		GameObject root,
		string name,
		float cx,
		float cy,
		float halfD,
		float halfW,
		float zMid,
		float depth,
		float h,
		float t,
		Color wallC,
		float sideSign,
		float? openA,
		float passA,
		float passXA,
		float? openB,
		float passB,
		float passXB,
		float? omitSideSign )
	{
		if ( omitSideSign.HasValue && MathF.Sign( omitSideSign.Value ) == MathF.Sign( sideSign ) )
			return;

		var wantsOpen = (openA.HasValue && MathF.Sign( openA.Value ) == MathF.Sign( sideSign ))
			|| (openB.HasValue && MathF.Sign( openB.Value ) == MathF.Sign( sideSign ));
		var passW = 0f;
		var passX = cx;
		if ( openA.HasValue && MathF.Sign( openA.Value ) == MathF.Sign( sideSign ) )
		{
			passW = passA;
			passX = passXA;
		}
		else if ( openB.HasValue && MathF.Sign( openB.Value ) == MathF.Sign( sideSign ) )
		{
			passW = passB;
			passX = passXB;
		}

		var y = cy + sideSign * (halfW - t * 0.5f);
		var xMin = cx - halfD;
		var xMax = cx + halfD;

		if ( !wantsOpen || passW <= t )
		{
			KitBox.CollidingBox( root, name,
				new Vector3( cx, y, zMid ),
				new Vector3( depth, t, h ),
				wallC );
			return;
		}

		var passMin = xMin + passW * 0.5f + t;
		var passMax = xMax - passW * 0.5f - t;
		if ( passMin > passMax )
		{
			// Opening wider than wall — leave a solid wall.
			KitBox.CollidingBox( root, name,
				new Vector3( cx, y, zMid ),
				new Vector3( depth, t, h ),
				wallC );
			return;
		}

		passX = SafeClamp( passX, passMin, passMax );
		var gapLo = passX - passW * 0.5f;
		var gapHi = passX + passW * 0.5f;
		var leftD = gapLo - xMin;
		var rightD = xMax - gapHi;
		if ( leftD > t * 0.5f )
		{
			KitBox.CollidingBox( root, name,
				new Vector3( xMin + leftD * 0.5f, y, zMid ),
				new Vector3( leftD, t, h ),
				wallC );
		}

		if ( rightD > t * 0.5f )
		{
			KitBox.CollidingBox( root, name,
				new Vector3( xMax - rightD * 0.5f, y, zMid ),
				new Vector3( rightD, t, h ),
				wallC );
		}

		var lintH = MathF.Max( 16f, h * 0.18f );
		KitBox.CollidingBox( root, name + "Lint",
			new Vector3( passX, y, zMid + h * 0.5f - Depth.CenterLift( lintH ) ),
			new Vector3( passW + t, t, lintH ),
			wallC );
	}

	public static void OpenDoorTrim( GameObject root, float frontX, float cy, float wallBase, float doorW, float doorH, Color trimC, Color doorC )
	{
		var faceX = frontX + Depth.Step;
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "DoorJamb",
				new Vector3( faceX, cy + sy * (doorW * 0.5f + 4f), wallBase + doorH * 0.5f ),
				new Vector3( 6f, 8f, doorH + 8f ),
				trimC );
		}

		KitBox.Box( root, "DoorHeader",
			new Vector3( faceX, cy, wallBase + doorH + 5f ),
			new Vector3( 6f, doorW + 16f, 10f ),
			trimC );
		// Leaf parked beside the opening, hinged on +Y jamb.
		KitBox.Box( root, "DoorOpen",
			new Vector3( frontX + 14f, cy + doorW * 0.5f + 18f, wallBase + doorH * 0.5f ),
			new Vector3( 4f, doorW * 0.85f, doorH * 0.92f ),
			doorC,
			new Angles( 0f, 85f, 0f ) );
	}

	/// <summary>
	/// Overlay frame+glass. Prefer punching via <see cref="HollowRoom"/> frontWindows so walls open.
	/// </summary>
	public static void WindowOnFront( GameObject root, float faceX, float cy, float z, float winW, float winH, Color trimC, bool shutters = false )
		=> PlaceWindowGlass( root, faceX, cy, z, winW, winH, trimC, shutters );

	public static void WindowOnSide( GameObject root, float cx, float faceY, float z, float winW, float winH, Color trimC )
	{
		var sign = MathF.Sign( faceY );
		if ( sign == 0 )
			sign = 1;
		var rim = 5f;
		var fy = faceY + sign * Depth.Step * 0.5f;
		KitBox.Box( root, "WinSill",
			new Vector3( cx, fy, z - winH * 0.5f - rim * 0.5f ),
			new Vector3( winW + rim * 2f, 4f, rim ),
			trimC );
		KitBox.Box( root, "WinHead",
			new Vector3( cx, fy, z + winH * 0.5f + rim * 0.5f ),
			new Vector3( winW + rim * 2f, 4f, rim ),
			trimC );
		foreach ( var sx in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "WinJamb",
				new Vector3( cx + sx * (winW * 0.5f + rim * 0.5f), fy, z ),
				new Vector3( rim, 4f, winH ),
				trimC );
		}

		KitBox.Box( root, "WinGlass",
			new Vector3( cx, faceY - sign * WallT * 0.25f, z ),
			new Vector3( winW * 0.96f, MathF.Max( 3f, WallT * 0.35f ), winH * 0.96f ),
			Palette.WindowGlass,
			opaque: false );
	}

	/// <summary>
	/// Porch deck top flush with foundation/sill; steps climb from grade (z=0) to deck;
	/// posts hold a roof that meets the posts (no floating canopy).
	/// </summary>
	public static void Porch( GameObject root, float frontX, float cy, float porchD, float porchW, float wallBase, float wallH, Color trimC, Color roofC, bool deepRoof )
	{
		const float deckH = 8f;
		var deckTop = wallBase;
		// Tuck under the front wall slightly so the deck reads attached.
		var porchX = frontX + porchD * 0.5f - 4f;
		KitBox.CollidingBox( root, "PorchDeck",
			new Vector3( porchX, cy, deckTop - deckH * 0.5f ),
			new Vector3( porchD + 8f, porchW, deckH ),
			Palette.HouseFoundation );

		BuildGradeSteps( root, porchX + porchD * 0.35f, cy, deckTop, porchW * 0.55f );

		var postH = MathF.Min( wallH * 0.55f, deepRoof ? 95f : 80f );
		var postTop = deckTop + postH;
		var postSize = deepRoof ? 12f : 9f;
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.CollidingBox( root, "PorchPost",
				new Vector3( porchX + porchD * 0.28f, cy + sy * (porchW * 0.38f), deckTop + postH * 0.5f ),
				new Vector3( postSize, postSize, postH ),
				trimC );
		}

		var roofH = deepRoof ? 12f : 9f;
		KitBox.Box( root, "PorchRoof",
			new Vector3( porchX + 4f, cy, postTop + roofH * 0.5f ),
			new Vector3( porchD + (deepRoof ? 14f : 8f), porchW + (deepRoof ? 16f : 8f), roofH ),
			roofC );

		if ( deepRoof )
		{
			KitBox.Box( root, "CraftBeam",
				new Vector3( porchX + porchD * 0.15f, cy, postTop - 6f ),
				new Vector3( 8f, porchW * 0.75f, 8f ),
				trimC );
		}
	}

	/// <summary>Simple stoop: deck at sill + steps from grade.</summary>
	public static void Stoop( GameObject root, float frontX, float cy, float wallBase, float stoopW )
	{
		const float deckH = 7f;
		var deckD = 30f;
		var porchX = frontX + deckD * 0.5f - 2f;
		KitBox.CollidingBox( root, "Stoop",
			new Vector3( porchX, cy, wallBase - deckH * 0.5f ),
			new Vector3( deckD, stoopW, deckH ),
			Palette.HouseFoundation );
		BuildGradeSteps( root, porchX + deckD * 0.25f, cy, wallBase, stoopW * 0.7f );
	}

	private static void BuildGradeSteps( GameObject root, float x, float cy, float deckTop, float stepW )
	{
		// Climb from grade (0) up to deckTop with stacked risers — no floating midair slabs.
		var rise = MathF.Max( deckTop, 8f );
		var steps = (int)MathF.Ceiling( rise / 7f );
		if ( steps < 2 )
			steps = 2;
		else if ( steps > 5 )
			steps = 5;
		var stepH = rise / steps;
		var stepD = 16f;
		for ( var i = 0; i < steps; i++ )
		{
			var top = (i + 1) * stepH;
			KitBox.CollidingBox( root, "Step",
				new Vector3( x + i * (stepD * 0.7f), cy, top - stepH * 0.5f ),
				new Vector3( stepD, stepW * (1f - i * 0.06f), stepH ),
				Palette.HouseFoundation );
		}
	}

	/// <param name="maxRun">
	/// Cap along −X so the stair/well fits inside the room depth (Colonial rooms are ~220).
	/// </param>
	public static (float wellX, float wellY, float wellD, float wellW) Stairs(
		GameObject root,
		float startX,
		float cy,
		float wallBase,
		float landingZ,
		int steps = 10,
		float maxRun = CityScale.HouseStory )
	{
		var rise = MathF.Max( 40f, landingZ - wallBase );
		var stepW = 56f;
		var stepH = rise / MathF.Max( 1f, (float)steps );
		if ( stepH > 14f )
		{
			steps = (int)MathF.Ceiling( rise / 14f );
			if ( steps < 1 )
				steps = 1;
			stepH = rise / steps;
		}

		var stepD = MathF.Min( 18f, maxRun / MathF.Max( 1f, (float)steps ) );
		for ( var i = 0; i < steps; i++ )
		{
			KitBox.CollidingBox( root, "Stair",
				new Vector3( startX - i * stepD, cy, wallBase + stepH * 0.5f + i * stepH ),
				new Vector3( stepD, stepW, MathF.Max( 5f, stepH ) ),
				Palette.WoodDark );
		}

		var topX = startX - (steps - 0.5f) * stepD;
		KitBox.CollidingBox( root, "Landing",
			new Vector3( topX - 14f, cy, landingZ - 3f ),
			new Vector3( 28f, stepW + 6f, 6f ),
			Palette.Wood );

		var wellD = MathF.Min( maxRun + 16f, steps * stepD + 20f );
		var wellX = startX - wellD * 0.5f + stepD * 0.5f;
		return (wellX, cy, wellD, stepW + 12f);
	}

	/// <summary>
	/// Walkable multi-story stack. Ground floor is enterable; stairs reach every level.
	/// Returns top wall-plate Z (for crowns / roofs).
	/// </summary>
	public static float AccessibleStack(
		GameObject root,
		float cx,
		float cy,
		float depth,
		float width,
		float wallBase,
		float storyH,
		int floors,
		Color wallC,
		Color floorC,
		bool groundFrontDoor = true,
		bool groundOpenFront = false,
		float doorW = DoorW,
		float doorH = DoorH,
		int windowCols = 0,
		float windowHFrac = 0.42f,
		Color? windowTrim = null )
	{
		if ( floors < 1 )
			floors = 1;
		var stairStartX = cx + depth * 0.2f;
		var stairY = cy;
		var maxRun = depth * 0.55f;
		var wellX = cx;
		var wellY = cy;
		var wellD = 80f;
		var wellW = 68f;
		var multi = floors > 1;
		var trim = windowTrim ?? KitBox.Solid( wallC, 1.1f );

		for ( var f = 0; f < floors; f++ )
		{
			var baseZ = wallBase + f * storyH;
			var isGround = f == 0;
			var isTop = f == floors - 1;

			if ( multi && !isTop )
			{
				var landingZ = baseZ + storyH + FloorT;
				(wellX, wellY, wellD, wellW) = Stairs( root, stairStartX, stairY, baseZ, landingZ, steps: 10, maxRun: maxRun );
			}

			FrontWindow[] wins = null;
			if ( windowCols > 0 && !(isGround && groundOpenFront) )
			{
				var list = new List<FrontWindow>();
				var winH = storyH * windowHFrac;
				var winZ = baseZ + storyH * 0.52f;
				var pitch = width / (windowCols + 1);
				var winW = pitch * 0.55f;
				for ( var c = 0; c < windowCols; c++ )
				{
					var wcy = cy - width * 0.5f + pitch * (c + 1);
					// Keep clear of the ground door opening.
					if ( isGround && groundFrontDoor && MathF.Abs( wcy - cy ) < doorW * 0.65f )
						continue;
					list.Add( new FrontWindow( wcy, winZ, winW, winH ) );
				}

				if ( list.Count > 0 )
					wins = list.ToArray();
			}

			HollowRoom( root, $"F{f}", cx, cy, depth, width, baseZ, storyH, wallC, floorC,
				frontDoor: isGround && groundFrontDoor && !groundOpenFront,
				openFront: isGround && groundOpenFront,
				doorW: doorW,
				doorH: MathF.Min( doorH, storyH * 0.72f ),
				stairWellX: multi ? wellX : null,
				stairWellY: wellY,
				stairWellD: wellD,
				stairWellW: wellW,
				wellInFloor: multi && !isGround,
				wellInCeiling: multi && !isTop,
				frontWindows: wins,
				windowTrim: trim );
		}

		return wallBase + floors * storyH;
	}

	public static void Gable( GameObject root, float cx, float cy, float widthAlongY, float lengthAlongX, float wallTop, Color roofC, float riseFrac = 0.30f )
	{
		var hw = widthAlongY * 0.5f;
		var rise = MathF.Max( 22f, widthAlongY * riseFrac );
		var slope = MathF.Sqrt( hw * hw + rise * rise );
		var angle = MathF.Atan2( rise, hw ) * RadToDeg;
		var roofLen = lengthAlongX + 16f;
		const float roofThick = 12f;
		// Seat eave line on the wall plate (small upward bias only).
		var z = wallTop + rise * 0.5f + Depth.Step;

		KitBox.Box( root, "RoofR",
			new Vector3( cx, cy + hw * 0.45f, z ),
			new Vector3( roofLen, slope, roofThick ),
			roofC,
			new Angles( 0f, 0f, -angle ) );
		KitBox.Box( root, "RoofL",
			new Vector3( cx, cy - hw * 0.45f, z ),
			new Vector3( roofLen, slope, roofThick ),
			roofC,
			new Angles( 0f, 0f, angle ) );
	}

	public static void HipRoof( GameObject root, float cx, float cy, float widthAlongY, float lengthAlongX, float wallTop, Color roofC )
	{
		const float deckH = 12f;
		KitBox.Box( root, "RoofDeck",
			new Vector3( cx, cy, wallTop + deckH * 0.5f + Depth.Step ),
			new Vector3( lengthAlongX + 12f, widthAlongY + 12f, deckH ),
			roofC );
		KitBox.Box( root, "RoofCap",
			new Vector3( cx, cy, wallTop + deckH + 5f ),
			new Vector3( lengthAlongX * 0.55f, widthAlongY * 0.35f, 8f ),
			KitBox.Solid( roofC, 0.9f ) );
	}

	public static void Chimney( GameObject root, float cx, float cy, float wallTop, float roofRise, Color brick )
	{
		var h = 50f;
		KitBox.Box( root, "Chimney",
			new Vector3( cx, cy, wallTop + roofRise * 0.35f + h * 0.5f ),
			new Vector3( 22f, 22f, h ),
			brick );
	}

	public static void GarageFrame( GameObject root, float garX, float garY, float garD, float garW, float wallBase, float garH, Color trimC )
	{
		var faceX = garX + garD * 0.5f + Depth.Step;
		KitBox.Box( root, "GarageTrim",
			new Vector3( faceX, garY, wallBase + garH - 8f ),
			new Vector3( 5f, garW * 0.88f, 12f ),
			trimC );
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "GarageJamb",
				new Vector3( faceX, garY + sy * (garW * 0.5f - 8f), wallBase + garH * 0.45f ),
				new Vector3( 5f, 10f, garH * 0.85f ),
				trimC );
		}
	}
}
