using Sandbox.Citizen;

namespace Sandbox;

/// <summary>
/// Spawns the replicated third-person Citizen mesh + <see cref="CitizenAnimationHelper"/> under <c>Body</c>,
/// per Facepunch's ready-to-use Citizen pipeline (source lives under <c>addons/citizen</c>).
/// </summary>
public static class YaCitizenRig
{
	/// <summary>Creates or replaces <c>Body</c> with a skinned Citizen and wires eyes to <c>View</c> if present.</summary>
	public static void SetupCitizenBody( GameObject pawnRoot )
	{
		if ( !pawnRoot.IsValid() )
			return;

		GameObject bodyGo = default;
		foreach ( var ch in pawnRoot.Children )
		{
			if ( ch.Name == "Body" )
			{
				bodyGo = ch;
				break;
			}
		}

		if ( !bodyGo.IsValid() )
		{
			bodyGo = new GameObject( true, "Body" );
			bodyGo.SetParent( pawnRoot );
		}

		bodyGo.LocalPosition = Vector3.Zero;
		bodyGo.LocalRotation = Rotation.Identity;
		bodyGo.LocalScale = Vector3.One;

		foreach ( var c in bodyGo.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( c.IsValid() )
				c.Destroy();
		}

		foreach ( var c in bodyGo.Components.GetAll<CitizenAnimationHelper>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( c.IsValid() )
				c.Destroy();
		}

		var skin = bodyGo.Components.Create<SkinnedModelRenderer>();
		skin.Model = Model.Load( YaCitizenPaths.CitizenVmdl );
		skin.Tint = new Color( 0.94f, 0.94f, 0.94f, 1f );
		skin.UseAnimGraph = true;

		var helper = bodyGo.Components.Create<CitizenAnimationHelper>();
		helper.Target = skin;

		GameObject viewGo = default;
		foreach ( var ch in pawnRoot.Children )
		{
			if ( ch.Name == "View" )
			{
				viewGo = ch;
				break;
			}
		}

		if ( viewGo.IsValid() )
			helper.EyeSource = viewGo;

		if ( !pawnRoot.Components.Get<YaCitizenBodyDriver>( FindMode.EnabledInSelf ).IsValid() )
			pawnRoot.Components.Create<YaCitizenBodyDriver>();
	}
}
