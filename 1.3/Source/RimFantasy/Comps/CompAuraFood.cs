using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
    public interface IAura
    {
        void SpawnSetup();
    }
    public class CompProperties_Aura_Food : CompProperties
    {
        public float auraRadius;
        public float auraStrength;
        public AuraActiveLocation locationMode;
        public float minFood;
        public float maxFood;
        public CompProperties_Aura_Food()
        {
            this.compClass = typeof(CompAuraFood);
        }
    }
    public class CompAuraFood : ThingComp, IAura
    {
        public static Dictionary<Map, HashSet<CompAuraFood>> cachedComps = new Dictionary<Map, HashSet<CompAuraFood>>();

        public void SpawnSetup()
        {
            if (this.parent.MapHeld != null)
            {
                if (cachedComps.ContainsKey(this.parent.MapHeld))
                {
                    cachedComps[this.parent.MapHeld].Add(this);
                }
                else
                {
                    cachedComps[this.parent.MapHeld] = new HashSet<CompAuraFood> { this };
                }
            }
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            SpawnSetup();
            base.PostSpawnSetup(respawningAfterLoad);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            SpawnSetup();
        }
        public override void PostDeSpawn(Map map)
        {
            if (cachedComps.ContainsKey(map))
            {
                cachedComps[map].Remove(this);
            }
            base.PostDeSpawn(map);
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (cachedComps.ContainsKey(previousMap))
            {
                cachedComps[previousMap].Remove(this);
            }
            base.PostDestroy(mode, previousMap);
        }
        public CompProperties_Aura_Food Props => base.props as CompProperties_Aura_Food;
        public bool CanApplyOn(Pawn pawn)
        {
            if (pawn.Position.DistanceTo(this.parent.PositionHeld) > Props.auraRadius)
            {
                return false;
            }
            if (!CanWorkIn(pawn.Position, pawn.MapHeld))
            {
                return false;
            }
            if (pawn.needs?.food != null)
            {
                var pct = pawn.needs.food.CurLevelPercentage;
                return pct >= Props.minFood && pct <= Props.maxFood;
            }
            return false;
        }

        public bool CanWorkIn(IntVec3 cell, Map map)
        {
            bool isOutdoor = cell.UsesOutdoorTemperature(map);
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
    }
}
