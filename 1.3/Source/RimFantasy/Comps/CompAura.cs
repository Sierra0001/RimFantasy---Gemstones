using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimFantasy
{
	public enum AuraActiveLocation
	{
		Both,
		Indoors,
		Outdoors
	}
	public class CompProperties_Aura : CompProperties
    {
		public float auraRadius;
		public float auraStrength;
		public AuraActiveLocation locationMode;
		public bool workThroughWalls;
		public IntVec3 tileOffset = IntVec3.Invalid;
	}
	public abstract class CompAura : ThingComp
	{
		private AuraManager manager;
		protected AuraManager Manager
        {
            get
            {
				if (manager is null && this.MapHeld != null)
                {
					manager = this.MapHeld.GetComponent<AuraManager>();
				}
				return manager;
            }
        }
		protected bool active;

		protected HashSet<IntVec3> affectedCells = new HashSet<IntVec3>();
		public HashSet<IntVec3> AffectedCells => affectedCells;

		protected List<IntVec3> affectedCellsList = new List<IntVec3>();
		public CompProperties_Aura Props => (CompProperties_Aura)props;

		protected Map MapHeld => base.parent.MapHeld;
		protected IntVec3 PositionHeld => base.parent.PositionHeld;
		public bool dirty = false;

		public virtual void UnConnectFromManager()
        {
			if (Manager.compAuras.Contains(this))
			{
				Manager.compAuras.Remove(this);
			}
		}
		protected bool CanWorkIn(IntVec3 cell)
		{
			bool isOutdoor = cell.PsychologicallyOutdoors(MapHeld);
			if (Props.locationMode == AuraActiveLocation.Indoors && isOutdoor)
			{
				return false;
			}
			else if (Props.locationMode == AuraActiveLocation.Outdoors && !isOutdoor)
			{
				return false;
			}
			return true;
		}

		public ThingWithComps ParentHeld
		{
			get
			{
				if (this.parent.ParentHolder is Pawn_EquipmentTracker tracker)
				{
					return tracker.pawn;
				}
				else if (this.parent.ParentHolder is Pawn_ApparelTracker apparelTracker)
				{
					return apparelTracker.pawn;
				}
				else if (this.parent.ParentHolder is Pawn_InventoryTracker inventoryTracker)
				{
					return inventoryTracker.pawn;
				}
				return this.parent;
			}
		}
		public virtual void RecalculateAffectedCells()
		{
			affectedCells.Clear();
			affectedCellsList.Clear();
			this.UnConnectFromManager();
			if (this.active)
			{
				HashSet<IntVec3> tempCells = new HashSet<IntVec3>();
				foreach (var cell in GetCells())
				{
					foreach (var intVec in GenRadial.RadialCellsAround(cell, Props.auraRadius, true))
					{
						tempCells.Add(intVec);
					}
				}

				Predicate<IntVec3> validator = delegate (IntVec3 cell)
				{
					if (!tempCells.Contains(cell)) return false;
					var edifice = cell.GetEdifice(MapHeld);
					var result = CanWorkIn(cell) && (Props.workThroughWalls || edifice == null || edifice.def.passability != Traversability.Impassable || edifice == ParentHeld);
					return result;
				};

				var offset = this.Props.tileOffset != IntVec3.Invalid ? ParentHeld.OccupiedRect().MovedBy(this.Props.tileOffset.RotatedBy(ParentHeld.Rotation)).CenterCell : PositionHeld;
                MapHeld.floodFiller.FloodFill(offset, validator, (Action<IntVec3>)delegate (IntVec3 x)
				{
					if (tempCells.Contains(x))
					{
						var edifice = x.GetEdifice(MapHeld);
						var result = Props.workThroughWalls || edifice == null || edifice.def.passability != Traversability.Impassable || edifice == ParentHeld;
						if (result && (Props.workThroughWalls || (GenSight.LineOfSight(offset, x, MapHeld) || offset.DistanceTo(x) <= 1.5f)) && CanWorkIn(x))
						{
							this.affectedCells.Add(x);
						}
					}
				}, int.MaxValue, rememberParents: false, null);
				affectedCells.AddRange(ParentHeld.OccupiedRect().Where(x => CanWorkIn(x)));
				affectedCellsList.AddRange(affectedCells.ToList());
				Manager.compAuras.Add(this);
			}
			Log.Message("RecalculateAffectedCells" + this + " - " + affectedCells.Count + " - workThroughWalls: " + this.Props.workThroughWalls);
		}
		public IEnumerable<IntVec3> GetCells()
		{
			if (this.Props.tileOffset != IntVec3.Invalid)
			{
				return ParentHeld.OccupiedRect().MovedBy(this.Props.tileOffset.RotatedBy(ParentHeld.Rotation)).Cells.Where(x => CanWorkIn(x));
			}
			else
			{
				return ParentHeld.OccupiedRect().Cells.Where(x => CanWorkIn(x));
			}
		}

		protected void SetActive(bool value)
		{
			this.active = value;
			this.dirty = true;
		}

		public virtual void Tick()
        {

        }
		public bool InRangeAndActive(IntVec3 nearByCell)
		{
			if (this.active && this.PositionHeld.DistanceTo(nearByCell) <= Props.auraRadius)
			{
				return true;
			}
			return false;
		}

		public virtual bool CanApplyOn(Pawn pawn)
        {
			Log.Message("this.affectedCells: " + this.affectedCells.Count());
			if (!this.affectedCells.Contains(pawn.Position))
			{
				return false;
			}
			return true;
		}
		public void MarkDirty()
		{
			var map = this.MapHeld;
			if (this.Manager != null && map != null)
			{
				this.Manager.MarkDirty(this);
				this.dirty = false;
            }
		}
		public virtual void SpawnSetup()
        {
			MarkDirty();
			this.active = true;
		}
		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			SpawnSetup();
		}
		public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
			this.UnConnectFromManager();
		}
		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref active, "active" + this.GetType());
		}
	}
}
