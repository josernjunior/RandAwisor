using System.ComponentModel;
using RandAwisor.Common.AIRandomizer;
using Terraria.ModLoader.Config;

namespace RandAwisor.Common.Configs
{
	public enum RandomizerMode
	{
		Off,
		// Every NPC type borrows the behavior of one other NPC type, fixed by the seed.
		ShuffleByType,
		// Every spawned NPC borrows the behavior of a random NPC type.
		RandomPerNPC
	}

	public class RandomizerConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		[Header("Behavior")]
		[DefaultValue(RandomizerMode.ShuffleByType)]
		[DrawTicks]
		public RandomizerMode AIMode;

		// Text so any number fits (an int field would be a 0-100 slider). Empty means the world's seed.
		[DefaultValue("")]
		public string Seed;

		[DefaultValue(true)]
		public bool AffectHostiles;

		[DefaultValue(true)]
		public bool AffectTownNPCs;

		[DefaultValue(true)]
		public bool AffectCritters;

		[Header("AIStyles")]
		public AIStyleToggles AIStyles = new();

		public override void OnChanged() {
			BehaviorPool.ApplyConfig(this);
		}
	}
}
