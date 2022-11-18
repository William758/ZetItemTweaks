using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2.Orbs;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class Polylute
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "Polylute";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> ProcChance { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<int> BaseCount { get; set; }
		public static ConfigEntry<int> StackCount { get; set; }



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
			ProcChance = ConfigEntry(
				itemIdentifier, "ProcChance", 25f,
				"Chance to proc item effect. 25 = 25%"
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 0.6f,
				"Lightning damage gained from item."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0f,
				"Lightning damage gained from item per stack."
			);
			BaseCount = ConfigEntry(
				itemIdentifier, "BaseCount", 3,
				"Lightning strikes against target."
			);
			StackCount = ConfigEntry(
				itemIdentifier, "StackCount", 2,
				"Lightning strikes against target per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			ProcChanceHook();
			DamageHook();
			StrikeCountHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("POLYLUTE", "{0} chance to fire <style=cIsDamage>lightning</style> for {1} TOTAL damage up to {2} times.");
				RegisterFragment("POLYLUTE_CORRUPTION", "\n<style=cIsVoid>Corrupts all Ukuleles</style>.");
				RegisterToken("ITEM_CHAINLIGHTNINGVOID_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("POLYLUTE", "{0} de chande de disparar um <style=cIsDamage>raio</style> e causar {1} de dano TOTAL em até {2} vezes.");
				RegisterFragment("POLYLUTE_CORRUPTION", "\n<style=cIsVoid>Corrompe todos os Ukeleles</style>.");
				RegisterToken("ITEM_CHAINLIGHTNINGVOID_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("POLYLUTE"),
				ScalingText(ProcChance.Value, "chance", "cIsDamage"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage"),
				ScalingText(BaseCount.Value, StackCount.Value, "flat", "cIsDamage")
			);

			output += TextFragment("POLYLUTE_CORRUPTION");

			return output;
		}



		private static void ProcChanceHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(25f),
					x => x.MatchStloc(56)
				);

				if (found)
				{
					c.Index += 2;

					c.EmitDelegate<Func<float>>(() =>
					{
						return ProcChance.Value;
					});
					c.Emit(OpCodes.Stloc, 56);
				}
				else
				{
					LogWarn(itemIdentifier + " :: ProcChanceHook Failed!");
				}
			};
		}

		private static void DamageHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdcR4(0.6f),
					x => x.MatchStloc(57)
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Ldloc, 55);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseDamage.Value + StackDamage.Value * (count - 1);
					});
					c.Emit(OpCodes.Stloc, 57);
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed!");
				}
			};
		}

		private static void StrikeCountHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStfld<VoidLightningOrb>("totalStrikes")
				);

				if (found)
				{
					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, 55);
					c.EmitDelegate<Func<int, int>>((count) =>
					{
						return BaseCount.Value + StackCount.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: StrikeCountHook Failed!");
				}
			};
		}
	}
}
