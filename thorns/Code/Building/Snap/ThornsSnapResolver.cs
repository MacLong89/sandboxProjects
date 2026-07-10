using System.Linq;

namespace Sandbox;

/// <summary>Socket-based placement — no global XY grid rounding; terrain seed uses exact ray hit XY.</summary>
public static class ThornsSnapResolver
{
	static bool _snapLogPrimed;
	static Guid _snapLogHost;
	static ushort _snapLogEdge;
	static ushort _snapLogPlug;
	static ThornsSnapChannel _snapLogChannel;

	static int _terrainSnapLogCzBucket = int.MinValue;
	static int _terrainSnapLogRx = int.MinValue;
	static int _terrainSnapLogRy = int.MinValue;

	public const float SocketSearchRadiusUnits = 90f;

	/// <summary>Alias — prefer <see cref="SocketSearchRadiusUnits"/>.</summary>
	public const float SocketSearchRadius = SocketSearchRadiusUnits;
	public const float AuthoritativePoseTolerance = 8f;

	public static int OppositeEdgeIndex( int edge0123 ) => (edge0123 + 2) % 4;

	public static bool ClientTrySolve(
		Vector3 aimNearWorld,
		IReadOnlyList<ThornsPlacedStructure> hostsNear,
	string incomingDefId,
	out ThornsPlacementSuggestion suggest )
	{
		suggest = new ThornsPlacementSuggestion();

		var plugs = ThornsSnapBlueprintLibrary.GetPlugsFor( incomingDefId );
		if ( plugs.Length == 0 )
			return false;

		var bestDist = float.MaxValue;
		var solved = false;

		foreach ( var ps in hostsNear )
		{
			if ( ps is null || !ps.GameObject.IsValid() )
				continue;

			foreach ( var soc in ThornsSnapBlueprintLibrary.GetSocketsFor( ps.StructureDefId ) )
			foreach ( var plug in plugs )
			{
				if ( !soc.Accepts.Contains( plug.Channel ) )
					continue;

				if ( !TryComputePose( ps,
					    soc.SocketIndex,
					    incomingDefId,
					    plug.Channel,
					    out var posGuess,
					    out var rotGuess ) )
					continue;

				var piv = ThornsSnapTransforms.WorldPivot( ps, soc );
				float d = ThornsSnapTransforms.ScoreAimProximity( piv, aimNearWorld );

				if ( d > SocketSearchRadiusUnits )
					continue;

				if ( !(d < bestDist) )
					continue;

				solved = true;
				bestDist = d;

				suggest.UsesSocketSnap = true;
				suggest.Channel = plug.Channel;
				suggest.HostSnap = new ThornsPlacementSocketBind( ps.InstanceId, ushortMathClamped( soc.SocketIndex ) );
				suggest.IncomingPlugIndex = ushortMathClamped( plug.PlugIndex );
				suggest.OppositeTwinSocketPreview = plug.Channel is ThornsSnapChannel.FloorSeatOnWallTop
						or ThornsSnapChannel.RampSeatOnFoundationTop
					? (ushort)0
					: ushortMathClamped( ThornsSnapResolver.OppositeEdgeIndex( soc.SocketIndex ) );
				suggest.ProposedWorldPosition = posGuess;
				suggest.ProposedWorldRotation = rotGuess;
				suggest.TerrainKind = ThornsTerrainSeedKind.NotTerrain;
			}
		}

		if ( solved )
		{
			var g = suggest.HostSnap.InstanceGuid;
			var e = suggest.HostSnap.SocketIndex;
			var p = suggest.IncomingPlugIndex;
			var ch = suggest.Channel;
			if ( !_snapLogPrimed || g != _snapLogHost || e != _snapLogEdge || p != _snapLogPlug || ch != _snapLogChannel )
			{
				_snapLogPrimed = true;
				_snapLogHost = g;
				_snapLogEdge = e;
				_snapLogPlug = p;
				_snapLogChannel = ch;
				Log.Info(
					$"[Thorns] SNAP pick host={g} edge={e} plug={p} ch={ch}" );
			}

			return true;
		}

		if ( incomingDefId != "wood_foundation" )
			return false;

		// Z is replaced by ClampFoundationTerrainSlabToSurface from terrain under XY; start from aim height so preview is stable before clamp.
		var cz = aimNearWorld.z + ThornsBuildingModule.FloorThickness * 0.5f;

		suggest.ProposedWorldPosition = new Vector3( aimNearWorld.x, aimNearWorld.y, cz );
		suggest.ProposedWorldRotation = Rotation.Identity;
		suggest.UsesSocketSnap = false;
		suggest.TerrainKind = ThornsTerrainSeedKind.SlabOnRay;

		var rx = (int)MathF.Round( aimNearWorld.x * 0.25f );
		var ry = (int)MathF.Round( aimNearWorld.y * 0.25f );
		var czBucket = (int)MathF.Round( cz * 0.05f );
		if ( czBucket != _terrainSnapLogCzBucket || rx != _terrainSnapLogRx || ry != _terrainSnapLogRy )
		{
			_terrainSnapLogCzBucket = czBucket;
			_terrainSnapLogRx = rx;
			_terrainSnapLogRy = ry;
			Log.Info(
				$"[Thorns] SNAP terrain seed xy=({aimNearWorld.x:F1},{aimNearWorld.y:F1}) cz≈{cz:F1}" );
		}

		return true;
	}

	static ushort ushortMathClamped( int v ) =>
		(ushort)Math.Clamp( v, 0, 65535 );

	/// <summary>Host mirrors client — same inputs must reproduce pose before accepting RPC.</summary>
	public static bool HostTryReplaySnap(
		ThornsPlacedStructure host,
		int hostSocketIndex,
	string incomingDefId,
	ThornsSnapChannel channel,
		out Vector3 pos,
	out Rotation rot )
	{
		pos = default;
		rot = default;

		var sockets = ThornsSnapBlueprintLibrary.GetSocketsFor( host.StructureDefId );
		ThornsSnapSocketBlueprint found = null;

		foreach ( var s in sockets )
		{
			if ( s.SocketIndex == hostSocketIndex )
			{
				found = s;
				break;
			}
		}

		if ( found is null )
			return false;

		if ( !ThornsSnapBlueprintLibrary.GetPlugsFor( incomingDefId ).Any( p => p.Channel == channel ) ||
		     !found.Accepts.Contains( channel ) )
			return false;

		return TryComputePose( host, hostSocketIndex, incomingDefId, channel, out pos, out rot );
	}

	public static bool TryComputePose(
		ThornsPlacedStructure host,
		int edgeSocketIndex,
	string incomingDefId,
	ThornsSnapChannel plugChannel,
	out Vector3 pos,
	out Rotation rot )
	{
		pos = default;
		rot = default;

		if ( plugChannel == ThornsSnapChannel.FoundationEdgeMate &&
		     incomingDefId == "wood_foundation" &&
		     host.StructureDefId == "wood_foundation" )
		{
			pos = ThornsSnapPoseUtility.NeighbourFoundationCenterWorldFromEdge(
				host.GameObject.WorldPosition,
				host.GameObject.WorldRotation,
				edgeSocketIndex );

			rot = host.GameObject.WorldRotation;
			return true;
		}

		if ( host.StructureDefId == "wood_foundation" &&
		     plugChannel == ThornsSnapChannel.RampSeatOnFoundationTop &&
		     incomingDefId == "wood_ramp" )
		{
			var slab = host.GameObject.WorldPosition;
			var basis = host.GameObject.WorldRotation;
			pos = new Vector3( slab.x, slab.y, ThornsSnapPoseUtility.RampSeatPivotZWorld( slab ) );
			rot = basis * Rotation.FromAxis( Vector3.Left, -MathF.PI / 4f );
			return true;
		}

		if ( host.StructureDefId == "wood_foundation" &&
		     plugChannel == ThornsSnapChannel.WallSeatOnFoundationEdge &&
		     IsWallFamily( incomingDefId ) )
		{
			var mid = ThornsSnapPoseUtility.FoundationEdgeMidpointHorizontal(
				host.GameObject.WorldPosition,
				host.GameObject.WorldRotation,
				edgeSocketIndex );

			var wz = ThornsSnapPoseUtility.WallMidpointCenterZWorld( host.GameObject.WorldPosition );

			pos = new Vector3( mid.x, mid.y, wz );

			var baseRot = ThornsSnapPoseUtility.WallRotationAroundUpFromEdge(
				host.GameObject.WorldRotation,
				edgeSocketIndex );

			rot = baseRot;

			return true;
		}

		if ( plugChannel == ThornsSnapChannel.FloorSeatOnWallTop &&
		     incomingDefId == "wood_foundation" &&
		     IsCeilingWallSocketHost( host.StructureDefId ) )
		{
			if ( host.StructureDefId == "wood_doorframe" && edgeSocketIndex == 0 )
				return false;

			return ThornsSnapPoseUtility.TryCeilingFoundationCentreFromWall( host, out pos, out rot );
		}

		if ( incomingDefId == "wood_door" &&
		     host.StructureDefId == "wood_doorframe" &&
		     plugChannel == ThornsSnapChannel.DoorPanelIntoFrame )
		{
			var f = host.GameObject.WorldPosition;
			var r = host.GameObject.WorldRotation;

			pos = f + r * ( ThornsBuildingModule.DoorPanelHingeLocal + ThornsBuildingModule.DoorPanelOffsetFromHinge );

			rot = r;
			return true;
		}

		return false;
	}

	static bool IsCeilingWallSocketHost( string id ) =>
		id is "wood_wall" or "wood_window" or "wood_doorframe";

	static bool IsWallFamily( string id ) =>
		id is "wood_wall" or "wood_window" or "wood_doorframe";
}
