using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace RandAwisor.Common.AIRandomizer.CustomAIs
{
	// Behaviors that walk, hop or climb. Gravity on unless noted.

	// Tiny nonstop hops toward the target.
	public class Flea : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			if (OnGround(npc)) {
				npc.velocity = new Vector2(dir * 3f, -4.5f);
			}
			Face(npc, dir);
		}
	}

	// Rests, then a long leap toward the target.
	public class Kangaroo : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			Face(npc, dir);
			if (!OnGround(npc)) {
				return;
			}
			npc.velocity.X *= 0.8f;
			if (++npc.ai[0] >= 50f) {
				npc.ai[0] = 0f;
				npc.velocity = new Vector2(dir * 7f, -10f);
			}
		}
	}

	// Runs away from the closest player.
	public class Coward : CustomAI
	{
		public override void Update(NPC npc) {
			int away = -DirectionTo(npc, Target(npc));
			Walk(npc, away, 3.5f, 0.15f);
			JumpIfBlocked(npc, 7f);
			Face(npc, away);
		}
	}

	// Paces back and forth around its spawn point and ignores players.
	public class Patrol : CustomAI
	{
		public override void Update(NPC npc) {
			Vector2 home = Home(npc);
			if (npc.ai[1] == 0f) {
				npc.ai[1] = 1f;
			}
			bool tooFar = (npc.Center.X - home.X) * npc.ai[1] > 160f;
			if (tooFar || (npc.collideX && OnGround(npc))) {
				npc.ai[1] = -npc.ai[1];
			}
			Walk(npc, (int)npc.ai[1], 2f, 0.1f);
			Face(npc, (int)npc.ai[1]);
		}
	}

	// Winds up, charges in a straight line, then catches its breath.
	public class Ram : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.ai[0]++;
			switch ((int)npc.ai[1]) {
				case 0: // winding up
					npc.velocity.X *= 0.8f;
					Face(npc, DirectionTo(npc, target));
					if (npc.ai[0] >= 60f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 1f;
						npc.ai[2] = npc.direction;
					}
					break;
				case 1: // charging
					npc.velocity.X = npc.ai[2] * 11f;
					JumpIfBlocked(npc, 5f);
					if (npc.ai[0] >= 35f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 2f;
					}
					break;
				default: // recovering
					npc.velocity.X *= 0.9f;
					if (npc.ai[0] >= 30f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 0f;
					}
					break;
			}
		}
	}

	// Shuffles slowly in one direction and turns around at walls.
	public class Sleepwalker : CustomAI
	{
		public override void Update(NPC npc) {
			if (npc.ai[1] == 0f) {
				npc.ai[1] = npc.direction != 0 ? npc.direction : 1;
			}
			if (npc.collideX && OnGround(npc)) {
				npc.ai[1] = -npc.ai[1];
			}
			Walk(npc, (int)npc.ai[1], 1.2f, 0.05f);
			Face(npc, (int)npc.ai[1]);
		}
	}

	// Creeps closer only while the target looks away, and freezes when watched.
	public class Shy : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			int dir = DirectionTo(npc, target);
			bool watched = target.direction == -dir;
			if (watched) {
				npc.velocity.X *= 0.7f;
			}
			else {
				Walk(npc, dir, 3.5f, 0.2f);
				JumpIfBlocked(npc, 7f);
			}
			Face(npc, dir);
		}
	}

	// Moves opposite to the target's horizontal movement and jumps when it jumps.
	public class Mirror : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.velocity.X = MathHelper.Lerp(npc.velocity.X, -target.velocity.X, 0.2f);
			if (target.velocity.Y < -2f && OnGround(npc)) {
				npc.velocity.Y = -7f;
			}
			FaceVelocity(npc);
		}
	}

	// Copies the target's movement, catching up when left far behind.
	public class Shadow : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			bool farBehind = Math.Abs(target.Center.X - npc.Center.X) > 600f;
			float follow = farBehind ? DirectionTo(npc, target) * 5f : target.velocity.X;
			npc.velocity.X = MathHelper.Lerp(npc.velocity.X, follow, 0.2f);
			if (target.velocity.Y < -2f && OnGround(npc)) {
				npc.velocity.Y = -7f;
			}
			JumpIfBlocked(npc, 6f);
			FaceVelocity(npc);
		}
	}

	// Bounces like a rubber ball in one direction, reversing at walls. Ignores players.
	public class Bouncer : CustomAI
	{
		public override void Update(NPC npc) {
			if (npc.ai[1] == 0f) {
				npc.ai[1] = DirectionTo(npc, Target(npc));
			}
			if (npc.collideX) {
				npc.ai[1] = -npc.ai[1];
			}
			npc.velocity.X = npc.ai[1] * 4f;
			if (OnGround(npc)) {
				npc.velocity.Y = -8f;
			}
			Face(npc, (int)npc.ai[1]);
		}
	}

	// Builds up speed toward the target like a rolling boulder and overshoots.
	public class Boulder : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			Walk(npc, dir, 9f, 0.07f);
			JumpIfBlocked(npc, 6f);
			npc.rotation += npc.velocity.X * 0.03f;
			Face(npc, dir);
		}
	}

	// Stumbles toward the target, sometimes the wrong way, with random hops.
	public class Dizzy : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			npc.ai[0]++;
			if (IsServer && Main.rand.NextBool(120)) {
				npc.ai[1] = npc.ai[1] == 0f ? 1f : 0f;
				npc.netUpdate = true;
			}
			if (IsServer && OnGround(npc) && Main.rand.NextBool(90)) {
				npc.velocity.Y = -5f;
				npc.netUpdate = true;
			}
			int walkDir = npc.ai[1] == 0f ? dir : -dir;
			float speed = 2f + (float)Math.Sin(npc.ai[0] * 0.05f) * 1.5f;
			Walk(npc, walkDir, speed, 0.1f);
			JumpIfBlocked(npc, 6f);
			npc.rotation = (float)Math.Sin(npc.ai[0] * 0.1f) * 0.15f;
			Face(npc, walkDir);
		}
	}

	// Barely moves.
	public class Sloth : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			Walk(npc, dir, 0.35f, 0.02f);
			Face(npc, dir);
		}
	}

	// Pushes off toward the target, then glides without friction, like on ice.
	public class Skater : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			npc.ai[0] = (npc.ai[0] + 1f) % 60f;
			if (npc.ai[0] < 10f && OnGround(npc)) {
				npc.velocity.X = MathHelper.Clamp(npc.velocity.X + dir * 0.5f, -8f, 8f);
			}
			JumpIfBlocked(npc, 6f);
			Face(npc, dir);
		}
	}

	// Floaty low-gravity jumps toward the target.
	public class Lunar : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			if (OnGround(npc)) {
				npc.velocity = new Vector2(dir * 3f, -6f);
			}
			// Cancels most of the 0.3 gravity applied after the AI.
			npc.velocity.Y -= 0.22f;
			Face(npc, dir);
		}
	}

	// Leaps high when close to the target and slams down hard.
	public class Anvil : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			int dir = DirectionTo(npc, target);
			Face(npc, dir);
			if (!OnGround(npc)) {
				if (npc.velocity.Y > 0f) {
					npc.velocity.Y += 0.6f;
					npc.ai[1] = 1f;
				}
				return;
			}
			if (npc.ai[1] == 1f) {
				npc.ai[1] = 0f;
				if (!Main.dedServ) {
					for (int i = 0; i < 20; i++) {
						Dust.NewDust(npc.BottomLeft - new Vector2(8f, 8f), npc.width + 16, 16, DustID.Smoke);
					}
				}
			}
			if (Math.Abs(target.Center.X - npc.Center.X) < 100f) {
				npc.velocity = new Vector2(dir * 2f, -13f);
			}
			else {
				Walk(npc, dir, 2f, 0.1f);
				JumpIfBlocked(npc, 7f);
			}
		}
	}

	// Falls upward and walks along ceilings, dropping back when it rises too far above the target.
	public class Ceiling : CustomAI
	{
		public override bool NoGravity => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			int dir = DirectionTo(npc, target);
			bool tooHigh = npc.Center.Y < target.Center.Y - 480f;
			npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + (tooHigh ? 0.3f : -0.3f), -10f, 10f);
			Walk(npc, dir, 3f, 0.15f, stepUp: false);
			npc.rotation = MathHelper.Pi;
			Face(npc, dir);
		}
	}

	// Walks toward the target and climbs straight up walls instead of jumping.
	public class Gecko : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			Walk(npc, dir, 3f, 0.15f);
			if (npc.collideX) {
				npc.velocity.Y = -3f;
			}
			Face(npc, dir);
		}
	}

	// Springs high into the air, then drifts down toward the target under a parachute.
	public class Parachute : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			Face(npc, dir);
			if (OnGround(npc)) {
				npc.rotation = 0f;
				npc.velocity.X *= 0.8f;
				if (++npc.ai[0] >= 30f) {
					npc.ai[0] = 0f;
					npc.velocity.Y = -12f;
				}
			}
			else if (npc.velocity.Y > 0f) {
				npc.velocity.Y = Math.Min(npc.velocity.Y, 1.5f);
				npc.velocity.X = MathHelper.Lerp(npc.velocity.X, dir * 2.5f, 0.05f);
				npc.rotation = (float)Math.Sin(++npc.ai[1] * 0.08f) * 0.2f;
			}
		}
	}

	// Moves in stiff steps: walks a beat, stops a beat.
	public class Clockwork : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			npc.ai[0] = (npc.ai[0] + 1f) % 40f;
			if (npc.ai[0] < 20f) {
				npc.velocity.X = dir * 4f;
				JumpIfBlocked(npc, 6f);
			}
			else {
				npc.velocity.X = 0f;
			}
			Face(npc, dir);
		}
	}

	// Wanders peacefully until hurt, then hunts for a while.
	public class Grudge : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			if (npc.justHit) {
				npc.ai[1] = 300f;
			}
			if (npc.ai[1] > 0f) {
				npc.ai[1]--;
				int dir = DirectionTo(npc, target);
				Walk(npc, dir, 6f, 0.3f);
				JumpIfBlocked(npc, 8f);
				Face(npc, dir);
				if (!Main.dedServ && Main.rand.NextBool(4)) {
					Dust.NewDust(npc.position, npc.width, npc.height, DustID.Torch);
				}
				return;
			}
			if (npc.ai[2] == 0f) {
				npc.ai[2] = 1f;
			}
			if (npc.collideX && OnGround(npc)) {
				npc.ai[2] = -npc.ai[2];
			}
			Walk(npc, (int)npc.ai[2], 1f, 0.05f);
			Face(npc, (int)npc.ai[2]);
		}
	}

	// Chases the target, but runs away once below half health.
	public class Skittish : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			bool hurt = npc.life < npc.lifeMax / 2;
			int walkDir = hurt ? -dir : dir;
			Walk(npc, walkDir, hurt ? 4.5f : 2.5f, 0.15f);
			JumpIfBlocked(npc, 7f);
			Face(npc, walkDir);
		}
	}

	// Walks toward the target and keeps skipping a few tiles at random.
	public class Glitch : CustomAI
	{
		public override void Update(NPC npc) {
			int dir = DirectionTo(npc, Target(npc));
			Walk(npc, dir, 2.5f, 0.1f);
			JumpIfBlocked(npc, 7f);
			Face(npc, dir);
			if (IsServer && ++npc.ai[0] >= 45f) {
				npc.ai[0] = 0f;
				var offset = new Vector2(Main.rand.Next(-3, 4) * 16f, Main.rand.Next(-2, 1) * 16f);
				TryTeleport(npc, npc.Center + offset);
			}
			ShowTeleports(npc);
		}
	}

	// Stalks the target, nearly invisible until it gets close.
	public class Chameleon : CustomAI
	{
		public override void Update(NPC npc) {
			Player target = Target(npc);
			int dir = DirectionTo(npc, target);
			Walk(npc, dir, 2.2f, 0.1f);
			JumpIfBlocked(npc, 6f);
			Face(npc, dir);
			int goal = npc.Distance(target.Center) > 160f ? 235 : 0;
			npc.alpha = (int)MathHelper.Lerp(npc.alpha, goal, 0.1f);
		}
	}
}
