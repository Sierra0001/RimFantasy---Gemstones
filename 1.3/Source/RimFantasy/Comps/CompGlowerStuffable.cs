using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimFantasy
{
	public class CompProperties_GlowerStuffable : CompProperties
	{
		public float overlightRadius;
		public float glowRadius = 14f;
		public ColorInt glowColor = new ColorInt(255, 255, 255, 0) * 1.45f;
		public bool stuffGlow;
		public bool? glowWhileStockpiled;
		public float? glowRadiusStockpiled;
		public bool? glowWhileEquipped;
		public float? glowRadiusEquipped;
		public bool? glowWhileDrawn;
		public float? glowRadiusDrawn;
		public CompProperties_GlowerStuffable()
		{
			compClass = typeof(CompGlowerStuffable);
		}
	}
	public class CompGlowerStuffable : ThingComp
    {
        public CompGlower compGlower;
        private bool dirty;
        public CompProperties_GlowerStuffable Props => (CompProperties_GlowerStuffable)props;
        private CompPowerTrader compPower;
        private Map map;
        private IntVec3 prevPosition;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            this.dirty = true;
            this.compPower = this.parent.GetComp<CompPowerTrader>();
            this.map = this.parent.MapHeld;
            RimFantasyManager.Instance.compGlowerToTick.Add(this);
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDeSpawn(map);
            if (RimFantasyManager.Instance.compGlowerToTick.Contains(this))
            {
                RimFantasyManager.Instance.compGlowerToTick.Remove(this);
            }
            base.PostDestroy(mode, previousMap);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            RimFantasyManager.Instance.compGlowerToTick.Add(this);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            RimFantasyManager.Instance.compGlowerToTick.Add(this);
        }
        public void Tick()
        {
            if (this.parent.MapHeld != null)
            {
                if (prevPosition != this.parent.PositionHeld)
                {
                    prevPosition = this.parent.PositionHeld;
                    dirty = true;
                }
                if (compGlower is null && ShouldGlow())
                {
                    dirty = true;
                }
                if (dirty)
                {
                    if (compPower == null || compPower.PowerOn)
                    {
                        this.UpdateGlower();
                    }
                    dirty = false;
                }
                if (compPower != null)
                {
                    if (compPower.PowerOn && this.compGlower == null)
                    {
                        this.UpdateGlower();
                    }
                    else if (!compPower.PowerOn && this.compGlower != null)
                    {
                        this.RemoveGlower();
                    }
                }
            }

        }
        public void RemoveGlower()
        {
            if (this.compGlower != null)
            {
                base.parent.MapHeld.glowGrid.DeRegisterGlower(this.compGlower);
                this.compGlower = null;
            }
        }
        public void UpdateGlower()
        {
            RemoveGlower();
            if (ShouldGlow())
            {
                this.compGlower = new CompGlower();
                var parent = GetParent();
                var glow = this.parent.Stuff != null ? new ColorInt(this.parent.DrawColor) : this.Props.glowColor;
                var radius = GetRadius();
                var position = GetPosition();
                this.compGlower.Initialize(new CompProperties_Glower()
                {
                    glowColor = glow,
                    glowRadius = radius,
                    overlightRadius = Props.overlightRadius
                });
                this.compGlower.parent = parent;
                base.parent.MapHeld.mapDrawer.MapMeshDirty(position, MapMeshFlag.Things);
                base.parent.MapHeld.glowGrid.RegisterGlower(this.compGlower);
            }
        }
        public Pawn Wearer
        {
            get
            {
                if (this.parent.ParentHolder is Pawn_EquipmentTracker tracker)
                {
                    return tracker.pawn;
                }
                else if (this.parent.ParentHolder is Pawn_ApparelTracker apparelTracker)
                {
                    return apparelTracker.pawn;
                }
                else if (this.parent.ParentHolder is Pawn_InventoryTracker inventoryTracker)
                {
                    return inventoryTracker.pawn;
                }
                return null;
            }
        }
        private bool ShouldGlow()
        {
            bool shouldGlow = false;
            if (Props.glowWhileStockpiled.HasValue && InStockpile)
            {
                shouldGlow = Props.glowWhileStockpiled.Value;
            }
            if (Wearer != null && Props.glowWhileDrawn.HasValue)
            {
                if (Props.glowWhileDrawn.Value)
                {
                    if (Wearer.IsCarryingWeaponOpenly())
                    {
                        shouldGlow = true;
                    }
                    else if (Wearer.equipment.Primary.TryGetCachedComp<CompWornWeapon>(out var comp) && comp.ShouldShowWeapon(Wearer))
                    {
                        shouldGlow = true;
                    }
                }
            }
            if (Wearer != null && Props.glowWhileEquipped.HasValue && Wearer.equipment.Primary == this.parent && Props.glowWhileEquipped.Value)
            {
                shouldGlow = true;
            }
            return shouldGlow;
        }
        private float GetRadius()
        {
            var radius = 0f;
            if (Props.glowRadiusStockpiled.HasValue && InStockpile)
            {
                radius = Props.glowRadiusStockpiled.Value;
            }
            if (Wearer != null && Props.glowRadiusDrawn.HasValue)
            {
                if (Wearer.IsCarryingWeaponOpenly())
                {
                    radius = Props.glowRadiusDrawn.Value;
                }
                else if (Wearer.equipment.Primary.TryGetCachedComp<CompWornWeapon>(out var comp) && comp.ShouldShowWeapon(Wearer))
                {
                    radius = Props.glowRadiusDrawn.Value;
                }
            }
            if (Wearer != null && Props.glowWhileEquipped.HasValue && Wearer.equipment.Primary == this.parent)
            {
                radius = Props.glowRadiusEquipped.Value;
            }
            if (radius == 0f)
            {
                return Props.glowRadius;
            }
            return radius;
        }

        private IntVec3 GetPosition()
        {
            if (Wearer != null)
            {
                return Wearer.Position;
            }
            return this.parent.PositionHeld;
        }

        private ThingWithComps GetParent()
        {
            if (Wearer != null)
            {
                return Wearer;
            }
            return this.parent;
        }
        private bool InStockpile => (this.parent.PositionHeld.GetZone(this.parent.MapHeld) is Zone_Stockpile || this.parent.IsInAnyStorage());
    }
}
