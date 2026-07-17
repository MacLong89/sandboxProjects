namespace Offshore;

/// <summary>
/// Static PNG sky/water plate — avoids runtime Texture.Create in SkyLayer/OceanLayer
/// (those were a likely native crash vector on boot).
/// </summary>
public sealed class StaticSceneBackdrop : Component
{
	protected override void OnStart()
	{
		var path = OffshoreSprites.Paths.SunriseWaterBg;
		if ( !OffshoreSprites.HasTexture( path ) )
			path = OffshoreSprites.Paths.WaterFill;

		var tex = OffshoreSprites.Load( path );
		var go = new GameObject( true, "StaticBackdropSprite" );
		go.WorldPosition = Vector3.Zero;

		var renderer = go.Components.Create<SpriteRenderer>();
		renderer.Sprite = OffshoreSprites.MakeSprite( tex );
		renderer.StartingAnimationName = "Default";
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Lighting = false;
		renderer.FogStrength = 0f;
		renderer.Opaque = true;
		renderer.AlphaCutoff = 0.04f;
		renderer.IsSorted = true;
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Bilinear;
		renderer.Size = new Vector2( OffshoreConstants.BackdropWidth, OffshoreConstants.BackdropHeight );
		renderer.Color = Color.White;

		Log.Info( $"[Offshore] Static backdrop loaded '{path}'" );
	}
}
