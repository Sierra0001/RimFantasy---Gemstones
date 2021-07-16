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

		public virtual void DoEffect(TargetInfo target)
        {
			if (target.HasThing && !target.ThingDestroyed)
            {
				if (effectDamage != null)
                {
					var damageValue = baseDamageValue.HasValue ? baseDamageValue.Value : effectDamage.defaultDamage;
					target.Thing.TakeDamage(new DamageInfo(effectDamage, damageValue));
                }
				if (fleckDefOnTarget != null)
                {
					FleckMaker.Static(target.Cell, target.Map, fleckDefOnTarget, fleckScale);
                }
			}
        }
	}

	public class WeaponEffect_SetOnFire : WeaponSettings
    {
		public bool setsTargetOnFire;
		public FloatRange fireSize;
        public override void DoEffect(TargetInfo target)
        {
            base.DoEffect(target);
			if (setsTargetOnFire && target.HasThing && !target.ThingDestroyed)
            {
				target.Thing.TryAttachFire(fireSize.RandomInRange);
            }
        }
    }
	public class WeaponTraitDefCustom : WeaponTraitDef
	{
		public WeaponSettings workerSettings;
	}
	public class WeaponTraitWorkerCustom : WeaponTraitWorker
	{
		public new WeaponTraitDefCustom def => base.def as WeaponTraitDefCustom;
		public virtual void OnDamageDealt(TargetInfo target)
		{
			if (Rand.Chance(def.workerSettings.effectChance))
            {
				def.workerSettings.DoEffect(target);
            }
		}
	}
}
