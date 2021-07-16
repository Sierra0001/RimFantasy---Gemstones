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
    public class WeaponSettings
    {
        public float effectChance;
        public DamageDef effectDamage;
        public int? baseDamageValue;
        public FleckDef fleckDefOnTarget;
        public float fleckScale = 1;
        public virtual void DoEffect(LocalTargetInfo target)
        {
            if (target.HasThing && !target.ThingDestroyed)
            {
                if (fleckDefOnTarget != null)
                {
                    FleckMaker.Static(target.Cell, target.Thing.Map, fleckDefOnTarget, fleckScale);
                }
                if (effectDamage != null)
                {
                    var damageValue = baseDamageValue.HasValue ? baseDamageValue.Value : effectDamage.defaultDamage;
                    target.Thing.TakeDamage(new DamageInfo(effectDamage, damageValue));
                }
            }
        }
    }

    public class WeaponEffect_SetOnFire : WeaponSettings
    {
        public bool setsTargetOnFire;
        public FloatRange fireSize;
        public override void DoEffect(LocalTargetInfo target)
        {
            if (setsTargetOnFire && target.HasThing && !target.ThingDestroyed)
            {
                target.Thing.TryAttachFire(fireSize.RandomInRange);
            }
            base.DoEffect(target);
        }
    }
    public class ArcaneWeaponTraitDef : WeaponTraitDef
    {
        public WeaponSettings workerSettings;
        public new ArcaneWeaponTraitWorker Worker => base.Worker as ArcaneWeaponTraitWorker;
    }
    public class ArcaneWeaponTraitWorker : WeaponTraitWorker
    {
        public new ArcaneWeaponTraitDef def => base.def as ArcaneWeaponTraitDef;
        public virtual void OnDamageDealt(LocalTargetInfo target)
        {
            if (Rand.Chance(def.workerSettings.effectChance))
            {
                def.workerSettings.DoEffect(target);
            }
        }
    }
}