using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class IrradiantPearl
	{
		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "IrradiantPearl";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseHealth { get; set; }
		public static ConfigEntry<float> StackHealth { get; set; }
		public static ConfigEntry<float> BaseRegen { get; set; }
		public static ConfigEntry<float> StackRegen { get; set; }
		public static ConfigEntry<float> BaseArmor { get; set; }
		public static ConfigEntry<float> StackArmor { get; set; }
		public static ConfigEntry<float> BaseMove { get; set; }
		public static ConfigEntry<float> StackMove { get; set; }
		public static ConfigEntry<float> BaseCooldown { get; set; }
		public static ConfigEntry<float> StackCooldown { get; set; }
		public static ConfigEntry<float> BaseCrit { get; set; }
		public static ConfigEntry<float> StackCrit { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<float> BaseAtkSpd { get; set; }
		public static ConfigEntry<float> StackAtkSpd { get; set; }



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
			BaseHealth = ConfigEntry(
				itemIdentifier, "BaseHealth", 0.1f,
				"Health percent gained from item."
			);
			StackHealth = ConfigEntry(
				itemIdentifier, "StackHealth", 0.1f,
				"Health percent gained from item per stack."
			);
			BaseRegen = ConfigEntry(
				itemIdentifier, "BaseRegen", 4f,
				"Health regeneration gained from item."
			);
			StackRegen = ConfigEntry(
				itemIdentifier, "StackRegen", 4f,
				"Health regeneration gained from item per stack."
			);
			BaseArmor = ConfigEntry(
				itemIdentifier, "BaseArmor", 10f,
				"Armor gained from item."
			);
			StackArmor = ConfigEntry(
				itemIdentifier, "StackArmor", 10f,
				"Armor gained from item per stack."
			);
			BaseMove = ConfigEntry(
				itemIdentifier, "BaseMove", 0.1f,
				"Movement speed gained from item."
			);
			StackMove = ConfigEntry(
				itemIdentifier, "StackMove", 0.1f,
				"Movement speed gained from item per stack."
			);
			BaseCooldown = ConfigEntry(
				itemIdentifier, "BaseCooldown", 0.1f,
				"Cooldown reduction gained from item."
			);
			StackCooldown = ConfigEntry(
				itemIdentifier, "StackCooldown", 0.1f,
				"Cooldown reduction gained from item per stack."
			);
			BaseCrit = ConfigEntry(
				itemIdentifier, "BaseCrit", 10f,
				"Critical strike chance gained from item."
			);
			StackCrit = ConfigEntry(
				itemIdentifier, "StackCrit", 10f,
				"Critical strike chance gained from item per stack."
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 0.1f,
				"Damage gained from item."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0.1f,
				"Damage gained from item per stack."
			);
			BaseAtkSpd = ConfigEntry(
				itemIdentifier, "BaseAtkSpd", 0.1f,
				"Attack speed gained from item."
			);
			StackAtkSpd = ConfigEntry(
				itemIdentifier, "StackAtkSpd", 0.1f,
				"Attack speed gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DisableDefaultStatHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterToken("ITEM_SHINYPEARL_DESC", DescriptionText());
				RegisterToken("ITEM_SHINYPEARL_PICKUP", "Increases Stats.");
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseHealth.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_HEALTH", true),
					ScalingText(BaseHealth.Value, StackHealth.Value, "percent", "cIsHealing")
				);
			}
			if (BaseRegen.Value > 0)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_REGENERATION", true),
					ScalingText(BaseRegen.Value, StackRegen.Value, "flatregen", "cIsHealing")
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
			if (BaseMove.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_MOVESPEED", true),
					ScalingText(BaseMove.Value, StackMove.Value, "percent", "cIsUtility")
				);
			}
			if (BaseCooldown.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_COOLDOWN", true),
					ScalingText(BaseCooldown.Value, StackCooldown.Value, "percent", "cIsUtility")
				);
			}
			if (BaseCrit.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_CRIT", true),
					ScalingText(BaseCrit.Value, StackCrit.Value, "chance", "cIsDamage")
				);
			}
			if (BaseDamage.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_DAMAGE", true),
					ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
				);
			}
			if (BaseAtkSpd.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("STAT_ATKSPEED", true),
					ScalingText(BaseAtkSpd.Value, StackAtkSpd.Value, "percent", "cIsDamage")
				);
			}

			return output;
		}



		private static void DisableDefaultStatHook()
		{
			IL.RoR2.CharacterBody.RecalculateStats += (il) =>
			{
				ILCursor c = new ILCursor(il);

				int index = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items), "ShinyPearl"),
					x => x.MatchCallOrCallvirt<Inventory>("GetItemCount"),
					x => x.MatchStloc(out index)
				);

				if (found)
				{
					c.Index += 3;

					c.Emit(OpCodes.Ldc_I4, 0);
					c.Emit(OpCodes.Stloc, index);
				}
				else
				{
					LogWarn(itemIdentifier + " :: DisableDefaultStatHook Failed!");
				}
			};
		}
	}
}
