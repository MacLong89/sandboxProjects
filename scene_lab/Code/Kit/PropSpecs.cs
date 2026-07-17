namespace SceneLab;

/// <summary>Numeric design system for kit props.</summary>
public static class PropSpecs
{
	public sealed class Sedan
	{
		public float Length = 175f;
		public float Width = 72f;
		public float Ride = 11f;
		public float SkirtH = 14f;
		public float BodyH = 22f;
		public float CabinH = 24f;
		public float WheelDiameter = 26f;
		public float WheelWidthFrac = 0.48f;
		public float WheelBaseFrac = 0.29f;
		public float WheelInsetFrac = 0.02f;
		public float HeadlightSpanFrac = 0.72f;
		public float TaillightSpanFrac = 0.78f;
		public static Sedan Default { get; } = new();
	}

	public sealed class Dumpster
	{
		public float Width = 110f;
		public float Depth = 70f;
		public float Height = 84f;
		public float LidOverhang = 4f;
		public float CasterDiameterFrac = 0.12f;
		public static Dumpster Default { get; } = new();
	}

	public sealed class Chair
	{
		public float SeatW = 42f;
		public float SeatD = 40f;
		public float SeatZ = 40f;
		public float BackH = 40f;
		public float LegT = 4f;
		public static Chair Default { get; } = new();
	}
}
