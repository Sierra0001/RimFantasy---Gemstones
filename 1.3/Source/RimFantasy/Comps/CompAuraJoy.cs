using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
    public class CompProperties_Aura_Joy : CompProperties
    {
        public float auraRadius;
        public float auraStrength;
        public bool indoorsOnly;
        public float minJoy;
        public float maxJoy;
        public CompProperties_Aura_Joy()
        {
            this.compClass = typeof(CompAuraJoy);
        }
    }
    public class CompAuraJoy : ThingComp, IAura
    {
        public static Dictionary<Map, HashSet<CompAuraJoy>> cachedComps = new Dictionary<Map, HashSet<CompAuraJoy>>();

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
                    cachedComps[this.parent.MapHeld] = new HashSet<CompAuraJoy> { this };
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
        public CompProperties_Aura_Joy Props => base.props as CompProperties_Aura_Joy;
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
            if (pawn.needs?.joy != null)
            {
                var pct = pawn.needs.joy.CurLevelPercentage;
                return pct >= Props.minJoy && pct <= Props.maxJoy;
            }
            return false;
        }
    }
}
