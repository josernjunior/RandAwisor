using System;
using System.IO;
using RandAwisor.Common.Configs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace RandAwisor.Common.AIRandomizer
{
	// Runs another behavior in place of the NPC's own AI: either a CustomAI, or another NPC type's vanilla AI
	// the same way ModNPC.AIType works (npc.type and npc.aiStyle are swapped only while that AI runs).
	// The server picks the behavior and syncs it, so clients run the same one.
	public class BehaviorSwapGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		// See BehaviorPool for the encoding; 0 means the NPC's own AI.
		private int behavior;
		// This NPC's draw from the seed in RandomPerNPC mode (see BehaviorPool.SeededBehavior); 0 until it needs one.
		// The only thing saved: the behavior itself is always recomputed from the seed, type and roll.
		private int roll;
		// Mode and BehaviorPool.Generation the current behavior was picked for, so config changes re-pick it.
		private RandomizerMode? pickedMode;
		private int pickedGeneration;
		// Behavior whose noGravity/noTileCollide are currently applied; 0 means the NPC's own.
		private int physicsOf;

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
			return lateInstantiation && BehaviorPool.IsEligible(entity);
		}

		public override void OnSpawn(NPC npc, IEntitySource source) {
			PickBehavior(npc, ModContent.GetInstance<RandomizerConfig>());
		}

		public override bool PreAI(NPC npc) {
			var config = ModContent.GetInstance<RandomizerConfig>();
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				// Covers NPCs that never went through OnSpawn, like town NPCs loaded with the world.
				PickBehavior(npc, config);
			}

			// Clients check IsEnabled too, so a behavior that was just disabled stops before the server's re-pick arrives.
			bool active = behavior != 0
				&& config.AIMode != RandomizerMode.Off
				&& BehaviorPool.IsAffected(npc, config)
				&& BehaviorPool.IsEnabled(behavior);
			ApplyPhysics(npc, active ? behavior : 0);
			if (!active) {
				return true;
			}

			RunBehavior(npc);
			return false;
		}

		private void PickBehavior(NPC npc, RandomizerConfig config) {
			if (config.AIMode == RandomizerMode.Off || !NeedsPick(config)) {
				return;
			}
			pickedMode = config.AIMode;
			pickedGeneration = BehaviorPool.Generation;
			if (config.AIMode == RandomizerMode.RandomPerNPC && roll == 0) {
				roll = BehaviorPool.NextRoll();
			}
			int picked = BehaviorPool.SeededBehavior(npc.type, config.AIMode == RandomizerMode.RandomPerNPC ? roll : 0);
			SetBehavior(npc, picked);
		}

		// Picks are a pure function of the seed, config, type and roll, so re-picking after any change is safe:
		// NPCs whose result didn't change keep their state (SetBehavior ignores the same value).
		private bool NeedsPick(RandomizerConfig config) {
			return pickedMode != config.AIMode || pickedGeneration != BehaviorPool.Generation;
		}

		private void SetBehavior(NPC npc, int value) {
			if (value == behavior) {
				return;
			}
			behavior = value;
			// Leftover state from the previous AI would confuse the new one.
			Array.Clear(npc.ai);
			Array.Clear(npc.localAI);
			npc.netUpdate = true;
		}

		private void RunBehavior(NPC npc) {
			try {
				if (BehaviorPool.IsCustom(behavior)) {
					BehaviorPool.CustomAIOf(behavior).Update(npc);
				}
				else {
					RunBorrowedVanillaAI(npc);
				}
			}
			catch (Exception e) {
				Mod.Logger.Warn($"NPC {npc.type} crashed while running {BehaviorPool.Describe(behavior)}; that behavior is disabled for this session.", e);
				BehaviorPool.MarkBroken(behavior);
				// Fall back to the NPC's own AI; the server picks a new behavior on its next PreAI.
				behavior = 0;
				pickedMode = null;
			}
		}

		private void RunBorrowedVanillaAI(NPC npc) {
			int ownType = npc.type;
			int ownStyle = npc.aiStyle;
			npc.type = behavior;
			npc.aiStyle = ContentSamples.NpcsByNetId[behavior].aiStyle;
			BehaviorPool.MimickingNPC = npc.whoAmI;
			try {
				npc.VanillaAI();
			}
			finally {
				BehaviorPool.MimickingNPC = -1;
				npc.type = ownType;
				npc.aiStyle = ownStyle;
			}
		}

		// Flying AIs need noGravity, ghosts need noTileCollide, etc. Vanilla sets these in SetDefaults rather than
		// in the AI, so copy them from whatever behavior is running.
		private void ApplyPhysics(NPC npc, int newBehavior) {
			if (physicsOf == newBehavior) {
				return;
			}
			NPC own = ContentSamples.NpcsByNetId[npc.type];
			if (BehaviorPool.IsCustom(newBehavior)) {
				var ai = BehaviorPool.CustomAIOf(newBehavior);
				npc.noGravity = ai.NoGravity;
				npc.noTileCollide = ai.NoTileCollide;
			}
			else {
				NPC from = newBehavior > 0 ? ContentSamples.NpcsByNetId[newBehavior] : own;
				npc.noGravity = from.noGravity;
				npc.noTileCollide = from.noTileCollide;
			}
			// Some behaviors spin or fade the NPC; each one starts from the NPC's own look.
			npc.rotation = 0f;
			npc.alpha = own.alpha;
			physicsOf = newBehavior;
		}

		// Town NPCs (and other NPCs the game saves) keep their roll, so after reloading the world they get the same
		// behavior from the same seed. ShuffleByType needs nothing saved: its pick depends only on the seed and type.
		public override void SaveData(NPC npc, TagCompound tag) {
			if (roll > 0) {
				tag["Roll"] = roll;
			}
		}

		public override void LoadData(NPC npc, TagCompound tag) {
			roll = tag.GetInt("Roll");
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			bool custom = BehaviorPool.IsCustom(behavior);
			bitWriter.WriteBit(custom);
			binaryWriter.Write7BitEncodedInt(custom ? BehaviorPool.CustomIndexOf(behavior) : behavior);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			bool custom = bitReader.ReadBit();
			int value = binaryReader.Read7BitEncodedInt();
			if (custom) {
				behavior = BehaviorPool.IsValidCustomIndex(value) ? BehaviorPool.CustomBehavior(value) : 0;
			}
			else {
				behavior = value > NPCID.None && value < NPCID.Count ? value : 0;
			}
		}
	}
}
