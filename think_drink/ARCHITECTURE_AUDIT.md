# Think&Drink Game Night Architecture Audit

## Completed Platform Split

- Added a networked `GameNightManager` for shared lobby settings and active game-mode selection.
- Added `GameModeId`, `LobbyType`, and `TeamMode` domain enums.
- Added `IGameMode`, `GameModeBase`, game-mode definitions, and a central `GameModeRegistry`.
- Routed match start, timing, screen labels, point awards, and player submissions through the active game mode.
- Registered current and future game-night modes without changing the studio geometry.
- Added lobby UI mode selection and large-board mode labels.

## Implemented Modes

- `TriviaShowdownMode`: current classic trivia behavior.
- `SpeedTriviaMode`: shorter reveal/buzz/answer windows and a fast-answer point bonus.
- `MajorityRules`: open-answer voting mode. Everyone submits once during the answer window and all correct voters can score.
- `GuessTheImage`: staged image-clue mode using masked answer silhouettes on the shared billboard.
- `FibbageStyle` and `LieLineup`: multiple-choice truth/lie modes with shuffled decoys and exact option validation.
- `MemoryGrid`: recall mode that generates a symbol sequence and requires exact typed recall.
- `OddOneOut`: option-comparison mode that asks players to identify the outlier.
- `EstimateBattle`: numeric estimation mode derived from the hidden answer.
- `SpotTheDifference`: text-comparison mode that asks for the changed word.

## Remaining Coupling To Remove

- `MatchManager` still owns `TriviaQuestion` and question selection directly. Prompt transformation now runs through each `IGameMode`, but future pass should move source selection into the mode layer too.
- `GameEvents.QuestionShown` is trivia-specific. Future pass should add a generic `GamePromptShown` event while keeping trivia as one prompt type.
- `BotManager` still uses `TriviaBotService` for difficulty and timing. It now routes answers through mode prompts, but a future pass should add an `IBotGameModeDriver` or per-mode bot strategy.
- `GameHud` now supports buzz-in and everyone-answer flows, but future pass should replace the text box with richer mode input panels selected by active `GameModeId`.
- `StatsManager`, challenges, and achievements mostly track trivia metrics. Future pass should add game-mode scoped stat events such as `ModePlayed`, `VoteCast`, `BluffWon`, and `ImageGuessed`.

## Safe Next Refactor

Move source-question selection out of `MatchManager` and let each mode return a richer prompt model:

- `Title`
- `Category`
- `Body`
- `AcceptedAnswers`
- `RevealText`
- `Explanation`
- `InputKind`

That will let Majority Rules, Guess The Image, and Fibbage stop pretending to be trivia rounds while preserving the shared studio, persistence, leaderboard, and host-authoritative match shell.
