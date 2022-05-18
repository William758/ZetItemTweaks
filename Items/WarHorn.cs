using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class WarHorn
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "WarHorn";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseBuffMove { get; set; }
		public static ConfigEntry<float> StackBuffMove { get; set; }
		public static ConfigEntry<float> BaseBuffAtkSpd { get; set; }
		public static ConfigEntry<float> StackBuffAtkSpd { get; set; }
		public static ConfigEntry<float> BaseBuffDuration { get; set; }
		public static ConfigEntry<float> StackBuffDuration { get; set; }



		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
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
			BaseBuffMove = ConfigEntry(
				itemIdentifier, "BaseBuffMove", 0.2f,
				"Movement speed gained from buff."
			);
			StackBuffMove = ConfigEntry(
				itemIdentifier, "StackBuffMove", 0.1f,
				"Movement speed gained from buff per item stack."
			);
			BaseBuffAtkSpd = ConfigEntry(
				itemIdentifier, "BaseBuffAtkSpd", 0.4f,
				"Attack speed gained from buff."
			);
			StackBuffAtkSpd = ConfigEntry(
				itemIdentifier, "StackBuffAtkSpd", 0.2f,
				"Attack speed gained from buff per item stack."
			);
			BaseBuffDuration = ConfigEntry(
				itemIdentifier, "BaseBuffDuration", 8f,
				"Buff duration gained from item."
			);
			StackBuffDuration = ConfigEntry(
				itemIdentifier, "StackBuffDuration", 2f,
				"Buff duration gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DurationHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("ENERGIZE_MOVE", "Activating your Equipment gives you {0} <style=cIsDamage>attack speed</style> for {1}.");
				RegisterFragment("ENERGIZE_ATKSPD", "Activating your Equipment gives you {0} <style=cIsUtility>movement speed</style> for {1}.");
				RegisterFragment("ENERGIZE_BOTH", "Activating your Equipment gives you {0} <style=cIsUtility>movement speed</style> and {1} <style=cIsDamage>attack speed</style> for {2}.");
				RegisterToken("ITEM_ENERGIZEDONEQUIPMENTUSE_DESC", DescriptionText());
				RegisterToken("ITEM_ENERGIZEDONEQUIPMENTUSE_PICKUP", PickupText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "wat";

			if (BaseBuffMove.Value > 0f && BaseBuffAtkSpd.Value > 0f)
			{
				output = String.Format(
					TextFragment("ENERGIZE_BOTH"),
					ScalingText(BaseBuffMove.Value, StackBuffMove.Value, "percent", "cIsUtility"),
					ScalingText(BaseBuffAtkSpd.Value, StackBuffAtkSpd.Value, "percent", "cIsDamage"),
					ScalingText(BaseBuffDuration.Value, StackBuffDuration.Value, "duration", "cIsDamage")
				);
			}
			else if (BaseBuffMove.Value > 0f)
			{
				output = String.Format(
					TextFragment("ENERGIZE_MOVE"),
					ScalingText(BaseBuffMove.Value, StackBuffMove.Value, "percent", "cIsUtility"),
					ScalingText(BaseBuffDuration.Value, StackBuffDuration.Value, "duration", "cIsUtility")
				);
			}
			else if (BaseBuffAtkSpd.Value > 0f)
			{
				output = String.Format(
					TextFragment("ENERGIZE_ATKSPD"),
					ScalingText(BaseBuffAtkSpd.Value, StackBuffAtkSpd.Value, "percent", "cIsDamage"),
					ScalingText(BaseBuffDuration.Value, StackBuffDuration.Value, "duration", "cIsDamage")
				);
			}

			return output;
		}

		private static string PickupText()
		{
			if (BaseBuffMove.Value > 0f && BaseBuffAtkSpd.Value > 0f)
			{
				return "Activating your Equipment gives you a burst of movement speed and attack speed.";
			}
			else if (BaseBuffMove.Value > 0f)
			{
				return "Activating your Equipment gives you a burst of movement speed.";
			}
			else if (BaseBuffAtkSpd.Value > 0f)
			{
				return "Activating your Equipment gives you a burst of attack speed.";
			}
			return "wat";
		}



		private static void DurationHook()
		{
			IL.RoR2.EquipmentSlot.OnEquipmentExecuted += (il) =>
			{
				ILCursor c = new ILCursor(il);

				int index = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items), "EnergizedOnEquipmentUse"),
					x => x.MatchCallOrCallvirt<Inventory>("GetItemCount"),
					x => x.MatchStloc(out index)
				);

				if (found)
				{
					found = c.TryGotoNext(
						x => x.MatchLdcI4(8),
						x => x.MatchLdcI4(4),
						x => x.MatchLdloc(index),
						x => x.MatchLdcI4(1),
						x => x.MatchSub(),
						x => x.MatchMul(),
						x => x.MatchAdd(),
						x => x.MatchConvR4()
					);

					if (found)
					{
						c.Index += 8;

						c.Emit(OpCodes.Pop);
						c.Emit(OpCodes.Ldloc, index);
						c.EmitDelegate<Func<int, float>>((count) =>
						{
							return BaseBuffDuration.Value + StackBuffDuration.Value * (count - 1);
						});
					}
					else
					{
						LogWarn(itemIdentifier + " :: DurationHook:AddTimedBuff Failed!");
					}
				}
				else
				{
					LogWarn(itemIdentifier + " :: DurationHook:ItemCount Failed!");
				}
			};
		}
	}
}
