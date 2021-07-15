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
	public class CompProperties_Aura_Temperature : CompProperties_Aura
	{
		public float? minTemperature;
		public float? maxTemperature;

		public bool dependsOnPower;
		public bool dependsOnFuel;
		public bool dependsOnGas;
		public bool flickable;

		public float smeltSnowRadius;
		public float smeltSnowAtTemperature;
		public float smeltSnowPower;
		public CompProperties_Aura_Temperature()
		{
			compClass = typeof(CompTemperatureSource);
		}
	}
	public class CompTemperatureSource : CompAura
	{
		public new CompProperties_Aura_Temperature Props => (CompProperties_Aura_Temperature)props;
		private CompPowerTrader powerComp;
		private ThingComp gasComp;
		private CompRefuelable fuelComp;
		private CompFlickable compFlickable;
		private CompTempControl tempControlComp;
		public static MethodInfo methodInfoGasOn;
		public static Type gasCompType;
		private IntVec3 prevPosition;
		public float TemperatureOutcome
        {
			get
            {
				return this.Props.auraStrength;
            }
        }

        public override void RecalculateAffectedCells()
        {
            base.RecalculateAffectedCells();
			foreach (var cell in affectedCells)
			{
				if (Manager.temperatureSources.ContainsKey(cell))
				{
					Manager.temperatureSources[cell].Add(this);
				}
				else
				{
					Manager.temperatureSources[cell] = new List<CompTemperatureSource> { this };
				}
			}
		}
        public override void SpawnSetup()
        {
			base.SpawnSetup();
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
				if (Props.dependsOnPower || Props.dependsOnFuel || Props.dependsOnGas || Props.flickable || active)
				{
					this.Manager.compAurasToTick.Add(this);
				}
			}
		}
		public override void PostSpawnSetup(bool respawningAfterLoad)
        {
			base.PostSpawnSetup(respawningAfterLoad);
			SpawnSetup();
		}

        public override void PostExposeData()
        {
            base.PostExposeData();
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
			if (Manager.compAurasToTick.Contains(this))
			{
				Manager.compAurasToTick.Remove(this);
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
		
		public override void Tick()
        {
			if (compFlickable != null)
            {
				if (!compFlickable.SwitchIsOn)
                {
					if (this.active && MapHeld != null)
					{
						SetActive(false);
						RecalculateAffectedCells();
						if (Manager.compAuras.Contains(this))
                        {
							this.UnConnectFromManager();
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
								if (cell2.GetSnowDepth(MapHeld) > 0 && HarmonyPatches.areaTemperatureManagers.TryGetValue(MapHeld, out AuraManager proxyHeatManager))
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

        public override void UnConnectFromManager()
        {
			base.UnConnectFromManager();
			foreach (var data in Manager.temperatureSources.Values)
			{
				if (data.Contains(this))
				{
					data.Remove(this);
				}
			}
		}
    }
}
