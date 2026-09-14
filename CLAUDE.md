## Project Context

stonewards-mod-settings is one shared settings menu inside Stonewards for all mods. Any mod can register its options, such as toggles, sliders, dropdowns and text fields, with default values. Players change them in the game without editing config files, and the changes are saved and passed back to the mod they belong to. It follows the conventions of the sibling stonewards-upgrade-queue mod, which will be the first mod to use it.

## Goals

- Give all Stonewards mods one shared settings menu inside the game
- Let any mod register its options (toggles, sliders, dropdowns, text fields) with default values
- Save players' changes and pass them back to the mod they belong to
- Match the language, mod loader, build and packaging setup of stonewards-upgrade-queue
- Make stonewards-upgrade-queue the first mod to use it
- Look at the similar settings mod already on Thunderstore and use it as a reference for features and design

## Out of scope

- Settings for a single mod only; the manager must stay generic
- Changing gameplay itself

## Suggested stack

- **Same language, mod loader and build tooling as stonewards-upgrade-queue** — The user wants this mod to follow the conventions of its sibling mod. Confirm the details by reading that project first.
