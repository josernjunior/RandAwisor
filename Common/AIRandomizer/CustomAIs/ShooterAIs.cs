using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace RandAwisor.Common.AIRandomizer.CustomAIs
{
	// Behaviors built around shooting. Shoot() only fires for hostile NPCs, so a town NPC or critter
	// with one of these still moves the same way but never attacks.

	// Hovers at its spawn point and fires lasers at the target when it has line of sight.
	public class Sentinel : CustomAI
	{
		public override bool NoGravity => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			Vector2 home = Home(npc);
			Steer(npc, home + new Vector2(0f, (float)Math.Sin(npc.ai[0] * 0.05f) * 10f), 4f, 0.1f);
			Face(npc, DirectionTo(npc, target));
			bool canSee = Collision.CanHitLine(npc.position, npc.width, npc.height, target.position, target.width, target.height);
			if (++npc.ai[0] % 90f == 0f && canSee) {
				Shoot(npc, AimAt(npc, target, 8f), ProjectileID.EyeLaser);
			}
		}
	}

	// Plants itself and fires three-shot laser bursts.
	public class Turret : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.velocity.X *= 0.8f;
			Face(npc, DirectionTo(npc, target));
			float t = ++npc.ai[0] % 120f;
			if (t == 100f || t == 106f || t == 112f) {
				Shoot(npc, AimAt(npc, target, 9f), ProjectileID.DeathLaser);
			}
		}
	}

	// Keeps its distance and lobs snowballs in an arc.
	public class Mortar : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			int dir = DirectionTo(npc, target);
			float dx = target.Center.X - npc.Center.X;
			Walk(npc, Math.Abs(dx) < 200f ? -dir : dir, 1f, 0.05f);
			JumpIfBlocked(npc, 5f);
			Face(npc, dir);
			if (++npc.ai[0] % 100f == 0f) {
				Shoot(npc, new Vector2(MathHelper.Clamp(dx / 45f, -9f, 9f), -8f), ProjectileID.SnowBallHostile);
			}
		}
	}

	// Flies at a distance from the target and fires a spread of stingers.
	public class Fan : CustomAI
	{
		public override bool NoGravity => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			Steer(npc, target.Center + target.DirectionTo(npc.Center) * 260f, 6f, 0.08f);
			Face(npc, DirectionTo(npc, target));
			if (++npc.ai[0] % 110f == 0f) {
				for (int i = -2; i <= 2; i++) {
					Shoot(npc, AimAt(npc, target, 7f).RotatedBy(i * 0.17f), ProjectileID.Stinger);
				}
			}
		}
	}

	// Sits still and sprays stingers in eight directions.
	public class Urchin : CustomAI
	{
		public override void Update(NPC npc) {
			npc.velocity.X *= 0.9f;
			if (++npc.ai[0] % 100f == 0f) {
				for (int i = 0; i < 8; i++) {
					Shoot(npc, (i * MathHelper.PiOver4).ToRotationVector2() * 6f, ProjectileID.Stinger);
				}
			}
		}
	}

	// Keeps a long distance, shows its aim for a second, then fires one fast laser.
	public class Sniper : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			int dir = DirectionTo(npc, target);
			float distance = npc.Distance(target.Center);
			int walkDir = distance < 350f ? -dir : distance > 550f ? dir : 0;
			Walk(npc, walkDir, 2.5f, 0.1f);
			JumpIfBlocked(npc, 6f);
			Face(npc, dir);
			float t = ++npc.ai[0] % 150f;
			if (t >= 90f && t % 3f == 0f && CanAttack(npc) && !Main.dedServ) {
				Vector2 aim = npc.DirectionTo(target.Center);
				for (int i = 1; i <= 6; i++) {
					Dust dust = Dust.NewDustPerfect(npc.Center + aim * i * 40f, DustID.RedTorch, Vector2.Zero);
					dust.noGravity = true;
				}
			}
			if (t == 149f) {
				Shoot(npc, AimAt(npc, target, 14f), ProjectileID.DeathLaser);
			}
		}
	}
}
