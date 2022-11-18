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
	public static class Shuriken
	{
		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "Shuriken";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<int> BaseCount { get; set; }
		public static ConfigEntry<int> StackCount { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage{ get; set; }
		public static ConfigEntry<float> BaseRecharge { get; set; }
		public static ConfigEntry<float> StackReduction { get; set; }
		public static ConfigEntry<bool> ReloadAll { get; set; }



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
			BaseCount = ConfigEntry(
				itemIdentifier, "BaseCount", 3,
				"Shuriken count gained from item."
			);
			StackCount = ConfigEntry(
				itemIdentifier, "StackCount", 1,
				"Shuriken count gained from item per stack."
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 4f,
				"Shuriken damage."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 1f,
				"Shuriken damage per stack."
			);
			BaseRecharge = ConfigEntry(
				itemIdentifier, "BaseRecharge", 10f,
				"Recharge interval for reload."
			);
			StackReduction = ConfigEntry(
				itemIdentifier, "StackReduction", 0f,
				"Recharge interval reduction per stack."
			);
			ReloadAll = ConfigEntry(
				itemIdentifier, "ReloadAll", true,
				"Spread reload time over all shuriken."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DamageHook();
			StackHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("SHURIKEN_DAMAGE", "Activating your <style=cIsUtility>Primary Skill</style> also throws a <style=cIsDamage>shuriken</style> that deals {0} base damage.");
				RegisterFragment("SHURIKEN_COUNT", "\nYou can hold up to {0} <style=cIsDamage>shurikens</style>.");
				RegisterFragment("SHURIKEN_RECHARGE", "\nGain a <style=cIsDamage>shuriken</style> {0}.");
				RegisterFragment("SHURIKEN_RECHARGE_REDUCE", "\nGain a <style=cIsDamage>shuriken</style> {0} <style=cStack>(-{1} per stack)</style>.");
				RegisterFragment("SHURIKEN_RECHARGE_ALL", "\nReload all <style=cIsDamage>shuriken</style> {0}.");
				RegisterFragment("SHURIKEN_RECHARGE_REDUCE_ALL", "\nReload all <style=cIsDamage>shuriken</style> {0} <style=cStack>(-{1} per stack)</style>.");
				RegisterToken("ITEM_PRIMARYSKILLSHURIKEN_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("SHURIKEN_DAMAGE", "Ativar sua <style=cIsUtility>Habilidade Primária</style> também lança uma <style=cIsDamage>shuriken</style> que causa {0} de dano base.");
				RegisterFragment("SHURIKEN_COUNT", "\nÉ possível ter até {0} <style=cIsDamage>shurikens</style>.");
				RegisterFragment("SHURIKEN_RECHARGE", "\nGanhe uma <style=cIsDamage>shuriken</style> {0}.");
				RegisterFragment("SHURIKEN_RECHARGE_REDUCE", "\nGanhe uma <style=cIsDamage>shuriken</style> {0} <style=cStack>(-{1} por acúmulo)</style>.");
				RegisterFragment("SHURIKEN_RECHARGE_ALL", "\nRecarregue todas as <style=cIsDamage>shuriken</style> {0}.");
				RegisterFragment("SHURIKEN_RECHARGE_REDUCE_ALL", "\nRecarregue todas as <style=cIsDamage>shuriken</style> {0} <style=cStack>(-{1} por acúmulo)</style>.");
				RegisterToken("ITEM_PRIMARYSKILLSHURIKEN_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("SHURIKEN_DAMAGE"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);
			output += String.Format(
				TextFragment("SHURIKEN_COUNT"),
				ScalingText(BaseCount.Value, StackCount.Value, "flat", "cIsDamage")
			);

			if (ReloadAll.Value)
			{
				if (StackReduction.Value == 0f)
				{
					output += String.Format(
						TextFragment("SHURIKEN_RECHARGE_ALL"),
						SecondText(BaseRecharge.Value, "over", "cIsUtility")
					);
				}
				else
				{
					output += String.Format(
						TextFragment("SHURIKEN_RECHARGE_REDUCE_ALL"),
						SecondText(BaseRecharge.Value, "over", "cIsUtility"),
						ScalingText(StackReduction.Value, "percent")
					);
				}
			}
			else
			{
				if (StackReduction.Value == 0f)
				{
					output += String.Format(
						TextFragment("SHURIKEN_RECHARGE"),
						SecondText(BaseRecharge.Value, "every", "cIsUtility")
					);
				}
				else
				{
					output += String.Format(
						TextFragment("SHURIKEN_RECHARGE_REDUCE"),
						SecondText(BaseRecharge.Value, "every", "cIsUtility"),
						ScalingText(StackReduction.Value, "percent")
					);
				}
			}

			return output;
		}



		private static void DamageHook()
		{
			IL.RoR2.PrimarySkillShurikenBehavior.FireShuriken += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(3f),
					x => x.MatchLdcR4(1f)
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Pop);
					c.EmitDelegate<Func<float>>(() =>
					{
						return BaseDamage.Value - StackDamage.Value;
					});
					c.EmitDelegate<Func<float>>(() =>
					{
						return StackDamage.Value;
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed!");
				}
			};
		}

		private static void StackHook()
		{
			On.RoR2.PrimarySkillShurikenBehavior.FixedUpdate += (orig, self) =>
			{
				int shurikenCount = BaseCount.Value + StackCount.Value * (self.stack - 1);
				if (self.body.GetBuffCount(DLC1Content.Buffs.PrimarySkillShurikenBuff) < shurikenCount)
				{
					float cooldown = BaseRecharge.Value * Mathf.Pow(1f - StackReduction.Value, self.stack - 1);
					if (ReloadAll.Value)
					{
						cooldown /= shurikenCount;
					}

					self.reloadTimer += Time.fixedDeltaTime;
					while (self.reloadTimer > cooldown && self.body.GetBuffCount(DLC1Content.Buffs.PrimarySkillShurikenBuff) < shurikenCount)
					{
						self.body.AddBuff(DLC1Content.Buffs.PrimarySkillShurikenBuff);
						self.reloadTimer -= cooldown;
					}
				}
			};
		}
	}
}
