using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class PlasmaShrimp
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod" };

		public static string itemIdentifier = "PlasmaShrimp";
		public static bool appliedChanges = false;

		private static int ItemCountLocIndex = 0;
		private static int StlocCursorIndex = 0;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseHealthAsShield { get; set; }
		public static ConfigEntry<float> StackHealthAsShield { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }



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
			BaseHealthAsShield = ConfigEntry(
				itemIdentifier, "BaseHealthAsShield", 0.1f,
				"Health gained as shield from item."
			);
			StackHealthAsShield = ConfigEntry(
				itemIdentifier, "StackHealthAsShield", 0f,
				"Health gained as shield from item per stack."
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 0.4f,
				"Missile damage gained from item."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0.25f,
				"Missile damage gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			FindIndexHook();

			if (ItemCountLocIndex != 0)
			{
				DamageHook();
			}
			else
			{
				LogWarn(itemIdentifier + " :: LateSetup Failed!");
				return;
			}

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("PLASMA_MISSILE", "\nWhile you have <style=cIsHealing>shield</style>, hitting an enemy fires a <style=cIsDamage>missile</style> that deals {0} TOTAL damage.");
				RegisterFragment("PLASMASHRIMP_CORRUPTION", "\n<style=cIsVoid>Corrupts all AtG Missile Mk. 1s</style>.");
				RegisterToken("ITEM_MISSILEVOID_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("PLASMA_MISSILE", "\nEnquanto possuir um <style=cIsHealing>escudo</style>, golpear um inimigo dispara um <style=cIsDamage>míssil</style> que causa {0} de dano TOTAL.");
				RegisterFragment("PLASMASHRIMP_CORRUPTION", "\n<style=cIsVoid>Corrompe todos os Mísseis ASM Mk. 1s</style>.");
				RegisterToken("ITEM_MISSILEVOID_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseHealthAsShield.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_HEALTH_EXTRA_SHIELD", true),
					ScalingText(BaseHealthAsShield.Value, StackHealthAsShield.Value, "percent", "cIsHealing")
				);
			}

			if (output != "") output += "\n";

			output += String.Format(
				TextFragment("PLASMA_MISSILE", true),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);

			output += TextFragment("PLASMASHRIMP_CORRUPTION");

			return output;
		}



		private static void FindIndexHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(DLC1Content.Items).GetField("MissileVoid")),
					x => x.MatchCallOrCallvirt<Inventory>("GetItemCount"),
					x => x.MatchStloc(out ItemCountLocIndex)
				);

				if (found)
				{
					StlocCursorIndex = c.Index;
				}
				else
				{
					LogWarn(itemIdentifier + " :: FindIndexHook Failed!");
				}
			};
		}

		private static void DamageHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(ItemCountLocIndex),
					x => x.MatchConvR4(),
					x => x.MatchMul(),
					x => x.MatchStloc(out _)
				);

				if (found)
				{
					c.Index += 3;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, ItemCountLocIndex);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseDamage.Value + StackDamage.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed!");
				}
			};
		}
	}
}
