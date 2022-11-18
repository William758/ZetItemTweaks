using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class RenaldsBand
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod" };

		public static string itemIdentifier = "RenaldsBand";
		public static bool appliedChanges = false;
		public static bool appliedChillDurationHook = false;

		private static int ItemCountLocIndex = 0;
		private static int StlocCursorIndex = 0;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<float> BaseSlowDuration { get; set; }
		public static ConfigEntry<float> StackSlowDuration { get; set; }



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
				itemIdentifier, "BaseDamage", 2.4f,
				"Ice blast damage gained from item."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 1.6f,
				"Ice blast damage gained from item per stack."
			);
			BaseSlowDuration = ConfigEntry(
				itemIdentifier, "BaseSlowDuration", 4f,
				"Chill duration from ice blast."
			);
			StackSlowDuration = ConfigEntry(
				itemIdentifier, "StackSlowDuration", 0f,
				"Chill duration from ice blast per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			FindIndexHook();

			if (ItemCountLocIndex != 0)
			{
				DamageHook();
				ChillDurationHook();
			}
			else
			{
				LogWarn(itemIdentifier + " :: LateSetup Failed!");
				return;
			}

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("ICE_BLAST_TRIGGER", "Hits that deal <style=cIsDamage>more than 400% damage</style> also blasts enemies with a <style=cIsDamage>runic ice blast</style>,");
				RegisterFragment("ICE_BLAST_SLOW", " <style=cIsUtility>slowing</style> them by <style=cIsUtility>80%</style> for {0}");
				RegisterFragment("ICE_BLAST_DAMAGE", " and dealing {0} TOTAL damage. Recharges every <style=cIsUtility>10</style> seconds.");
				RegisterToken("ITEM_ICERING_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("ICE_BLAST_TRIGGER", "Golpes que causam <style=cIsDamage>mais do que 400% de dano</style> também atingem inimigos com uma <style=cIsDamage>explosão de gelo rúnico</style>,");
				RegisterFragment("ICE_BLAST_SLOW", " <style=cIsUtility>desacelerando-os</style> em <style=cIsUtility>80%</style> por {0}");
				RegisterFragment("ICE_BLAST_DAMAGE", " e causando {0} de dano TOTAL. Recarrega a cada <style=cIsUtility>10</style> segundos.");
				RegisterToken("ITEM_ICERING_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = TextFragment("ICE_BLAST_TRIGGER");
			if (appliedChillDurationHook)
			{
				output += String.Format(
					TextFragment("ICE_BLAST_SLOW"),
					ScalingText(BaseSlowDuration.Value, StackSlowDuration.Value, "duration", "cIsUtility")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("ICE_BLAST_SLOW"),
					ScalingText(3f, 3f, "duration", "cIsUtility")
				);
			}
			output += String.Format(
				TextFragment("ICE_BLAST_DAMAGE"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);

			return output;
		}



		private static void FindIndexHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Items).GetField("IceRing")),
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
					x => x.MatchLdcI4(0),
					x => x.MatchBle(out _)
				);

				if (found)
				{
					found = c.TryGotoNext(
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
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed! - Could not find count > 0.");
				}
			};
		}

		private static void ChillDurationHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.Index = StlocCursorIndex;

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld(typeof(RoR2Content.Buffs).GetField("Slow80")),
					x => x.MatchLdcR4(3f),
					x => x.MatchLdloc(ItemCountLocIndex),
					x => x.MatchConvR4(),
					x => x.MatchMul(),
					x => x.MatchCallOrCallvirt<CharacterBody>("AddTimedBuff")
				);

				if (found)
				{
					appliedChillDurationHook = true;

					c.Index += 5;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldloc, ItemCountLocIndex);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseSlowDuration.Value + StackSlowDuration.Value * (count - 1);
					});
				}
				else
				{
					LogWarn(itemIdentifier + " :: ChillDurationHook Failed!");
				}
			};
		}
	}
}
