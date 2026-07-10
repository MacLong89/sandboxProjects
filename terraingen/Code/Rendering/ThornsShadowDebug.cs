namespace Terraingen.Rendering;

public static class ThornsShadowDebug
{
	[ConCmd( "thorns_shadow_repair" )]
	public static void Repair()
	{
		var stats = ThornsWorldShadowUtil.RepairSceneWorldShadows( Game.ActiveScene );
		Log.Info(
			$"[Thorns Shadows] repair scanned={stats.Scanned} enabled={stats.Enabled} " +
			$"alreadyOn={stats.AlreadyOn} skipped={stats.Skipped}" );
	}

	[ConCmd( "thorns_shadow_status" )]
	public static void Status()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
		{
			Log.Warning( "[Thorns Shadows] No active scene." );
			return;
		}

		var scanned = 0;
		var on = 0;
		var off = 0;
		var skipped = 0;
		var disabled = 0;

		foreach ( var renderer in scene.GetAllComponents<ModelRenderer>() )
		{
			if ( renderer is null || !renderer.IsValid() )
				continue;

			if ( !renderer.Enabled )
			{
				disabled++;
				continue;
			}

			scanned++;
			if ( ThornsWorldShadowUtil.ShouldStayShadowless( renderer.GameObject ) )
			{
				skipped++;
				continue;
			}

			if ( renderer.RenderType == ModelRenderer.ShadowRenderType.On )
				on++;
			else
				off++;
		}

		Log.Info(
			$"[Thorns Shadows] status active={scanned} on={on} off={off} " +
			$"intentionalOff={skipped} disabled={disabled}" );
	}
}
