using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class BerzerkersPauldron
	{
		internal static BuffDef MultiKillBuff;

		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod" };

		public static string itemIdentifier = "BerzerkersPauldron";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BuffMove { get; set; }
		public static ConfigEntry<float> BuffAtkSpd { get; set; }
		public static ConfigEntry<int> BaseBuffCount { get; set; }
		public static ConfigEntry<int> StackBuffCount { get; set; }
		public static ConfigEntry<float> BaseBuffDuration { get; set; }
		public static ConfigEntry<float> StackBuffDuration { get; set; }
		public static ConfigEntry<bool> BuffRefresh { get; set; }



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
			BuffMove = ConfigEntry(
				itemIdentifier, "BuffMove", 0.075f,
				"Movement speed gained per buff."
			);
			BuffAtkSpd = ConfigEntry(
				itemIdentifier, "BuffAtkSpd", 0.15f,
				"Attack speed gained per buff."
			);
			BaseBuffCount = ConfigEntry(
				itemIdentifier, "BaseBuffCount", 3,
				"Maximum buff count gained from item."
			);
			StackBuffCount = ConfigEntry(
				itemIdentifier, "StackBuffCount", 2,
				"Maximum buff count gained from item per stack."
			);
			BaseBuffDuration = ConfigEntry(
				itemIdentifier, "BaseBuffDuration", 4f,
				"Buff duration gained from item."
			);
			StackBuffDuration = ConfigEntry(
				itemIdentifier, "StackBuffDuration", 0f,
				"Buff duration gained from item per stack."
			);
			BuffRefresh = ConfigEntry(
				itemIdentifier, "BuffRefresh", true,
				"Refresh all buff durations on kill."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DisableDefaultHook();
			ApplyBuffHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("MULTIKILL_ATKSPD", "Killing an enemy increase <style=cIsDamage>attack speed</style> by {0} for {1}.");
				RegisterFragment("MULTIKILL_MOVE", "Killing an enemy increase <style=cIsUtility>movement speed</style> by {0} for {1}.");
				RegisterFragment("MULTIKILL_BOTH", "Killing an enemy increase <style=cIsUtility>movement speed</style> by {0} and <style=cIsDamage>attack speed</style> by {1} for {2}.");

				RegisterFragment("MULTIKILL_ATKSPD_CAP", "\nMaximum cap of {0} <style=cIsDamage>attack speed</style>.");
				RegisterFragment("MULTIKILL_MOVE_CAP", "\nMaximum cap of {0} <style=cIsUtility>movement speed</style>.");
				RegisterFragment("MULTIKILL_BOTH_CAP", "\nMaximum cap of {0} <style=cIsUtility>movement speed</style> and {1} <style=cIsDamage>attack speed</style>.");

				RegisterToken("ITEM_WARCRYONMULTIKILL_DESC", DescriptionText());
				RegisterToken("ITEM_WARCRYONMULTIKILL_PICKUP", PickupText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "wat";

			if (BuffMove.Value > 0f && BuffAtkSpd.Value > 0f)
			{
				output = String.Format(
					TextFragment("MULTIKILL_BOTH"),
					ScalingText(BuffMove.Value, "percent", "cIsUtility"),
					ScalingText(BuffAtkSpd.Value, "percent", "cIsDamage"),
					ScalingText(BaseBuffDuration.Value, StackBuffDuration.Value, "duration", "cIsDamage")
				);

				output += String.Format(
					TextFragment("MULTIKILL_BOTH_CAP"),
					ScalingText(BuffMove.Value * BaseBuffCount.Value, BuffMove.Value * StackBuffCount.Value, "percent", "cIsUtility"),
					ScalingText(BuffAtkSpd.Value * BaseBuffCount.Value, BuffAtkSpd.Value * StackBuffCount.Value, "percent", "cIsDamage")
				);
			}
			else if (BuffMove.Value > 0f)
			{
				output = String.Format(
					TextFragment("MULTIKILL_MOVE"),
					ScalingText(BuffMove.Value, "percent", "cIsUtility"),
					ScalingText(BaseBuffDuration.Value, StackBuffDuration.Value, "duration", "cIsUtility")
				);

				output += String.Format(
					TextFragment("MULTIKILL_MOVE_CAP"),
					ScalingText(BuffMove.Value * BaseBuffCount.Value, BuffMove.Value * StackBuffCount.Value, "percent", "cIsUtility")
				);
			}
			else if (BuffAtkSpd.Value > 0f)
			{
				output = String.Format(
					TextFragment("MULTIKILL_ATKSPD"),
					ScalingText(BuffAtkSpd.Value, "percent", "cIsDamage"),
					ScalingText(BaseBuffDuration.Value, StackBuffDuration.Value, "duration", "cIsDamage")
				);

				output += String.Format(
					TextFragment("MULTIKILL_ATKSPD_CAP"),
					ScalingText(BuffAtkSpd.Value * BaseBuffCount.Value, BuffAtkSpd.Value * StackBuffCount.Value, "percent", "cIsDamage")
				);
			}

			return output;
		}

		private static string PickupText()
		{
			if (BuffMove.Value > 0f && BuffAtkSpd.Value > 0f)
			{
				return "Killing enemies grants movement speed and attack speed.";
			}
			else if (BuffMove.Value > 0f)
			{
				return "Killing enemies grants movement speed.";
			}
			else if (BuffAtkSpd.Value > 0f)
			{
				return "Killing enemies grants attack speed.";
			}
			return "wat";
		}



		private static void DisableDefaultHook()
		{
			IL.RoR2.CharacterBody.AddMultiKill += (il) =>
			{
				ILCursor c = new ILCursor(il);

				int index = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items), "WarCryOnMultiKill"),
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
					LogWarn(itemIdentifier + " :: DisableDefaultHook Failed!");
				}
			};
		}

		private static void ApplyBuffHook()
		{
			On.RoR2.GlobalEventManager.OnCharacterDeath += (orig, self, damageReport) =>
			{
				orig(self, damageReport);

				if (NetworkServer.active && damageReport != null)
				{
					if (BaseBuffDuration.Value > 0f)
					{
						if (damageReport.attacker && damageReport.attackerBody)
						{
							CharacterBody body = damageReport.attackerBody;
							if (body)
							{
								Inventory inventory = body.inventory;
								if (inventory)
								{
									int count = inventory.GetItemCount(RoR2Content.Items.WarCryOnMultiKill);
									if (count > 0)
									{
										float duration = BaseBuffDuration.Value + StackBuffDuration.Value * (count - 1);
										int maxStacks = BaseBuffCount.Value + StackBuffCount.Value * (count - 1);
										body.AddTimedBuff(MultiKillBuff, duration, maxStacks);

										if (BuffRefresh.Value)
										{
											RefreshTimedBuffStacks(body, MultiKillBuff.buffIndex, duration);
										}
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
