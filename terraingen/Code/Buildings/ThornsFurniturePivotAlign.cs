namespace Terraingen.Buildings;

/// <summary>Mesh pivot alignment (ported from thorns <c>ThornsFoliageScatter</c>).</summary>
public static class ThornsFurniturePivotAlign
{
	public static Vector3 AlignPivotMeshBottomOnGround(
		Vector3 pivotWorld,
		Model model,
		Vector3 localScale,
		Rotation worldRotation )
	{
		if ( !model.IsValid() )
			return pivotWorld;

		var bb = model.Bounds;
		if ( bb.Size.LengthSquared < 1e-18f )
			return pivotWorld;

		var mn = bb.Center - bb.Size * 0.5f;
		var mx = bb.Center + bb.Size * 0.5f;

		var minWorldZ = float.MaxValue;
		for ( var corner = 0; corner < 8; corner++ )
		{
			var scaled = new Vector3(
				( ( corner & 1 ) == 0 ? mn.x : mx.x ) * localScale.x,
				( ( corner & 2 ) == 0 ? mn.y : mx.y ) * localScale.y,
				( ( corner & 4 ) == 0 ? mn.z : mx.z ) * localScale.z );
			var w = worldRotation * scaled;
			if ( w.z < minWorldZ )
				minWorldZ = w.z;
		}

		if ( minWorldZ > 1e29f )
			return pivotWorld;

		return pivotWorld + Vector3.Up * ( -minWorldZ );
	}
}
