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
	[StaticConstructorOnStartup]
	internal static class HarmonyPatches
	{
		public static Dictionary<Map, AreaTemperatureManager> areaTemperatureManagers = new Dictionary<Map, AreaTemperatureManager>();
		static HarmonyPatches()
		{
			Harmony harmony = new Harmony("Sierra.RimFantasy");
			CompTemperatureSource.gasCompType = AccessTools.TypeByName("GasNetwork.CompGasTrader");
			if (CompTemperatureSource.gasCompType != null)
			{
				CompTemperatureSource.methodInfoGasOn = AccessTools.PropertyGetter(CompTemperatureSource.gasCompType, "GasOn");
			}
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(Need_Food), "NeedInterval")]
		internal static class Patch_FoodNeedInterval
		{
			private static void Postfix(Need_Food __instance, Pawn ___pawn)
			{
				if (___pawn.Map != null && CompAuraFood.cachedComps.TryGetValue(___pawn.Map, out var comps))
                {
					foreach (var comp in comps)
                    {
						if (comp.CanApplyOn(___pawn))
                        {
							Log.Message("Applying food to " + ___pawn + " - " + __instance.CurLevel);
							__instance.CurLevel += comp.Props.auraStrength;
							Traverse.Create(__instance).Field("lastNonStarvingTick").SetValue(Find.TickManager.TicksGame);
							Log.Message("2 Applying food to " + ___pawn + " - " + __instance.CurLevel);
						}
					}
                }
			}
		}

		[HarmonyPatch(typeof(Need_Rest), "NeedInterval")]
		internal static class Patch_RestNeedInterval
		{
			private static void Postfix(Need_Rest __instance, Pawn ___pawn)
			{
				if (___pawn.Map != null && CompAuraRest.cachedComps.TryGetValue(___pawn.Map, out var comps))
				{
					foreach (var comp in comps)
					{
						if (comp.CanApplyOn(___pawn))
						{
							Log.Message("Applying rest to " + ___pawn + " - " + __instance.CurLevel + " from " + comp);
							__instance.CurLevel += comp.Props.auraStrength;
							Traverse.Create(__instance).Field("lastRestTick").SetValue(Find.TickManager.TicksGame);
							Log.Message("2 Applying rest to " + ___pawn + " - " + __instance.CurLevel + " from " + comp);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Need_Joy), "NeedInterval")]
		public static class FallNeedInterval_Patch
		{
			private static void Postfix(Pawn ___pawn, Need_Joy __instance)
			{
				if (___pawn.Map != null && CompAuraJoy.cachedComps.TryGetValue(___pawn.Map, out var comps))
				{
					foreach (var comp in comps)
					{
						if (comp.CanApplyOn(___pawn))
						{
							Log.Message("Applying joy to " + ___pawn + " - " + __instance.CurLevel);
							__instance.CurLevel += comp.Props.auraStrength;
							Traverse.Create(__instance).Field("lastGainTick").SetValue(Find.TickManager.TicksGame);
							Log.Message("2 Applying joy to " + ___pawn + " - " + __instance.CurLevel);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(ThoughtHandler), "TotalMoodOffset")]
		public static class TotalMoodOffset_Patch
		{
			private static void Postfix(ThoughtHandler __instance, ref float __result)
			{
				if (__instance.pawn.Map != null && CompAuraMood.cachedComps.TryGetValue(__instance.pawn.Map, out var comps))
				{
					foreach (var comp in comps)
					{
						if (comp.CanApplyOn(__instance.pawn))
						{
							Log.Message("Applying mood to " + __instance.pawn + " - " + __result);
							__result += comp.Props.auraStrength;
							Log.Message("2 Applying mood to " + __instance.pawn + " - " + __result);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
		public static class Patch_Pawn_SpawnSetup
		{
			private static void Postfix(Pawn __instance)
			{
				if (__instance.equipment?.Primary != null && __instance.equipment.Primary.TryGetCachedComp<CompTemperatureSource>(out var compTemp))
                {
					compTemp.SpawnSetup();
                }
				foreach (var comp in __instance.AllComps)
                {
					if (comp is IAura aura)
                    {
						aura.SpawnSetup();
                    }
                }
				if (__instance.apparel?.WornApparel != null)
                {
					foreach (var thing in __instance.apparel.WornApparel)
					{
						foreach (var comp in thing.AllComps)
                        {
							if (comp is IAura aura)
							{
								aura.SpawnSetup();
							}
						}
					}
				}
				if (__instance.equipment?.AllEquipmentListForReading != null)
                {
					foreach (var thing in __instance.equipment.AllEquipmentListForReading)
					{
						foreach (var comp in thing.AllComps)
						{
							if (comp is IAura aura)
							{
								aura.SpawnSetup();
							}
						}
					}
				}
				if (__instance.inventory?.innerContainer != null)
				{
					foreach (var thing in __instance.inventory.innerContainer)
					{
						if (thing is ThingWithComps withComps)
                        {
							foreach (var comp in withComps.AllComps)
							{
								if (comp is IAura aura)
								{
									aura.SpawnSetup();
								}
							}
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Building), nameof(Building.SpawnSetup))]
		public static class Patch_SpawnSetup
		{
			private static void Postfix(Building __instance)
			{
				if (areaTemperatureManagers.TryGetValue(__instance.Map, out AreaTemperatureManager proxyHeatManager))
				{
					foreach (var comp in proxyHeatManager.compTemperatures)
					{
						if (comp.InRangeAndActive(__instance.Position))
						{
							proxyHeatManager.MarkDirty(comp);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(Building), nameof(Building.DeSpawn))]
		public static class Patch_DeSpawn
		{
			private static void Prefix(Building __instance)
			{
				if (areaTemperatureManagers.TryGetValue(__instance.Map, out AreaTemperatureManager proxyHeatManager))
				{
					foreach (var comp in proxyHeatManager.compTemperatures)
					{
						if (comp.InRangeAndActive(__instance.Position))
						{
							proxyHeatManager.MarkDirty(comp);
						}
					}
				}
			}
		}

		[HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
		public static class Patch_TemperatureString
		{
			private static string indoorsUnroofedStringCached;

			private static int indoorsUnroofedStringCachedRoofCount = -1;

			private static bool Prefix(ref string __result)
			{
				IntVec3 intVec = UI.MouseCell();
				IntVec3 c = intVec;
				Room room = intVec.GetRoom(Find.CurrentMap);
				if (room == null)
				{
					for (int i = 0; i < 9; i++)
					{
						IntVec3 intVec2 = intVec + GenAdj.AdjacentCellsAndInside[i];
						if (intVec2.InBounds(Find.CurrentMap))
						{
							Room room2 = intVec2.GetRoom(Find.CurrentMap);
							if (room2 != null && ((!room2.PsychologicallyOutdoors && !room2.UsesOutdoorTemperature) || (!room2.PsychologicallyOutdoors && (room == null || room.PsychologicallyOutdoors)) || (room2.PsychologicallyOutdoors && room == null)))
							{
								c = intVec2;
								room = room2;
							}
						}
					}
				}
				if (room == null && intVec.InBounds(Find.CurrentMap))
				{
					Building edifice = intVec.GetEdifice(Find.CurrentMap);
					if (edifice != null)
					{
						foreach (IntVec3 item in edifice.OccupiedRect().ExpandedBy(1).ClipInsideMap(Find.CurrentMap))
						{
							room = item.GetRoom(Find.CurrentMap);
							if (room != null && !room.PsychologicallyOutdoors)
							{
								c = item;
								break;
							}
						}
					}
				}
				string text;
				if (c.InBounds(Find.CurrentMap) && !c.Fogged(Find.CurrentMap) && room != null && !room.PsychologicallyOutdoors)
				{
					if (room.OpenRoofCount == 0)
					{
						text = "Indoors".Translate();
					}
					else
					{
						if (indoorsUnroofedStringCachedRoofCount != room.OpenRoofCount)
						{
							indoorsUnroofedStringCached = "IndoorsUnroofed".Translate() + " (" + room.OpenRoofCount.ToStringCached() + ")";
							indoorsUnroofedStringCachedRoofCount = room.OpenRoofCount;
						}
						text = indoorsUnroofedStringCached;
					}
				}
				else
				{
					text = "Outdoors".Translate();
				}
				var map = Find.CurrentMap;
				float num = 0f;
				if (room == null || c.Fogged(map))
				{
					num = GetOutDoorTemperature(Find.CurrentMap.mapTemperature.OutdoorTemp, map, c);
				}
				else
				{
					num = GetOutDoorTemperature(room.Temperature, map, c);
				}
				__result = text + " " + num.ToStringTemperature("F0");
				return false;
			}

			private static float GetOutDoorTemperature(float result, Map map, IntVec3 cell)
			{
				if (areaTemperatureManagers.TryGetValue(map, out AreaTemperatureManager proxyHeatManager))
				{
					return proxyHeatManager.GetTemperatureOutcomeFor(cell, result);
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(Thing), nameof(Thing.AmbientTemperature), MethodType.Getter)]
		public static class Patch_AmbientTemperature
		{
			private static void Postfix(Thing __instance, ref float __result)
			{
				var map = __instance.Map;
				if (map != null && areaTemperatureManagers.TryGetValue(map, out AreaTemperatureManager proxyHeatManager))
				{
					__result = proxyHeatManager.GetTemperatureOutcomeFor(__instance.Position, __result);
				}
			}
		}

		[HarmonyPatch(typeof(PlantUtility), nameof(PlantUtility.GrowthSeasonNow))]
		public static class Patch_GrowthSeasonNow
		{
			private static bool Prefix(ref bool __result, IntVec3 c, Map map, bool forSowing = false)
			{
				if (areaTemperatureManagers.TryGetValue(map, out AreaTemperatureManager proxyHeatManager))
				{
					var tempResult = proxyHeatManager.GetTemperatureOutcomeFor(c, 0f);
					if (tempResult != 0)
					{
						float temperature = c.GetTemperature(map) + tempResult;
						if (temperature > 0f)
						{
							__result = temperature < 58f;
						}
						else
						{
							__result = false;
						}
						return false;
					}
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(GenTemperature), "TryGetTemperatureForCell")]
		public static class Patch_TryGetTemperatureForCell
		{
			private static void Postfix(bool __result, IntVec3 c, Map map, ref float tempResult)
			{
				if (__result)
				{
					if (areaTemperatureManagers.TryGetValue(map, out AreaTemperatureManager proxyHeatManager))
					{
						tempResult = proxyHeatManager.GetTemperatureOutcomeFor(c, tempResult);
					}
				}
			}
		}



		[HarmonyPatch(typeof(PawnRenderer))]
		[HarmonyPatch("DrawEquipment")]
		public static class DrawEquipment_Patch
		{
			public const float drawSYSYPosition = 0.03904f;
			public static void Postfix(Pawn ___pawn, Vector3 rootLoc, Rot4 pawnRotation, PawnRenderFlags flags)
			{
				Pawn pawn = ___pawn;
				if (pawn.Dead || !pawn.Spawned || pawn.equipment == null || pawn.equipment.Primary == null || (pawn.CurJob != null && pawn.CurJob.def.neverShowWeapon))
				{
					return;
				}
				if (pawn.equipment.Primary.TryGetCachedComp<CompWornWeapon>(out var comp) && comp != null && comp.ShouldShowWeapon(___pawn, flags))
				{
					DrawWornWeapon(comp, ___pawn, rootLoc, comp.FullGraphic);
				}
			}

			public static void DrawSheath(Pawn pawn, Thing eq, Vector3 drawLoc, float aimAngle, Graphic graphic)
			{
				float num = aimAngle;
				num %= 360f;
				CompWornWeapon comp = eq.TryGetComp<CompWornWeapon>();
				if (comp != null)
				{
					Graphics.DrawMesh(graphic.MeshAt(pawn.Rotation), drawLoc, Quaternion.AngleAxis(num, Vector3.up), graphic.MatAt(pawn.Rotation), 0);
				}
			}
			public static void DrawWornWeapon(CompWornWeapon compSheath, Pawn pawn, Vector3 drawLoc, Graphic graphic)
			{
				switch (compSheath.Props.drawPosition)
				{
					case DrawPosition.Side:
						if (pawn.Rotation == Rot4.South)
						{
							drawLoc += compSheath.Props.northOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawSheath(pawn, pawn.equipment.Primary, drawLoc, compSheath.Props.northOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.North)
						{
							drawLoc += compSheath.Props.southOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawSheath(pawn, pawn.equipment.Primary, drawLoc, compSheath.Props.southOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.East)
						{
							drawLoc += compSheath.Props.eastOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawSheath(pawn, pawn.equipment.Primary, drawLoc, compSheath.Props.eastOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.West)
						{
							drawLoc += compSheath.Props.westOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawSheath(pawn, pawn.equipment.Primary, drawLoc, compSheath.Props.westOffset.angle, graphic);
							return;
						}
						break;
					case DrawPosition.Back:
						if (pawn.Rotation == Rot4.South)
						{
							drawLoc += compSheath.Props.southOffset.position;
							DrawSheath(pawn, pawn.equipment.Primary, drawLoc, compSheath.Props.southOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.North)
						{
							drawLoc += compSheath.Props.northOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawSheath(pawn, pawn.equipment.Primary, drawLoc, compSheath.Props.northOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.East)
						{
							drawLoc += compSheath.Props.eastOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawSheath(pawn, pawn.equipment.Primary, drawLoc, compSheath.Props.eastOffset.angle, graphic);
							return;
						}
						if (pawn.Rotation == Rot4.West)
						{
							drawLoc += compSheath.Props.westOffset.position;
							drawLoc.y += drawSYSYPosition;
							DrawSheath(pawn, pawn.equipment.Primary, drawLoc, compSheath.Props.westOffset.angle, graphic);
							return;
						}
						break;
					default:
						return;
				}
			}

		}
	}
}
