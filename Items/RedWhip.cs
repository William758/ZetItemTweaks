using System;
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
				targetLanguage = "default";

				RegisterFragment("SAFE_MOVESPEED", "\nIncreases <style=cIsUtility>movement speed</style> by {0} while out of combat.");
				RegisterFragment("WHIP_PICKUP_PEACESPEED", "Move fast out of combat.");
				RegisterFragment("WHIP_PICKUP_SPEEDARMOR", "Move faster and take less damage.");
				RegisterFragment("WHIP_PICKUP_SPEED", "Move faster.");
				RegisterFragment("WHIP_PICKUP_ARMOR", "Take less damage.");
				RegisterToken("ITEM_SPRINTOUTOFCOMBAT_DESC", DescriptionText());
				RegisterToken("ITEM_SPRINTOUTOFCOMBAT_PICKUP", PickupText());

				targetLanguage = "pt-BR";

				RegisterFragment("SAFE_MOVESPEED", "\nAumenta a <style=cIsUtility>velocidade de movimento</style> em {0} enquanto fora do combate.");
				RegisterFragment("WHIP_PICKUP_PEACESPEED", "Retire-se do combate rapidamente.");
				RegisterFragment("WHIP_PICKUP_SPEEDARMOR", "Mova-se mais rápido e receba menos dano.");
				RegisterFragment("WHIP_PICKUP_SPEED", "Mova-se mais rápido.");
				RegisterFragment("WHIP_PICKUP_ARMOR", "Receba menos dano.");
				RegisterToken("ITEM_SPRINTOUTOFCOMBAT_DESC", DescriptionText());
				RegisterToken("ITEM_SPRINTOUTOFCOMBAT_PICKUP", PickupText());

				targetLanguage = "";
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

			if (output == "") output += TextFragment("CFG_NO_EFFECT");

			return output;
		}

		private static string PickupText()
		{
			if (BaseSafeMove.Value > 0f)
			{
				return TextFragment("WHIP_PICKUP_PEACESPEED");
			}
			if (BaseMove.Value > 0f && BaseArmor.Value > 0f)
			{
				return TextFragment("WHIP_PICKUP_SPEEDARMOR");
			}
			if (BaseMove.Value > 0f)
			{
				return TextFragment("WHIP_PICKUP_SPEED");
			}
			if (BaseMove.Value > 0f && BaseArmor.Value > 0f)
			{
				return TextFragment("WHIP_PICKUP_ARMOR");
			}

			return TextFragment("PICKUP_NO_EFFECT");
		}
	}
}
