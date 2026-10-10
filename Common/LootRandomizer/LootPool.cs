using System.Collections.Generic;
using System.Linq;
using RandAwisor.Common.AIRandomizer;
using RandAwisor.Common.Configs;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace RandAwisor.Common.LootRandomizer
{
	// Knows each enemy's original loot and picks what a randomized drop becomes.
	// Loot tables are built once at load, so RandomLootGlobalNPC wraps them in rules that check the config
	// every time something dies; switching DropMode in game needs no reload.
	public class LootPool : ModSystem
	{
		// Seeded picks use BehaviorPool.SeededRandom with this roll, so they don't repeat the AI picks.
		private const int LootRoll = -1;

		// Enemy netID -> its own loot rules as they were before RandomLootGlobalNPC wrapped them.
		private static readonly Dictionary<int, List<IItemDropRule>> originalRules = new();
		private static readonly Dictionary<int, int> shuffledSources = new();
		private static int shuffledGeneration = -1;
		private static int[] lootSources;
		private static int[] droppableItems;

		public static DropMode Mode => ModContent.GetInstance<RandomizerConfig>().RandomDrops;

		// Regular enemies only: bosses, Old One's Army, town NPCs and critters keep their loot,
		// so boss progression and event rewards stay intact.
		public static bool IsEligible(NPC npc) {
			return npc.type > NPCID.None
				&& !npc.friendly
				&& !npc.townNPC
				&& !npc.boss
				&& npc.lifeMax > 5
				&& !NPCID.Sets.ShouldBeCountedAsBoss[npc.type]
				&& !NPCID.Sets.CountsAsCritter[npc.type]
				&& !NPCID.Sets.BelongsToInvasionOldOnesArmy[npc.type];
		}

		internal static void RegisterOriginal(int netID, List<IItemDropRule> rules) => originalRules[netID] = rules;

		// The enemy whose loot this enemy drops in ShuffleBetweenEnemies, fixed by the seed.
		public static int ShuffledSource(int netID) {
			if (shuffledGeneration != BehaviorPool.Generation) {
				shuffledSources.Clear();
				shuffledGeneration = BehaviorPool.Generation;
			}
			if (!shuffledSources.TryGetValue(netID, out int source)) {
				lootSources ??= originalRules.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).OrderBy(id => id).ToArray();
				var candidates = lootSources.Where(id => id != netID).ToArray();
				source = candidates.Length == 0 ? netID : candidates[BehaviorPool.SeededRandom(netID, LootRoll).Next(candidates.Length)];
				shuffledSources[netID] = source;
			}
			return source;
		}

		// Any real item: no deprecated items and no pickups like hearts that vanish when touched.
		public static int RandomItem(DropAttemptInfo info) {
			droppableItems ??= Enumerable.Range(1, ItemLoader.ItemCount - 1)
				.Where(type => !ItemID.Sets.Deprecated[type] && !ItemID.Sets.IsAPickup[type]
					&& ContentSamples.ItemsByType.TryGetValue(type, out Item item) && !item.IsAir)
				.ToArray();
			return droppableItems[info.rng.Next(droppableItems.Length)];
		}

		// Runs another enemy's original loot as if this enemy had it, the same way vanilla's ItemDropResolver does.
		public static void DropLootOf(int netID, DropAttemptInfo info) {
			if (originalRules.TryGetValue(netID, out var rules)) {
				foreach (IItemDropRule rule in rules) {
					Resolve(rule, info);
				}
			}
		}

		private static ItemDropAttemptResult Resolve(IItemDropRule rule, DropAttemptInfo info) {
			ItemDropAttemptResult result;
			if (!rule.CanDrop(info)) {
				result = new ItemDropAttemptResult { State = ItemDropAttemptResultState.DoesntFillConditions };
			}
			else if (rule is INestedItemDropRule nested) {
				result = nested.TryDroppingItem(info, Resolve);
			}
			else {
				result = rule.TryDroppingItem(info);
			}
			foreach (IItemDropRuleChainAttempt chain in rule.ChainedRules) {
				if (chain.CanChainIntoRule(result)) {
					Resolve(chain.RuleToChain, info);
				}
			}
			return result;
		}

		public override void Unload() {
			originalRules.Clear();
			shuffledSources.Clear();
			lootSources = null;
			droppableItems = null;
		}
	}
}
