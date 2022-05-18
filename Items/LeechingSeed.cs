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
	public static class LeechingSeed
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff", "Withor.LeechingSeedBuff" };

		public static string itemIdentifier = "LeechingSeed";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> MinHealCoeff { get; set; }
		public static ConfigEntry<bool> NoProcHealing { get; set; }
		public static ConfigEntry<float> BaseHeal { get; set; }
		public static ConfigEntry<float> StackHeal { get; set; }
		public static ConfigEntry<float> BaseLeech { get; set; }
		public static ConfigEntry<float> StackLeech { get; set; }
		public static ConfigEntry<int> LeechModifier { get; set; }
		public static ConfigEntry<float> LeechModMult { get; set; }
		public static ConfigEntry<float> LeechParameter { get; set; }
		public static ConfigEntry<float> LeechPostModMult { get; set; }



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
			MinHealCoeff = ConfigEntry(
				itemIdentifier, "MinHealCoeff", 0.25f,
				"Minimum healing multiplier from proc coefficient."
			);
			NoProcHealing = ConfigEntry(
				itemIdentifier, "NoProcHealing", true,
				"Heal from damage with 0 proc coefficient."
			);
			BaseHeal = ConfigEntry(
				itemIdentifier, "BaseHeal", 2f,
				"Life gain on hit from item."
			);
			StackHeal = ConfigEntry(
				itemIdentifier, "StackHeal", 2f,
				"Life gain on hit from item per stack."
			);
			BaseLeech = ConfigEntry(
				itemIdentifier, "BaseHealFrac", 0f,
				"Percent damage leech amount."
			);
			StackLeech = ConfigEntry(
				itemIdentifier, "StackHealFrac", 0f,
				"Percent damage leech amount per stack."
			);
			LeechModifier = ConfigEntry(
				itemIdentifier, "LeechModifier", 1,
				"Leech modifier. 0 = None, 1 = POW, 2 = LOG."
			);
			LeechModMult = ConfigEntry(
				itemIdentifier, "LeechModMult", 1f,
				"Multiply leech value before modifier applied. Only applied when a modifier is enabled."
			);
			LeechParameter = ConfigEntry(
				itemIdentifier, "LeechParameter", 0.65f,
				"Leech modifier parameter."
			);
			LeechPostModMult = ConfigEntry(
				itemIdentifier, "LeechPostModMult", 2f,
				"Multiply leech value after modifier applied. Only applied when a modifier is enabled."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DisableDefaultHook();
			GlobalEventManager.onServerDamageDealt += HealingEffect;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				RegisterFragment("HEAL_ON_HIT", "\nDealing damage <style=cIsHealing>heals</style> you for {0} <style=cIsHealing>health</style>.");
				RegisterFragment("HEAL_PERCENT_ON_HIT", "\n<style=cIsHealing>Heal</style> for {0} of the <style=cIsDamage>damage</style> you deal.");
				RegisterFragment("LEECH_MODIFIER_FORMULA", "\n<style=cStack>Leech Modifier => {0}( [a]{1} , {2} ){3}</style>");
				RegisterToken("ITEM_SEED_DESC", DescriptionText());
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseHeal.Value > 0f)
			{
				output += String.Format(
					TextFragment("HEAL_ON_HIT", true),
					ScalingText(BaseHeal.Value, StackHeal.Value, "flat", "cIsHealing")
				);
			}
			if (BaseLeech.Value > 0f)
			{
				if (output != "") output += "\n";

				output += String.Format(
					TextFragment("HEAL_PERCENT_ON_HIT", true),
					ScalingText(BaseLeech.Value, StackLeech.Value, "percent", "cIsHealing")
				);

				int leechMod = LeechModifier.Value;
				if (leechMod > 0 && leechMod < 3)
				{
					float modMult = LeechModMult.Value;
					float postModMult = LeechPostModMult.Value;

					output += "\n" + String.Format(
						TextFragment("LEECH_MODIFIER_FORMULA"),
						leechMod == 1 ? "POW" : "LOG",
						modMult == 1f ? "" : (" * " + modMult),
						LeechParameter.Value,
						postModMult == 1f ? "" : (" * " + postModMult)
					);
				}
			}

			if (output == "") output += "<style=cStack>(current configuration :: item with no effect)</style>";

			return output;
		}



		private static void DisableDefaultHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				int index = -1;

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items), "Seed"),
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
					LogWarn(itemIdentifier + " :: DisableDefault Failed!");
				}
			};
		}

		private static void HealingEffect(DamageReport damageReport)
		{
			DamageInfo damageInfo = damageReport.damageInfo;
			if (NoProcHealing.Value || damageInfo.procCoefficient > 0f)
			{
				if (damageReport.attacker && damageReport.attackerBody)
				{
					CharacterBody body = damageReport.attackerBody;
					if (body)
					{
						Inventory inventory = body.inventory;
						if (inventory)
						{
							int count = inventory.GetItemCount(RoR2Content.Items.Seed);
							if (count > 0)
							{
								float healing = 0f;

								if (BaseLeech.Value > 0f)
								{
									healing += BaseLeech.Value + StackLeech.Value * (count - 1);

									healing *= damageInfo.damage;
									if (damageInfo.crit) healing *= body.critMultiplier;

									if (healing >= 1.25f)
									{
										int leechMod = LeechModifier.Value;
										if (leechMod > 0)
										{
											float modMult = LeechModMult.Value;
											float modParameter = LeechParameter.Value;
											float postModMult = LeechPostModMult.Value;

											if (leechMod == 1)
											{
												healing = Mathf.Max(1.25f, healing * modMult);
												healing = Mathf.Pow(healing, modParameter) * postModMult;
											}
											if (leechMod == 2)
											{
												healing = Mathf.Max(1.25f, healing * modMult);
												healing = Mathf.Log(healing, modParameter) * postModMult;
											}

											healing = Mathf.Max(1.25f, healing);
										}
									}
								}

								if (BaseHeal.Value > 0f)
								{
									healing += BaseHeal.Value + StackHeal.Value * (count - 1);
								}

								healing *= Mathf.Max(MinHealCoeff.Value, damageInfo.procCoefficient);

								if (healing > 0f)
								{
									body.healthComponent.Heal(healing, damageInfo.procChainMask, true);
								}
							}
						}
					}
				}
			}
		}
	}
}
