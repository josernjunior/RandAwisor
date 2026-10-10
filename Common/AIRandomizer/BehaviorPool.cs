using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RandAwisor.Common.Configs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace RandAwisor.Common.AIRandomizer
{
	// Knows which vanilla NPCs can lend or borrow a behavior, and picks the behavior each NPC gets.
	public class BehaviorPool : ModSystem
	{
		// Swappable AI style -> its toggle in the config. AIStyleToggles is the single list of swappable styles.
		private static readonly Dictionary<int, FieldInfo> StyleToggles = typeof(AIStyleToggles).GetFields()
			.Where(f => f.FieldType == typeof(bool))
			.ToDictionary(StyleIdOf, f => f);

		// NPCs whose type-specific AI code is destructive or removes the NPC from the world.
		private static readonly HashSet<int> ExcludedTypes = new() {
			NPCID.Clown, // its bombs destroy tiles
			NPCID.ChatteringTeethBomb,
			NPCID.Gnome,
			NPCID.TravellingMerchant,
			NPCID.SkeletonMerchant,
			NPCID.OldMan,
		};

		// Draws per NPC before giving up on finding an enabled style.
		private const int MaxDraws = 100;

		private static readonly SortedDictionary<int, List<int>> typesByStyle = new();
		private static readonly Dictionary<int, int> shuffledSources = new();
		// Behaviors that threw while borrowed; never handed out again this session.
		private static readonly HashSet<int> brokenSources = new();
		private static HashSet<int> disabledStyles = new();
		private static string seedText = "";
		private static int worldSeed;

		// Bumped whenever the shuffle mapping may have changed, so NPCs know to pick again.
		internal static int Generation;
		// whoAmI of the NPC currently running a borrowed AI, or -1.
		internal static int MimickingNPC = -1;

		private static int StyleIdOf(FieldInfo toggle) {
			FieldInfo id = typeof(NPCAIStyleID).GetField(toggle.Name)
				?? throw new InvalidOperationException($"{nameof(AIStyleToggles)}.{toggle.Name} does not match any {nameof(NPCAIStyleID)} constant.");
			return Convert.ToInt32(id.GetValue(null));
		}

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

		public static bool IsStyleEnabled(int style) => !disabledStyles.Contains(style);

		public static void ApplyConfig(RandomizerConfig config) {
			var toggles = config.AIStyles ?? new AIStyleToggles();
			disabledStyles = StyleToggles.Where(kv => !(bool)kv.Value.GetValue(toggles)).Select(kv => kv.Key).ToHashSet();
			seedText = config.Seed?.Trim() ?? "";
			InvalidateShuffle();
		}

		public static int ShuffledSource(int type) {
			if (!shuffledSources.TryGetValue(type, out int source)) {
				source = Pick(type, new UnifiedRandom(unchecked(ResolveSeed() * 486187739 + type)));
				shuffledSources[type] = source;
			}
			return source;
		}

		public static int RandomSource(int type) => Pick(type, Main.rand);

		public static void MarkBroken(int type) {
			brokenSources.Add(type);
			InvalidateShuffle();
		}

		private static void InvalidateShuffle() {
			shuffledSources.Clear();
			Generation++;
		}

		// A number is used as is; any other text is hashed. Empty falls back to the world's seed.
		private static int ResolveSeed() {
			if (seedText.Length == 0) {
				return worldSeed;
			}
			if (int.TryParse(seedText, out int number)) {
				return number;
			}
			// FNV-1a, because string.GetHashCode changes every time the game starts.
			uint hash = 2166136261;
			foreach (char c in seedText) {
				hash = unchecked((hash ^ c) * 16777619);
			}
			return unchecked((int)hash);
		}

		// Every draw takes a style (never the NPC's own, so the change is visible) and then an NPC of that style.
		// A disabled style or broken behavior just moves on to the next draw, so toggling a style only changes
		// the NPCs that had drawn it; everyone else keeps the same behavior for the same seed.
		// Drawing the style first keeps the 200+ Fighter NPCs from drowning out every other style.
		private static int Pick(int ownType, UnifiedRandom rand) {
			int ownStyle = ContentSamples.NpcsByNetId[ownType].aiStyle;
			var styles = typesByStyle.Keys.Where(s => s != ownStyle).ToList();
			if (styles.Count == 0) {
				return 0;
			}
			for (int draw = 0; draw < MaxDraws; draw++) {
				int style = styles[rand.Next(styles.Count)];
				var types = typesByStyle[style];
				int type = types[rand.Next(types.Count)];
				if (IsStyleEnabled(style) && !brokenSources.Contains(type)) {
					return type;
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
		}

		public override void OnWorldLoad() {
			worldSeed = Main.ActiveWorldFileData.Seed;
			InvalidateShuffle();
		}

		public override void OnWorldUnload() {
			shuffledSources.Clear();
		}

		public override void Unload() {
			typesByStyle.Clear();
			shuffledSources.Clear();
			brokenSources.Clear();
		}
	}
}
