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
        public virtual void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            Log.Message(this + " doing weapon effect on " + target + ", attacker: " + attacker + ", comp: " + comp + " damageInfo: " + damageInfo);
            if (target.HasThing && !target.ThingDestroyed)
            {
                if (fleckDefOnTarget != null)
                {
                    FleckMaker.Static(target.Cell, target.Thing.Map, fleckDefOnTarget, fleckScale);
                }
                if (effectDamage != null)
                {
                    var damageValue = baseDamageValue.HasValue ? baseDamageValue.Value : effectDamage.defaultDamage;
                    target.Thing.TakeDamage(new DamageInfo(effectDamage, damageValue, instigator: attacker, weapon: comp.parent.def));
                }
            }
        }
    }

    public class WeaponEffect_SetOnFire : WeaponEffect
    {
        public bool setsTargetOnFire;
        public FloatRange fireSize;
        public override void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (setsTargetOnFire && target.HasThing && !target.ThingDestroyed)
            {
                target.Thing.TryAttachFire(fireSize.RandomInRange);
            }
            base.DoEffect(damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_ApplyHediff : WeaponEffect
    {
        public HediffDef hediffDef;
        public BodyPartDef partToApply;
        public float severityOffset = 1f;
        public override void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn victim)
            {
                if (victim.health.hediffSet.GetFirstHediffOfDef(hediffDef) is null)
                {
                    var part = partToApply != null ? victim.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def == partToApply) : null;
                    if (hediffDef.hediffClass.IsAssignableFrom(typeof(Hediff_Injury)))
                    {
                        part = victim.health.hediffSet.GetNotMissingParts().Where(x => x.depth == BodyPartDepth.Outside && x.coverage >= 0.1f).RandomElement();
                    }
                    var hediff = HediffMaker.MakeHediff(hediffDef, victim, part);
                    victim.health.AddHediff(hediff);
                }
                HealthUtility.AdjustSeverity(victim, hediffDef, severityOffset);
            }
            base.DoEffect(damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_Drain : WeaponEffect
    {
        public FloatRange hpToDrain;
        public DamageDef drainDamage;
        public override void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn victim)
            {
                var num = hpToDrain.RandomInRange;
                var partsToDrain = victim.health.hediffSet.GetNotMissingParts().Where(x => x.depth == BodyPartDepth.Outside).InRandomOrder();
                foreach (var part in partsToDrain)
                {
                    if (num <= 0)
                    {
                        break;
                    }
                    var diff = Math.Abs(num - victim.health.hediffSet.GetPartHealth(part));
                    var toDrain = num - diff;
                    num -= toDrain;
                    victim.TakeDamage(new DamageInfo(drainDamage, toDrain, hitPart: part, instigator: attacker, weapon: comp.parent.def));
                } 
            }
            base.DoEffect(damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_Heal : WeaponEffect
    {
        public FloatRange hpToHeal;
        public override void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
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
            base.DoEffect(damageInfo, comp, attacker, target);
        }
    }

    public class WeaponEffect_Stun : WeaponEffect
    {
        public IntRange stunDuration;
        public override void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing is Pawn victim)
            {
                victim.stances.stunner.StunFor(stunDuration.RandomInRange, attacker);
            }
            base.DoEffect(damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_Slash : WeaponEffect
    {
        public DamageDef damageDef;
        public IntRange amountOfEnemies;
        public float maxDistance;
        public float baseDamageFactor = 1f;
        public override void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.Thing != null)
            {
                var num = amountOfEnemies.RandomInRange;
                var damAmount = baseDamageValue.HasValue ? baseDamageValue.Value : damageInfo.Amount;
                var damDef = damageDef != null ? damageDef : damageInfo.Def;
                target.Thing.TakeDamage(new DamageInfo(damDef, damAmount, instigator: attacker, weapon: comp.parent.def));

                foreach (var thing in GenRadial.RadialDistinctThingsAround(attacker.Position, attacker.Map, maxDistance, true)
                    .OfType<Pawn>().Where(x => x.Faction == target.Thing.Faction && x != target.Thing).Take(num))
                {
                    thing.TakeDamage(new DamageInfo(damDef, damAmount * baseDamageFactor, instigator: attacker, weapon: comp.parent.def));
                }
            }
            base.DoEffect(damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_Slam : WeaponEffect
    {
        public DamageDef damageDef;
        public IntRange amountOfEnemies;
        public float maxDistance;
        public float knockbackDistance;
        public float knockbackDistanceSecondaryTargets;
        public float baseDamageFactor = 1f;
        public override void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (target.Thing != null)
            {
                var num = amountOfEnemies.RandomInRange;
                var damDef = damageDef != null ? damageDef : damageInfo.Def;
                var damAmount = baseDamageValue.HasValue ? baseDamageValue.Value : damageInfo.Amount;

                target.Thing.TakeDamage(new DamageInfo(damDef, damAmount, instigator: attacker, weapon: comp.parent.def));
                var cells = GenRadial.RadialCellsAround(target.Thing.Position, knockbackDistance, true).Where(x => x.DistanceTo(target.Thing.Position) >= knockbackDistance
                    && x.DistanceTo(target.Thing.Position) < x.DistanceTo(attacker.Position));
                var cell = cells.RandomElement();
                target.Thing.Position = cell;

                foreach (var thing in GenRadial.RadialDistinctThingsAround(attacker.Position, attacker.Map, maxDistance, true)
                    .OfType<Pawn>().Where(x => x.Faction == target.Thing.Faction && x != target.Thing).Take(num))
                {

                    thing.TakeDamage(new DamageInfo(damDef, damAmount * baseDamageFactor, instigator: attacker, weapon: comp.parent.def));
                    cells = GenRadial.RadialCellsAround(thing.Position, knockbackDistanceSecondaryTargets, true).Where(x => x.DistanceTo(thing.Position) >= knockbackDistanceSecondaryTargets
                        && x.DistanceTo(thing.Position) < x.DistanceTo(attacker.Position));
                    cell = cells.RandomElement();
                    thing.Position = cell;
                }
            }
            base.DoEffect(damageInfo, comp, attacker, target);
        }
    }
    public class WeaponEffect_Multiple : WeaponEffect
    {
        public List<WeaponEffect> weaponEffects;
        public override void DoEffect(DamageInfo damageInfo, CompArcaneWeapon comp, Thing attacker, LocalTargetInfo target)
        {
            if (weaponEffects != null && Rand.Chance(effectChance))
            {
                foreach (var weaponEffect in weaponEffects)
                {
                    weaponEffect.DoEffect(damageInfo, comp, attacker, target);
                }
            }
        }
    }
}