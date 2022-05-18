﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Configuration;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class BisonSteak
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff", "Hayaku.VanillaRebalance", "com.OkIGotIt.Fresh_Bison_Steak" };

		public static string itemIdentifier = "BisonSteak";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseHealth { get; set; }
		public static ConfigEntry<float> StackHealth { get; set; }
		public static ConfigEntry<bool> BuffStack { get; set; }
		public static ConfigEntry<float> BaseKillRegen { get; set; }
		public static ConfigEntry<float> StackKillRegen { get; set; }
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
			BaseHealth = ConfigEntry(
				itemIdentifier, "BaseHealth", 25f,
				"Health gained from item."
			);
			StackHealth = ConfigEntry(
				itemIdentifier, "StackHealth", 25f,
				"Health gained from item per stack."
			);
			BuffStack = ConfigEntry(
				itemIdentifier, "BuffStack", true,
				"Whether buff can stack."
			);
			BaseKillRegen = ConfigEntry(
				itemIdentifier, "BaseKillRegen", 2.4f,
				"Health regeneration gained from buff."
			);
			StackKillRegen = ConfigEntry(
				itemIdentifier, "StackKillRegen", 0f,
				"Health regeneration gained from buff per item stack."
			);
			BaseBuffDuration = ConfigEntry(
				itemIdentifier, "BaseBuffDuration", 3f,
				"Regeneration buff duration."
			);
			StackBuffDuration = ConfigEntry(
				itemIdentifier, "StackBuffDuration", 2f,
				"Regeneration buff duration per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			JunkContent.Buffs.MeatRegenBoost.canStack = BuffStack.Value;

			MeatRegenHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("REGEN_ON_KILL", "\nKilling an enemy increases <style=cIsHealing>health regeneration</style> by {0} for {1} seconds.");
				RegisterToken("ITEM_FLATHEALTH_DESC", DescriptionText());
				RegisterToken("ITEM_FLATHEALTH_PICKUP", PickupText());
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
					ScalingText(BaseHealth.Value, StackHealth.Value, "flat", "cIsHealing")
				);
			}
			if (BaseKillRegen.Value > 0f && BaseBuffDuration.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("REGEN_ON_KILL", true),
					ScalingText(BaseKillRegen.Value, StackKillRegen.Value, "flatregen", "cIsHealing"),
					ScalingText(BaseBuffDuration.Value, StackBuffDuration.Value, "flat", "cIsUtility")
				);
			}

			if (output == "") output += "<style=cStack>(current configuration :: item with no effect)</style>";

			return output;
		}

		private static string PickupText()
		{
			bool regenOnKill = BaseKillRegen.Value > 0f && BaseBuffDuration.Value > 0f;

			if (BaseHealth.Value > 0f)
			{
				if (regenOnKill)
				{
					return "Increase max health. Boost regen on kill.";
				}
				else
				{
					return "Increase max health.";
				}
			}
			else
			{
				if (regenOnKill)
				{
					return "Boost regen on kill.";
				}
				else
				{
					return "No effect.";
				}
			}
		}



		private static void MeatRegenHook()
		{
			On.RoR2.GlobalEventManager.OnCharacterDeath += (orig, self, damageReport) =>
			{
				orig(self, damageReport);

				if (NetworkServer.active && damageReport != null)
				{
					if (BaseKillRegen.Value > 0f && BaseBuffDuration.Value > 0f)
					{
						if (damageReport.attacker && damageReport.attackerBody)
						{
							CharacterBody body = damageReport.attackerBody;
							if (body)
							{
								Inventory inventory = body.inventory;
								if (inventory)
								{
									int count = inventory.GetItemCount(RoR2Content.Items.FlatHealth);
									if (count > 0)
									{
										float duration = BaseBuffDuration.Value + StackBuffDuration.Value * (count - 1);
										body.AddTimedBuff(JunkContent.Buffs.MeatRegenBoost, duration);
									}
								}
							}
						}
					}
				}
			};
		}
	}
}
