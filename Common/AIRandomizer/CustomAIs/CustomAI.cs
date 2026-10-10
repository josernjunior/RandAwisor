using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RandAwisor.Common.AIRandomizer.CustomAIs
{
	// One of the mod's own behaviors, handed out by BehaviorPool next to the borrowed vanilla AIs.
	// A single instance serves every NPC, so all per-NPC state lives in npc.ai (synced) and npc.localAI (not synced),
	// which BehaviorSwapGlobalNPC clears when the behavior is assigned.
	// Randomness, teleports and projectiles happen only on the server, which then sets npc.netUpdate.
	public abstract class CustomAI : ILoadable
	{
		// Must match a bool field of AdditionalAIToggles.
		public string Name => GetType().Name;

		public virtual bool NoGravity => false;
		public virtual bool NoTileCollide => false;

		public abstract void Update(NPC npc);

		void ILoadable.Load(Mod mod) => BehaviorPool.RegisterCustomAI(this);

		void ILoadable.Unload() { }

		protected static bool IsServer => Main.netMode != NetmodeID.MultiplayerClient;

		protected static Player Target(NPC npc) {
			npc.TargetClosest(false);
			return Main.player[npc.target];
		}

		protected static int DirectionTo(NPC npc, Player player) => npc.Center.X < player.Center.X ? 1 : -1;

		protected static bool OnGround(NPC npc) => npc.velocity.Y == 0f;

		protected static void Face(NPC npc, int direction) {
			if (direction != 0) {
				npc.direction = npc.spriteDirection = direction;
			}
		}

		protected static void FaceVelocity(NPC npc) => Face(npc, Math.Sign(npc.velocity.X));

		// Accelerates toward direction * maxSpeed (direction 0 brakes) and walks up one-tile steps.
		protected static void Walk(NPC npc, int direction, float maxSpeed, float acceleration, bool stepUp = true) {
			float goal = direction * maxSpeed;
			if (npc.velocity.X < goal) {
				npc.velocity.X = Math.Min(npc.velocity.X + acceleration, goal);
			}
			else if (npc.velocity.X > goal) {
				npc.velocity.X = Math.Max(npc.velocity.X - acceleration, goal);
			}
			if (stepUp) {
				Collision.StepUp(ref npc.position, ref npc.velocity, npc.width, npc.height, ref npc.stepSpeed, ref npc.gfxOffY);
			}
		}

		protected static void JumpIfBlocked(NPC npc, float jumpSpeed) {
			if (npc.collideX && OnGround(npc)) {
				npc.velocity.Y = -jumpSpeed;
			}
		}

		// Blends velocity toward the one that reaches point, capped at maxSpeed. A lower blend means more inertia.
		protected static void Steer(NPC npc, Vector2 point, float maxSpeed, float blend = 0.2f) {
			Vector2 desired = point - npc.Center;
			if (desired.Length() > maxSpeed) {
				desired = Vector2.Normalize(desired) * maxSpeed;
			}
			npc.velocity = Vector2.Lerp(npc.velocity, desired, blend);
		}

		protected static void LimitSpeed(NPC npc, float maxSpeed) {
			if (npc.velocity.Length() > maxSpeed) {
				npc.velocity = Vector2.Normalize(npc.velocity) * maxSpeed;
			}
		}

		// Spawn point, kept in ai[2]/ai[3] by behaviors that stay around it. (0, 0) means not recorded yet.
		protected static Vector2 Home(NPC npc) {
			if (npc.ai[2] == 0f && npc.ai[3] == 0f) {
				npc.ai[2] = npc.Center.X;
				npc.ai[3] = npc.Center.Y;
			}
			return new Vector2(npc.ai[2], npc.ai[3]);
		}

		// Server only. Fails when the spot is inside tiles.
		protected static bool TryTeleport(NPC npc, Vector2 center) {
			Vector2 position = center - npc.Size / 2f;
			if (Collision.SolidCollision(position, npc.width, npc.height)) {
				return false;
			}
			npc.position = position;
			npc.velocity = Vector2.Zero;
			npc.netUpdate = true;
			return true;
		}

		// Teleports run on the server, so every machine spots the jump itself and draws the effect.
		// Uses localAI[2]/localAI[3]; behaviors that teleport must leave those alone.
		protected static void ShowTeleports(NPC npc) {
			var last = new Vector2(npc.localAI[2], npc.localAI[3]);
			if (last != Vector2.Zero && !Main.dedServ && Vector2.Distance(last, npc.Center) > 64f) {
				for (int i = 0; i < 15; i++) {
					Dust.NewDust(npc.position, npc.width, npc.height, DustID.MagicMirror);
				}
			}
			npc.localAI[2] = npc.Center.X;
			npc.localAI[3] = npc.Center.Y;
		}

		// Town NPCs and critters keep their movement but never shoot.
		protected static bool CanAttack(NPC npc) => !npc.friendly && npc.damage > 0;

		protected static Vector2 AimAt(NPC npc, Player player, float speed) => npc.DirectionTo(player.Center) * speed;

		protected static void Shoot(NPC npc, Vector2 velocity, int projectileType) {
			if (!IsServer || !CanAttack(npc)) {
				return;
			}
			int damage = Math.Max(1, npc.damage / 4);
			Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity, projectileType, damage, 1f);
		}
	}
}
