using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using BepInEx.Configuration;
using MonoMod.RuntimeDetour;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using RoR2;

using EntityStates.CaptainDefenseMatrixItem;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class DefensiveMicrobots
	{
		public static List<string> autoCompatList = new List<string> { "com.arimah.MicroDropBots" };

		public static string itemIdentifier = "DefensiveMicrobots";
		public static bool appliedChanges = false;

		private static readonly BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

		private delegate float MicrobotReturnFloat(DefenseMatrixOn self);
		private static MicrobotReturnFloat origRechargeFrequencyGetter;
		private static MicrobotReturnFloat origFireFrequencyGetter;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<int> BaseTarget { get; set; }
		public static ConfigEntry<int> StackTarget { get; set; }
		public static ConfigEntry<float> BaseRange { get; set; }
		public static ConfigEntry<float> StackRange { get; set; }
		public static ConfigEntry<float> BaseRecharge { get; set; }
		public static ConfigEntry<float> StackReduction { get; set; }
		public static ConfigEntry<bool> ScaleAtkSpd { get; set; }
		public static ConfigEntry<bool> Findable { get; set; }
		public static ConfigEntry<bool> Blacklist { get; set; }



		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
				OnItemCatalogPreInit += ModifyItem;
				OnLateSetup += LateSetup;
			}
		}

		private static void SetupConfig()
		{
			EnableChanges = ConfigEntry(
				itemIdentifier, "EnableChanges", 1,
				SectionEnableDesc
			);
			if (GenerateOverrideText.Value)
			{
				OverrideText = ConfigEntry(
					itemIdentifier, "OverrideText", true,
					"Replace item description text."
				);
			}
			BaseTarget = ConfigEntry(
				itemIdentifier, "BaseTarget", 1,
				"Maximum targets gained from item."
			);
			StackTarget = ConfigEntry(
				itemIdentifier, "StackTarget", 1,
				"Maximum targets gained from item per stack."
			);
			BaseRange = ConfigEntry(
				itemIdentifier, "BaseRange", 20f,
				"Protection range gained from item."
			);
			StackRange = ConfigEntry(
				itemIdentifier, "StackRange", 0f,
				"Protection range gained from item per stack."
			);
			BaseRecharge = ConfigEntry(
				itemIdentifier, "BaseRecharge", 0.5f,
				"Recharge interval for item."
			);
			StackReduction = ConfigEntry(
				itemIdentifier, "StackReduction", 0f,
				"Recharge interval reduction per stack."
			);
			ScaleAtkSpd = ConfigEntry(
				itemIdentifier, "ScaleAtkSpd", true,
				"Attack speed effects recharge interval."
			);
			Findable = ConfigEntry(
				itemIdentifier, "Findable", true,
				"Remove WorldUnique ItemTag from item. (add item to red drop pool)"
			);
			Blacklist = ConfigEntry(
				itemIdentifier, "Blacklist", false,
				"Add AIBlacklist ItemTag to item."
			);
		}

		private static void ModifyItem()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList, true)) return;

			LogInfo(itemIdentifier + " :: Proceed with ModifyItem.");

			bool modified = false;

			ItemDef itemDef = RoR2Content.Items.CaptainDefenseMatrix;

			if (Findable.Value)
			{
				if (itemDef.ContainsTag(ItemTag.WorldUnique))
				{
					List<ItemTag> itemTags = itemDef.tags.ToList();
					itemTags.Remove(ItemTag.WorldUnique);

					itemDef.tags = itemTags.ToArray();

					modified = true;
				}
			}

			if (Blacklist.Value)
			{
				if (itemDef.DoesNotContainTag(ItemTag.AIBlacklist))
				{
					List<ItemTag> itemTags = itemDef.tags.ToList();
					itemTags.Add(ItemTag.AIBlacklist);

					itemDef.tags = itemTags.ToArray();

					modified = true;
				}
			}

			if (modified)
			{
				ModifyCount++;
			}
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			RechargeFrequencyHook();
			FireFrequencyHook();
			ProjectileTargetingHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("MICROBOT_DEFENSE", "Shoot down {0} projectiles within {1}.");
				RegisterFragment("MICROBOT_RECHARGE", "\nRecharges {0}.");
				RegisterFragment("MICROBOT_RECHARGE_REDUCE", "\nRecharges {0} <style=cStack>(-{1} per stack)</style>.");
				RegisterFragment("MICROBOT_DETAIL", "\n<style=cStack>(Recharge rate scales with attack speed)</style>");
				RegisterToken("ITEM_CAPTAINDEFENSEMATRIX_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("MICROBOT_DEFENSE", true),
				ScalingText(BaseTarget.Value, StackTarget.Value, "flat", "cIsDamage"),
				ScalingText(BaseRange.Value, StackRange.Value, "distance", "cIsDamage")
			);

			if (StackReduction.Value == 0f)
			{
				output += String.Format(
					TextFragment("MICROBOT_RECHARGE"),
					SecondText(BaseRecharge.Value, "every", "cIsUtility")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("MICROBOT_RECHARGE_REDUCE"),
					SecondText(BaseRecharge.Value, "every", "cIsUtility"),
					ScalingText(StackReduction.Value, "percent")
				);
			}

			if (ScaleAtkSpd.Value)
			{
				output += TextFragment("MICROBOT_DETAIL");
			}

			return output;
		}



		private static void RechargeFrequencyHook()
		{
			MethodInfo get_RF_Method = typeof(DefenseMatrixOn).GetMethod("get_rechargeFrequency", flags);
			MethodInfo GRFH_Method = typeof(DefensiveMicrobots).GetMethod(nameof(GetRechargeFrequencyHook), flags);

			Hook get_RF_Hook = new Hook(get_RF_Method, GRFH_Method);
			origRechargeFrequencyGetter = get_RF_Hook.GenerateTrampoline<MicrobotReturnFloat>();
		}

		private static float GetRechargeFrequencyHook(DefenseMatrixOn self)
		{
			float interval = Mathf.Max(0.01f, BaseRecharge.Value) * Mathf.Pow(1f - StackReduction.Value, self.GetItemStack() - 1);

			if (ScaleAtkSpd.Value && self.attachedBody)
			{
				float atkSpd = self.attachedBody.attackSpeed;
				if (atkSpd > 1f)
				{
					interval /= atkSpd;
				}
			}

			float freq = 1f / Mathf.Max(0.01f, interval);

			return freq;

			//return origRechargeFrequencyGetter(self);
		}

		private static void FireFrequencyHook()
		{
			MethodInfo get_FF_Method = typeof(DefenseMatrixOn).GetMethod("get_fireFrequency", flags);
			MethodInfo GFFH_Method = typeof(DefensiveMicrobots).GetMethod(nameof(GetFireFrequencyHook), flags);

			Hook get_FF_Hook = new Hook(get_FF_Method, GFFH_Method);
			origFireFrequencyGetter = get_FF_Hook.GenerateTrampoline<MicrobotReturnFloat>();
		}

		private static float GetFireFrequencyHook(DefenseMatrixOn self)
		{
			return Mathf.Max(1f / Mathf.Max(0.01f, BaseRecharge.Value), self.rechargeFrequency);

			//return origFireFrequencyGetter(self);
		}

		private static void ProjectileTargetingHook()
		{
			IL.EntityStates.CaptainDefenseMatrixItem.DefenseMatrixOn.DeleteNearbyProjectile += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(4)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldloc, 4);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						float range = BaseRange.Value + StackRange.Value * (count - 1);
						return range * range;
					});
					c.Emit(OpCodes.Stloc, 2);

					c.Emit(OpCodes.Ldloc, 4);
					c.EmitDelegate<Func<int, int>>((count) =>
					{
						return BaseTarget.Value + StackTarget.Value * (count - 1);
					});
					c.Emit(OpCodes.Stloc, 4);
				}
				else
				{
					LogWarn(itemIdentifier + " :: ProjectileTargetingHook Failed!");
				}
			};
		}
	}
}
