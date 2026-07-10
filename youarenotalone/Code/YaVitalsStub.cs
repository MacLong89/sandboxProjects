namespace Sandbox;

/// <summary>
/// Crouch + (NotAlone-only) sprint stamina. Host simulates; intents from owners via RPC. Used by movement, citizen body, weapon FP.
/// </summary>
[Title( "YouAreNotAlone — Vitals" )]
[Category( "YouAreNotAlone" )]
[Icon( "favorite" )]
[Order( 40 )]
public sealed class YaVitalsStub : Component
{
	[Sync( SyncFlags.FromHost )] public bool ServerCrouching { get; set; }

	[Sync( SyncFlags.FromHost )] public bool ServerSprinting { get; set; }

	/// <summary>1 = full. Only Not Alone drains during sprint in-round; replicated for HUD.</summary>
	[Sync( SyncFlags.FromHost )] public float StaminaNormalized { get; set; } = 1f;

	float _prevParanoiaDebuffSeconds;

	[Property, Range( 0.35f, 0.65f )] public float CrouchHeightScale { get; set; } = 0.55f;

	[Property, Range( 0.35f, 0.85f )] public float CrouchMoveSpeedScale { get; set; } = 0.58f;

	[Property, Range( 1.1f, 2f )] public float SprintSpeedMultiplier { get; set; } = 1.48f;

	/// <summary>Seconds of holding sprint at full speed to go from full stamina to empty.</summary>
	[Property, Range( 0.5f, 8f )] public float NotAloneSprintDrainDurationSeconds { get; set; } = 2f;

	[Property, Range( 0.05f, 1f )] public float StaminaRegenPerSecond { get; set; } = 0.32f;

	bool _remoteDuck;
	bool _remoteSprint;
	float _remoteMoveAnalog;

	protected override void OnFixedUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		if ( Networking.IsHost )
		{
			HostSimulateLocomotion();
			return;
		}

		if ( YaPawn.IsLocalConnectionOwner( this ) )
			HostPushLocomotionIntent( ReadDuck(), ReadSprint(), Input.AnalogMove.Length );
	}

	static bool ReadDuck() => Input.Down( "duck" ) || Input.Down( "Duck" );

	static bool ReadSprint() => Input.Down( "run" ) || Input.Down( "Run" );

	void HostSimulateLocomotion()
	{
		var isLocalOwner = Connection.Local is not null && GameObject.Network.OwnerId == Connection.Local.Id;

		bool duck;
		bool sprintHeld;
		float moveMag;

		if ( isLocalOwner )
		{
			duck = ReadDuck();
			sprintHeld = ReadSprint();
			moveMag = Input.AnalogMove.Length;
		}
		else
		{
			duck = _remoteDuck;
			sprintHeld = _remoteSprint;
			moveMag = _remoteMoveAnalog;
		}

		ServerCrouching = duck;

		var gs = YaHudMatchSnapshot.TryGameState( GameObject.Scene );
		var inRound = gs is { IsValid: true, CurrentState: YaGameState.InRound };
		var roleCmp = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		var role = roleCmp.IsValid() ? roleCmp.Role : YaPlayerRole.Unassigned;
		var paranoiaRem = gs is { IsValid: true } ? gs.ParanoiaDebuffSecondsRemaining : 0f;

		var dt = Time.Delta;

		if ( !inRound || role != YaPlayerRole.NotAlone )
		{
			StaminaNormalized = 1f;
			ServerSprinting = false;
			return;
		}

		var canSprint = sprintHeld && moveMag > 0.12f && !duck && StaminaNormalized > 0.0005f;
		var drainPerSecond = NotAloneSprintDrainDurationSeconds > 0.01f ? 1f / NotAloneSprintDrainDurationSeconds : 1f;

		if ( canSprint )
		{
			StaminaNormalized = Math.Max( 0f, StaminaNormalized - drainPerSecond * dt );
			ServerSprinting = StaminaNormalized > 0.001f;
		}
		else
		{
			ServerSprinting = false;
			StaminaNormalized = Math.Min( 1f, StaminaNormalized + StaminaRegenPerSecond * dt );
		}

		if ( role == YaPlayerRole.NotAlone && inRound
		     && _prevParanoiaDebuffSeconds > 0.04f && paranoiaRem <= 0.04f )
			StaminaNormalized = Math.Min( 1f, StaminaNormalized + 0.22f );

		_prevParanoiaDebuffSeconds = paranoiaRem;
	}

	[Rpc.Host]
	void HostPushLocomotionIntent( bool duck, bool sprint, float moveAnalog )
	{
		if ( !Networking.IsHost )
			return;

		if ( !YaPawn.ValidateHostRpcCallerOwnsPawnRoot( GameObject ) )
			return;

		_remoteDuck = duck;
		_remoteSprint = sprint;
		_remoteMoveAnalog = moveAnalog;
	}

	public float GetMoveSpeedMultiplier()
	{
		var m = ServerCrouching ? CrouchMoveSpeedScale : 1f;
		if ( ServerSprinting )
			m *= SprintSpeedMultiplier;
		return m;
	}

	public float GetCrouchHeightMultiplier() => ServerCrouching ? CrouchHeightScale : 1f;

	/// <summary>Host: reward surviving paranoia debuff (small stamina refund).</summary>
	public void HostGrantStaminaBurst( float amount01 )
	{
		if ( !Networking.IsHost )
			return;

		StaminaNormalized = Math.Clamp( StaminaNormalized + Math.Max( 0f, amount01 ), 0f, 1f );
	}
}
