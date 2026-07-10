namespace UnderPressure;

/// <summary>
/// A non-interactive citizen that crosses the job site on a simple path.
/// Pure ambience — no combat, no collider, no re-soiling.
/// </summary>
public sealed class AmbientPedestrian : Component
{
	private CitizenHumanoid _humanoid;
	private Vector3 _destination;
	private float _walkSpeed;
	private float _alive;
	private bool _ready;

	public void Init( Vector3 start, Vector3 destination, float walkSpeed, Color skinTint, float heightScale )
	{
		WorldPosition = start.WithZ( 0f );
		_destination = destination.WithZ( 0f );
		_walkSpeed = walkSpeed;
		_alive = 0f;

		_humanoid = Components.Create<CitizenHumanoid>();
		_ready = _humanoid.TrySetup( heightScale, skinTint );

		var flat = (_destination - WorldPosition).WithZ( 0f );
		if ( flat.Length > 0.01f )
			WorldRotation = Rotation.LookAt( flat.Normal );
	}

	public bool IsExpired( Vector3 workCenter, float roamRadius )
	{
		if ( !_ready )
			return true;

		var toDest = (_destination - WorldPosition).WithZ( 0f );
		if ( toDest.Length <= 24f )
			return true;

		if ( _alive > 150f )
			return true;

		var fromWork = (WorldPosition - workCenter).WithZ( 0f );
		return fromWork.Length > roamRadius;
	}

	protected override void OnUpdate()
	{
		if ( !_ready || _humanoid is null )
			return;

		var core = GameCore.Instance;
		if ( core is null || core.IsWorldFrozen )
		{
			_humanoid.TickIdle();
			return;
		}

		_alive += Time.Delta;

		var toDest = (_destination - WorldPosition).WithZ( 0f );
		var dist = toDest.Length;
		if ( dist <= 0.01f )
		{
			_humanoid.TickIdle();
			return;
		}

		var dir = toDest / dist;
		var step = dir * _walkSpeed * Time.Delta;
		if ( step.Length >= dist )
			WorldPosition = _destination;
		else
			WorldPosition += step;

		WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( dir ), Time.Delta * 5f );
		_humanoid.TickLocomotion( dir * _walkSpeed, running: _walkSpeed > 52f );
	}
}
