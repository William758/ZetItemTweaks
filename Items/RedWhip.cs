﻿using System;
using System.Collections.Generic;
using BepInEx.Configuration;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class RedWhip
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff", "com.Ben.BenBalanceMod" };

		public static string itemIdentifier = "RedWhip";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseMove { get; set; }
		public static ConfigEntry<float> StackMove { get; set; }
		public static ConfigEntry<float> BaseSafeMove { get; set; }
		public static ConfigEntry<float> StackSafeMove { get; set; }
		public static ConfigEntry<float> BaseArmor { get; set; }
		public static ConfigEntry<float> StackArmor { get; set; }



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
			BaseMove = ConfigEntry(
				itemIdentifier, "BaseMove", 0.1f,
				"Movement speed gained from item."
			);
			StackMove = ConfigEntry(
				itemIdentifier, "StackMove", 0.1f,
				"Movement speed gained from item per stack."
			);
			BaseSafeMove = ConfigEntry(
				itemIdentifier, "BaseSafeMove", 0.2f,
				"Movement speed gained while safe."
			);
			StackSafeMove = ConfigEntry(
				itemIdentifier, "StackSafeMove", 0.2f,
				"Movement speed gained while safe per stack."
			);
			BaseArmor = ConfigEntry(
				itemIdentifier, "BaseArmor", 0f,
				"Armor gained from item."
			);
			StackArmor = ConfigEntry(
				itemIdentifier, "StackArmor", 0f,
				"Armor gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("SAFE_MOVESPEED", "\nIncreases <style=cIsUtility>movement speed</style> by {0} while out of combat.");
				RegisterToken("ITEM_SPRINTOUTOFCOMBAT_DESC", DescriptionText());
				RegisterToken("ITEM_SPRINTOUTOFCOMBAT_PICKUP", PickupText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseSafeMove.Value > 0f)
			{
				output += String.Format(
					TextFragment("SAFE_MOVESPEED", true),
					ScalingText(BaseSafeMove.Value, StackSafeMove.Value, "percent", "cIsUtility")
				);
			}

			if (BaseMove.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_MOVESPEED", true),
					ScalingText(BaseMove.Value, StackMove.Value, "percent", "cIsUtility")
				);
			}
			if (BaseArmor.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_ARMOR", true),
					ScalingText(BaseArmor.Value, StackArmor.Value, "flat", "cIsHealing")
				);
			}

			if (output == "") output += "<style=cStack>(current configuration :: item with no effect)</style>";

			return output;
		}

		private static string PickupText()
		{
			if (BaseSafeMove.Value > 0f)
			{
				return "Move fast out of combat.";
			}
			if (BaseMove.Value > 0f && BaseArmor.Value > 0f)
			{
				return "Move faster and take less damage.";
			}
			if (BaseMove.Value > 0f)
			{
				return "Move faster.";
			}
			if (BaseMove.Value > 0f && BaseArmor.Value > 0f)
			{
				return "Take less damage.";
			}

			return "No effect.";
		}
	}
}
