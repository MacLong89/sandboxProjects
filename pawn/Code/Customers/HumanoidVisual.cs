namespace PawnShop;

/// <summary>
/// Chunky primitive person with a face, hair, hands, shoes, and the occasional hat
/// or bag. Limbs hang off unscaled pivot objects so they can swing while walking.
/// No external model dependencies.
/// </summary>
public sealed class HumanoidVisual : Component
{
	private GameObject _body;
	private GameObject _armL, _armR, _legL, _legR;
	private TextRenderer _nameLabel;
	private float _walkPhase;
	private Vector3 _lastPos;

	private static GameObject Pivot( GameObject parent, string name, Vector3 pos )
	{
		var go = new GameObject( parent, true, name );
		go.LocalPosition = pos;
		return go;
	}

	public void Setup( CustomerProfile profile )
	{
		_body = new GameObject( GameObject, true, "Body" );

		var shirt = profile.Shirt;
		var pants = profile.Pants;
		var skin = profile.Skin;
		var shoe = new Color( 0.16f, 0.13f, 0.11f );

		// Stable per-customer style rolls (seeded off the name so regulars keep their look).
		var seed = (uint)(profile.Name?.GetHashCode() ?? 0) * 2654435761u;
		float Roll( int salt ) => ((seed >> (salt * 4)) & 255u) / 255f;

		var heavy = Roll( 1 ) < 0.3f;                 // broader build
		var torsoW = heavy ? 22f : 18f;
		var torsoD = heavy ? 30f : 26f;

		// Legs: pivot at the hip so the swing looks like a stride.
		_legL = Pivot( _body, "LegL", new Vector3( 0, -7, 34 ) );
		_legR = Pivot( _body, "LegR", new Vector3( 0, 7, 34 ) );
		foreach ( var leg in new[] { _legL, _legR } )
		{
			MeshKit.Spawn( leg, "Leg", new Vector3( 0, 0, -17 ), new Vector3( 12, 11, 34 ), pants );
			MeshKit.Spawn( leg, "Shoe", new Vector3( 3, 0, -31.5f ), new Vector3( 16, 12, 5 ), shoe );
		}

		// Torso + belt + collar.
		MeshKit.Spawn( _body, "Torso", new Vector3( 0, 0, 55 ), new Vector3( torsoW, torsoD, 42 ), shirt );
		MeshKit.Spawn( _body, "Belt", new Vector3( 0, 0, 35.5f ), new Vector3( torsoW + 1, torsoD + 1, 4 ), Color.Lerp( pants, Color.Black, 0.35f ) );
		MeshKit.Spawn( _body, "Collar", new Vector3( 0, 0, 74.5f ), new Vector3( torsoW - 4, torsoD - 6, 3 ), Color.Lerp( shirt, Color.White, 0.25f ) );

		// Arms: pivot at the shoulder, hand sphere at the cuff.
		_armL = Pivot( _body, "ArmL", new Vector3( 0, -(torsoD * 0.5f + 5f), 74 ) );
		_armR = Pivot( _body, "ArmR", new Vector3( 0, torsoD * 0.5f + 5f, 74 ) );
		foreach ( var arm in new[] { _armL, _armR } )
		{
			MeshKit.Spawn( arm, "Arm", new Vector3( 0, 0, -19 ), new Vector3( 9, 8, 38 ), shirt );
			MeshKit.SpawnSphere( arm, "Hand", new Vector3( 0, 0, -40 ), 9f, skin );
		}

		// Head with a simple face. Customers face +X in local space.
		MeshKit.Spawn( _body, "Head", new Vector3( 0, 0, 86 ), new Vector3( 15, 15, 18 ), skin );
		var eye = new Color( 0.12f, 0.12f, 0.14f );
		MeshKit.Spawn( _body, "EyeL", new Vector3( 7.6f, -3.2f, 88.5f ), new Vector3( 1, 2.2f, 2.6f ), eye );
		MeshKit.Spawn( _body, "EyeR", new Vector3( 7.6f, 3.2f, 88.5f ), new Vector3( 1, 2.2f, 2.6f ), eye );
		MeshKit.Spawn( _body, "BrowL", new Vector3( 7.2f, -3.2f, 91.2f ), new Vector3( 1, 3.2f, 1.2f ), Color.Lerp( skin, Color.Black, 0.35f ) );
		MeshKit.Spawn( _body, "BrowR", new Vector3( 7.2f, 3.2f, 91.2f ), new Vector3( 1, 3.2f, 1.2f ), Color.Lerp( skin, Color.Black, 0.35f ) );
		MeshKit.Spawn( _body, "Nose", new Vector3( 8f, 0, 85 ), new Vector3( 2, 2, 3 ), Color.Lerp( skin, Color.Black, 0.12f ) );
		MeshKit.Spawn( _body, "Mouth", new Vector3( 7.4f, 0, 81.5f ), new Vector3( 1, 4.5f, 1.4f ), new Color( 0.45f, 0.2f, 0.2f ) );

		// Hair / hat.
		var hair = new ColorHsv( Roll( 3 ) * 50f, 0.3f + Roll( 4 ) * 0.5f, 0.1f + Roll( 5 ) * 0.4f );
		var hatRoll = Roll( 2 );
		if ( hatRoll < 0.22f )
		{
			// Flat cap with a brim.
			var capColor = Color.Lerp( pants, shirt, 0.5f );
			MeshKit.Spawn( _body, "Cap", new Vector3( 0, 0, 96.5f ), new Vector3( 16, 16, 5 ), capColor );
			MeshKit.Spawn( _body, "Brim", new Vector3( 9, 0, 94.8f ), new Vector3( 8, 13, 1.6f ), Color.Lerp( capColor, Color.Black, 0.3f ) );
		}
		else if ( hatRoll < 0.34f )
		{
			// Beanie.
			MeshKit.Spawn( _body, "Beanie", new Vector3( 0, 0, 96 ), new Vector3( 16, 16, 8 ), new ColorHsv( Roll( 6 ) * 360f, 0.5f, 0.45f ) );
		}
		else
		{
			MeshKit.Spawn( _body, "Hair", new Vector3( -1, 0, 96 ), new Vector3( 15, 16, 6 ), hair );
			MeshKit.Spawn( _body, "HairBack", new Vector3( -6.5f, 0, 88 ), new Vector3( 3, 15, 12 ), hair );
		}

		// Some customers carry a shoulder bag.
		if ( Roll( 7 ) < 0.3f )
		{
			MeshKit.Spawn( _body, "Strap", new Vector3( 0, 0, 62 ), new Vector3( torsoW + 1.5f, 4, 30 ), new Color( 0.35f, 0.26f, 0.16f ), new Angles( 0, 0, 28 ) );
			MeshKit.Spawn( _body, "Bag", new Vector3( 0, torsoD * 0.5f + 8f, 40 ), new Vector3( 12, 8, 14 ), new Color( 0.42f, 0.3f, 0.18f ) );
		}

		// Name label.
		var labelGo = new GameObject( GameObject, true, "Name" );
		labelGo.LocalPosition = new Vector3( 0, 0, 112 );
		_nameLabel = labelGo.Components.Create<TextRenderer>();
		_nameLabel.Text = profile.Name;
		_nameLabel.FontSize = 32;
		_nameLabel.Scale = 0.14f;
		_nameLabel.Color = new Color( 1f, 0.97f, 0.9f );
		_nameLabel.Billboard = TextRenderer.BillboardMode.YOnly;
		_nameLabel.HorizontalAlignment = TextRenderer.HAlignment.Center;

		_lastPos = WorldPosition;
	}

	public void SetMoodColor( float mood )
	{
		if ( _nameLabel.IsValid() )
			_nameLabel.Color = mood switch
			{
				>= 0.5f => new Color( 1f, 0.97f, 0.9f ),
				>= 0.3f => new Color( 1f, 0.75f, 0.4f ),
				_ => new Color( 1f, 0.4f, 0.35f ),
			};
	}

	protected override void OnUpdate()
	{
		if ( !_body.IsValid() ) return;

		var speed = (WorldPosition - _lastPos).WithZ( 0 ).Length / Math.Max( Time.Delta, 0.001f );
		_lastPos = WorldPosition;

		if ( speed > 10f )
		{
			_walkPhase += Time.Delta * 9f;
			var swing = MathF.Sin( _walkPhase ) * 14f;
			if ( _legL.IsValid() ) _legL.LocalRotation = Rotation.FromPitch( swing );
			if ( _legR.IsValid() ) _legR.LocalRotation = Rotation.FromPitch( -swing );
			if ( _armL.IsValid() ) _armL.LocalRotation = Rotation.FromPitch( -swing * 0.7f );
			if ( _armR.IsValid() ) _armR.LocalRotation = Rotation.FromPitch( swing * 0.7f );
			_body.LocalPosition = new Vector3( 0, 0, MathF.Abs( MathF.Sin( _walkPhase ) ) * 2.2f );
		}
		else
		{
			_walkPhase = 0f;
			if ( _legL.IsValid() ) _legL.LocalRotation = Rotation.Identity;
			if ( _legR.IsValid() ) _legR.LocalRotation = Rotation.Identity;
			if ( _armL.IsValid() ) _armL.LocalRotation = Rotation.Identity;
			if ( _armR.IsValid() ) _armR.LocalRotation = Rotation.Identity;
			_body.LocalPosition = Vector3.Zero;
		}
	}
}
