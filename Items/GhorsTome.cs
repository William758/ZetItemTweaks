using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class GhorsTome
	{
		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "GhorsTome";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseChance { get; set; }
		public static ConfigEntry<float> StackChance { get; set; }
		public static ConfigEntry<float> BaseMult { get; set; }
		public static ConfigEntry<float> StackMult { get; set; }


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
			BaseChance = ConfigEntry(
				itemIdentifier, "BaseChance", 5f,
				"Chance to drop treasure."
			);
			StackChance = ConfigEntry(
				itemIdentifier, "StackChance", 2.5f,
				"Chance to drop treasure per stack."
			);
			BaseMult = ConfigEntry(
				itemIdentifier, "BaseMult", 0.1f,
				"Gold from kills increase."
			);
			StackMult = ConfigEntry(
				itemIdentifier, "StackMult", 0.1f,
				"Gold from kills increase per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			GoldFromKillHook();
			GoldPackChanceHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("GOLD_FROM_KILL", "Kills grant {0} more gold.");
				RegisterFragment("TREASURE", "Kills have a {0} chance to drop a treasure worth <style=cIsUtility>$25</style> that <style=cIsUtility>scales over time</style>.");
				RegisterToken("ITEM_BONUSGOLDPACKONKILL_DESC", DescriptionText());
				RegisterToken("ITEM_BONUSGOLDPACKONKILL_PICKUP", PickupText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";
			if (BaseMult.Value > 0f)
			{
				output += String.Format(
					TextFragment("GOLD_FROM_KILL"),
					ScalingText(BaseMult.Value, StackMult.Value, "percent", "cIsUtility")
				);
			}
			if (BaseChance.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("TREASURE"),
					ScalingText(BaseChance.Value, StackChance.Value, "chance", "cIsUtility")
				);
			}

			if (output == "") output += "<style=cStack>(current configuration :: item with no effect)</style>";

			return output;
		}

		private static string PickupText()
		{
			if (BaseChance.Value > 0f && BaseMult.Value > 0f)
			{
				return "Kills grant more gold and have a chance to drop treasure.";
			}
			if (BaseChance.Value > 0f)
			{
				return "Kills have a chance to drop treasure.";
			}
			if (BaseMult.Value > 0f)
			{
				return "Kills grant more gold.";
			}

			return "No effect.";
		}



		private static void GoldPackChanceHook()
		{
			IL.RoR2.GlobalEventManager.OnCharacterDeath += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(4f),
					x => x.MatchLdloc(85),
					x => x.MatchConvR4(),
					x => x.MatchMul()
				);

				if (found)
				{
					c.Index += 4;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, 85);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						if (BaseChance.Value > 0f)
						{
							return BaseChance.Value + StackChance.Value * (count - 1);
						}

						return 0f;
					});
				}
				else
				{
					Debug.LogWarning(itemIdentifier + " :: GoldPackChanceHook Failed!");
				}
			};
		}

		private static void GoldFromKillHook()
		{
			IL.RoR2.DeathRewards.OnKilledServer += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(2)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldloc, 0);
					c.Emit(OpCodes.Ldloc, 2);
					c.EmitDelegate<Func<CharacterBody, uint, uint>>((attackBody, reward) =>
					{
						if (BaseMult.Value > 0f)
						{
							Inventory inventory = attackBody.inventory;
							if (inventory)
							{
								int count = inventory.GetItemCount(RoR2Content.Items.BonusGoldPackOnKill);
								if (count > 0)
								{
									float mult = 1f + BaseMult.Value + StackMult.Value * (count - 1);
									return (uint)(reward * mult);
								}
							}
						}

						return reward;
					});
					c.Emit(OpCodes.Stloc, 2);
				}
				else
				{
					Debug.LogWarning(itemIdentifier + " :: GoldFromKillHook Failed!");
				}
			};
		}
	}
}
