namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Player;

/// <summary>Registers citizen model skeleton hitboxes for <see cref="SceneTrace.UseHitboxes"/> combat traces.</summary>
public static class ThornsCitizenCombatHitboxes
{
	public static void EnsureOnCitizenPawn( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() )
			return;

		var body = ThornsBanditUtil.FindDescendantNamed( pawnRoot, ThornsCitizenRig.BodyChildName );
		if ( !body.IsValid() )
			return;

		var skin = body.Components.Get<SkinnedModelRenderer>();
		if ( !skin.IsValid() || skin.Model is null || !skin.Model.IsValid() )
			return;

		var hitboxes = body.Components.Get<ModelHitboxes>();
		if ( !hitboxes.IsValid() )
			hitboxes = body.Components.Create<ModelHitboxes>();

		hitboxes.Renderer = skin;
		hitboxes.Target = pawnRoot;
		hitboxes.Rebuild();
	}
}
