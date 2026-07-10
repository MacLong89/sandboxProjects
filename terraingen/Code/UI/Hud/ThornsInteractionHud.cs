namespace Terraingen.UI.Hud;

using Sandbox.UI;

public sealed class ThornsInteractionHud
{
	readonly ThornsHudPromptCard _card;

	public ThornsInteractionHud( Panel parent ) => _card = new ThornsHudPromptCard( parent );

	public void Refresh() => _card.Apply( ThornsInteractionPrompt.Resolve() );
}
