using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace RandAwisor.Common.Configs
{
	// One toggle per behavior in Common/AIRandomizer/CustomAIs. Each field must be named exactly like its
	// CustomAI class; BehaviorPool checks at load that the two lists match.
	[SeparatePage]
	public class AdditionalAIToggles : ToggleSet
	{
		[Header("Ground")]
		[DefaultValue(true)] public bool Flea = true;
		[DefaultValue(true)] public bool Kangaroo = true;
		[DefaultValue(true)] public bool Coward = true;
		[DefaultValue(true)] public bool Patrol = true;
		[DefaultValue(true)] public bool Ram = true;
		[DefaultValue(true)] public bool Sleepwalker = true;
		[DefaultValue(true)] public bool Shy = true;
		[DefaultValue(true)] public bool Mirror = true;
		[DefaultValue(true)] public bool Shadow = true;
		[DefaultValue(true)] public bool Bouncer = true;
		[DefaultValue(true)] public bool Boulder = true;
		[DefaultValue(true)] public bool Dizzy = true;
		[DefaultValue(true)] public bool Sloth = true;
		[DefaultValue(true)] public bool Skater = true;
		[DefaultValue(true)] public bool Lunar = true;
		[DefaultValue(true)] public bool Anvil = true;
		[DefaultValue(true)] public bool Ceiling = true;
		[DefaultValue(true)] public bool Gecko = true;
		[DefaultValue(true)] public bool Parachute = true;
		[DefaultValue(true)] public bool Clockwork = true;
		[DefaultValue(true)] public bool Grudge = true;
		[DefaultValue(true)] public bool Skittish = true;
		[DefaultValue(true)] public bool Glitch = true;
		[DefaultValue(true)] public bool Chameleon = true;

		[Header("Flying")]
		[DefaultValue(true)] public bool Orbit = true;
		[DefaultValue(true)] public bool Spiral = true;
		[DefaultValue(true)] public bool Zigzag = true;
		[DefaultValue(true)] public bool Diver = true;
		[DefaultValue(true)] public bool Hummingbird = true;
		[DefaultValue(true)] public bool Haunt = true;
		[DefaultValue(true)] public bool Blink = true;
		[DefaultValue(true)] public bool Magnet = true;
		[DefaultValue(true)] public bool Spring = true;
		[DefaultValue(true)] public bool Pendulum = true;
		[DefaultValue(true)] public bool Wave = true;
		[DefaultValue(true)] public bool Boomerang = true;
		[DefaultValue(true)] public bool Swarm = true;
		[DefaultValue(true)] public bool Kite = true;
		[DefaultValue(true)] public bool Comet = true;
		[DefaultValue(true)] public bool Whirlwind = true;
		[DefaultValue(true)] public bool FigureEight = true;
		[DefaultValue(true)] public bool Tick = true;
		[DefaultValue(true)] public bool Rocket = true;
		[DefaultValue(true)] public bool Knight = true;

		[Header("Shooters")]
		[DefaultValue(true)] public bool Sentinel = true;
		[DefaultValue(true)] public bool Turret = true;
		[DefaultValue(true)] public bool Mortar = true;
		[DefaultValue(true)] public bool Fan = true;
		[DefaultValue(true)] public bool Urchin = true;
		[DefaultValue(true)] public bool Sniper = true;
	}
}
