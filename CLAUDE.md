# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

RandAwisor ("Randomizer Awisor") is a Terraria mod built on tModLoader (C#, .NET 8). It is still the bare template: `RandAwisor.cs` only has an empty `Mod` subclass, and `description.txt` / `description_workshop.txt` still hold the template placeholder text.

## Build and run

- **Build:** run `dotnet build` in this directory. There are no tests and no linter.
- **What a build does:** the `.csproj` is nearly empty because the real build setup lives outside the repo. It imports `..\tModLoader.targets`, which imports `C:\Program Files (x86)\Steam\steamapps\common\tModLoader\tMLMod.targets`. That file sets `net8.0`, references the tModLoader/Terraria assemblies, and adds a `BuildMod` step after `Build`. That step runs the tModLoader server with `-build` to package the mod into a `.tmod`. So a build only works if tModLoader is installed through Steam at that path.
- **Run:** `Properties/launchSettings.json` has two profiles, `Terraria` and `TerrariaServer`, which start the tModLoader client and dedicated server through `dotnet`. You can also use the in-game menu: Workshop → Develop Mods → Build + Reload.

## How a tModLoader mod is organized

- **`build.txt`:** the mod's metadata (`displayName`, `author`, `version`). Change `version` here when releasing.
- **Autoloading:** tModLoader finds content by reflection. Any public subclass of a content type (`ModItem`, `ModNPC`, `ModPlayer`, `ModSystem`, etc.) in the assembly is registered automatically, so nothing needs a manual registration list. A content class's texture is the `.png` at the same namespace path as the class (for example, `RandAwisor.Content.Items.Foo` loads `Content/Items/Foo.png`).
- **Localization:** `Localization/en-US_Mods.RandAwisor.hjson` gets entries for new content added to it automatically after a build and reload in game. Edit the values tModLoader generates there instead of hardcoding display names or tooltips in C#.
- **Ignored files:** `bin/` and `obj/` are build output and are listed in `.gitignore`.

## ExampleMod reference

The official tModLoader ExampleMod (a Steam Workshop download) is kept in this folder as reference material. When you need to see how a tModLoader API is used, look it up here before guessing.

- **`ExampleModSource/`:** source extracted from `ExampleMod.tmod`. It has the full C# code (`Content/`, `Common/`), `README.md`, and the localization files. The localization files were renamed `*.hjson.txt` so tModLoader's localization updater never treats them as RandAwisor's own files. Images and audio were not extracted.
- **`ExampleMod.tmod`, `Workshop/`, `enabled.json`, `publish.vdf`:** the raw download (`Workshop/` holds one `.tmod` per tModLoader version).

None of these are part of the mod, and three places keep them out:
- `.gitignore` excludes them from git.
- `<Compile Remove="ExampleModSource\**" />` in the `.csproj` keeps their C# out of RandAwisor's assembly. Without it, the build compiles ExampleMod's code into this mod.
- `buildIgnore` in `build.txt` keeps them out of the packaged `.tmod`.

Keep all three in sync if you add more reference material.
