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

		const double SpawnDistance         = 1000D;
		const double MinMoveDistance       = 2.25D;
		const double LowHPFactor           = 0.50D;
		const double SafeHPFactor          = 0.75D;
		const double MeleeDistance         = 150D;
		const double WaypointRadius        = 175D;
		const double StrafeCoeff           = 4D;
		const double EnemyDetectCoeff      = 1.50D;
		const double FriendDetectCoeff     = 0.5D;
		const double NeutralDetectCoeff    = 0.25D;
		const double BonusDetectCoeff      = 1.00D;
		const double SafeZoneBaseDistance  = 1250D;
		const double SafeZoneTowerDistance = 50D;
		const double GrindZoneDistance     = 800D;
		const double BehindDistance        = 150D;
		const double BaseDangerCoeff       = 0.25D;
		const double BaseDistance          = 750D;
		const int    StopGrindTime         = 5000;
		const double BonusTimeFactor       = 0.125D;
		const double StartTime             = 1000;
		const double EnemyBaseDistance     = 850; 

		Wizard self;
		World world;
		Game game;
		Move move;

		Random random;
		Dictionary<LaneType, Vector2[]> waypointsByLane;
		LaneType curLane;
		Vector2[] curWaypoints;
		List<Vector2> bonusPoints;
		List<Vector2> bonusWaypoints;
		bool selectedBonus;
		bool strafeDir;
		double strafeValue;

		Vector2 prevPos        = new Vector2(0, 0);
		double lastDistance    = 0D;
		bool spawned           = true;
		bool lowHp             = false;
		bool safeHp            = false;
		bool meleeDanger       = false;
		bool enemyOverwaight   = false;
		bool hasFriendsForward = false;
		bool onSafeZone        = false;
		bool onGrindZone       = false;
		bool baseDanger        = false;
		bool onBase            = false;
		bool canGrind          = false;
		bool hasBonus          = false;
		bool bonusTime         = false;
		bool enemyBaseDanger   = false;

		List<Minion>   nearMinions         = new List<Minion>();
		List<Wizard>   nearWizards         = new List<Wizard>();
		List<Building> nearBuildings       = new List<Building>();
		List<Wizard>   nearFriendWizards   = new List<Wizard>();
		List<Minion>   nearFriendMinions   = new List<Minion>();
		List<Building> nearFriendBuildings = new List<Building>();
		List<Minion>   nearNeutrals        = new List<Minion>();
		List<Bonus>    nearBonuses         = new List<Bonus>();
		List<Tree>     nearTrees           = new List<Tree>();

		string prevAction = "";
		string curAction  = "";
		bool   strafed    = false;

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
			switch ( random.Next(3) ) {
				case 0:
					curLane = LaneType.Top;
					break;
				case 1:
					curLane = LaneType.Bottom;
					break;
				case 2:
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
			strafed = true;
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
			move.Speed = game.WizardForwardSpeed;
		}

		Vector2 GetNextWaypoint(List<Vector2> waypoints) {
			int lastWaypointIndex = waypoints.Count - 1;
			Vector2 lastWaypoint = waypoints[lastWaypointIndex];

			for ( int waypointIndex = 0; waypointIndex < lastWaypointIndex; waypointIndex++ ) {
				Vector2 waypoint = waypoints[waypointIndex];

				if ( waypoint.GetDistanceTo(self) <= WaypointRadius ) {
					return waypoints[waypointIndex + 1];
				}

				if ( lastWaypoint.GetDistanceTo(waypoint) < lastWaypoint.GetDistanceTo(self) ) {
					return waypoint;
				}
			}

			return lastWaypoint;
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

		Vector2 GetNearestVector(List<Vector2> vectors) {
			var distance = int.MaxValue;
			Vector2 vector = null;
			for ( int i = 0; i < vectors.Count; i++ ) {
				var curVector = vectors[i];
				var curDistance = self.GetDistanceTo(curVector.X, curVector.Y);
				if( curDistance < distance ) {
					vector = curVector;
				}

			}
			return vector;
		}

		// Conditions
		void UpdateUnits() {
			var enemyDistance   = self.VisionRange * EnemyDetectCoeff;
			var friendDistance  = self.VisionRange * FriendDetectCoeff;
			var neutralDistance = self.VisionRange * NeutralDetectCoeff;
			var bonusDistance   = self.VisionRange * BonusDetectCoeff;
			SelectNearUnits(enemyDistance,   world.Buildings, EnemyFaction,   nearBuildings);
			SelectNearUnits(enemyDistance,   world.Minions,   EnemyFaction,   nearMinions);
			SelectNearUnits(enemyDistance,   world.Wizards,   EnemyFaction,   nearWizards);
			SelectNearUnits(friendDistance,  world.Wizards,   FriendFaction,  nearFriendWizards);
			SelectNearUnits(friendDistance,  world.Minions,   FriendFaction,  nearFriendMinions);
			SelectNearUnits(friendDistance,  world.Buildings, FriendFaction,  nearFriendBuildings);
			SelectNearUnits(neutralDistance, world.Minions,   NeutralFaction, nearNeutrals);
			SelectNearUnits(enemyDistance,   world.Trees,     OtherFaction,   nearTrees);
			SelectNearUnits(bonusDistance,   world.Bonuses,                   nearBonuses);
		}

		void UpdateConditions() {
			lowHp = self.Life < self.MaxLife * LowHPFactor;
			safeHp = self.Life > self.MaxLife * SafeHPFactor;
			meleeDanger = IsMeleeDanger();
			hasFriendsForward = IsFriendsForward();
			onSafeZone = IsSafeZone();
			onGrindZone = IsGrindZone();
			baseDanger = IsBaseDanger();
			onBase = IsOnBase();
			canGrind = world.TickIndex < StopGrindTime;
			hasBonus = HasBonus();
			bonusTime = IsBonusTime();
			enemyOverwaight = IsEnemyOverwaight();
			enemyBaseDanger = IsEnemyBaseDanger();
		}

		bool IsEnemyOverwaight() {
			if( hasBonus ) {
				return false;
			}
			var friendLife = GetUnitLifes(nearFriendWizards);
			var enemyLife = GetUnitLifes(nearWizards);
			return enemyLife > friendLife;
		}

		double GetUnitLifes<T>(List<T> units) where T:LivingUnit {
			var life = 0.0D;
			foreach ( var unit in units ) {
				life += (double)unit.Life / unit.MaxLife;
			}
			return life;
		}

		bool IsMeleeDanger() {			
			return HasMeleeDanger(nearMinions) || HasMeleeDanger(nearWizards) || HasMeleeDanger(nearNeutrals);
		}

		Building GetBase() {
			foreach( var building in world.Buildings ) {
				if( building.Faction != FriendFaction ) {
					continue;
				}
				if( building.Type == BuildingType.FactionBase ) {
					return building;
				}
			}
			return null;
		}

		bool IsFriendsForward() {
			if ( world.TickIndex > StartTime ) {
				return true;
			}
			var ownBase = GetBase(); 
			if( ownBase == null ) {
				return false;
			}
			var ownDistance = ownBase.GetDistanceTo(self);
			foreach(var unit in nearFriendMinions) {
				if( ownBase.GetDistanceTo(unit) - BehindDistance > ownDistance ) {
					return true;
				}
			}
			foreach ( var unit in nearFriendWizards ) {
				if ( ownBase.GetDistanceTo(unit) - BehindDistance > ownDistance ) {
					return true;
				}
			}
			return false;
		}

		bool IsSafeZone() {
			foreach( var building in world.Buildings ) {
				if( building.Faction == FriendFaction ) {
					var distance = self.GetDistanceTo(building);
					var maxDistance = 
						(building.Type == BuildingType.FactionBase) ? 
						SafeZoneBaseDistance : 
						SafeZoneTowerDistance;
					if ( distance < maxDistance ) {
						return true;
					}
				}
			}
			return false;
		}

		bool IsGrindZone() {
			foreach ( var building in world.Buildings ) {
				if ( building.Faction == FriendFaction ) {
					var distance = self.GetDistanceTo(building);
					var maxDistance = GrindZoneDistance;
					if ( distance < maxDistance ) {
						return true;
					}
				}
			}
			return false;
		}

		bool IsBaseDanger() {
			foreach( var building in nearFriendBuildings ) {
				if( building.Type == BuildingType.FactionBase ) {
					if( building.Life < building.MaxLife * BaseDangerCoeff ) {
						return true;
					}
				}
			}
			return false;
		}

		bool IsOnBase() {
			var ownBase = GetBase();
			if( ownBase != null ) {
				return self.GetDistanceTo(ownBase) < BaseDistance;
			}
			return false;
		}

		bool HasMeleeDanger<T>(List<T> units) where T:LivingUnit {
			var nearestUnit = GetNearestUnit(units);
			if( nearestUnit != null ) {
				return self.GetDistanceTo(nearestUnit) <= MeleeDistance + self.Radius;
			}
			return false;
		}

		bool HasBonus() {
			for( int i = 0; i < self.Statuses.Length; i++ ) {
				var status = self.Statuses[i];
				switch( status.Type ) {
					case StatusType.Empowered:
					case StatusType.Hastened:
					case StatusType.Shielded:
						return true;
				}
			}
			return false;
		}

		bool IsBonusTime() {
			return false;

			if( !this.bonusTime ) {
				selectedBonus = GetRandomBool();
			}
			if( hasBonus  ) {
				return false;
			}
			var bonusTime = game.BonusAppearanceIntervalTicks;
			var tickBase = world.TickIndex % bonusTime;
			return 
				((world.TickIndex > bonusTime) && (tickBase < bonusTime * BonusTimeFactor)) || 
				(tickBase > bonusTime * (1 - BonusTimeFactor));
		}

		bool IsEnemyBaseDanger() {
			for( int i = 0; i < world.Buildings.Length; i++ ) {
				var building = world.Buildings[i];
				if( (building.Type == BuildingType.FactionBase) && (building.Faction == EnemyFaction) ) {
					if( self.GetDistanceTo(building) < EnemyBaseDistance ) {
						return true;
					}
				}
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
			strafed = false;
			prevAction = curAction;
			curAction = "";
			if( baseDanger ) {
				if ( nearWizards.Count > 0 ) {
					AttackWizard();
				} else if ( nearMinions.Count > 0 ) {
					AttackMinion(true);
				} else {
					Wait();
				}
			} else if( lowHp && !onBase ) {
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
					if ( nearBonuses.Count > 0 ) {
						MoveToBonus();
					} else if ( bonusTime ) {
						GoToBonus();
					} else if ( nearWizards.Count > 0 ) {
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
						if ( nearMinions.Count > 0 ) {
							AttackMinion(false);
						} else if ( (nearBuildings.Count > 0) && !enemyBaseDanger ) {
							AttackBuilding();
						} else if ( (nearNeutrals.Count > 0) && onGrindZone && canGrind ) {
							AttackNeutral();
						} else {
							if( enemyBaseDanger ) {
								Retreat("Enemy Base");
							} else if ( (hasFriendsForward || onSafeZone) ) {
								Move();
							} else {
								Wait();
							}
						}
					}
				}
			}
			if( curAction == "" ) {
				SetAction("None");
			}
			if( !strafed ) {
				strafeValue = 0;
			}
		}

		void Wait() {
			SetAction("Wait");
		}

		void Move() {
			MoveTo(GetNextWaypoint());
			if( lastDistance < MinMoveDistance ) {
				SetStrafe();
				TryAttackTree();
				move.Speed = -game.WizardBackwardSpeed;
			}
			SetAction("Move forward");
		}

		void Retreat(string param) {
			MoveTo(GetPreviousWaypoint());
			SetStrafe();
			if ( lastDistance < MinMoveDistance ) {
				TryAttackTree();
				move.Speed = -game.WizardBackwardSpeed;
			}
			SetAction("Retreat by " + param);
		}

		void SetupBonusWaypoints(Vector2 point) {
			if( bonusWaypoints == null ) {
				bonusWaypoints = new List<Vector2>();
			} else {
				bonusWaypoints.Clear();
			}
			for(int i = 0; i < curWaypoints.Length/2 + 1; i++ ) {
				bonusWaypoints.Add(curWaypoints[i]);
			}
			bonusWaypoints.Add(point);
		}

		Vector2 GetNextBonusWaypoints() {
			return GetNextWaypoint(bonusWaypoints);
		}

		void GoToBonus() {
			var point = selectedBonus ? bonusPoints[0] : bonusPoints[1];
			SetupBonusWaypoints(point);
			MoveTo(GetNextBonusWaypoints());
			if ( lastDistance < MinMoveDistance ) {
				SetStrafe();
				TryAttackTree();
				move.Speed = -game.WizardBackwardSpeed;
			}
			SetAction("Go to bonus");
		}

		void MoveToBonus() {
			var bonus = GetNearestUnit(nearBonuses);
			MoveTo(new Vector2(bonus.X, bonus.Y));
			SetAction("Move to bonus");
		}

		void AttackWizard() {
			AttackLowestHpUnit(nearWizards, "Wizard");
		}

		void AttackMinion(bool nearest) {
			if ( nearest ) {
				AttackNearestUnit(nearMinions, "Minion", true);
			} else {
				AttackLowestHpUnit(nearMinions, "Minion");
			}
		}

		void AttackBuilding() {
			foreach( var building in nearBuildings ) {
				if( building.Type == BuildingType.FactionBase ) {
					AttackConcreteUnit(building, "Base", true);
					return;
				}
			}
			AttackNearestUnit(nearBuildings, "Building", true);
		}

		void AttackNeutral() {
			AttackNearestUnit(nearNeutrals, "Neutral", true);
		}

		void TryAttackTree() {
			var tree = GetNearestUnit(nearTrees);
			if ( tree != null ) {
				if ( self.GetDistanceTo(tree) < (self.Radius + tree.Radius) * 1.25D ) {
					AttackNearestUnit(nearTrees, "Tree", false);
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
				AttackNearestUnit(units, param, true);
			}
		}

		void AttackUnit(LivingUnit target) {
			double angle = self.GetAngleTo(target);
			move.Turn = angle;
			move.Action = ActionType.MagicMissile;
			move.CastAngle = angle;
			move.MinCastDistance = self.GetDistanceTo(target) - target.Radius + game.MagicMissileRadius;
		}

		void AttackNearestUnit<T>(List<T> units, string param, bool withMove) where T:LivingUnit {
			SetStrafe();
			var target = GetNearestUnit(units);
			if( target != null ) {
				AttackConcreteUnit(target, param, withMove);
			}
		}
		
		void AttackConcreteUnit<T>(T target, string param, bool withMove) where T:LivingUnit {
			var distance = self.GetDistanceTo(target);
			if ( distance < self.CastRange ) {
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
			if ( withMove ) {
				SetAction("Move to attack " + param);
				MoveTo(new Vector2(target.X, target.Y));
				if ( lastDistance < MinMoveDistance ) {
					SetStrafe();
					TryAttackTree();
					move.Speed = -game.WizardBackwardSpeed;
				}
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

		void SelectNearUnits<T>(double distance, T[] from, Faction faction, List<T> to) where T:LivingUnit {
			to.Clear();
			foreach( var unit in from ) {
				if ( unit.Faction == faction ) {
					if ( self.GetDistanceTo(unit) <= distance ) {
						to.Add(unit);
					}
				}
			}
		}

		void SelectNearUnits(double distance, Bonus[] from, List<Bonus> to) {
			to.Clear();
			foreach ( var unit in from ) {
				if ( self.GetDistanceTo(unit) <= distance ) {
					to.Add(unit);
				}
			}
		}
		T GetNearestUnit<T>(List<T> units) where T:CircularUnit {
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
				random = new Random(DateTime.Now.Millisecond);
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

				bonusPoints = new List<Vector2>();
				var offset = self.Radius * 1.5D;
				bonusPoints.Add(new Vector2(1200 - offset, 1200 - offset));
				bonusPoints.Add(new Vector2(2800 - offset, 2800 - offset));
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