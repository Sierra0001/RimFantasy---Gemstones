using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using System.Reflection;
using Verse;
using UnityEngine;
using HarmonyLib;
using Verse.AI.Group;

namespace RimFantasy
{
	public class CompProperties_ArkaneWeapon : CompProperties_Biocodable
	{
		public List<WeaponTraitDef> weaponTraitsPool;
		public CompProperties_ArkaneWeapon()
		{
			compClass = typeof(CompArkaneWeapon);
		}
	}
	public class CompArkaneWeapon : CompBladelinkWeapon
	{
		public new CompProperties_ArkaneWeapon Props => base.props as CompProperties_ArkaneWeapon;
		private static readonly IntRange TraitsRange = new IntRange(1, 2);
		public override void PostPostMake()
		{
			InitializeTraitsCustom();
		}
		public void InitializeTraitsCustom()
        {
			List<WeaponTraitDef> traits = Traverse.Create(this).Field("traits").GetValue<List<WeaponTraitDef>>();
            IEnumerable<WeaponTraitDef> allDefs = Props.weaponTraitsPool;
			if (traits == null)
			{
				traits = new List<WeaponTraitDef>();
			}
			Rand.PushState(parent.HashOffset());
			int randomInRange = TraitsRange.RandomInRange;
			for (int i = 0; i < randomInRange; i++)
			{
				IEnumerable<WeaponTraitDef> source = allDefs.Where((WeaponTraitDef x) => CanAddTrait(x, traits));
				if (source.Any())
				{
					traits.Add(source.RandomElementByWeight((WeaponTraitDef x) => x.commonality));
				}
			}
			Rand.PopState();
		}
		private bool CanAddTrait(WeaponTraitDef trait, List<WeaponTraitDef> traits)
		{
			if (!traits.NullOrEmpty())
			{
				for (int i = 0; i < traits.Count; i++)
				{
					if (trait.Overlaps(traits[i]))
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}
