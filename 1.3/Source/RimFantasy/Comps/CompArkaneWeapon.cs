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
		public int minWeaponTraits;
		public int maxWeaponTraits;
		public CompProperties_ArkaneWeapon()
		{
			compClass = typeof(CompArkaneWeapon);
		}
	}
	public class CompArkaneWeapon : CompBladelinkWeapon
	{
		public new CompProperties_ArkaneWeapon Props => base.props as CompProperties_ArkaneWeapon;
		public HashSet<Projectile> releasedProjectiles = new HashSet<Projectile>();
		private static HashSet<CompArkaneWeapon> compArkaneWeapons = new HashSet<CompArkaneWeapon>();
		public static CompArkaneWeapon GetLinkedCompFor(Projectile projectle)
        {
			foreach (var comp in compArkaneWeapons)
            {
				if (comp.releasedProjectiles.Contains(projectle))
                {
					return comp;
                }
            }
			return null;
        }

		public Pawn Wearer
        {
            get
            {
				if (this.parent.ParentHolder is Pawn_EquipmentTracker equipmentTracker)
                {
					return equipmentTracker.pawn;
                }
				return null;
            }
        }
		public CompArkaneWeapon()
        {
			compArkaneWeapons.Add(this);
		}
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
			int randomInRange = new IntRange(Props.minWeaponTraits, Props.maxWeaponTraits).RandomInRange;
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

        public override void PostExposeData()
        {
            base.PostExposeData();
			releasedProjectiles.RemoveWhere(x => x is null || x.Destroyed);
			Scribe_Collections.Look(ref releasedProjectiles, "releasedProjectiles", LookMode.Reference);
        }
    }
}
