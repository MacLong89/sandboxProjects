namespace Sandbox;

[Title( "Aimbox Viewmodel Tuner" )]
[Category( "Aimbox" )]
public sealed class AimboxViewModelTuner : Component
{
	[Property, Group( "Follow" )] public bool FollowCamera { get; set; } = true;

	/// <summary>When false, ADS pose comes from the anim graph (matches terraingen). When true, swaps to the ADS transform group below.</summary>
	[Property, Group( "Follow" )] public bool UseAdsTransformOverride { get; set; }

	[Property, Group( "Hip Transform" )] public Vector3 HipOffset { get; set; }
	[Property, Group( "Hip Transform" )] public Vector3 HipRotation { get; set; }
	[Property, Group( "Hip Transform" )] public Vector3 HipScale { get; set; }

	[Property, Group( "ADS Transform" )] public Vector3 AdsOffset { get; set; }
	[Property, Group( "ADS Transform" )] public Vector3 AdsRotation { get; set; }
	[Property, Group( "ADS Transform" )] public Vector3 AdsScale { get; set; }

	/// <summary>View-local forward slide while ADS (anim graph handles pose; this is an optional extra nudge).</summary>
	[Property, Group( "ADS Transform" )] public float AdsForwardOffset { get; set; }
	[Property, Group( "ADS Transform" )] public float AdsOffsetLerpSpeed { get; set; } = 32f;

	[Property, Group( "Model" )] public bool UseModelOverride { get; set; }
	[Property, Group( "Model" )] public string ModelOverride { get; set; } = "";

	[Property, Group( "Debug" )] public bool LogTransform { get; set; }
	[Property, Group( "Debug" )] public bool ForceVisible { get; set; } = true;

	public Vector3 GetOffset( bool ads )
	{
		if ( ads && UseAdsTransformOverride )
			return AdsOffset.AlmostEqual( Vector3.Zero ) ? new( 18f, 2f, -7f ) : AdsOffset;

		return HipOffset.AlmostEqual( Vector3.Zero ) ? new( 22f, 9f, -9f ) : HipOffset;
	}

	public Vector3 GetEulerRotation( bool ads )
	{
		if ( ads && UseAdsTransformOverride )
			return AdsRotation.AlmostEqual( Vector3.Zero ) ? new( -4f, 0f, 0f ) : AdsRotation;

		return HipRotation.AlmostEqual( Vector3.Zero ) ? new( -6f, 3f, 0f ) : HipRotation;
	}

	public Vector3 GetScale( bool ads )
	{
		if ( ads && UseAdsTransformOverride )
			return AdsScale.AlmostEqual( Vector3.Zero ) ? Vector3.One : AdsScale;

		return HipScale.AlmostEqual( Vector3.Zero ) ? Vector3.One : HipScale;
	}

	public string ResolveModelPath( AimboxWeaponDefinition weapon )
	{
		if ( UseModelOverride && !string.IsNullOrWhiteSpace( ModelOverride ) )
		{
			var trimmed = ModelOverride.Trim();
			if ( !trimmed.Contains( "first_person_arms_preview", StringComparison.OrdinalIgnoreCase )
			     && !trimmed.Contains( "v_first_person_arms_citizen", StringComparison.OrdinalIgnoreCase ) )
				return trimmed;
		}

		return weapon?.ViewModelPath;
	}
}
