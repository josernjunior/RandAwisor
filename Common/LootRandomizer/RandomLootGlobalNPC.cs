using System.Collections.Generic;
using System.Linq;
using RandAwisor.Common.Configs;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace RandAwisor.Common.LootRandomizer
{
	// Rewires every eligible enemy's loot table: its own rules move under NormalLootCondition, and RandomLootRule
	// adds the randomized part. Global drops (souls, biome keys...), coins, hearts and banners are untouched.
	public class RandomLootGlobalNPC : GlobalNPC
	{
		public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) {
			if (!LootPool.IsEligible(npc)) {
				return;
			}
			List<IItemDropRule> rules = npcLoot.Get(includeGlobalDrops: false).ToList();
			LootPool.RegisterOriginal(npc.netID, rules);

			var normalLoot = new LeadingConditionRule(new NormalLootCondition());
			foreach (IItemDropRule rule in rules) {
				npcLoot.Remove(rule);
				normalLoot.OnSuccess(rule);
			}
			npcLoot.Add(normalLoot);
			npcLoot.Add(new RandomLootRule());
		}
	}

	// The enemy's own loot drops unless a mode replaces it.
	public class NormalLootCondition : IItemDropRuleCondition
	{
		public bool CanDrop(DropAttemptInfo info) => LootPool.Mode is DropMode.Off or DropMode.ExtraRandomItem;

		// The bestiary keeps showing the normal drops, with no extra condition text.
		public bool CanShowItemDropInUI() => true;

		public string GetConditionDescription() => null;
	}

	public class RandomLootRule : IItemDropRule
	{
		public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();

		// Statue enemies never get random drops, or statue farms would print items.
		public bool CanDrop(DropAttemptInfo info) => LootPool.Mode != DropMode.Off && !info.npc.SpawnedFromStatue;

		public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info) {
			if (LootPool.Mode == DropMode.ShuffleBetweenEnemies) {
				LootPool.DropLootOf(LootPool.ShuffledSource(info.npc.netID), info);
			}
			else {
				CommonCode.DropItem(info, LootPool.RandomItem(info), 1);
			}
			return new ItemDropAttemptResult { State = ItemDropAttemptResultState.Success };
		}

		// Randomized drops aren't listed in the bestiary.
		public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo) { }
	}
}
