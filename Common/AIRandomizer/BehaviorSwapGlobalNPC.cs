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
			int picked = config.AIMode == RandomizerMode.ShuffleByType
				? BehaviorPool.ShuffledBehavior(npc.type)
				: BehaviorPool.RandomBehavior(npc.type);
			SetBehavior(npc, picked);
		}

		private bool NeedsPick(RandomizerConfig config) {
			if (pickedMode != config.AIMode) {
				return true;
			}
			// A new seed or toggle can change the shuffle mapping; NPCs whose result is unchanged keep their state.
			if (config.AIMode == RandomizerMode.ShuffleByType) {
				return pickedGeneration != BehaviorPool.Generation;
			}
			// Random mode only re-rolls NPCs whose behavior was just disabled, or that found nothing enabled last time.
			if (behavior == 0) {
				return pickedGeneration != BehaviorPool.Generation;
			}
			return !BehaviorPool.IsEnabled(behavior);
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
