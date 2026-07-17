using Sandbox;
using System;
using System.Collections.Generic;

namespace Plunge;

/// <summary>
/// Point-authored sprite actor using engine Sprite resources (not the obsolete Texture setter).
/// </summary>
public sealed class PixelActor : Component
{
	[Property] public string Animation { get; set; } = "diver";
	[Property] public Vector2 Size { get; set; } = new( 64, 64 );
	[Property] public float FramesPerSecond { get; set; } = 6;
	[Property] public bool Flip { get; set; }
	[Property] public int FrameCount { get; set; } = 4;
	[Property] public int SortBias { get; set; }

	private SpriteRenderer Renderer;
	private readonly List<Sprite> Frames = new();
	private float Clock;
	private string LoadedAnimation;
	private int LastFrame = -1;

	protected override void OnStart()
	{
		Renderer = Components.GetOrCreate<SpriteRenderer>();
		ConfigureRenderer( Renderer );
		Reload();
	}

	protected override void OnUpdate()
	{
		if ( Renderer is null )
			return;

		if ( LoadedAnimation != Animation )
			Reload();

		ConfigureRenderer( Renderer );
		Renderer.Size = Size;
		Renderer.FlipHorizontal = Flip;

		if ( Frames.Count == 0 )
			return;

		Clock += Time.Delta * FramesPerSecond;
		var frame = ((int)Clock) % Frames.Count;
		if ( frame == LastFrame )
			return;

		LastFrame = frame;
		Renderer.Sprite = Frames[frame];
	}

	public void SetAnimation( string animation, int frameCount = 4 )
	{
		if ( Animation == animation && FrameCount == frameCount )
			return;

		Animation = animation;
		FrameCount = frameCount;
		Reload();
	}

	public static void ConfigureRenderer( SpriteRenderer renderer )
	{
		if ( renderer is null )
			return;

		renderer.IsSorted = true;
		renderer.Shadows = false;
		renderer.Lighting = false;
		renderer.Opaque = false;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
	}

	public static Sprite LoadSprite( string path )
	{
		var texture = Texture.LoadFromFileSystem( path, FileSystem.Mounted, false )
			?? Texture.LoadFromFileSystem( "/" + path.TrimStart( '/' ), FileSystem.Mounted, false );

		if ( texture is null || !texture.IsValid )
		{
			Log.Warning( $"PLUNGE could not load texture '{path}'" );
			return null;
		}

		return Sprite.FromTexture( texture );
	}

	private void Reload()
	{
		Frames.Clear();
		LoadedAnimation = Animation;
		Clock = 0;
		LastFrame = -1;

		for ( var i = 0; i < Math.Max( 1, FrameCount ); i++ )
		{
			var suffix = FrameCount > 1 ? $"_{i}" : "";
			var path = $"sprites/{Animation}{suffix}.png";
			var sprite = LoadSprite( path );
			if ( sprite is not null )
				Frames.Add( sprite );
		}

		if ( Frames.Count == 0 )
		{
			Log.Warning( $"PLUNGE missing sprite animation '{Animation}'" );
			return;
		}

		if ( Renderer is not null )
			Renderer.Sprite = Frames[0];
	}
}

public sealed class DiveEntity
{
	public GameObject Object { get; set; }
	public PixelActor Sprite { get; set; }
	public EntityKind Kind { get; set; }
	public LootDef Loot { get; set; }
	public float Health { get; set; } = 1;
	public float MaxHealth { get; set; } = 1;
	public float Damage { get; set; }
	public float Speed { get; set; }
	public Vector3 Velocity { get; set; }
	public float Radius { get; set; } = 24;
	public float AttackCooldown { get; set; }
	public bool Opened { get; set; }
}
