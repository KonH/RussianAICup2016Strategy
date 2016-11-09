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

		const double SpawnDistance = 1000D;
		const double MinMoveDistance = 2.25D;
		const double LowHPFactor = 0.50D;
		const double SafeHPFactor = 0.85D;
		const double MeleeDistance = 175D;
		const double WaypointRadius = 100D;
		const double StrafeCoeff = 4D;

		Wizard self;
		World world;
		Game game;
		Move move;

		Random random;
		Dictionary<LaneType, Vector2[]> waypointsByLane;
		LaneType curLane;
		Vector2[] curWaypoints;
		bool strafeDir = false;
		double strafeValue = 0;

		double detectDistance;
		Vector2 prevPos = new Vector2(0, 0);
		double lastDistance = 0D;
		bool spawned = true;
		bool lowHp = false;
		bool safeHp = false;
		bool meleeDanger = false;
		bool enemyOverwaight = false;
		List<Minion> nearMinions = new List<Minion>();
		List<Wizard> nearWizards = new List<Wizard>();
		List<Building> nearBuildings = new List<Building>();
		List<Wizard> nearFriends = new List<Wizard>();
		List<Minion> nearNeutrals = new List<Minion>();
		List<Tree> nearTrees = new List<Tree>();
		string prevAction = "";
		string curAction = "";

		public void Move(Wizard self, World world, Game game, Move move) {
			SetupTick(self, world, game, move);
			TryInit(self, game);
			CheckSpawn();
			UpdateUnits();
			UpdateConditions();
			MakeAction();
		}

		void SetupTick(Wizard self, World world, Game game, Move move) {
			this.self = self;
			this.world = world;
			this.game = game;
			this.move = move;
		}

		// Move
		void CheckSpawn() {
			lastDistance = self.GetDistanceTo(prevPos.X, prevPos.Y);
			spawned = lastDistance > SpawnDistance;
			prevPos = new Vector2(self.X, self.Y);
			if ( spawned ) {
				SelectLane();
			}
		}

		void SelectLane() {
			switch ( random.Next(4) ) {
				case 0:
					curLane = LaneType.Top;
					break;
				case 1:
					curLane = LaneType.Bottom;
					break;
				case 2:
				case 3:
					curLane = LaneType.Middle;
					break;
			}

			curWaypoints = waypointsByLane[curLane];
			Console.WriteLine("Select lane: " + curLane);
		}

		bool NeedChangeStrafeDir() {
			return (strafeValue > self.Radius * StrafeCoeff);
		}

		void SetStrafe() {
			var strafe = game.WizardStrafeSpeed;
			strafeValue += strafe;
			if( NeedChangeStrafeDir() ) {
				strafeDir = !strafeDir;
				strafeValue = 0;
			}
			move.StrafeSpeed = strafeDir ? strafe : -strafe;
		}

		void MoveTo(Vector2 point) {
			double angle = self.GetAngleTo(point.X, point.Y);

			move.Turn = angle;

			if ( Math.Abs(angle) < game.StaffSector / 4.0D ) {
				move.Speed = game.WizardForwardSpeed;
			} else {
				move.Speed = game.WizardBackwardSpeed;
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

		// Conditions
		void UpdateUnits() {
			detectDistance = self.VisionRange;
			SelectNearUnits(world.Buildings, EnemyFaction, nearBuildings);
			SelectNearUnits(world.Minions, EnemyFaction, nearMinions);
			SelectNearUnits(world.Wizards, EnemyFaction, nearWizards);
			SelectNearUnits(world.Wizards, FriendFaction, nearFriends);
			SelectNearUnits(world.Minions, NeutralFaction, nearNeutrals);
			SelectNearUnits(world.Trees, OtherFaction, nearTrees);
		}

		void UpdateConditions() {
			lowHp = self.Life < self.MaxLife * LowHPFactor;
			safeHp = self.Life > self.MaxLife * SafeHPFactor;
			meleeDanger = IsMeleeDanger();
			enemyOverwaight = nearWizards.Count > nearFriends.Count;
		}

		bool IsMeleeDanger() {			
			return HasMeleeDanger(nearMinions) || HasMeleeDanger(nearWizards) || HasMeleeDanger(nearNeutrals);
		}

		bool HasMeleeDanger<T>(List<T> units) where T:LivingUnit {
			var nearestUnit = GetNearestUnit(units);
			if( nearestUnit != null ) {
				return self.GetDistanceTo(nearestUnit) <= MeleeDistance + self.Radius;
			}
			return false;
		}

		// Actions
		void SetAction(string action) {
			curAction = action;
			if( action != prevAction ) {
				Console.WriteLine(string.Format("Action: {0}", action));
			}
		}

		void MakeAction() {
			prevAction = curAction;
			curAction = "";
			if( lowHp ) {
				Retreat("low HP");
			} else {
				if( meleeDanger ) {
					if ( nearWizards.Count > 0 ) {
						Retreat("melee danger");
					} else if ( safeHp ) {
						if( nearMinions.Count > 0 ) {
							AttackMinion(true);
						} else if ( nearNeutrals.Count > 0 ) {
							AttackNeutral();
						}
					} else {
						Retreat("melee danger");
					}
				}
				if ( curAction == "" ) {
					if ( nearWizards.Count > 0 ) {
						if ( enemyOverwaight ) {
							if ( safeHp ) {
								AttackWizard();
							} else {
								Retreat("enemy overwaight");
							}
						} else {
							AttackWizard();
						}
					} else {
						if ( nearBuildings.Count > 0 ) {
							AttackBuilding();
						} else if ( nearMinions.Count > 0 ) {
							AttackMinion(false);
						} else if ( nearNeutrals.Count > 0 ) {
							AttackNeutral();
						} else {
							Move();
						}
					}
				}
			}
			if( curAction == "" ) {
				SetAction("None");
			}
		}

		void Move() {
			MoveTo(GetNextWaypoint());
			if( lastDistance < MinMoveDistance ) {
				SetStrafe();
				TryAttackTree();
				MoveTo(GetPreviousWaypoint());
			}
			SetAction("Move forward");
		}

		void Retreat(string param) {
			MoveTo(GetPreviousWaypoint());
			if ( lastDistance < MinMoveDistance ) {
				SetStrafe();
				TryAttackTree();
				MoveTo(GetNextWaypoint());
			}
			SetAction("Retreat by " + param);
		}

		void AttackWizard() {
			AttackLowestHpUnit(nearWizards, "Wizard");
		}

		void AttackMinion(bool nearest) {
			if ( nearest ) {
				AttackNearestUnit(nearMinions, "Minion");
			} else {
				AttackLowestHpUnit(nearMinions, "Minion");
			}
		}

		void AttackBuilding() {
			AttackNearestUnit(nearBuildings, "Building");
		}

		void AttackNeutral() {
			AttackNearestUnit(nearNeutrals, "Neutral");
		}

		void TryAttackTree() {
			var tree = GetNearestUnit(nearTrees);
			if ( tree != null ) {
				if ( self.GetDistanceTo(tree) < (self.Radius + tree.Radius) * 1.25D ) {
					AttackNearestUnit(nearTrees, "Tree");
				}
			}
		}

		// Attack
		void AttackLowestHpUnit<T>(List<T> units, string param) where T:LivingUnit {
			var reachableUnits = new List<T>();
			foreach ( var unit in units ) {
				var distance = self.GetDistanceTo(unit);
				if ( distance < self.CastRange ) {
					double angle = self.GetAngleTo(unit);
					if ( Math.Abs(angle) < game.StaffSector / 2.0D ) {
						reachableUnits.Add(unit);
					}
				}
			}
			if( reachableUnits.Count > 0 ) {
				var target = GetLowestHPUnit(reachableUnits);
				SetStrafe();
				AttackUnit(target);
				SetAction("Attack lowest hp " + param);
			} else {
				AttackNearestUnit(units, param);
			}
		}

		void AttackUnit(LivingUnit target) {
			double angle = self.GetAngleTo(target);
			move.Turn = angle;
			move.Action = ActionType.MagicMissile;
			move.CastAngle = angle;
			move.MinCastDistance = self.GetDistanceTo(target) - target.Radius + game.MagicMissileRadius;
		}

		void AttackNearestUnit<T>(List<T> units, string param) where T:LivingUnit {
			SetStrafe();
			var target = GetNearestUnit(units);
			if( target != null ) {
				var distance = self.GetDistanceTo(target);
				if( distance < self.CastRange ) {
					double angle = self.GetAngleTo(target);
					move.Turn = angle;
					if ( Math.Abs(angle) < game.StaffSector / 2.0D ) {
						move.Action = ActionType.MagicMissile;
						move.CastAngle = angle;
						move.MinCastDistance = distance - target.Radius + game.MagicMissileRadius;
						SetAction("Attack " + param);
						return;
					}
				}
				MoveTo(new Vector2(target.X, target.Y));
				SetAction("Move to attack " + param);
			}
		}

		// Units
		Faction EnemyFaction {
			get {
				if( self.Faction == Faction.Academy ) {
					return Faction.Renegades;
				} else {
					return Faction.Academy;
				}
			}
		}

		Faction FriendFaction {
			get {
				if ( self.Faction == Faction.Renegades ) {
					return Faction.Renegades;
				} else {
					return Faction.Academy;
				}
			}
		}

		Faction NeutralFaction {
			get {
				return Faction.Neutral;
			}
		}

		Faction OtherFaction {
			get {
				return Faction.Other;
			}
		}

		void SelectNearUnits<T>(T[] from, Faction faction, List<T> to) where T:LivingUnit {
			to.Clear();
			foreach( var unit in from ) {
				if ( unit.Faction == faction ) {
					if ( self.GetDistanceTo(unit) <= detectDistance ) {
						to.Add(unit);
					}
				}
			}
		}

		T GetNearestUnit<T>(List<T> units) where T:LivingUnit {
			T nearestTarget = null;
			double nearestTargetDistance = double.MaxValue;

			foreach ( var target in units ) {
				double distance = self.GetDistanceTo(target);

				if ( distance < nearestTargetDistance ) {
					nearestTarget = target;
					nearestTargetDistance = distance;
				}
			}

			return nearestTarget;
		}

		T GetLowestHPUnit<T>(List<T> units) where T:LivingUnit {
			T lowestHpTarget = null;
			int lowestHp = int.MaxValue;

			foreach ( var target in units ) {
				int hp = target.Life;

				if ( hp < lowestHp ) {
					lowestHpTarget = target;
					lowestHp = hp;
				}
			}

			return lowestHpTarget;
		}

		// Init

		void TryInit(Wizard self, Game game) {
			if ( random == null ) {
				random = new Random(GetRandomSeed());
				double mapSize = game.MapSize;

				waypointsByLane = new Dictionary<LaneType, Vector2[]>();

				waypointsByLane.Add(LaneType.Middle, new Vector2[]{
					new Vector2(50.0D, mapSize - 50.0D),
					new Vector2(100.0D, mapSize - 100.0D),
					GetRandomBool()
							? new Vector2(600.0D, mapSize - 200.0D)
							: new Vector2(200.0D, mapSize - 600.0D),
					new Vector2(800.0D, mapSize - 800.0D),
					new Vector2(mapSize - 600.0D, 600.0D)
				});

				waypointsByLane.Add(LaneType.Top, new Vector2[]{
					new Vector2(50.0D, mapSize - 50.0D),
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
					new Vector2(50.0D, mapSize - 50.0D),
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