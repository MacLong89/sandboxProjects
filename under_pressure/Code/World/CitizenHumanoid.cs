using Sandbox.Citizen;

namespace UnderPressure;

/// <summary>
/// Spawns a Facepunch Citizen model with the standard animgraph and drives walk/look cycles.
/// Falls back gracefully if the citizen model isn't available.
/// </summary>
public sealed class CitizenHumanoid : Component
{
	public const string CitizenVmdl = "models/citizen/citizen.vmdl";
	public const string BodyChildName = "Body";
	public const float DefaultHeight = GameConstants.CitizenHeightScale;

	private GameObject _body;
	private SkinnedModelRenderer _renderer;
	private CitizenAnimationHelper _anim;
	private Vector3 _lastPosition;
	private bool _ready;

	public bool IsReady => _ready;

	/// <summary>Attach a citizen mesh + animation helper to this object.</summary>
	public bool TrySetup( float heightScale = DefaultHeight, Color? tint = null )
	{
		if ( _ready )
			return true;

		var model = Model.Load( CitizenVmdl );
		if ( model is null || !model.IsValid || model.IsError )
			return false;

		heightScale = Math.Clamp( heightScale, 0.85f, 1.15f );

		_body = FindNamedChild( GameObject, BodyChildName );
		if ( !_body.IsValid() )
		{
			_body = new GameObject( true, BodyChildName );
			_body.SetParent( GameObject );
		}

		_body.LocalPosition = Vector3.Zero;
		_body.LocalRotation = Rotation.Identity;
		_body.LocalScale = Vector3.One * heightScale;

		_renderer = _body.Components.Get<SkinnedModelRenderer>() ?? _body.Components.Create<SkinnedModelRenderer>();
		_renderer.Model = model;
		_renderer.Tint = tint ?? new Color( 0.88f, 0.82f, 0.76f, 1f );
		_renderer.UseAnimGraph = true;
		_renderer.CreateBoneObjects = true;
		_renderer.Enabled = true;

		_anim = _body.Components.Get<CitizenAnimationHelper>() ?? _body.Components.Create<CitizenAnimationHelper>();
		_anim.Target = _renderer;
		_anim.Height = heightScale;

		_lastPosition = WorldPosition;
		_ready = true;
		return true;
	}

	/// <summary>Drive locomotion from actual motion + desired direction.</summary>
	public void TickLocomotion( Vector3 wishVelocity, bool running = false )
	{
		if ( !_ready || _anim is null )
			return;

		var dt = Math.Max( Time.Delta, 0.001f );
		var velocity = (WorldPosition - _lastPosition) / dt;
		_lastPosition = WorldPosition;

		_anim.IsGrounded = true;
		_anim.MoveStyle = running
			? CitizenAnimationHelper.MoveStyles.Run
			: CitizenAnimationHelper.MoveStyles.Walk;
		_anim.WithVelocity( velocity );
		_anim.WithWishVelocity( wishVelocity.Length > 2f ? wishVelocity : velocity );
	}

	/// <summary>Turn head/body toward a world position (player, target, etc.).</summary>
	public void TickLookAt( Vector3 worldPosition )
	{
		if ( !_ready || _anim is null )
			return;

		var dir = (worldPosition - (WorldPosition + Vector3.Up * 60f)).Normal;
		if ( dir.Length < 0.01f )
			return;

		_anim.WithLook( dir, 1f, 0.8f, 0.35f );
	}

	public void TickIdle()
	{
		if ( !_ready || _anim is null )
			return;

		_lastPosition = WorldPosition;
		_anim.IsGrounded = true;
		_anim.MoveStyle = CitizenAnimationHelper.MoveStyles.Walk;
		_anim.WithVelocity( Vector3.Zero );
		_anim.WithWishVelocity( Vector3.Zero );
	}

	static GameObject FindNamedChild( GameObject root, string name )
	{
		if ( root is null || !root.IsValid() || string.IsNullOrWhiteSpace( name ) )
			return default;

		foreach ( var child in root.Children )
		{
			if ( child.IsValid() && string.Equals( child.Name, name, StringComparison.OrdinalIgnoreCase ) )
				return child;
		}

		return default;
	}
}
