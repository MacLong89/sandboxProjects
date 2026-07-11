namespace FinalOutpost;

/// <summary>Runtime zombie instance — logic lives in <see cref="CombatSystem"/>.</summary>
public sealed class ZombieInstance
{
	public GameObject Go;
	public CharacterModel Character;
	public UI.WorldLabel Label;
	public ZombieTypeDef TypeDef;
	public float Health;
	public float MaxHealth;
	public float Damage;
	public float Speed;
	public float AttackTimer;
	public float MoveStuckTimer;
	public int StrafeSign;
	public bool IsEngaged;
	public bool Dead;
	/// <summary>Which perimeter edge this zombie approached from — locks breach behavior to one side.</summary>
	public WallApproachSide ApproachSide;

	public Vector3 Position => Go.IsValid() ? Go.WorldPosition : Vector3.Zero;

	public bool Hit( float dmg )
	{
		if ( Dead ) return false;
		var taken = dmg * (TypeDef?.DamageTakenMult ?? 1f);
		Health = MathF.Max( 0f, Health - taken );
		RefreshLabel();
		if ( Health <= 0f )
		{
			Dead = true;
			return true;
		}

		return false;
	}

	public void RefreshLabel()
	{
		if ( Label is null ) return;
		Label.Text = $"{(int)MathF.Ceiling( Health )}";
	}
}
