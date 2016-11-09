using System;
using System.Collections.Generic;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk {
	public class Vector2 {
		public double X;
		public double Y;

		public Vector2(double x, double y) {
			X = x;
			Y = y;
		}

		public double GetDistanceTo(double x, double y) {
			var cx = X - x;
			var cy = Y - y;
			return Math.Sqrt(cx * cx + cy * cy);
		}

		public double GetDistanceTo(Vector2 point) {
			return GetDistanceTo(point.X, point.Y);
		}

		public double GetDistanceTo(Unit unit) {
			return GetDistanceTo(unit.X, unit.Y);
		}
	}

	public sealed class MyStrategy : IStrategy {

		const double LowHPFactor = 0.25D;
		const double WaypointRadius = 100D;

		Wizard self;
		World world;
		Game game;
		Move move;

		Random random;
		Dictionary<LaneType, Vector2[]> waypointsByLane;
		LaneType curLane;
		Vector2[] curWaypoints;

		public void Move(Wizard self, World world, Game game, Move move) {
			SetupTick(self, world, game, move);
			TryInit(self, game);
			SetStrafe();
			if ( IsLowHP() ) {
				MoveTo(GetPreviousWaypoint());
			} else {
				if ( !TryAttack() ) {
					MoveTo(GetNextWaypoint());
				}
			}
		}

		void SetupTick(Wizard self, World world, Game game, Move move) {
			this.self = self;
			this.world = world;
			this.game = game;
			this.move = move;
		}

		bool IsLowHP() {
			return self.Life < self.MaxLife * LowHPFactor;
		}

		// Attack
		LivingUnit GetNearestTarget() {
			var targets = new List<LivingUnit>();
			targets.AddRange(world.Buildings);
			targets.AddRange(world.Wizards);
			targets.AddRange(world.Minions);

			LivingUnit nearestTarget = null;
			double nearestTargetDistance = double.MaxValue;

			foreach ( var target in targets ) {
				if ( target.Faction == Faction.Neutral || target.Faction == self.Faction ) {
					continue;
				}

				double distance = self.GetDistanceTo(target);

				if ( distance < nearestTargetDistance ) {
					nearestTarget = target;
					nearestTargetDistance = distance;
				}
			}

			return nearestTarget;
		}

		bool TryAttack() {
			LivingUnit nearestTarget = GetNearestTarget();

			if ( nearestTarget != null ) {
				double distance = self.GetDistanceTo(nearestTarget);

				if ( distance <= self.CastRange ) {
					double angle = self.GetAngleTo(nearestTarget);
					move.Turn = angle;
					if ( Math.Abs(angle) < game.StaffSector / 2.0D ) {
						move.Action = ActionType.MagicMissile;
						move.CastAngle = angle;
						move.MinCastDistance = distance - nearestTarget.Radius + game.MagicMissileRadius;
					}

					return true;
				}
			}
			return false;
		}

		// Move
		void SetStrafe() {
			var strafe = game.WizardStrafeSpeed;
			move.StrafeSpeed = GetRandomBool() ? strafe : -strafe;
		}

		void MoveTo(Vector2 point) {
			double angle = self.GetAngleTo(point.X, point.Y);

			move.Turn = angle;

			if ( Math.Abs(angle) < game.StaffSector / 4.0D ) {
				move.Speed = game.WizardForwardSpeed;
			}
		}

		Vector2 GetNextWaypoint() {
			int lastWaypointIndex = curWaypoints.Length - 1;
			Vector2 lastWaypoint = curWaypoints[lastWaypointIndex];

			for ( int waypointIndex = 0; waypointIndex < lastWaypointIndex; waypointIndex++ ) {
				Vector2 waypoint = curWaypoints[waypointIndex];

				if ( waypoint.GetDistanceTo(self) <= WaypointRadius ) {
					return curWaypoints[waypointIndex + 1];
				}

				if ( lastWaypoint.GetDistanceTo(waypoint) < lastWaypoint.GetDistanceTo(self) ) {
					return waypoint;
				}
			}

			return lastWaypoint;
		}

		Vector2 GetPreviousWaypoint() {
			Vector2 firstWaypoint = curWaypoints[0];

			for ( int waypointIndex = curWaypoints.Length - 1; waypointIndex > 0; --waypointIndex ) {
				Vector2 waypoint = curWaypoints[waypointIndex];

				if ( waypoint.GetDistanceTo(self) <= WaypointRadius ) {
					return curWaypoints[waypointIndex - 1];
				}

				if ( firstWaypoint.GetDistanceTo(waypoint) < firstWaypoint.GetDistanceTo(self) ) {
					return waypoint;
				}
			}

			return firstWaypoint;
		}

		// Init

		void TryInit(Wizard self, Game game) {
			if ( random == null ) {
				random = new Random(GetRandomSeed());
				double mapSize = game.MapSize;

				waypointsByLane = new Dictionary<LaneType, Vector2[]>();

				waypointsByLane.Add(LaneType.Middle, new Vector2[]{
					new Vector2(100.0D, mapSize - 100.0D),
					GetRandomBool()
							? new Vector2(600.0D, mapSize - 200.0D)
							: new Vector2(200.0D, mapSize - 600.0D),
					new Vector2(800.0D, mapSize - 800.0D),
					new Vector2(mapSize - 600.0D, 600.0D)
				});

				waypointsByLane.Add(LaneType.Top, new Vector2[]{
					new Vector2(100.0D, mapSize - 100.0D),
					new Vector2(100.0D, mapSize - 400.0D),
					new Vector2(200.0D, mapSize - 800.0D),
					new Vector2(200.0D, mapSize * 0.75D),
					new Vector2(200.0D, mapSize * 0.5D),
					new Vector2(200.0D, mapSize * 0.25D),
					new Vector2(200.0D, 200.0D),
					new Vector2(mapSize * 0.25D, 200.0D),
					new Vector2(mapSize * 0.5D, 200.0D),
					new Vector2(mapSize * 0.75D, 200.0D),
					new Vector2(mapSize - 200.0D, 200.0D)
				});

				waypointsByLane.Add(LaneType.Bottom, new Vector2[]{
					new Vector2(100.0D, mapSize - 100.0D),
					new Vector2(400.0D, mapSize - 100.0D),
					new Vector2(800.0D, mapSize - 200.0D),
					new Vector2(mapSize * 0.25D, mapSize - 200.0D),
					new Vector2(mapSize * 0.5D, mapSize - 200.0D),
					new Vector2(mapSize * 0.75D, mapSize - 200.0D),
					new Vector2(mapSize - 200.0D, mapSize - 200.0D),
					new Vector2(mapSize - 200.0D, mapSize * 0.75D),
					new Vector2(mapSize - 200.0D, mapSize * 0.5D),
					new Vector2(mapSize - 200.0D, mapSize * 0.25D),
					new Vector2(mapSize - 200.0D, 200.0D)
			});

				switch ( (int)self.Id ) {
					case 1:
					case 2:
					case 6:
					case 7:
						curLane = LaneType.Top;
						break;
					case 3:
					case 8:
						curLane = LaneType.Middle;
						break;
					case 4:
					case 5:
					case 9:
					case 10:
						curLane = LaneType.Bottom;
						break;
				}

				curWaypoints = waypointsByLane[curLane];

			}
		}

		// Ulility

		int GetRandomSeed() {
			var seed = game.RandomSeed;
			if ( seed > int.MaxValue ) {
				return int.MaxValue;
			} else if ( seed < int.MinValue ) {
				return int.MinValue;
			} else {
				return (int)seed;
			}
		}

		bool GetRandomBool() {
			return random.Next(2) >= 1;
		}
	}
}