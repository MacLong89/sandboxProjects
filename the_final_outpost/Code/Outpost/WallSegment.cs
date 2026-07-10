namespace FinalOutpost;

/// <summary>One perimeter wall chunk with its own HP pool.</summary>
public sealed class WallSegment
{
	public GameObject Go;
	public ModelRenderer Renderer;
	public Vector3 Center;
	public float Health;
	public float MaxHealth;
	public Color IntactColor = new( 0.55f, 0.58f, 0.62f );
	/// <summary>Stable id ("x,y" of the segment centre) for persisting player-removed walls.</summary>
	public string Key;

	public bool IsBroken => Health <= 0f;
	public float HealthFraction => MaxHealth <= 0f ? 0f : Health / MaxHealth;

	public void SetMaxHealth( float max, bool healToFull )
	{
		if ( healToFull )
		{
			MaxHealth = max;
			Health = max;
		}
		else
		{
			var frac = HealthFraction;
			MaxHealth = max;
			Health = max * frac;
		}

		RefreshVisual();
	}

	public void Damage( float amount )
	{
		Health = MathF.Max( 0f, Health - amount );
		RefreshVisual();
	}

	public void Repair( float amount )
	{
		Health = MathF.Min( MaxHealth, Health + amount );
		RefreshVisual();
	}

	public void SetHealth( float hp )
	{
		Health = MathF.Min( MaxHealth, MathF.Max( 0f, hp ) );
		RefreshVisual();
	}

	public void RepairToFull()
	{
		Health = MaxHealth;
		RefreshVisual();
	}

	public void RefreshVisual()
	{
		if ( Renderer is null ) return;

		if ( IsBroken )
		{
			Go.Enabled = false;
			return;
		}

		Go.Enabled = true;
		var t = HealthFraction;
		Renderer.Tint = Color.Lerp( new Color( 0.5f, 0.15f, 0.12f ), IntactColor, t );
	}
}
