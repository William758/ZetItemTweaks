using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class Planula
	{
		public static List<string> autoCompatList = new List<string> { "com.kking117.FlatItemBuff" };

		public static string itemIdentifier = "Planula";
		public static bool appliedChanges = false;

		private static bool HealHookSuccess = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseHeal { get; set; }
		public static ConfigEntry<float> StackHeal { get; set; }
		public static ConfigEntry<float> BaseBarrier { get; set; }
		public static ConfigEntry<float> StackBarrier { get; set; }



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
			BaseHeal = ConfigEntry(
				itemIdentifier, "BaseHeal", 25f,
				"Heal amount from taking damage."
			);
			StackHeal = ConfigEntry(
				itemIdentifier, "StackHeal", 25f,
				"Heal amount from taking damage per stack."
			);
			BaseBarrier = ConfigEntry(
				itemIdentifier, "BaseBarrier", 0f,
				"Barrier amount from taking damage."
			);
			StackBarrier = ConfigEntry(
				itemIdentifier, "StackBarrier", 0f,
				"Barrier amount from taking damage per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			HealHook();

			if (!HealHookSuccess) return;

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("HEAL_HEALTH_FROM_DAMAGE", "Taking damage heals you for {0} <style=cIsHealing>health</style>.");
				RegisterFragment("HEAL_BARRIER_FROM_DAMAGE", "Taking damage generates {0} <style=cIsHealing>barrier</style>.");
				RegisterFragment("HEAL_BOTH_FROM_DAMAGE", "Taking damage heals you for {0} <style=cIsHealing>health</style> and generates {1} <style=cIsHealing>barrier</style>.");
				RegisterFragment("PLANULA_PICKUP_HEAL", "Taking damage heals you.");
				RegisterFragment("PLANULA_PICKUP_BARRIER", "Taking damage grants barrier.");
				RegisterToken("ITEM_PARENTEGG_DESC", DescriptionText());
				RegisterToken("ITEM_PARENTEGG_PICKUP", PickupText());

				targetLanguage = "pt-BR";

				RegisterFragment("HEAL_HEALTH_FROM_DAMAGE", "Receber dano cura você por {0} de <style=cIsHealing>saúde</style>.");
				RegisterFragment("HEAL_BARRIER_FROM_DAMAGE", "Receber dano cura você por {0} de <style=cIsHealing>barreira</style>.");
				RegisterFragment("HEAL_BOTH_FROM_DAMAGE", "Receber dano cura você por {0} de <style=cIsHealing>saúde</style> e gera {1} de <style=cIsHealing>barreira</style>.");
				RegisterFragment("PLANULA_PICKUP_HEAL", "Receber dano cura você.");
				RegisterFragment("PLANULA_PICKUP_BARRIER", "Receber dano conce uma barreira.");
				RegisterToken("ITEM_PARENTEGG_DESC", DescriptionText());
				RegisterToken("ITEM_PARENTEGG_PICKUP", PickupText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseHeal.Value > 0f && BaseBarrier.Value > 0f)
			{
				output = String.Format(
					TextFragment("HEAL_BOTH_FROM_DAMAGE", true),
					ScalingText(BaseHeal.Value, StackHeal.Value, "flat", "cIsHealing"),
					ScalingText(BaseBarrier.Value, StackBarrier.Value, "flat", "cIsHealing")
				);
			}
			else if (BaseHeal.Value > 0f)
			{
				output = String.Format(
					TextFragment("HEAL_HEALTH_FROM_DAMAGE", true),
					ScalingText(BaseHeal.Value, StackHeal.Value, "flat", "cIsHealing")
				);
			}
			else if (BaseBarrier.Value > 0f)
			{
				output = String.Format(
					TextFragment("HEAL_BARRIER_FROM_DAMAGE", true),
					ScalingText(BaseBarrier.Value, StackBarrier.Value, "flat", "cIsHealing")
				);
			}

			if (output == "") output += TextFragment("CFG_NO_EFFECT");

			return output;
		}

		private static string PickupText()
		{
			if (BaseHeal.Value > 0f)
			{
				return TextFragment("PLANULA_PICKUP_HEAL");
			}
			if (BaseBarrier.Value > 0f)
			{
				return TextFragment("PLANULA_PICKUP_BARRIER");
			}

			return TextFragment("PICKUP_NO_EFFECT");
		}




		private static void HealHook()
		{
			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdarg(0),
					x => x.MatchLdflda<HealthComponent>("itemCounts"),
					x => x.MatchLdfld("RoR2.HealthComponent/ItemCounts", "parentEgg"),
					x => x.MatchConvR4(),
					x => x.MatchLdcR4(15f),
					x => x.MatchMul()
				);

				if (found)
				{
					c.Index += 6;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<HealthComponent, float>>((healthComponent) =>
					{
						int count = healthComponent.itemCounts.parentEgg;

						if (BaseBarrier.Value > 0f)
						{
							float barrier = BaseBarrier.Value + StackBarrier.Value * (count - 1);
							if (barrier > 0f)
							{
								healthComponent.AddBarrier(barrier);
							}
						}

						return BaseHeal.Value + StackHeal.Value * (count - 1);
					});

					HealHookSuccess = true;
				}
				else
				{
					LogWarn(itemIdentifier + " :: HealHook Failed!");
				}
			};
		}
	}
}
