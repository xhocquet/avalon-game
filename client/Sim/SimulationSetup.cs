using System.Collections.Generic;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Physics;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.ECS.Systems;

namespace Meesles.Avalon {
	public static class SimulationSetup {
		public static void RegisterSystems(EcsSimulation simulation) {
			var physics = new PhysicsSystem(64);
			physics.LoadStaticColliders("", new List<FPStaticCollider> { CreateGroundCollider() });
			simulation.AddSystem(physics, SystemPhase.Update);
			simulation.AddSystem(new MovementSystem(), SystemPhase.Update);
			simulation.AddSystem(new RespawnSystem(), SystemPhase.Update);
			simulation.AddSystem(new ScoreSystem(), SystemPhase.LateUpdate);
			simulation.AddSystem(new EventSystem(), SystemPhase.LateUpdate);
		}

		public static void InitializeWorld(IKlothoEngine engine, int maxPlayers) {
			var frame = engine.PredictedFrame.Frame;
			var stats = frame.AssetRegistry.Get<PlayerStatsAsset>();

			for (int playerId = 0; playerId < maxPlayers; playerId++) {
				var entity = frame.CreateEntity();
				FP64 offsetX = stats.InitialSpawnOffsetX * (playerId == 0 ? -1 : 1);
				FPVector3 initialPos = stats.SpawnPoint + new FPVector3(offsetX, FP64.Zero, FP64.Zero);
				FPVector3 halfExt = new FPVector3(stats.PlayerHalfExtent, stats.PlayerHalfExtent, stats.PlayerHalfExtent);

				frame.Add(entity, new TransformComponent {
					Position = initialPos,
					Rotation = FP64.Zero,
					Scale = FPVector3.One,
				});
				frame.Add(entity, new PhysicsBodyComponent {
					RigidBody = FPRigidBody.CreateDynamic(stats.PlayerMass),
					Collider = FPCollider.FromBox(new FPBoxShape(halfExt, FPVector3.Zero)),
					ColliderOffset = FPVector3.Zero,
				});
				frame.Add(entity, new OwnerComponent { OwnerId = playerId + 1 });
				frame.Add(entity, new PlayerComponent { PlayerId = playerId + 1 });
			}
		}

		private static FPStaticCollider CreateGroundCollider() {
			return new FPStaticCollider {
				id = -1,
				collider = FPCollider.FromBox(new FPBoxShape(
				new FPVector3(FP64.FromInt(5), FP64.FromFloat(0.1f), FP64.FromInt(5)),
				new FPVector3(FP64.Zero, FP64.FromFloat(-0.1f), FP64.Zero))),
			};
		}
	}
}
