# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

RandAwisor ("Randomizer Awisor") is a Terraria mod built on tModLoader (C#, .NET 8). Its main feature is an NPC AI randomizer (see below). It also adds dirt-crafted gear in `Content/Items/`. `RandAwisor.cs` is still the empty `Mod` subclass from the template, because everything is autoloaded.

## Build and run

- **Build:** run `dotnet build` in this directory. There are no tests and no linter.
- **What a build does:** the `.csproj` is nearly empty because the real build setup lives outside the repo. It imports `..\tModLoader.targets`, which imports `C:\Program Files (x86)\Steam\steamapps\common\tModLoader\tMLMod.targets`. That file sets `net8.0`, references the tModLoader/Terraria assemblies, and adds a `BuildMod` step after `Build`. That step runs the tModLoader server with `-build` to package the mod into a `.tmod`. So a build only works if tModLoader is installed through Steam at that path.
- **Run:** `Properties/launchSettings.json` has two profiles, `Terraria` and `TerrariaServer`, which start the tModLoader client and dedicated server through `dotnet`. You can also use the in-game menu: Workshop → Develop Mods → Build + Reload.

## How a tModLoader mod is organized

- **`build.txt`:** the mod's metadata (`displayName`, `author`, `version`). Change `version` here when releasing.
- **Autoloading:** tModLoader finds content by reflection. Any public subclass of a content type (`ModItem`, `ModNPC`, `ModPlayer`, `ModSystem`, etc.) in the assembly is registered automatically, so nothing needs a manual registration list. A content class's texture is the `.png` at the same namespace path as the class (for example, `RandAwisor.Content.Items.Foo` loads `Content/Items/Foo.png`).
- **Localization:** `Localization/en-US_Mods.RandAwisor.hjson` gets entries for new content added to it automatically after a build and reload in game. Edit the values tModLoader generates there instead of hardcoding display names or tooltips in C#.
- **Ignored files:** `bin/` and `obj/` are build output and are listed in `.gitignore`.

## AI randomizer

This is the mod's main feature. Each vanilla NPC runs another behavior instead of its own AI: either another vanilla NPC's AI, or one of the mod's 50 additional AIs. It lives in `Common/AIRandomizer/` and is configured by `Common/Configs/RandomizerConfig.cs`, a server-side `ModConfig` with:
- three modes (Off / ShuffleByType / RandomPerNPC);
- a seed field (text; empty means the world seed; numbers outside the int range wrap into `0..int.MaxValue`; non-numeric text is hashed with FNV-1a because `string.GetHashCode` changes every launch);
- a toggle each for enemies, town NPCs and critters;
- a subpage of per-AI-style toggles (`AIStyleToggles`);
- an "Additional AIs" section: an on/off switch, the percentage of draws that pick an additional AI, and a subpage of per-AI toggles (`AdditionalAIToggles`).

- **Behavior encoding:** a behavior is one int, used everywhere, including sync and seeds. `> 0` borrows that vanilla NPC type's AI, `< 0` runs `CustomAI` number `-1 - behavior`, and `0` is the NPC's own AI.
- **Additional AIs:** these are `CustomAI` subclasses in `Common/AIRandomizer/CustomAIs/`, grouped into ground, flying and shooter files.
  - Each one is a single instance autoloaded through `ILoadable` and shared by every NPC, so per-NPC state must live in `npc.ai` (synced) or `npc.localAI` (not synced).
  - Randomness, teleports and projectiles must run on the server and set `npc.netUpdate`.
  - Helpers in the `CustomAI` base reserve `ai[2]/ai[3]` (`Home`) and `localAI[2]/localAI[3]` (`ShowTeleports`).
  - `Shoot` only fires for hostile NPCs with damage, so town NPCs and critters never attack.
  - Indices come from sorting by class name, so renaming or adding an AI changes what existing seeds produce.
  - Every class needs a same-named bool field in `AdditionalAIToggles` and labels in both `.hjson` files. `BehaviorPool.PostSetupContent` throws if the classes and toggles don't match.
- **How the swap works:** `BehaviorSwapGlobalNPC.PreAI` sets `npc.type` and `npc.aiStyle` to the borrowed NPC's values, calls `npc.VanillaAI()`, restores them, and returns false. This is the same mechanism `ModNPC.AIType` uses. Because of it, the borrowed AI's type-specific code runs too, including its projectiles and attacks, but stats, sprite, loot and chat stay the NPC's own. `noGravity` and `noTileCollide` come from `SetDefaults`, not from the AI, so they are copied from the borrowed type separately.
- **Who picks the behavior:** the server or single player picks it, in `OnSpawn` (and lazily in `PreAI` for town NPCs loaded from the save), and syncs it to clients through `SendExtraAI`/`ReceiveExtraAI`. Clients never pick.
- **Seeding:** every pick is `BehaviorPool.SeededBehavior(type, roll)`, a pure function of the config seed, the toggles, the NPC type and a roll.
  - ShuffleByType uses roll 0, so each type always gets the same behavior.
  - RandomPerNPC gives each NPC its own roll from a per-world counter (`NextRoll`, saved with the world).
  - The seed is scrambled with `Mix` (the MurmurHash3 finalizer) before seeding `UnifiedRandom`. Without it, seeds that differ by 1 give nearly the same first draws.
  - Since picks are pure, `NeedsPick` simply re-picks whenever the mode or `Generation` changes. NPCs whose result didn't change keep their state.
- **Persistence:** NPCs the game saves with the world, mainly town NPCs, save only their roll through `SaveData`/`LoadData`. Their behavior is recomputed from the seed, so it survives reloads and still follows seed changes. Other NPCs aren't saved by the game and get a new roll when they spawn.
- **Which NPCs take part:** `BehaviorPool.IsEligible` decides which NPCs can lend or borrow a behavior. It excludes bosses, Old One's Army NPCs and `ExcludedTypes`.
- **Swappable styles:** the bool fields of `AIStyleToggles` are the one list of swappable AI styles. Each field must be named exactly like its `NPCAIStyleID` constant, because `BehaviorPool` maps fields to style IDs by reflection and throws at load if a name doesn't match. To add a style, add a field and its labels in both `.hjson` files. First make sure the style's AI does not depend on a parent NPC, body segments, a tile anchor or values set by whatever spawned it.
- **How a pick works:** every draw first rolls the additional-AI chance. It then picks either an additional AI, or a vanilla style other than the NPC's own followed by an NPC of that style. A disabled or broken pick moves on to the next draw from the same RNG, so toggling one behavior only changes the NPCs that had drawn it. Config changes bump `BehaviorPool.Generation`, and NPCs re-pick when their generation is stale.
- **Safety nets:**
  - While a borrowed AI runs, a detour on `On_NPC.Transform` blocks transformations, so the Guide can't turn into a Lihzahrd Crawler.
  - An exception inside any behavior is logged, and that behavior is banned for the rest of the session.

## Random drops

This lives in `Common/LootRandomizer/` and is set by `RandomizerConfig.RandomDrops`, a `DropMode`: Off / ShuffleBetweenEnemies / AnyItem / ExtraRandomItem. The default is Off.

- **How the loot tables change:** loot tables are built once at load, so the mode can't be applied there. Instead, `RandomLootGlobalNPC.ModifyNPCLoot` rewires every eligible enemy:
  - its own (non-global) rules move under a `LeadingConditionRule(NormalLootCondition)`;
  - a `RandomLootRule` is added;
  - both check the config when something dies, so switching modes in game needs no reload.
- **Original rules:** they are also kept in `LootPool.originalRules`. ShuffleBetweenEnemies runs the source enemy's original rules through a copy of vanilla's `ItemDropResolver` logic (nested rules and chains). Running the wrapped table instead would hit the condition and recurse into `RandomLootRule`.
- **Seeding:** the shuffle mapping uses `BehaviorPool.SeededRandom(netID, LootRoll)`, so it follows the config seed. It is cached per `BehaviorPool.Generation`. AnyItem and ExtraRandomItem use `info.rng` on every kill.
- **What stays untouched:**
  - bosses, Old One's Army, town NPCs and critters (`LootPool.IsEligible`);
  - global rules like souls and biome keys, plus coins, hearts and banners, which are outside the per-NPC rules;
  - statue-spawned enemies, which never get random drops.
- **Other mods:** rules a mod adds or removes after this `ModifyNPCLoot` runs are not wrapped, and top-level `RemoveWhere` from other mods won't find the moved rules.

## ExampleMod reference

The official tModLoader ExampleMod (a Steam Workshop download) is kept in this folder as reference material. When you need to see how a tModLoader API is used, look it up here before guessing.

- **`ExampleModSource/`:** source extracted from `ExampleMod.tmod`. It has the full C# code (`Content/`, `Common/`), `README.md`, and the localization files. The localization files were renamed `*.hjson.txt` so tModLoader's localization updater never treats them as RandAwisor's own files. Images and audio were not extracted.
- **`ExampleMod.tmod`, `Workshop/`, `enabled.json`, `publish.vdf`:** the raw download (`Workshop/` holds one `.tmod` per tModLoader version).

None of these are part of the mod, and three places keep them out:
- `.gitignore` excludes them from git.
- `<Compile Remove="ExampleModSource\**" />` in the `.csproj` keeps their C# out of RandAwisor's assembly. Without it, the build compiles ExampleMod's code into this mod.
- `buildIgnore` in `build.txt` keeps them out of the packaged `.tmod`.

Keep all three in sync if you add more reference material.
