using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using RandAwisor.Common.AIRandomizer.CustomAIs;
using RandAwisor.Common.Configs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;

namespace RandAwisor.Common.AIRandomizer
{
	// Knows which behaviors exist, which NPCs can take part, and picks the behavior each NPC gets.
	// A behavior is an int: > 0 borrows that vanilla NPC type's AI, < 0 runs CustomAI number (-1 - behavior),
	// and 0 means the NPC's own AI.
	public class BehaviorPool : ModSystem
	{
		// Swappable AI style -> its toggle in the config. AIStyleToggles is the single list of swappable styles.
		private static readonly Dictionary<int, FieldInfo> StyleToggles = typeof(AIStyleToggles).GetFields()
			.Where(f => f.FieldType == typeof(bool))
			.ToDictionary(StyleIdOf, f => f);

		private static readonly FieldInfo[] CustomAIToggles = typeof(AdditionalAIToggles).GetFields()
			.Where(f => f.FieldType == typeof(bool))
			.ToArray();

		// NPCs whose type-specific AI code is destructive or removes the NPC from the world.
		private static readonly HashSet<int> ExcludedTypes = new() {
			NPCID.Clown, // its bombs destroy tiles
			NPCID.ChatteringTeethBomb,
			NPCID.Gnome,
			NPCID.TravellingMerchant,
			NPCID.SkeletonMerchant,
			NPCID.OldMan,
		};

		// Draws per NPC before giving up on finding an enabled behavior.
		private const int MaxDraws = 100;

		private static readonly SortedDictionary<int, List<int>> typesByStyle = new();
		// Sorted by name once content is loaded, so indices (synced to clients and used by seeds) are stable.
		private static readonly List<CustomAI> customAIs = new();
		// Per-type picks for roll 0 (ShuffleByType), cached because every NPC of a type asks for the same one.
		private static readonly Dictionary<int, int> shuffledBehaviors = new();
		// Behaviors that threw; never handed out again this session.
		private static readonly HashSet<int> brokenBehaviors = new();
		private static HashSet<int> disabledStyles = new();
		private static HashSet<string> disabledCustomAIs = new();
		private static int customAIChance;
		private static string seedText = "";
		private static int worldSeed;
		// Last roll handed out in this world (see SeededBehavior); saved with the world.
		private static int lastRoll;

		// Bumped whenever the shuffle mapping may have changed, so NPCs know to pick again.
		internal static int Generation;
		// whoAmI of the NPC currently running a borrowed vanilla AI, or -1.
		internal static int MimickingNPC = -1;

		private static int StyleIdOf(FieldInfo toggle) {
			FieldInfo id = typeof(NPCAIStyleID).GetField(toggle.Name)
				?? throw new InvalidOperationException($"{nameof(AIStyleToggles)}.{toggle.Name} does not match any {nameof(NPCAIStyleID)} constant.");
			return Convert.ToInt32(id.GetValue(null));
		}

		public static bool IsCustom(int behavior) => behavior < 0;

		public static int CustomBehavior(int index) => -1 - index;

		public static int CustomIndexOf(int behavior) => -1 - behavior;

		public static CustomAI CustomAIOf(int behavior) => customAIs[CustomIndexOf(behavior)];

		public static bool IsValidCustomIndex(int index) => index >= 0 && index < customAIs.Count;

		public static string Describe(int behavior) => IsCustom(behavior) ? $"custom AI {CustomAIOf(behavior).Name}" : $"the AI of NPC {behavior}";

		internal static void RegisterCustomAI(CustomAI ai) => customAIs.Add(ai);

		public static bool IsEligible(NPC npc) {
			return npc.type > NPCID.None && npc.type < NPCID.Count
				&& StyleToggles.ContainsKey(npc.aiStyle)
				&& !npc.boss
				&& !NPCID.Sets.ShouldBeCountedAsBoss[npc.type]
				&& !NPCID.Sets.BelongsToInvasionOldOnesArmy[npc.type]
				&& !ExcludedTypes.Contains(npc.type)
				&& !(npc.friendly && !npc.townNPC && !NPCID.Sets.CountsAsCritter[npc.type]);
		}

		public static bool IsAffected(NPC npc, RandomizerConfig config) {
			if (npc.townNPC) {
				return config.AffectTownNPCs;
			}
			if (NPCID.Sets.CountsAsCritter[npc.type]) {
				return config.AffectCritters;
			}
			return config.AffectHostiles;
		}

		public static bool IsEnabled(int behavior) {
			if (IsCustom(behavior)) {
				return customAIChance > 0 && !disabledCustomAIs.Contains(CustomAIOf(behavior).Name);
			}
			return !disabledStyles.Contains(ContentSamples.NpcsByNetId[behavior].aiStyle);
		}

		public static void ApplyConfig(RandomizerConfig config) {
			var styleToggles = config.AIStyles ?? new AIStyleToggles();
			disabledStyles = StyleToggles.Where(kv => !(bool)kv.Value.GetValue(styleToggles)).Select(kv => kv.Key).ToHashSet();
			var customToggles = config.AdditionalAIs ?? new AdditionalAIToggles();
			disabledCustomAIs = CustomAIToggles.Where(f => !(bool)f.GetValue(customToggles)).Select(f => f.Name).ToHashSet();
			customAIChance = config.EnableAdditionalAIs ? Math.Clamp(config.AdditionalAIChance, 0, 100) : 0;
			seedText = config.Seed?.Trim() ?? "";
			InvalidateShuffle();
		}

		// Every behavior comes from the config seed, the NPC type and a roll. Roll 0 is ShuffleByType: one pick per
		// type. RandomPerNPC gives each NPC its own roll from NextRoll, so each NPC gets its own draw from the same
		// seed. NPCs only need to save their roll: the behavior is recomputed from it, and follows seed changes.
		public static int SeededBehavior(int type, int roll) {
			if (roll == 0 && shuffledBehaviors.TryGetValue(type, out int cached)) {
				return cached;
			}
			int behavior = Pick(type, new UnifiedRandom(Mix(Mix(Mix(ResolveSeed()) ^ type) ^ roll)));
			if (roll == 0) {
				shuffledBehaviors[type] = behavior;
			}
			return behavior;
		}

		// Server only.
		public static int NextRoll() => ++lastRoll;

		// MurmurHash3's finalizer. UnifiedRandom gives nearly the same first draws for seeds that differ by 1
		// (neighboring NPC types used to alternate between vanilla and additional AIs), so seeds are scrambled first.
		private static int Mix(int value) {
			uint h = unchecked((uint)value);
			h ^= h >> 16;
			h = unchecked(h * 0x85ebca6b);
			h ^= h >> 13;
			h = unchecked(h * 0xc2b2ae35);
			h ^= h >> 16;
			return unchecked((int)h);
		}

		public static void MarkBroken(int behavior) {
			brokenBehaviors.Add(behavior);
			InvalidateShuffle();
		}

		private static void InvalidateShuffle() {
			shuffledBehaviors.Clear();
			Generation++;
		}

		// Empty falls back to the world's seed. A number that fits in an int is used as is; a bigger one, of any
		// length, wraps into 0..int.MaxValue. Any other text is hashed.
		private static int ResolveSeed() {
			if (seedText.Length == 0) {
				return worldSeed;
			}
			if (BigInteger.TryParse(seedText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out BigInteger number)) {
				if (number >= int.MinValue && number <= int.MaxValue) {
					return (int)number;
				}
				var range = new BigInteger(int.MaxValue) + 1;
				return (int)(((number % range) + range) % range);
			}
			// FNV-1a, because string.GetHashCode changes every time the game starts.
			uint hash = 2166136261;
			foreach (char c in seedText) {
				hash = unchecked((hash ^ c) * 16777619);
			}
			return unchecked((int)hash);
		}

		// Each draw first rolls whether to use one of the mod's own AIs (AdditionalAIChance), then picks one of them,
		// or a vanilla style (never the NPC's own, so the change is visible) and an NPC of that style.
		// Picking the style first keeps the 200+ Fighter NPCs from drowning out every other style.
		// A disabled or broken pick just moves on to the next draw, so toggling one behavior only changes the NPCs
		// that had drawn it; everyone else keeps the same behavior for the same seed.
		private static int Pick(int ownType, UnifiedRandom rand) {
			int ownStyle = ContentSamples.NpcsByNetId[ownType].aiStyle;
			var styles = typesByStyle.Keys.Where(s => s != ownStyle).ToList();
			for (int draw = 0; draw < MaxDraws; draw++) {
				int behavior;
				if (rand.Next(100) < customAIChance) {
					if (customAIs.Count == 0) {
						continue;
					}
					behavior = CustomBehavior(rand.Next(customAIs.Count));
				}
				else {
					if (styles.Count == 0) {
						continue;
					}
					var types = typesByStyle[styles[rand.Next(styles.Count)]];
					behavior = types[rand.Next(types.Count)];
				}
				if (IsEnabled(behavior) && !brokenBehaviors.Contains(behavior)) {
					return behavior;
				}
			}
			return 0;
		}

		public override void Load() {
			On_NPC.Transform += BlockTransformWhileMimicking;
		}

		// Some borrowed AIs turn their NPC into another type (Lihzahrds, wall spiders...).
		// That must not happen to an NPC that is only borrowing the behavior, e.g. the Guide.
		private static void BlockTransformWhileMimicking(On_NPC.orig_Transform orig, NPC self, int newType) {
			if (MimickingNPC == self.whoAmI) {
				return;
			}
			orig(self, newType);
		}

		public override void PostSetupContent() {
			typesByStyle.Clear();
			for (int type = 1; type < NPCID.Count; type++) {
				if (!ContentSamples.NpcsByNetId.TryGetValue(type, out NPC sample) || !IsEligible(sample)) {
					continue;
				}
				if (!typesByStyle.TryGetValue(sample.aiStyle, out var types)) {
					typesByStyle[sample.aiStyle] = types = new List<int>();
				}
				types.Add(type);
			}

			customAIs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
			var withoutToggle = customAIs.Select(ai => ai.Name).Except(CustomAIToggles.Select(f => f.Name)).ToList();
			var withoutAI = CustomAIToggles.Select(f => f.Name).Except(customAIs.Select(ai => ai.Name)).ToList();
			if (withoutToggle.Count > 0 || withoutAI.Count > 0) {
				throw new InvalidOperationException(
					$"{nameof(AdditionalAIToggles)} must have exactly one field per {nameof(CustomAI)}. " +
					$"Missing toggles: [{string.Join(", ", withoutToggle)}]. Toggles without an AI: [{string.Join(", ", withoutAI)}].");
			}
		}

		public override void OnWorldLoad() {
			worldSeed = Main.ActiveWorldFileData.Seed;
			InvalidateShuffle();
			Mod.Logger.Info($"AI randomizer seed: {ResolveSeed()} (from \"{(seedText.Length > 0 ? seedText : "world seed")}\")");
		}

		public override void ClearWorld() {
			lastRoll = 0;
		}

		public override void SaveWorldData(TagCompound tag) {
			if (lastRoll > 0) {
				tag["LastRoll"] = lastRoll;
			}
		}

		public override void LoadWorldData(TagCompound tag) {
			lastRoll = tag.GetInt("LastRoll");
		}

		public override void OnWorldUnload() {
			shuffledBehaviors.Clear();
		}

		public override void Unload() {
			typesByStyle.Clear();
			customAIs.Clear();
			shuffledBehaviors.Clear();
			brokenBehaviors.Clear();
		}
	}
}
