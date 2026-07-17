namespace Offshore;

/// <summary>
/// World-anchored dock + bait shop. Side-scroller prop — can leave the frame
/// when the camera follows the fisherman. Drawn slightly behind the ocean plate
/// so waves sit in front of the pilings.
/// </summary>
public sealed class DockVisuals : Component
{
	private GameObject _dockGo;
	private SpriteRenderer _dock;

	protected override void OnStart()
	{
		WorldPosition = new Vector3( OffshoreConstants.DockHubWorldX, 0f, 0f );

		_dockGo = new GameObject( true, "DockHub" );
		_dock = _dockGo.Components.Create<SpriteRenderer>();
		_dock.Sprite = OffshoreSprites.MakeSprite( OffshoreSprites.Load( OffshoreSprites.Paths.DockHub ) );
		_dock.StartingAnimationName = "Default";
		_dock.Billboard = SpriteRenderer.BillboardMode.Always;
		_dock.Lighting = false;
		_dock.FogStrength = 0f;
		_dock.Opaque = false;
		_dock.AlphaCutoff = 0.04f;
		_dock.IsSorted = true;
		_dock.TextureFilter = Sandbox.Rendering.FilterMode.Bilinear;
		_dock.Color = Color.White;
		_dock.Size = new Vector2( OffshoreConstants.DockHubWidth, OffshoreConstants.DockHubHeight );

		_dockGo.WorldPosition = new Vector3(
			OffshoreConstants.DockHubWorldX,
			OffshoreConstants.DockPlaneY,
			OffshoreConstants.DockHubWorldZ );
	}
}
