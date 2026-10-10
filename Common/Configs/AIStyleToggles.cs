using System.ComponentModel;
using System.Linq;
using Terraria.ModLoader.Config;

namespace RandAwisor.Common.Configs
{
	// One toggle per AI style the randomizer can hand out. Each field must be named exactly like its
	// NPCAIStyleID constant: BehaviorPool reads these names to know which styles are swappable.
	// Styles not listed here need a parent NPC, body segments, a tile anchor or arena state
	// (bosses, worms, Man Eaters, spells, Old One's Army...) and break when another NPC borrows them.
	[SeparatePage]
	public class AIStyleToggles
	{
		[Header("Enemies")]
		[DefaultValue(true)] public bool Slime = true;
		[DefaultValue(true)] public bool DemonEye = true;
		[DefaultValue(true)] public bool Fighter = true;
		[DefaultValue(true)] public bool Flying = true;
		[DefaultValue(true)] public bool Caster = true;
		[DefaultValue(true)] public bool CursedSkull = true;
		[DefaultValue(true)] public bool Bat = true;
		[DefaultValue(true)] public bool Piranha = true;
		[DefaultValue(true)] public bool Vulture = true;
		[DefaultValue(true)] public bool Jellyfish = true;
		[DefaultValue(true)] public bool Antlion = true;
		[DefaultValue(true)] public bool HoveringFighter = true;
		[DefaultValue(true)] public bool EnchantedSword = true;
		[DefaultValue(true)] public bool Mimic = true;
		[DefaultValue(true)] public bool Unicorn = true;
		[DefaultValue(true)] public bool Snowman = true;
		[DefaultValue(true)] public bool GiantTortoise = true;
		[DefaultValue(true)] public bool Herpling = true;
		[DefaultValue(true)] public bool FlyingFish = true;
		[DefaultValue(true)] public bool AngryNimbus = true;
		[DefaultValue(true)] public bool DungeonSpirit = true;
		[DefaultValue(true)] public bool GraniteElemental = true;
		[DefaultValue(true)] public bool SandShark = true;

		[Header("CrittersAndTown")]
		[DefaultValue(true)] public bool Passive = true;
		[DefaultValue(true)] public bool Bird = true;
		[DefaultValue(true)] public bool Firefly = true;
		[DefaultValue(true)] public bool Butterfly = true;
		[DefaultValue(true)] public bool CritterWorm = true;
		[DefaultValue(true)] public bool Snail = true;
		[DefaultValue(true)] public bool Duck = true;
		[DefaultValue(true)] public bool Dragonfly = true;
		[DefaultValue(true)] public bool Ladybug = true;
		[DefaultValue(true)] public bool WaterStrider = true;
		[DefaultValue(true)] public bool Seahorse = true;

		// tModLoader compares nested config objects with Equals to detect pending changes.
		public override bool Equals(object obj) {
			return obj is AIStyleToggles other && GetType().GetFields().All(f => Equals(f.GetValue(this), f.GetValue(other)));
		}

		public override int GetHashCode() {
			return GetType().GetFields().Aggregate(17, (hash, f) => hash * 31 + f.GetValue(this).GetHashCode());
		}
	}
}
