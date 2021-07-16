using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimFantasy
{
    public class ArcaneWeaponTraitDef : WeaponTraitDef
    {
        public WeaponEffect workerSettings;
        public new ArcaneWeaponTraitWorker Worker => base.Worker as ArcaneWeaponTraitWorker;
    }
    public class ArcaneWeaponTraitWorker : WeaponTraitWorker
    {
        public new ArcaneWeaponTraitDef def => base.def as ArcaneWeaponTraitDef;
        public virtual void OnDamageDealt(Thing attacker, LocalTargetInfo target)
        {
            if (Rand.Chance(def.workerSettings.effectChance))
            {
                def.workerSettings.DoEffect(attacker, target);
            }
        }
    }
}