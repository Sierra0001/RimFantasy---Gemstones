using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
    public class CompProperties_Aura_Mood : CompProperties
    {
        public float auraRadius;
        public float auraStrength;
        public bool indoorsOnly;
        public float minMood;
        public float maxMood;
        public CompProperties_Aura_Mood()
        {
            this.compClass = typeof(CompAuraMood);
        }
    }
    public class CompAuraMood : ThingComp, IAura
    {
        public static Dictionary<Map, HashSet<CompAuraMood>> cachedComps = new Dictionary<Map, HashSet<CompAuraMood>>();
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
                    cachedComps[this.parent.MapHeld] = new HashSet<CompAuraMood> { this };
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
        public CompProperties_Aura_Mood Props => base.props as CompProperties_Aura_Mood;
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
            if (pawn.needs?.mood != null)
            {
                var pct = pawn.needs.mood.CurLevelPercentage;
                return pct >= Props.minMood && pct <= Props.maxMood;
            }
            return false;
        }
    }
}
