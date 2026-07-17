namespace SceneLab;

/// <summary>
/// FROZEN parts. Sedan goal: short fat wheel disks, faces ±Y; horizontal light bars.
/// Source Angles (pitch,yaw,roll): Z-up cylinder → roll 90 → axle along Y.
/// </summary>
public static class KitParts
{
	/// <summary>Goal-style wheel: fat cylinder, circular face out, lighter hub ring.</summary>
	public static void Wheel( GameObject parent, Vector3 localPos, float diameter, float? width = null, Color? tire = null, Color? hub = null )
	{
		var w = width ?? diameter * 0.48f;
		var tireC = tire ?? Palette.CarTire;
		var hubC = hub ?? Palette.CarHub;

		// roll 90° about X → axle Y, faces ±Y (out the sides)
		var axle = new Angles( 0f, 0f, 90f );
		KitBox.Cylinder( parent, "Tire", localPos, diameter, w, tireC, axle );
		// Hub slightly narrower / smaller diameter, pushed toward outer face
		KitBox.Cylinder( parent, "Hub", localPos, diameter * 0.62f, w * 0.35f, hubC, axle );
	}

	/// <summary>Thin horizontal headlight bars near front corners.</summary>
	public static void HeadlightPair( GameObject parent, float noseX, float bodyWidth, float lightZ, float lightW = 18f, float lightH = 5f, float lightD = 3f, float spanFraction = 0.72f, Color? color = null )
	{
		var c = color ?? Palette.CarHeadlight;
		var halfSpan = bodyWidth * spanFraction * 0.5f;
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.Box( parent, "Headlight",
				new Vector3( noseX, sy * halfSpan, lightZ ),
				new Vector3( lightD, lightW, lightH ),
				c );
		}
	}

	/// <summary>Blocky red taillights at rear outer corners.</summary>
	public static void TaillightPair( GameObject parent, float tailX, float bodyWidth, float lightZ, float lightW = 14f, float lightH = 10f, float lightD = 3f, float spanFraction = 0.78f, Color? color = null )
	{
		var c = color ?? Palette.CarTaillight;
		var halfSpan = bodyWidth * spanFraction * 0.5f;
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.Box( parent, "Taillight",
				new Vector3( tailX, sy * halfSpan, lightZ ),
				new Vector3( lightD, lightW, lightH ),
				c );
		}
	}
}
