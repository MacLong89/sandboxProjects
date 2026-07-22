# Brass Buck Pawn

A stylized first-person pawn shop management game for s&box. Buy low, spot fakes,
issue pawn loans, price your shelves, and keep the lights on.

## How to run

Open `pawn.sbproj` in the s&box editor and play `Assets/scenes/game.scene`.

## How to play

- **WASD / mouse** — move and look
- **E** — interact (open the shop at the front door, serve customers at the counter)
- **TAB** — management screen (inventory, pawns, tools, upgrades, finances, people)
- **ESC** — back out of menus / pause

### The loop

1. **Morning** — price items, place them on displays, buy tools and upgrades, then open the shop at the front door.
2. **Open hours** — customers walk in to sell, pawn, buy, or redeem. Serve whoever is at the counter with **E**.
3. **Inspect** items before you pay: click inspection points with the right tool to find damage, counterfeit signs, and hidden value.
4. **Negotiate** with the offer slider. Cite discovered flaws to drive prices down. You can always walk away.
5. Between customers, keep the shop tidy: **sweep** dirt, **dust** shelves, **restack** box piles, **polish** the counter, **water** the plant, and **take trash** out the back door to the alley dumpster.
6. Walk into the **BACKROOM** behind the counter — stock sits on the racks. Pick items up with **E**, clean them at the workbench, or carry them to a front shelf and press **E** to display. **Q** puts a carried item down. **TAB** opens the full management screen.
7. **Buyers** browse displayed items — some pay sticker, some haggle at the counter.
8. **Closing** — rent and bills are deducted, pawn deadlines advance, the daily summary shows profit and reputation.

Run out of money and an emergency loan kicks in — too much debt ends the run.

## Dev commands

Set `pawn_dev 1` in the console first. Then:

`pawn_cash`, `pawn_rep`, `pawn_customer <archetype> <intent>`, `pawn_fake`, `pawn_rare`,
`pawn_item [id]`, `pawn_truth`, `pawn_skipday`, `pawn_default`, `pawn_event [id]`,
`pawn_unlock`, `pawn_clear_inventory`, `pawn_reset`.

Headless loop drivers (used for smoke tests): `pawn_open`, `pawn_serve`, `pawn_offer <amount>`,
`pawn_accept`, `pawn_walk`, `pawn_price <id> <price>`, `pawn_display <id>`, `pawn_clean <id>`,
`pawn_repair <id>`, `pawn_research <id>`, `pawn_nextday`, `pawn_state`.

`pawn_daylength <seconds>` configures day length (default 600).

## Save data

Stored as JSON at `<s&box data>/pawnshop/save.json`. Delete it (or use New Game) to reset.
