using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimFantasy
{
	public class CompProperties_Aura_Temperature : CompProperties
	{
		public float auraRadius;
		public float auraStrength;
		public AuraActiveLocation locationMode;
		public float? minTemperature;
		public float? maxTemperature;
		public bool dependsOnPower;
		public bool dependsOnFuel;
		public bool dependsOnGas;
		public bool flickable;
		public IntVec3 tileOffset = IntVec3.Invalid;

		public float smeltSnowRadius;
		public float smeltSnowAtTemperature;
		public float smeltSnowPower;
		public CompProperties_Aura_Temperature()
		{
			compClass = typeof(CompTemperatureSource);
		}
	}

	public class CompTemperatureSource : ThingComp
    {
		public CompProperties_Aura_Temperature Props => (CompProperties_Aura_Temperature)props;
		private bool active;
		private Map MapHeld => base.parent.MapHeld;
		public IntVec3 PositionHeld => base.parent.PositionHeld;

		private CompPowerTrader powerComp;
		private ThingComp gasComp;
		private CompRefuelable fuelComp;
		private CompFlickable compFlickable;
		private CompTempControl tempControlComp;
		public static MethodInfo methodInfoGasOn;
		public static Type gasCompType;
		private HashSet<IntVec3> affectedCells = new HashSet<IntVec3>();
		public HashSet<IntVec3> AffectedCells => affectedCells;
		private List<IntVec3> affectedCellsList = new List<IntVec3>();
		private AreaTemperatureManager manager;
		private IntVec3 prevPosition;
		public float TemperatureOutcome
        {
			get
            {
				return this.Props.auraStrength;
            }
        }

		public void SpawnSetup()
        {
			if (this.MapHeld != null)
            {
				if (Props.dependsOnPower)
				{
					powerComp = this.parent.GetComp<CompPowerTrader>();
				}
				if (Props.dependsOnFuel)
				{
					fuelComp = this.parent.GetComp<CompRefuelable>();
				}
				if (Props.dependsOnGas)
				{
					gasComp = GetGasComp();
				}
				if (Props.flickable)
				{
					compFlickable = this.parent.GetComp<CompFlickable>();
				}
				if (!Props.dependsOnFuel && !Props.dependsOnPower)
				{
					active = true;
				}

				tempControlComp = this.parent.GetComp<CompTempControl>();
				this.manager = this.MapHeld.GetComponent<AreaTemperatureManager>();
				if (Props.dependsOnPower || Props.dependsOnFuel || Props.dependsOnGas || Props.flickable || active)
				{
					this.manager.compTemperaturesToTick.Add(this);
				}
				this.MarkDirty();
			}
		}
		public override void PostSpawnSetup(bool respawningAfterLoad)
        {
			base.PostSpawnSetup(respawningAfterLoad);
			SpawnSetup();
		}

		
		private ThingComp GetGasComp()
        {
			foreach (var comp in this.parent.AllComps)
			{
				if (comp.GetType() == gasCompType)
				{
					return comp;
				}
			}
			return null;
		}

		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			base.PostDestroy(mode, previousMap);
			manager.RemoveComp(this);
			if (manager.compTemperaturesToTick.Contains(this))
			{
				manager.compTemperaturesToTick.Remove(this);
			}
		}
		public void MarkDirty()
        {
			this.manager.MarkDirty(this);
			this.dirty = false;
        }

		public bool CanWorkIn(IntVec3 cell)
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
        public void RecalculateAffectedCells()
        {
			affectedCells.Clear();
			affectedCellsList.Clear();
			manager.RemoveComp(this);
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
					var result = edifice == null || edifice.def.passability != Traversability.Impassable || edifice == ParentHeld;
					return result;
				};
		
				var offset = this.Props.tileOffset != IntVec3.Invalid ? ParentHeld.OccupiedRect().MovedBy(this.Props.tileOffset.RotatedBy(ParentHeld.Rotation)).CenterCell : PositionHeld;
				MapHeld.floodFiller.FloodFill(offset, validator, delegate (IntVec3 x)
				{
					if (tempCells.Contains(x))
					{
						var edifice = x.GetEdifice(MapHeld);
						var result = edifice == null || edifice.def.passability != Traversability.Impassable || edifice == ParentHeld;
						if (result && (GenSight.LineOfSight(offset, x, MapHeld) || offset.DistanceTo(x) <= 1.5f))
						{
							affectedCells.Add(x);
						}
					}
				}, int.MaxValue, rememberParents: false, (IEnumerable<IntVec3>)null);
				affectedCells.AddRange(ParentHeld.OccupiedRect().Where(x => CanWorkIn(x)));
				affectedCellsList.AddRange(affectedCells.ToList());
				foreach (var cell in affectedCells)
				{
					if (manager.temperatureSources.ContainsKey(cell))
					{
						manager.temperatureSources[cell].Add(this);
					}
					else
					{
						manager.temperatureSources[cell] = new List<CompTemperatureSource> { this };
					}
				}
				manager.compTemperatures.Add(this);
			}
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
        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
			if (this.TemperatureOutcome >= 0)
            {
				GenDraw.DrawFieldEdges(affectedCellsList, GenTemperature.ColorRoomHot);
            }
			else
            {
				GenDraw.DrawFieldEdges(affectedCellsList, GenTemperature.ColorRoomCold);
			}
		}
		
		public bool dirty = false;
		private void SetActive(bool value)
        {
			this.active = value;
			this.dirty = true;
        }

		public void Tick()
        {
			if (compFlickable != null)
            {
				if (!compFlickable.SwitchIsOn)
                {
					if (this.active)
					{
						SetActive(false);
						RecalculateAffectedCells();
						if (manager.compTemperatures.Contains(this))
                        {
							manager.RemoveComp(this);
                        }
					}
					return;
				}
			}
		
			if (Props.dependsOnFuel && Props.dependsOnPower)
            {
				if (powerComp != null && powerComp.PowerOn && fuelComp != null && fuelComp.HasFuel)
                {
					if (!this.active)
                    {
						this.SetActive(true);
					}
				}
				else if (this.active)
                {
					this.SetActive(false);
                }
            }
		
			else if (powerComp != null)
            {
				if (!powerComp.PowerOn && this.active)
                {
					this.SetActive(false);
				}
				else if (powerComp.PowerOn && !this.active)
				{
					this.SetActive(true);
				}
			}
		
			else if (fuelComp != null)
            {
				if (!fuelComp.HasFuel && this.active)
                {
					this.SetActive(false);
				}
				else if (fuelComp.HasFuel && !this.active)
				{
					this.SetActive(true);
				}
            }
			else if (gasComp != null)
            {
				if (!(bool)methodInfoGasOn.Invoke(gasComp, null) && this.active)
                {
					this.SetActive(false);
				}
				else if ((bool)methodInfoGasOn.Invoke(gasComp, null) && !this.active)
                {
					this.SetActive(true);
				}

			}

			if (active)
            {
				if (prevPosition != this.PositionHeld)
                {
					prevPosition = this.PositionHeld;
					dirty = true;
                }
            }
			if (dirty)
			{
				MarkDirty();
			}
			if (active)
            {
				if (MapHeld != null)
				{
					var cellToSmeltSnow = new HashSet<IntVec3>();
					if (Props.smeltSnowRadius > 0)
                    {
						foreach (var cell in ParentHeld.OccupiedRect())
						{
							foreach (var cell2 in GenRadial.RadialCellsAround(cell, Props.smeltSnowRadius, true))
							{
								if (cell2.GetSnowDepth(MapHeld) > 0 && HarmonyPatches.areaTemperatureManagers.TryGetValue(MapHeld, out AreaTemperatureManager proxyHeatManager))
								{
									var finalTemperature = proxyHeatManager.GetTemperatureOutcomeFor(cell2, cell2.GetTemperature(MapHeld));
									if (finalTemperature >= Props.smeltSnowAtTemperature)
									{
										cellToSmeltSnow.Add(cell2);
									}
								}
							}
						}
					}


					foreach (var cell in cellToSmeltSnow)
					{
						MapHeld.snowGrid.AddDepth(cell, -Props.smeltSnowPower);
					}
				}
			}
		}

		public bool InRangeAndActive(IntVec3 nearByCell)
		{
			if (this.active && this.PositionHeld.DistanceTo(nearByCell) <= Props.auraRadius)
			{
				return true;
			}
			return false;
		}
		public override void PostExposeData()
        {
            base.PostExposeData();
			Scribe_Values.Look(ref active, "active");
			SpawnSetup();
		}
	}
}
