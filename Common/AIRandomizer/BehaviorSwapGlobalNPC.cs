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
	// Runs another NPC type's vanilla AI in place of the NPC's own, the same way ModNPC.AIType works:
	// npc.type and npc.aiStyle are swapped to the borrowed type only while the AI runs.
	// The server picks the borrowed type and syncs it, so clients run the same AI.
	public class BehaviorSwapGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		// NPC type whose behavior this NPC borrows; 0 means its own behavior.
		private int mimicType;
		// Mode and BehaviorPool.Generation the current mimicType was picked for, so config changes re-pick it.
		private RandomizerMode? pickedMode;
		private int pickedGeneration;
		// Type whose noGravity/noTileCollide are currently applied; 0 means the NPC's own.
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

			// Clients check the style too, so a style that was just disabled stops before the server's re-pick arrives.
			bool mimicking = mimicType > 0
				&& config.AIMode != RandomizerMode.Off
				&& BehaviorPool.IsAffected(npc, config)
				&& BehaviorPool.IsStyleEnabled(ContentSamples.NpcsByNetId[mimicType].aiStyle);
			ApplyPhysics(npc, mimicking ? mimicType : 0);
			if (!mimicking) {
				return true;
			}

			RunBorrowedAI(npc);
			return false;
		}

		private void PickBehavior(NPC npc, RandomizerConfig config) {
			if (config.AIMode == RandomizerMode.Off || !NeedsPick(config)) {
				return;
			}
			pickedMode = config.AIMode;
			pickedGeneration = BehaviorPool.Generation;
			int source = config.AIMode == RandomizerMode.ShuffleByType
				? BehaviorPool.ShuffledSource(npc.type)
				: BehaviorPool.RandomSource(npc.type);
			SetMimicType(npc, source);
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
			if (mimicType == 0) {
				return pickedGeneration != BehaviorPool.Generation;
			}
			return !BehaviorPool.IsStyleEnabled(ContentSamples.NpcsByNetId[mimicType].aiStyle);
		}

		private void SetMimicType(NPC npc, int type) {
			if (type == mimicType) {
				return;
			}
			mimicType = type;
			// Leftover state from the previous AI would confuse the new one.
			Array.Clear(npc.ai);
			Array.Clear(npc.localAI);
			npc.netUpdate = true;
		}

		private void RunBorrowedAI(NPC npc) {
			int ownType = npc.type;
			int ownStyle = npc.aiStyle;
			npc.type = mimicType;
			npc.aiStyle = ContentSamples.NpcsByNetId[mimicType].aiStyle;
			BehaviorPool.MimickingNPC = npc.whoAmI;
			try {
				npc.VanillaAI();
			}
			catch (Exception e) {
				Mod.Logger.Warn($"NPC {ownType} crashed while borrowing the AI of NPC {mimicType}; that behavior is disabled for this session.", e);
				BehaviorPool.MarkBroken(mimicType);
				// Fall back to the NPC's own AI; the server picks a new behavior on its next PreAI.
				mimicType = 0;
				pickedMode = null;
			}
			finally {
				BehaviorPool.MimickingNPC = -1;
				npc.type = ownType;
				npc.aiStyle = ownStyle;
			}
		}

		// Flying AIs need noGravity, ghosts need noTileCollide, etc. These are normally set in SetDefaults,
		// so copy them from the NPC whose behavior is running.
		private void ApplyPhysics(NPC npc, int type) {
			if (physicsOf == type) {
				return;
			}
			NPC sample = ContentSamples.NpcsByNetId[type > 0 ? type : npc.type];
			npc.noGravity = sample.noGravity;
			npc.noTileCollide = sample.noTileCollide;
			physicsOf = type;
		}

		public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
			binaryWriter.Write7BitEncodedInt(mimicType);
		}

		public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
			int type = binaryReader.Read7BitEncodedInt();
			mimicType = type > NPCID.None && type < NPCID.Count ? type : 0;
		}
	}
}
