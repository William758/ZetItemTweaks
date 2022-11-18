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
	public static class BrilliantBehemoth
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "BrilliantBehemoth";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<int> DamageFalloff { get; set; }
		public static ConfigEntry<float> BaseRadius { get; set; }
		public static ConfigEntry<float> StackRadius { get; set; }



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
				itemIdentifier, "BaseDamage", 0.6f,
				"Explosion damage."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0.2f,
				"Explosion damage per stack."
			);
			DamageFalloff = ConfigEntry(
				itemIdentifier, "DamageFalloff", 1,
				"Explosion damage falloff. 0 = None, 1 = Linear, 2 = SweetSpot."
			);
			BaseRadius = ConfigEntry(
				itemIdentifier, "BaseRadius", 5f,
				"Explosion radius."
			);
			StackRadius = ConfigEntry(
				itemIdentifier, "StackRadius", 2.5f,
				"Explosion radius per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			RadiusHook();
			DamageHook();
			FalloffHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default"; 
				
				RegisterFragment("EXPLODE_ON_HIT", "Attacks <style=cIsDamage>explode</style> on hit in a {0} radius for {1} TOTAL damage.");
				RegisterToken("ITEM_BEHEMOTH_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("EXPLODE_ON_HIT", "Ataques <style=cIsDamage>explodem</style> ao golpear em uma area de {0} que causa {1} de dano TOTAL.");
				RegisterToken("ITEM_BEHEMOTH_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("EXPLODE_ON_HIT"),
				ScalingText(BaseRadius.Value, StackRadius.Value, "distance", "cIsDamage"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage")
			);

			return output;
		}



		private static void RadiusHook()
		{
			IL.RoR2.GlobalEventManager.OnHitAll += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(4)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldarg, 1);
					c.Emit(OpCodes.Ldloc, 3);
					c.EmitDelegate<Func<DamageInfo, int, float>>((damageInfo, count) =>
					{
						float proc = Mathf.Max(0.25f, damageInfo.procCoefficient);

						return (BaseRadius.Value + StackRadius.Value * (count - 1)) * proc;
					});
					c.Emit(OpCodes.Stloc, 4);
				}
				else
				{
					LogWarn(itemIdentifier + " :: RadiusHook Failed!");
				}
			};
		}

		private static void DamageHook()
		{
			IL.RoR2.GlobalEventManager.OnHitAll += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(5)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldloc, 3);
					c.EmitDelegate<Func<int, float>>((count) =>
					{
						return BaseDamage.Value + StackDamage.Value * (count - 1);
					});
					c.Emit(OpCodes.Stloc, 5);
				}
				else
				{
					LogWarn(itemIdentifier + " :: DamageHook Failed!");
				}
			};
		}

		private static void FalloffHook()
		{
			IL.RoR2.GlobalEventManager.OnHitAll += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(4),
					x => x.MatchStfld(typeof(BlastAttack).GetField("radius"))
				);

				if (found)
				{
					int matchIndex = c.Index;

					found = c.TryGotoNext(
						x => x.MatchCallOrCallvirt(typeof(BlastAttack).GetMethod("Fire"))
					);

					if (found)
					{
						int offset = c.Index - matchIndex;

						if (offset <= 50)
						{
							c.EmitDelegate<Func<BlastAttack, BlastAttack>>((blastAttack) =>
							{
								int falloff = DamageFalloff.Value;
								if (falloff > 0 && falloff < 3)
								{
									blastAttack.falloffModel = (BlastAttack.FalloffModel)falloff;
								}

								return blastAttack;
							});
						}
						else
						{
							LogWarn(itemIdentifier + " :: FalloffHook Failed! - BlastAttack.Fire Offset [" + offset + "]");
						}
					}
					else
					{
						LogWarn(itemIdentifier + " :: FalloffHook Failed!");
					}
				}
				else
				{
					LogWarn(itemIdentifier + " :: FalloffHook:FindRadius Failed!");
				}
			};
		}
	}
}
