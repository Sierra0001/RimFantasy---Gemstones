using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
    public class CompProperties_Aura_Rest : CompProperties
    {
        public float auraRadius;
        public float auraStrength;
        public bool indoorsOnly;
        public float minRest;
        public float maxRest;
        public CompProperties_Aura_Rest()
        {
            this.compClass = typeof(CompAuraRest);
        }
    }
    public class CompAuraRest : ThingComp, IAura
    {
        public static Dictionary<Map, HashSet<CompAuraRest>> cachedComps = new Dictionary<Map, HashSet<CompAuraRest>>();

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
                    cachedComps[this.parent.MapHeld] = new HashSet<CompAuraRest> { this };
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
        public CompProperties_Aura_Rest Props => base.props as CompProperties_Aura_Rest;
        public bool CanApplyOn(Pawn pawn)
        {
            if (pawn.Position.DistanceTo(this.parent.PositionHeld) > Props.auraRadius)
            {
                return false;
            }
            if (Props.indoorsOnly && pawn.Position.UsesOutdoorTemperature(pawn.Map))
            {
                return false;
            }
            if (pawn.needs?.rest != null)
            {
                var pct = pawn.needs.rest.CurLevelPercentage;
                return pct >= Props.minRest && pct <= Props.maxRest;
            }
            return false;
        }
    }
}
