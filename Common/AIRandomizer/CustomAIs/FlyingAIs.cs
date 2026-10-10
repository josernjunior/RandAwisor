using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace RandAwisor.Common.AIRandomizer.CustomAIs
{
	// Behaviors that fly. Most follow a path around the target and pass through walls so they can't get stuck on it.

	// Circles the target.
	public class Orbit : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.ai[1] += 0.035f;
			Steer(npc, target.Center + npc.ai[1].ToRotationVector2() * 180f, 12f);
			FaceVelocity(npc);
		}
	}

	// Circles the target while drifting in and out.
	public class Spiral : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.ai[1] += 0.04f;
			float radius = 160f + (float)Math.Sin(++npc.ai[0] * 0.02f) * 100f;
			Steer(npc, target.Center + npc.ai[1].ToRotationVector2() * radius, 12f);
			FaceVelocity(npc);
		}
	}

	// Approaches the target swerving side to side.
	public class Zigzag : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			Vector2 toward = npc.DirectionTo(target.Center);
			Vector2 side = toward.RotatedBy(MathHelper.PiOver2);
			npc.velocity = toward * 3f + side * (float)Math.Sin(++npc.ai[0] * 0.12f) * 5f;
			FaceVelocity(npc);
		}
	}

	// Hovers above the target, dives straight down, then climbs back.
	public class Diver : CustomAI
	{
		public override bool NoGravity => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			Face(npc, DirectionTo(npc, target));
			npc.ai[0]++;
			switch ((int)npc.ai[1]) {
				case 0: // hovering
					Steer(npc, target.Center + new Vector2(0f, -220f), 8f, 0.1f);
					if (npc.ai[0] >= 90f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 1f;
					}
					break;
				case 1: // diving
					npc.velocity = new Vector2(0f, 14f);
					if (npc.ai[0] >= 40f || npc.collideY) {
						npc.ai[0] = 0f;
						npc.ai[1] = 2f;
					}
					break;
				default: // climbing back
					npc.velocity = new Vector2(0f, -4f);
					if (npc.ai[0] >= 40f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 0f;
					}
					break;
			}
		}
	}

	// Darts to a random spot near the target, pauses, and darts again.
	public class Hummingbird : CustomAI
	{
		public override bool NoGravity => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			var spot = new Vector2(npc.ai[2], npc.ai[3]);
			bool arrived = spot == Vector2.Zero || npc.Distance(spot) < 24f || npc.ai[0] > 90f;
			if (!arrived) {
				npc.ai[0]++;
				Steer(npc, spot, 14f, 0.3f);
				FaceVelocity(npc);
				return;
			}
			npc.velocity *= 0.8f;
			if (++npc.ai[1] >= 30f && IsServer) {
				spot = target.Center + Main.rand.NextVector2Circular(160f, 160f);
				npc.ai[0] = 0f;
				npc.ai[1] = 0f;
				npc.ai[2] = spot.X;
				npc.ai[3] = spot.Y;
				npc.netUpdate = true;
			}
		}
	}

	// Drifts slowly through walls toward the target, fading out when far away.
	public class Haunt : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.velocity = Vector2.Lerp(npc.velocity, npc.DirectionTo(target.Center) * 1.8f, 0.05f);
			int goal = npc.Distance(target.Center) > 300f ? 200 : 60;
			npc.alpha = (int)MathHelper.Lerp(npc.alpha, goal, 0.05f);
			FaceVelocity(npc);
		}
	}

	// Hangs in the air and teleports next to the target every few seconds.
	public class Blink : CustomAI
	{
		public override bool NoGravity => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.velocity *= 0.9f;
			Face(npc, DirectionTo(npc, target));
			if (IsServer && ++npc.ai[0] >= 100f) {
				npc.ai[0] = 0f;
				for (int attempt = 0; attempt < 10; attempt++) {
					Vector2 spot = target.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(180f, 260f);
					if (TryTeleport(npc, spot)) {
						break;
					}
				}
			}
			ShowTeleports(npc);
		}
	}

	// Pulled toward the target with no brakes, so it keeps overshooting.
	public class Magnet : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.velocity += npc.DirectionTo(target.Center) * 0.3f;
			LimitSpeed(npc, 13f);
			FaceVelocity(npc);
		}
	}

	// Bobs on an invisible spring hanging above the target.
	public class Spring : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			Vector2 anchor = target.Center + new Vector2(0f, -120f);
			npc.velocity = npc.velocity * 0.985f + (anchor - npc.Center) * 0.01f;
			LimitSpeed(npc, 16f);
			FaceVelocity(npc);
		}
	}

	// Swings back and forth under a point above its spawn. Ignores players.
	public class Pendulum : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Vector2 pivot = Home(npc) - new Vector2(0f, 160f);
			float angle = (float)Math.Sin(++npc.ai[0] * 0.04f) * 1.1f;
			Steer(npc, pivot + new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle)) * 160f, 10f, 0.3f);
			FaceVelocity(npc);
		}
	}

	// Sweeps past the target in a sine wave, turning around once well past it.
	public class Wave : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			if (npc.ai[1] == 0f) {
				npc.ai[1] = DirectionTo(npc, target);
			}
			if ((npc.Center.X - target.Center.X) * npc.ai[1] > 500f) {
				npc.ai[1] = -npc.ai[1];
			}
			float waveY = target.Center.Y + (float)Math.Sin(++npc.ai[0] * 0.06f) * 120f;
			npc.velocity = new Vector2(npc.ai[1] * 5f, MathHelper.Clamp((waveY - npc.Center.Y) * 0.08f, -6f, 6f));
			Face(npc, (int)npc.ai[1]);
		}
	}

	// Spins out toward the target and flies back to its spawn point, over and over.
	public class Boomerang : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			Vector2 home = Home(npc);
			npc.ai[0]++;
			if (npc.ai[1] == 0f) { // flying out
				if (npc.ai[0] == 1f) {
					npc.localAI[0] = (target.Center - npc.Center).ToRotation();
				}
				npc.velocity = npc.localAI[0].ToRotationVector2() * 12f;
				if (npc.ai[0] >= 45f) {
					npc.ai[0] = 0f;
					npc.ai[1] = 1f;
				}
			}
			else { // coming back
				Steer(npc, home, 12f, 0.15f);
				if (npc.Distance(home) < 30f) {
					npc.ai[0] = 0f;
					npc.ai[1] = 0f;
				}
			}
			npc.rotation += 0.3f;
		}
	}

	// Buzzes around the target in jittery bursts.
	public class Swarm : CustomAI
	{
		public override bool NoGravity => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.velocity += npc.DirectionTo(target.Center) * 0.12f + Main.rand.NextVector2Circular(0.8f, 0.8f);
			npc.velocity *= 0.96f;
			LimitSpeed(npc, 6f);
			FaceVelocity(npc);
		}
	}

	// Floats high above the target, trailing behind it and swaying.
	public class Kite : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			Vector2 spot = target.Center + new Vector2((float)Math.Sin(++npc.ai[0] * 0.03f) * 80f, -260f);
			Steer(npc, spot, 7f, 0.05f);
			npc.rotation = npc.velocity.X * 0.05f;
		}
	}

	// Streaks across the target's area in straight lines from random directions.
	public class Comet : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			if (IsServer && npc.ai[0] <= 0f) {
				float angle = Main.rand.NextFloat(MathHelper.TwoPi);
				npc.ai[0] = 90f;
				npc.ai[1] = angle;
				npc.Center = target.Center - angle.ToRotationVector2() * 700f;
				npc.netUpdate = true;
			}
			npc.ai[0]--;
			npc.velocity = npc.ai[1].ToRotationVector2() * 16f;
			npc.rotation = npc.ai[1] + MathHelper.PiOver2;
			ShowTeleports(npc);
		}
	}

	// Spirals in on the target, faster and tighter, then starts over wide.
	public class Whirlwind : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			float radius = 300f - (++npc.ai[0] % 240f) * 1.1f;
			npc.ai[1] += 0.03f + (300f - radius) * 0.0003f;
			Steer(npc, target.Center + npc.ai[1].ToRotationVector2() * radius, 14f, 0.25f);
			FaceVelocity(npc);
		}
	}

	// Traces a figure eight around the target.
	public class FigureEight : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			float t = ++npc.ai[0] * 0.03f;
			Steer(npc, target.Center + new Vector2((float)Math.Sin(t) * 220f, (float)Math.Sin(2f * t) * 90f), 12f, 0.25f);
			FaceVelocity(npc);
		}
	}

	// Latches onto the target for a couple of seconds, then gets shaken off.
	public class Tick : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.ai[0]++;
			switch ((int)npc.ai[1]) {
				case 0: // closing in
					Steer(npc, target.Center, 7f, 0.1f);
					if (npc.Distance(target.Center) < 30f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 1f;
					}
					break;
				case 1: // latched on
					npc.Center = target.Center + new Vector2(0f, -10f);
					npc.velocity = target.velocity;
					if (npc.ai[0] >= 120f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 2f;
						npc.velocity = new Vector2(-target.direction * 8f, -6f);
					}
					break;
				default: // shaken off
					npc.velocity.Y += 0.3f;
					if (npc.ai[0] >= 60f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 0f;
					}
					break;
			}
			FaceVelocity(npc);
		}
	}

	// Shoots up high above the target, then homes in on it from above.
	public class Rocket : CustomAI
	{
		public override bool NoGravity => true;
		public override bool NoTileCollide => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.ai[0]++;
			switch ((int)npc.ai[1]) {
				case 0: // climbing
					npc.velocity = new Vector2(0f, -10f);
					if (npc.Center.Y < target.Center.Y - 450f || npc.ai[0] >= 90f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 1f;
					}
					break;
				case 1: // homing in
					Steer(npc, target.Center, 15f, 0.1f);
					if (npc.ai[0] >= 70f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 2f;
					}
					break;
				default: // coasting
					npc.velocity *= 0.95f;
					if (npc.ai[0] >= 30f) {
						npc.ai[0] = 0f;
						npc.ai[1] = 0f;
					}
					break;
			}
			npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
			if (!Main.dedServ && Main.rand.NextBool(2)) {
				Dust.NewDust(npc.Center, 0, 0, DustID.Smoke);
			}
		}
	}

	// Moves only in chess-knight L jumps, always the one that lands closest to the target.
	public class Knight : CustomAI
	{
		private static readonly Point[] Jumps = {
			new(1, 2), new(2, 1), new(2, -1), new(1, -2), new(-1, -2), new(-2, -1), new(-2, 1), new(-1, 2)
		};

		public override bool NoGravity => true;

		public override void Update(NPC npc) {
			Player target = Target(npc);
			npc.velocity *= 0.8f;
			Face(npc, DirectionTo(npc, target));
			if (IsServer && ++npc.ai[0] >= 45f) {
				npc.ai[0] = 0f;
				Vector2 best = npc.Center;
				float bestDistance = npc.Distance(target.Center);
				foreach (Point jump in Jumps) {
					Vector2 spot = npc.Center + jump.ToVector2() * 48f;
					float distance = Vector2.Distance(spot, target.Center);
					if (distance < bestDistance && !Collision.SolidCollision(spot - npc.Size / 2f, npc.width, npc.height)) {
						best = spot;
						bestDistance = distance;
					}
				}
				if (best != npc.Center) {
					TryTeleport(npc, best);
				}
			}
			ShowTeleports(npc);
		}
	}
}
