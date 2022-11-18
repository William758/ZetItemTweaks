using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class LittleDisciple
	{
		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "LittleDisciple";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<float> BaseRange { get; set; }
		public static ConfigEntry<float> StackRange { get; set; }
		public static ConfigEntry<float> BaseRecharge { get; set; }
		public static ConfigEntry<float> StackReduction { get; set; }
		public static ConfigEntry<bool> ScaleMove { get; set; }



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
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 2f,
				"Wisp damage gained from item."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 2f,
				"Wisp damage gained from item per stack."
			);
			BaseRange = ConfigEntry(
				itemIdentifier, "BaseRange", 40f,
				"Wisp range gained from item."
			);
			StackRange = ConfigEntry(
				itemIdentifier, "StackRange", 0f,
				"Wisp range gained from item per stack."
			);
			BaseRecharge = ConfigEntry(
				itemIdentifier, "BaseRecharge", 1.75f,
				"Recharge interval for item."
			);
			StackReduction = ConfigEntry(
				itemIdentifier, "StackReduction", 0f,
				"Recharge interval reduction per stack."
			);
			ScaleMove = ConfigEntry(
				itemIdentifier, "ScaleMove", true,
				"Movement speed effects recharge interval."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			FireFrequencyHook();
			DamageHook();
			RangeHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("TRACKWISP_DAMAGE", "Fire a <style=cIsDamage>tracking wisp</style> at an enemy within {0} that deals {1} base damage.");
				RegisterFragment("TRACKWISP_RECHARGE", "\nFires {0} while sprinting.");
				RegisterFragment("TRACKWISP_RECHARGE_REDUCE", "\nFire {0} <style=cStack>(-{1} per stack)</style> while sprinting.");
				RegisterFragment("TRACKWISP_DETAIL", "\n<style=cStack>(Fire rate scales with movement speed)</style>");
				RegisterToken("ITEM_SPRINTWISP_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("TRACKWISP_DAMAGE", "Dispara <style=cIsDamage>espíritos rastreadores</style> em um inimigo dentro de {0} que causa {1} de dano base.");
				RegisterFragment("TRACKWISP_RECHARGE", "\nDispara {0} ao correr.");
				RegisterFragment("TRACKWISP_RECHARGE_REDUCE", "\nDispara {0} <style=cStack>(-{1} por acúmulo)</style> ao correr.");
				RegisterFragment("TRACKWISP_DETAIL", "\n<style=cStack>(A taxa de disparo aumenta com a velocidade de movimento)</style>");
				RegisterToken("ITEM_SPRINTWISP_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("TRACKWISP_DAMAGE", true),
				ScalingText(BaseRange.Value, StackRange.Value, "distance", "cIsDamage"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);

			if (StackReduction.Value == 0f)
			{
				output += String.Format(
					TextFragment("TRACKWISP_RECHARGE"),
					SecondText(BaseRecharge.Value, "every", "cIsUtility")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("TRACKWISP_RECHARGE_REDUCE"),
					SecondText(BaseRecharge.Value, "every", "cIsUtility"),
					ScalingText(StackReduction.Value, "percent")
				);
			}

			if (ScaleMove.Value)
			{
				output += TextFragment("TRACKWISP_DETAIL");
			}

			return output;
		}



		private static void FireFrequencyHook()
		{
			IL.RoR2.Items.SprintWispBodyBehavior.FixedUpdate += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchMul(),
					x => x.MatchDiv(),
					x => x.MatchAdd(),
					x => x.MatchStfld<RoR2.Items.SprintWispBodyBehavior>("fireTimer")
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<RoR2.Items.SprintWispBodyBehavior, float>>((behavior) =>
					{
						float rechargeTime = Mathf.Max(0.01f, BaseRecharge.Value) * Mathf.Pow(1f - StackReduction.Value, behavior.stack - 1);

						if (ScaleMove.Value)
						{
							CharacterBody body = behavior.body;
							rechargeTime /= body.moveSpeed / body.baseMoveSpeed;
						}

						return rechargeTime;
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: FireFrequencyHook Failed!");
				}
			};
		}

		private static void DamageHook()
		{
			IL.RoR2.Items.SprintWispBodyBehavior.Fire += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStfld<DevilOrb>("damageValue")
				);

				if (found)
				{
					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<RoR2.Items.SprintWispBodyBehavior, float>>((behavior) =>
					{
						return behavior.body.damage * (BaseDamage.Value + StackDamage.Value * (behavior.stack - 1));
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed!");
				}
			};
		}

		private static void RangeHook()
		{
			IL.RoR2.Items.SprintWispBodyBehavior.Fire += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld<RoR2.Items.SprintWispBodyBehavior>("searchRadius")
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.Emit(OpCodes.Ldfld, typeof(RoR2.Items.BaseItemBodyBehavior).GetField("stack"));
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseRange.Value + StackRange.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: RangeHook Failed!");
				}
			};
		}
	}
}
