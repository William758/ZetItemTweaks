using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class ShatterSpleen
	{
		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "Hayaku.VanillaRebalance", "com.NetherCrowCSOLYOO.ClassicCritAdd" };

		public static string itemIdentifier = "ShatterSpleen";
		public static bool appliedChanges = false;
		public static bool appliedBleedSeparation = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseCrit { get; set; }
		public static ConfigEntry<float> StackCrit { get; set; }
		public static ConfigEntry<bool> SeparateBleed { get; set; }
		public static ConfigEntry<float> BaseDotDamage { get; set; }
		public static ConfigEntry<float> StackDotDamage { get; set; }
		public static ConfigEntry<float> BaseExplodeAtkDamage { get; set; }
		public static ConfigEntry<float> StackExplodeAtkDamage { get; set; }
		public static ConfigEntry<float> BaseExplodeVicHealth { get; set; }
		public static ConfigEntry<float> StackExplodeVicHealth { get; set; }

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
			BaseCrit = ConfigEntry(
				itemIdentifier, "BaseCrit", 5f,
				"Critical strike chance gained from item."
			);
			StackCrit = ConfigEntry(
				itemIdentifier, "StackCrit", 0f,
				"Critical strike chance gained from item per stack."
			);
			SeparateBleed = ConfigEntry(
				itemIdentifier, "SeparateBleed", true,
				"Apply bleed independently from other sources."
			);
			BaseDotDamage = ConfigEntry(
				itemIdentifier, "BaseDotDamage", 1.2f,
				"DoT Damage of bleed from item. SeparateBleed required."
			);
			StackDotDamage = ConfigEntry(
				itemIdentifier, "StackDotDamage", 0f,
				"DoT Damage of bleed from item per stack. SeparateBleed required."
			);
			BaseExplodeAtkDamage = ConfigEntry(
				itemIdentifier, "BaseExplodeAtkDamage", 4f,
				"Explosion attacker damage gained from item."
			);
			StackExplodeAtkDamage = ConfigEntry(
				itemIdentifier, "StackAtkExplodeDamage", 4f,
				"Explosion attacker damage gained from item per stack."
			);
			BaseExplodeVicHealth = ConfigEntry(
				itemIdentifier, "BaseExplodeVicHealth", 0.1f,
				"Explosion victim health gained from item."
			);
			StackExplodeVicHealth = ConfigEntry(
				itemIdentifier, "StackExplodeVicHealth", 0f,
				"Explosion victim health gained from item per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			if (SeparateBleed.Value)
			{
				BleedSeparationHook();
				BleedOnHitHook();

				appliedBleedSeparation = true;
			}

			BleedExplodeHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("BLEED_ON_CRIT", "\nAttacks <style=cIsDamage>bleed</style> on <style=cIsDamage>critical strike</style> for {0} base damage.");
				RegisterFragment("BLEED_EXPLODE_BASE", "\n<style=cIsDamage>Bleeding</style> enemies <style=cIsDamage>explode</style> on death for {0} base damage.");
				RegisterFragment("BLEED_EXPLODE_HEALTH", "\n<style=cIsDamage>Bleeding</style> enemies <style=cIsDamage>explode</style> on death {0} of their maximum health.");
				RegisterFragment("BLEED_EXPLODE_BOTH", "\n<style=cIsDamage>Bleeding</style> enemies <style=cIsDamage>explode</style> on death for {0} base damage, plus an additional {1} of their maximum health.");
				RegisterFragment("CFG_NO_EXPLODE_EFFECT", "\n<style=cStack>(current configuration :: no explosion damage)</style>");
				RegisterToken("ITEM_BLEEDONHITANDEXPLODE_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("BLEED_ON_CRIT", "\nAtaques <style=cIsDamage>sangram</style> nos <style=cIsDamage>acertos críticos</style> em {0} de dano base.");
				RegisterFragment("BLEED_EXPLODE_BASE", "\nInimigos <style=cIsDamage>sangrando</style> <style=cIsDamage>explodem</style> ao abater em {0} de dano base.");
				RegisterFragment("BLEED_EXPLODE_HEALTH", "\nInimigos <style=cIsDamage>sangrando</style> <style=cIsDamage>explodem</style> ao abater {0} de sua saúde máxima.");
				RegisterFragment("BLEED_EXPLODE_BOTH", "\nInimigos <style=cIsDamage>sangrando</style> <style=cIsDamage>explodem</style> ao abater em {0} de dano base, mais um adicional de {1} de sua saúde máxima.");
				RegisterFragment("CFG_NO_EXPLODE_EFFECT", "\n<style=cStack>(configuração atual :: sem dano de explosão)</style>");
				RegisterToken("ITEM_BLEEDONHITANDEXPLODE_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = "";

			if (BaseCrit.Value > 0f)
			{
				output += String.Format(
					TextFragment("STAT_CRIT", true),
					ScalingText(BaseCrit.Value, StackCrit.Value, "chance", "cIsDamage")
				);
			}

			if (output != "") output += "\n";

			if (SeparateBleed.Value)
			{
				output += String.Format(
					TextFragment("BLEED_ON_CRIT", true),
					ScalingText(BaseDotDamage.Value, StackDotDamage.Value, "percent", "cIsDamage")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("BLEED_ON_CRIT", true),
					ScalingText(2.4f, "percent", "cIsDamage")
				);
			}

			if (BaseExplodeAtkDamage.Value > 0f)
			{
				if (BaseExplodeVicHealth.Value > 0f)
				{
					output += String.Format(
						TextFragment("BLEED_EXPLODE_BOTH"),
						ScalingText(BaseExplodeAtkDamage.Value, StackExplodeAtkDamage.Value, "percent", "cIsDamage"),
						ScalingText(BaseExplodeVicHealth.Value, StackExplodeVicHealth.Value, "percent", "cIsDamage")
					);
				}
				else
				{
					output += String.Format(
						TextFragment("BLEED_EXPLODE_BASE"),
						ScalingText(BaseExplodeAtkDamage.Value, StackExplodeAtkDamage.Value, "percent", "cIsDamage")
					);
				}
			}
			else
			{
				if (BaseExplodeVicHealth.Value > 0f)
				{
					output += String.Format(
						TextFragment("BLEED_EXPLODE_HEALTH"),
						ScalingText(BaseExplodeVicHealth.Value, StackExplodeVicHealth.Value, "percent", "cIsDamage")
					);
				}
				else
				{
					output += TextFragment("CFG_NO_EXPLODE_EFFECT");
				}
			}

			return output;
		}



		private static void BleedSeparationHook()
		{
			IL.RoR2.GlobalEventManager.OnHitEnemy += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdloc(5),
					x => x.MatchLdsfld(typeof(RoR2Content.Items).GetField("BleedOnHitAndExplode")),
					x => x.MatchCallOrCallvirt<Inventory>("GetItemCount")
				);

				if (found)
				{
					c.Index += 3;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldc_I4, 0);
				}
				else
				{
					LogWarn(itemIdentifier + " :: BleedSeparationHook Failed!");
				}
			};
		}

		private static void BleedOnHitHook()
		{
			On.RoR2.GlobalEventManager.OnHitEnemy += (orig, self, damageInfo, victimObject) =>
			{
				if (NetworkServer.active)
				{
					if (damageInfo.procCoefficient > 0.125f && damageInfo.crit && !damageInfo.rejected)
					{
						if (damageInfo.attacker)
						{
							CharacterBody attacker = damageInfo.attacker.GetComponent<CharacterBody>();
							CharacterBody victim = victimObject ? victimObject.GetComponent<CharacterBody>() : null;

							if (attacker && victim)
							{
								ApplyBleed(attacker, victim, damageInfo);
							}
						}
					}
				}

				orig(self, damageInfo, victimObject);
			};
		}

		private static void ApplyBleed(CharacterBody attacker, CharacterBody victim, DamageInfo damageInfo)
		{
			Inventory inventory = attacker.inventory;
			if (inventory)
			{
				int count = inventory.GetItemCount(RoR2Content.Items.BleedOnHitAndExplode);
				if (count > 0)
				{
					float damageCoefficient = (BaseDotDamage.Value + StackDotDamage.Value * (count - 1)) / 2.4f;
					DotController.InflictDot(victim.gameObject, attacker.gameObject, DotController.DotIndex.Bleed, 3f * damageInfo.procCoefficient, damageCoefficient);
				}
			}
		}

		private static void BleedExplodeHook()
		{
			IL.RoR2.GlobalEventManager.OnCharacterDeath += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(92)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldloc, 51);
					c.Emit(OpCodes.Ldloc, 15);
					c.Emit(OpCodes.Ldloc, 2);
					c.EmitDelegate<Func<int, CharacterBody, CharacterBody, float>>((count, attacker, victim) =>
					{
						float damage = 0f;

						damage += attacker.damage * (BaseExplodeAtkDamage.Value + StackExplodeAtkDamage.Value * (count - 1));
						damage += victim.maxHealth * (BaseExplodeVicHealth.Value + StackExplodeVicHealth.Value * (count - 1));

						return damage;
					});
					c.Emit(OpCodes.Stloc, 92);
				}
				else
				{
					LogWarn(itemIdentifier + " :: BleedExplodeHook Failed!");
				}
			};
		}
	}
}
