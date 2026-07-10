namespace Terraingen.Core;

/// <summary>Uniform ±10% scale jitter for procedurally placed nature props.</summary>
public static class ThornsNatureScaleVariance
{
	public const float MinMultiplier = 0.9f;
	public const float MaxMultiplier = 1.1f;

	public static float Sample( Random rng ) =>
		MathX.Lerp( MinMultiplier, MaxMultiplier, rng.NextSingle() );

	public static float Apply( float uniformScale, Random rng ) =>
		uniformScale * Sample( rng );

	public static Vector3 Apply( Vector3 scale, Random rng ) =>
		scale * Sample( rng );
}
