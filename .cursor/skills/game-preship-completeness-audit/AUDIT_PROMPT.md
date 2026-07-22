# PRE-SHIP COMPLETENESS AND FINISHING AUDIT

Perform a comprehensive pre-release audit of this entire game project.

The goal is to identify everything that is:

* Half implemented
* Partially functional
* Technically present but incomplete
* Disconnected from the actual game loop
* Missing supporting features
* Missing assets
* Missing animations
* Missing sounds
* Missing visual feedback
* Missing UI
* Missing player-facing explanations
* Missing purpose
* Using placeholders
* Using temporary logic
* Unable to function in real gameplay
* Not ready to ship

Do not begin implementing fixes yet.

First, inspect the entire project and produce a detailed, evidence-based audit report.

Do not assume that a feature is complete merely because:

* A class exists
* A method exists
* A UI panel exists
* A button exists
* An asset is referenced
* A system compiles
* A TODO comment is absent
* A feature works in one ideal scenario
* A variable or configuration option exists

A feature is only complete if it is fully connected, functional, understandable, purposeful, polished enough for release, and handles expected gameplay conditions.

---

# PRIMARY AUDIT QUESTION

For every system, feature, mechanic, screen, asset, and player interaction, determine:

> Is this fully usable, understandable, connected to the game loop, supported by appropriate feedback, and ready for a real player to encounter?

If not, explain exactly what is missing.

---

# 1. BUILD AND PROJECT HEALTH

Inspect the entire project for:

* Compiler errors
* Compiler warnings
* Runtime errors
* Recurring console errors
* Null-reference risks
* Invalid casts
* Missing components
* Missing resources
* Missing asset references
* Broken scene references
* Broken prefab references
* Deprecated APIs
* Temporary compatibility workarounds
* Hardcoded development paths
* Editor-only behavior required at runtime
* Systems that depend on manually configured scene objects
* Systems that silently fail if configuration is missing
* Code that exists but is never invoked
* Events that are never subscribed to
* Events that are subscribed to but never fired
* Unused components
* Unused data definitions
* Duplicate implementations
* Old implementations still present beside newer versions

Report every issue with:

* File
* Class or component
* Relevant method
* Severity
* Player-facing consequence
* Recommended resolution

---

# 2. FEATURE COMPLETENESS AUDIT

Identify every intended feature in the project.

Build a master feature inventory.

For each feature, assign one status:

* Complete
* Mostly complete
* Partially implemented
* Stubbed
* Placeholder only
* Disconnected
* Broken
* Missing
* Unclear purpose
* Obsolete

For every feature, inspect the complete chain:

1. How the feature begins
2. How the player discovers it
3. How the player interacts with it
4. What feedback is shown
5. What state changes
6. What success looks like
7. What failure looks like
8. How it ends
9. What reward or consequence occurs
10. How it connects to the larger game loop
11. Whether it resets correctly
12. Whether it works in multiplayer
13. Whether it works more than once
14. Whether it works after death, respawn, reconnect, or round reset

A feature is incomplete if any part of this chain is missing.

---

# 3. HALF-IMPLEMENTED SYSTEM DETECTION

Search aggressively for signs of incomplete implementation, including:

* TODO
* FIXME
* HACK
* TEMP
* PLACEHOLDER
* WIP
* NOT IMPLEMENTED
* FUTURE
* DEBUG
* TEST
* Stub methods
* Empty methods
* Methods returning default values
* Methods returning null without explanation
* Methods returning true unconditionally
* Methods returning false unconditionally
* Hardcoded test values
* Hardcoded player IDs
* Hardcoded object references
* Hardcoded scores
* Hardcoded timers
* Hardcoded inventory contents
* Fake randomization
* Fake networking
* Simulated results
* Buttons with no meaningful action
* UI that only prints text
* Features that only log to the console
* Components that expose settings which do nothing
* Serialized fields that are never read
* Public methods that are never called
* Code paths that cannot be reached
* Disabled blocks of code
* Commented-out implementations
* Temporary fallback behavior
* Catch blocks that suppress failures
* Empty error handlers
* Placeholder success states
* Placeholder rewards
* Placeholder NPC behavior
* Placeholder animations
* Placeholder audio
* Placeholder icons
* Placeholder models
* Placeholder materials

Do not only search by keywords.

Inspect behavior and identify code that is functionally incomplete even when it is not labeled as such.

---

# 4. “PRESENT BUT NOT REAL” FEATURES

Find features that appear to exist but are not actually complete.

Examples:

* An inventory that can store items but has no reason to collect them
* A shop with buttons but no economy balance
* An upgrade that changes a number but has no noticeable gameplay effect
* An objective system that displays text but cannot complete
* A quest that completes but gives no reward
* A weapon with no impact feedback
* A tool that animates but does not affect the world
* An NPC that exists but has no meaningful behavior
* A day/night system that changes lighting but affects nothing else
* Weather that is visual only and has no gameplay or audio support
* A progression system with no meaningful unlocks
* A role that has no unique decisions
* A room or area with no gameplay purpose
* A UI tab containing empty or duplicate information
* A stat that is tracked but never displayed or used
* A reward that is granted but cannot be spent
* A collectible that can be picked up but has no value
* A resource that can be earned but has no sink
* A feature that only works through debug controls
* A mechanic that functions but is never introduced to the player

List these under a dedicated section titled:

## Features That Exist but Do Not Yet Matter

For each one, explain:

* What currently exists
* Why it lacks purpose
* What player decision it should create
* What reward, risk, consequence, or progression it needs
* Whether it should be completed, simplified, merged, or removed

---

# 5. CORE GAME LOOP AUDIT

Identify the actual current game loop based on the implementation, not the design intention.

Describe the current loop as:

> Player does X → receives Y → uses Y to do Z → progresses toward A.

Then identify every break in the loop.

Check:

* Is the player’s immediate goal clear?
* Is the long-term goal clear?
* Does the player know what to do after spawning?
* Is there a reason to perform the main mechanic?
* Is there a reward for success?
* Is there a consequence for failure?
* Is there meaningful progression?
* Are rewards usable?
* Are upgrades noticeable?
* Does the loop repeat cleanly?
* Does the difficulty change?
* Does the player make meaningful decisions?
* Is there a reason to keep playing after the first successful loop?
* Is there an ending, round conclusion, milestone, or next objective?
* Does the game become stuck after completing the main objective?
* Are there systems that compete with or bypass the intended loop?

Identify:

* Missing loop steps
* Weak loop steps
* Redundant loop steps
* Unrewarded actions
* Rewards with no use
* Resources with no sinks
* Progression with no purpose
* Activities with no consequence
* Systems that do not feed back into gameplay

---

# 6. ROLE AND GAME MODE AUDIT

For every role, class, team, character type, and game mode, determine:

* What is its goal?
* What does it do each minute?
* What decisions does it make?
* What unique mechanic does it use?
* What information does it have?
* What information does it lack?
* What can it succeed at?
* What can it fail at?
* How does it win?
* How does it lose?
* What keeps it occupied?
* What happens during downtime?
* Does it meaningfully interact with other roles?
* Is it fun when no other player cooperates?
* Does it still function if another required role disconnects?
* Is there a bot or fallback?
* Is its UI complete?
* Are its controls explained?
* Does it have appropriate feedback?
* Does it have a complete round-end result?

Flag roles that are:

* Passive
* Redundant
* Waiting too often
* Missing authority
* Missing objectives
* Missing UI
* Missing win conditions
* Missing tools
* Missing interactions
* Only useful during rare events

For every game mode, verify that it has:

* Start condition
* Player assignment
* Spawn logic
* Objective
* Timer
* Win condition
* Loss condition
* Tie handling
* Disconnect handling
* Reset logic
* Results screen
* Replay flow

---

# 7. PLAYER JOURNEY AUDIT

Test the full experience from the perspective of a completely new player.

Audit:

1. Launching the game
2. Reaching the main menu
3. Understanding available options
4. Joining or starting a game
5. Loading into the map
6. Knowing the objective
7. Learning controls
8. Finding the first interactable
9. Completing the first gameplay action
10. Receiving feedback
11. Understanding success or failure
12. Progressing to the next action
13. Completing a round or session
14. Seeing results
15. Knowing what to do next
16. Starting another round
17. Leaving cleanly

Identify every point where a new player could ask:

* What am I supposed to do?
* Where do I go?
* Why should I do this?
* Did that work?
* Why did I fail?
* What did I earn?
* What changed?
* What happens now?
* Is the game broken?
* Am I waiting for something?
* Can I interact with this?
* Is this placeholder content?

---

# 8. UI AND UX COMPLETENESS

Audit every UI screen, panel, menu, popup, HUD element, tooltip, button, tab, and notification.

For every UI element, verify:

* It opens correctly
* It closes correctly
* It cannot become permanently stuck
* It works at common resolutions
* It handles long text
* It handles missing data
* It handles rapid input
* It handles controller input if supported
* Buttons have hover, pressed, disabled, and selected states
* Buttons clearly communicate their function
* Disabled buttons explain why they are disabled
* All displayed values update
* All labels are accurate
* Placeholder text is removed
* Debug text is removed
* Internal names are not shown
* Empty panels are handled
* Loading states exist where needed
* Errors are shown to the player
* Success is visibly confirmed
* UI focus is released when closing
* Player movement is restored when appropriate
* Cursor state is restored
* UI cannot be opened by unauthorized roles
* UI cannot reveal hidden server information
* UI closes on death, disconnect, round end, and scene reset

Find:

* Dead buttons
* Duplicate controls
* Empty tabs
* Missing icons
* Missing labels
* Temporary layouts
* Misaligned elements
* Inconsistent terminology
* Inconsistent button behavior
* Missing confirmation prompts
* Missing cancellation paths
* Missing back buttons
* Missing pause menu functions
* Settings that do not apply
* UI that exists but is never shown
* UI that displays data with no gameplay meaning

---

# 9. ASSET COMPLETENESS

Create a complete asset inventory.

Audit:

* Player models
* NPC models
* Environment models
* Props
* Tools
* Weapons
* Pickups
* Collectibles
* Items
* Icons
* UI images
* Materials
* Textures
* Particle effects
* Decals
* Animations
* Ragdolls
* Fonts
* Logos
* Thumbnails
* Loading screens
* Map images
* Promotional art
* Tutorial images

Identify:

* Missing assets
* Placeholder cubes
* Placeholder capsules
* Default materials
* White materials
* Checkerboard materials
* Missing textures
* Temporary icons
* Reused icons that are unclear
* Text labels standing in for icons
* Objects with no model
* Models with no collision
* Models with incorrect scale
* Models with incorrect orientation
* Models floating above the ground
* Models clipping into surfaces
* Objects lacking readable silhouettes
* Assets inconsistent with the visual style
* Assets too detailed or too simple relative to the rest
* Assets with missing LODs where needed
* Assets with unnecessary collision complexity
* Duplicate assets
* Unused assets
* Asset references that fail in packaged builds

For each missing asset, state:

* Where it is needed
* What gameplay purpose it serves
* Required visual style
* Approximate priority
* Whether a primitive placeholder is acceptable for launch

---

# 10. ANIMATION AUDIT

Audit every player and NPC action that should have animation.

Check:

* Idle
* Walk
* Run
* Sprint
* Jump
* Fall
* Land
* Crouch
* Use item
* Equip
* Unequip
* Attack
* Reload
* Interact
* Pick up
* Put down
* Carry
* Push
* Pull
* Open
* Close
* Sit
* Get up
* Stun
* Damage reaction
* Death
* Ragdoll
* Revive
* Arrest
* Handcuff
* Inspect
* Work animation
* Celebration
* Failure reaction

Identify:

* Missing animations
* Actions using idle animations
* Sliding characters
* Incorrect animation speed
* Animation/state desynchronization
* Missing transitions
* Popping between states
* Animations that do not match actual timing
* Interactions where hands do not align
* Tools that do not visually connect
* First-person and third-person mismatches
* Networked animations not visible to other players
* Animations that continue after the action stops

---

# 11. AUDIO AUDIT

Create a complete audio inventory.

Audit:

* Menu music
* Gameplay ambience
* Environment ambience
* Player footsteps
* NPC footsteps
* Jump
* Land
* Damage
* Death
* Interaction
* UI hover
* UI click
* UI error
* UI success
* Pickup
* Drop
* Equip
* Unequip
* Tool use
* Weapon fire
* Impact
* Reload
* Objective start
* Objective complete
* Objective failure
* Countdown
* Round start
* Round end
* Victory
* Defeat
* Warning
* Alert
* Door open
* Door close
* Machines
* Vehicles
* Weather
* Water
* Shops
* NPC reactions
* Announcements
* Voice lines if applicable

Identify:

* Missing sounds
* Placeholder sounds
* Default engine sounds
* Repeated sounds that become irritating
* Sounds with no variation
* Sounds that are too loud or quiet
* Missing spatial audio
* Missing attenuation
* Sounds audible through the entire map
* Sounds that do not stop
* Loops that overlap
* Sounds triggered multiple times
* Client/server duplicate playback
* Missing UI feedback sounds
* Important actions with no sound
* Sounds inconsistent with the game’s tone

For every major gameplay action with no sound, flag it.

---

# 12. VISUAL FEEDBACK AND GAME FEEL

Audit whether every important action has appropriate feedback.

Check for:

* Animation
* Sound
* Particle effect
* Screen effect
* World-space indicator
* UI notification
* Number change
* Object state change
* Controller feedback if supported
* Camera response
* Highlighting
* Outline
* Color change
* Progress bar
* Completion indicator

Important actions must clearly communicate:

* Interaction started
* Interaction interrupted
* Interaction completed
* Action succeeded
* Action failed
* Damage dealt
* Damage received
* Item collected
* Resource spent
* Upgrade applied
* Objective changed
* Enemy alerted
* Player detected
* Reward earned
* Round state changed

Flag interactions that technically work but feel unresponsive or invisible.

---

# 13. WORLD AND LEVEL PURPOSE AUDIT

Inspect every room, area, path, building, prop cluster, and landmark.

For each area, determine:

* What is its gameplay purpose?
* What player activity happens here?
* Why would a player visit it?
* Does it support navigation?
* Does it support atmosphere?
* Does it support combat, traversal, interaction, or progression?
* Is it required?
* Is it readable?
* Is it reachable?
* Can the player become stuck?
* Does it contain unfinished geometry?
* Does it have appropriate collision?
* Does it have appropriate lighting?
* Does it contain missing props?
* Does it look intentionally designed?
* Does it match the intended setting?

Identify:

* Empty rooms
* Dead-end spaces with no reward
* Areas that look important but do nothing
* Interactables that cannot be used
* Decorative doors that look usable
* Paths leading nowhere
* Unfinished map boundaries
* Visible voids
* Missing ceilings
* Missing walls
* Inaccessible objectives
* Areas with no audio
* Areas with no lighting purpose
* Areas players can exploit
* Areas NPCs cannot navigate
* Spawn points with poor orientation
* Locations where the player cannot understand the intended route

Create a section titled:

## Areas With No Current Gameplay Purpose

Recommend whether each should be:

* Given a purpose
* Used for an objective
* Used for progression
* Used for environmental storytelling
* Simplified
* Blocked off
* Removed

---

# 14. INTERACTABLE AUDIT

Identify every interactable object.

For each one, verify:

* It visually appears interactable
* It provides a prompt
* Prompt wording is accurate
* Interaction range is reasonable
* Line-of-sight behavior is correct
* It cannot be triggered through walls
* It works repeatedly if intended
* It locks correctly if single-use
* Multiplayer contention is handled
* It gives feedback
* It has a clear result
* It cannot become permanently occupied
* It resets between rounds
* It handles interruption
* It handles player death
* It handles disconnect
* It handles invalid state
* It has sound
* It has animation where appropriate

Identify props that look interactable but are not.

Identify interactables that exist but have no gameplay value.

---

# 15. PROGRESSION AND ECONOMY AUDIT

If the game contains progression, currency, XP, unlocks, upgrades, shops, or rewards, verify the entire economy.

Audit:

* How currency is earned
* How often currency is earned
* What currency can purchase
* Whether purchases matter
* Whether upgrade effects are noticeable
* Whether prices are balanced
* Whether progression is too fast or slow
* Whether players can become stuck
* Whether rewards save
* Whether unlocks save
* Whether duplicate rewards are handled
* Whether items can be purchased without funds
* Whether negative currency is possible
* Whether rewards can be duplicated
* Whether progression can be bypassed
* Whether upgrades apply after respawn
* Whether upgrades apply after reconnect
* Whether UI reflects actual values
* Whether upgrades have descriptions
* Whether the player understands what changed

Find:

* Currency with no use
* Rewards with no use
* Shops with insufficient products
* Upgrades with no effect
* Stats that change but are not noticeable
* Unlocks that are never equipped
* Progression systems disconnected from gameplay
* Infinite currency exploits
* Missing save logic
* Missing reset logic

---

# 16. CONTENT DEPTH AUDIT

Determine whether the game has enough content to support its intended session length and replayability.

Audit counts and variation for:

* Maps
* Levels
* Jobs
* Objectives
* Enemies
* NPCs
* Items
* Tools
* Weapons
* Rewards
* Upgrades
* Events
* Encounters
* Dialogue
* Music
* Ambient sounds
* Props
* Environmental variations
* Tutorials
* Difficulty levels
* Game modes

Identify where players will quickly see all available content.

Distinguish between:

* Must-have content before launch
* Content that can be added after launch
* Content that should be procedural
* Content that can be created through data variation
* Content that requires new models
* Content that requires new code

---

# 17. MULTIPLAYER COMPLETENESS

Audit all systems under real multiplayer conditions.

Check:

* Server authority
* Ownership
* Replication
* Prediction
* Late joining
* Leaving
* Reconnecting
* Host migration if applicable
* Player death
* Respawn
* Round reset
* Simultaneous interaction
* Duplicate rewards
* Duplicate damage
* Duplicate sound
* Duplicate effects
* Hidden information leakage
* Race conditions
* Role permissions
* Team permissions
* Spectators
* Bots replacing players
* Player names
* Voice indicators
* UI visible to wrong players
* Client-only state incorrectly treated as truth
* Actions that work for host but not clients
* Actions that work for one player only
* State that becomes different across clients

Explicitly identify features that have only been tested or designed for single-player.

---

# 18. SAVE AND PERSISTENCE AUDIT

If persistence exists, inspect:

* Settings
* Currency
* XP
* Unlocks
* Cosmetics
* Statistics
* Achievements
* Progress
* Tutorial completion
* Keybinds
* Volume settings

Check:

* New player state
* Existing player state
* Corrupted or missing data
* Version changes
* Default values
* Save timing
* Load timing
* Disconnect during save
* Duplicate rewards
* Data reset
* Unsupported old data
* Client manipulation
* Server validation

Identify any system that appears persistent but is actually session-only.

---

# 19. SETTINGS AND ACCESSIBILITY

Audit whether the game has the minimum expected settings.

Check:

* Master volume
* Music volume
* Sound-effect volume
* Voice volume
* Mouse sensitivity
* Invert look if relevant
* Field of view if appropriate
* Resolution handling
* Fullscreen or windowed behavior if applicable
* Graphics options if needed
* Keybind display
* Controller support if claimed
* Subtitles if dialogue is important
* Color reliance
* Text readability
* UI scale
* Motion-heavy effects
* Flashing effects
* Screen shake options
* Hold versus toggle options

Flag settings that exist but do nothing.

---

# 20. RELEASE PRESENTATION AUDIT

Check whether the project has all player-facing release materials.

Audit:

* Final game name
* Logo
* Main menu presentation
* Loading screen
* Game thumbnail
* Cover image
* Description
* Short description
* Instructions
* Controls
* Credits
* Version number
* Patch notes location
* Privacy or moderation information if needed
* Server browser presentation
* Lobby title
* Error messages
* Disconnect messages
* Empty-server behavior

Identify anything still using:

* Project template names
* Internal code names
* Test names
* Default thumbnails
* Placeholder descriptions
* Debug screenshots
* Developer-only wording

---

# 21. SHIP-BLOCKING BUG AUDIT

Classify every issue by severity.

## P0 — Cannot Ship

Examples:

* Game does not launch
* Core loop cannot complete
* Frequent crash
* Save corruption
* Multiplayer fundamentally broken
* Players cannot join
* Rounds cannot end
* Rounds cannot restart
* Main objective cannot complete
* Severe exploit
* Critical hidden information exposed
* Major content missing

## P1 — Must Fix Before Release

Examples:

* Major role lacks purpose
* Common interaction breaks
* Important UI is missing
* Major assets are placeholders
* Important actions have no feedback
* Players regularly become stuck
* NPCs break the loop
* Audio is absent across major systems
* New players cannot understand the objective

## P2 — Strongly Recommended Before Release

Examples:

* Weak polish
* Minor missing animation
* Inconsistent UI
* Repetitive sounds
* Secondary content unfinished
* Edge-case reset issue
* Noncritical balance problem

## P3 — Post-Launch Improvement

Examples:

* Additional variety
* Optional polish
* Extra cosmetics
* More content
* Minor visual cleanup

Do not place obvious ship blockers into P2 or P3.

---

# 22. REQUIRED FINAL REPORT FORMAT

Produce the report in the following order.

## A. Executive Summary

Include:

* Overall completion percentage
* Whether the game is currently shippable
* Number of P0 issues
* Number of P1 issues
* Number of P2 issues
* Number of P3 issues
* Three largest risks
* Three strongest completed areas
* Estimated status of the actual playable loop

Do not inflate the completion percentage.

## B. Current Real Game Loop

Describe what the player can actually do right now from launch to session end.

Do not describe intended or planned gameplay unless clearly labeled.

## C. Master Feature Inventory

Create a table with:

| Feature | Status | Purpose | Functional? | Connected to loop? | Assets complete? | Audio complete? | UI complete? | Multiplayer complete? | Priority |

## D. Ship Blockers

List every P0 and P1 issue.

For each include:

* Exact problem
* Evidence
* Location
* Player impact
* Required fix
* Dependencies
* Completion criteria

## E. Half-Implemented Features

List every system that is partially present but incomplete.

## F. Features That Exist but Do Not Yet Matter

List purposeless or disconnected systems.

## G. Missing Assets

Group by:

* Models
* Materials
* Textures
* Icons
* Animations
* Effects
* UI art
* Marketing art

## H. Missing Audio

Group by system and action.

## I. Missing UI and Feedback

List dead buttons, absent screens, missing states, unclear interactions, and actions without feedback.

## J. Role and Game Mode Gaps

Explain what each role or mode still needs.

## K. World and Level Gaps

List empty, unfinished, purposeless, or misleading areas.

## L. Multiplayer and Reset Risks

List replication, authority, disconnect, and round-reset issues.

## M. Content Gaps

Identify insufficient variety and what is required for launch.

## N. Remove, Merge, or Defer

Identify features that should not be completed before launch because they:

* Add too much scope
* Duplicate another feature
* Lack purpose
* Are not required for the core loop
* Create maintenance risk

## O. Prioritized Completion Plan

Create an ordered plan divided into:

### Stage 1 — Make the game fully playable

Only blockers that prevent a complete session.

### Stage 2 — Make every feature purposeful

Connect rewards, objectives, roles, and systems.

### Stage 3 — Replace unacceptable placeholders

Assets, animations, sounds, UI, and effects.

### Stage 4 — Make multiplayer and resets reliable

Authority, disconnects, repetition, and cleanup.

### Stage 5 — Make the game understandable

Tutorials, prompts, feedback, and menus.

### Stage 6 — Final release polish

Balance, presentation, optimization, and secondary issues.

For each task include:

* Priority
* Files or systems involved
* Dependencies
* Clear definition of done

## P. Final Pre-Ship Checklist

Create a checkbox checklist that can be rerun after fixes.

---

# 23. AUDIT RULES

Follow these rules strictly:

* Do not fix code during this audit.
* Do not praise systems without verifying them.
* Do not use vague phrases such as “needs polish” without explaining what is missing.
* Do not mark a feature complete solely because code exists.
* Do not ignore missing sounds or assets because placeholders technically work.
* Do not treat debug functionality as real gameplay.
* Do not treat console output as player feedback.
* Do not assume a feature has purpose merely because it is interactable.
* Do not assume multiplayer works because single-player works.
* Do not assume a round resets because the first round works.
* Do not ignore low-frequency bugs that can block a session.
* Do not recommend building optional content before core blockers are solved.
* Reference exact files, classes, scenes, prefabs, resources, and methods wherever possible.
* Clearly distinguish confirmed issues from suspected risks.
* Explain how to reproduce confirmed issues where possible.
* Continue examining connected code instead of stopping at the first visible implementation.

The final report should answer one practical question:

> What exactly must be completed, connected, replaced, clarified, or removed before this game is safe to release to real players?
