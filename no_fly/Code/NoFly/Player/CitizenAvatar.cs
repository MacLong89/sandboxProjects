using Sandbox.Citizen;

namespace NoFly;

/// <summary>
/// Stock sbox citizen mesh + <see cref="CitizenAnimationHelper"/> for players and NPCs.
/// </summary>
public sealed class CitizenAvatar : Component
{
	public SkinnedModelRenderer Skin { get; private set; }
	public CitizenAnimationHelper Anim { get; private set; }

	Vector3 _lastPos;
	bool _hasLastPos;
	bool _wasGrounded = true;

	public void Ensure( Color tint, Connection dressFrom = null )
	{
		if ( Skin.IsValid() ) return;

		var body = new GameObject( true, "CitizenBody" );
		body.SetParent( GameObject );
		body.LocalPosition = Vector3.Zero;
		body.LocalRotation = Rotation.Identity;

		Skin = body.Components.Create<SkinnedModelRenderer>();
		Skin.Model = Model.Load( Kit.CitizenModel );
		Skin.UseAnimGraph = true;
		Skin.Tint = tint;

		Anim = body.Components.Create<CitizenAnimationHelper>();
		Anim.Target = Skin;
		Anim.HoldType = CitizenAnimationHelper.HoldTypes.None;
		Anim.MoveStyle = CitizenAnimationHelper.MoveStyles.Walk;

		if ( dressFrom is not null )
		{
			try
			{
				var clothing = ClothingContainer.CreateFromConnection( dressFrom );
				clothing?.Apply( Skin );
			}
			catch
			{
				// Offline / bots — bare citizen is fine.
			}
		}
	}

	public void SetTint( Color tint )
	{
		if ( Skin.IsValid() )
			Skin.Tint = tint;
	}

	/// <summary>Hide the mesh for local first-person (keep shadows so others still see a silhouette on the ground).</summary>
	public void SetLocalFirstPerson( bool firstPerson )
	{
		if ( !Skin.IsValid() ) return;
		Skin.RenderType = firstPerson
			? ModelRenderer.ShadowRenderType.ShadowsOnly
			: ModelRenderer.ShadowRenderType.On;
	}

	public void Tick( Vector3? velocity = null, bool? grounded = null, Angles? aim = null )
	{
		if ( !Anim.IsValid() ) return;

		Vector3 vel;
		if ( velocity.HasValue )
		{
			vel = velocity.Value;
		}
		else
		{
			var dt = MathF.Max( Time.Delta, 0.0001f );
			var pos = WorldPosition;
			if ( !_hasLastPos )
			{
				_lastPos = pos;
				_hasLastPos = true;
				vel = Vector3.Zero;
			}
			else
			{
				vel = (pos - _lastPos) / dt;
			}
			_lastPos = pos;
		}

		var onGround = grounded ?? true;
		Anim.WithVelocity( vel );
		Anim.WithWishVelocity( vel.WithZ( 0f ) );
		Anim.IsGrounded = onGround;
		Anim.WithLook( Rotation.From( aim ?? WorldRotation.Angles() ).Forward );

		if ( _wasGrounded && !onGround && vel.z > 80f )
			Anim.TriggerJump();
		_wasGrounded = onGround;

		var speed = vel.WithZ( 0f ).Length;
		Anim.MoveStyle = speed > 220f
			? CitizenAnimationHelper.MoveStyles.Run
			: CitizenAnimationHelper.MoveStyles.Walk;
	}
}
