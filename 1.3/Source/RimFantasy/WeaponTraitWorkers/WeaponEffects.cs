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
    public class WeaponEffect
    {
        public float effectChance;
        public DamageDef effectDamage;
        public int? baseDamageValue;
        public FleckDef fleckDefOnTarget;
        public float fleckScale = 1;
        public virtual void DoEffect(Thing attacker, LocalTargetInfo target)
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

    public class WeaponEffect_SetOnFire : WeaponEffect
    {
        public bool setsTargetOnFire;
        public FloatRange fireSize;
        public override void DoEffect(Thing attacker, LocalTargetInfo target)
        {
            if (setsTargetOnFire && target.HasThing && !target.ThingDestroyed)
            {
                target.Thing.TryAttachFire(fireSize.RandomInRange);
            }
            base.DoEffect(attacker, target);
        }
    }

    public class WeaponEffect_Drain : WeaponEffect
    {
        public FloatRange hpToDrain;
        public override void DoEffect(Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn victim)
            {

            }
            base.DoEffect(attacker, target);
        }
    }
    public class WeaponEffect_Heal : WeaponEffect
    {
        public FloatRange hpToHeal;
        public override void DoEffect(Thing attacker, LocalTargetInfo target)
        {
            if (attacker is Pawn pawn && pawn.health?.hediffSet != null)
            {
                var num = hpToHeal.RandomInRange;
                var hediffSet = pawn.health.hediffSet;
                var hediffs = hediffSet.GetHediffs<Hediff_Injury>().Where(x => x.CanHealNaturally() || x.CanHealFromTending()).InRandomOrder();
                foreach (var hediff in hediffs)
                {
                    if (num <= 0)
                    {
                        break;
                    }
                    var diff = Math.Abs(num - hediff.Severity);
                    var toHeal = num - diff;
                    num -= toHeal;
                    hediff.Heal(toHeal);
                }
            }
            base.DoEffect(attacker, target);
        }
    }

}