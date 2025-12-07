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
	public static class ShatteringJustice
	{
		public static GameObject PulverizedEffectPrefab;

		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "ShatteringJustice";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> ArmorReduction { get; set; }
		public static ConfigEntry<float> BaseDamage { get; set; }
		public static ConfigEntry<float> StackDamage { get; set; }
		public static ConfigEntry<float> FullEffectThreshold { get; set; }
		public static ConfigEntry<float> DebuffDuration { get; set; }



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
			ArmorReduction = ConfigEntry(
				itemIdentifier, "ArmorReduction", 20f,
				"Armor reduction of pulverize debuff."
			);
			BaseDamage = ConfigEntry(
				itemIdentifier, "BaseDamage", 0.5f,
				"Damage taken increase at full effect against pulverized enemies."
			);
			StackDamage = ConfigEntry(
				itemIdentifier, "StackDamage", 0.25f,
				"Damage taken increase at full effect against pulverized enemies per stack."
			);
			FullEffectThreshold = ConfigEntry(
				itemIdentifier, "FullEffectThreshold", 0.25f,
				"Health threshold for maximum effect on target."
			);
			FullEffectThreshold.Value = Mathf.Min(0.75f, Mathf.Abs(FullEffectThreshold.Value));
			DebuffDuration = ConfigEntry(
				itemIdentifier, "DebuffDuration", 8f,
				"Duration of pulverize debuff."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			PulverizedEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/PulverizedEffect");

			PulverizeApplicationHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("PULVERIZE_ON_HIT", "Attacks <style=cIsDamage>pulverize</style> on hit {0}, reducing <style=cIsDamage>armor</style> by {1}.");
				RegisterFragment("PULVERIZE_DAMAGE", "\nDeal up to an additional {0} <style=cIsDamage>damage</style> against pulverized enemies, with maximum effect when target has <style=cIsHealth>{1} health</style> remaining.");
				RegisterToken("ITEM_ARMORREDUCTIONONHIT_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("PULVERIZE_ON_HIT", "Ataques <style=cIsDamage>pulverizam</style> ao golpear {0}, reduzindo a <style=cIsDamage>armadura</style> em {1}.");
				RegisterFragment("PULVERIZE_DAMAGE", "\nCausa até um adicional de {0} de <style=cIsDamage>dano</style> contra inimigos pulverizados, com efeito máximo quando o alvo tem <style=cIsHealth>{1} de saúde</style> restante.");
				RegisterToken("ITEM_ARMORREDUCTIONONHIT_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string output = String.Format(
				TextFragment("PULVERIZE_ON_HIT"),
				SecondText(DebuffDuration.Value, "for"),
				ScalingText(ArmorReduction.Value, "flat", "cIsDamage")
			);
			output += String.Format(
				TextFragment("PULVERIZE_DAMAGE"),
				ScalingText(BaseDamage.Value, StackDamage.Value, "percent", "cIsDamage"),
				ScalingText(FullEffectThreshold.Value, "percent")
			);

			return output;
		}



		private static void PulverizeApplicationHook()
		{
			IL.RoR2.HealthComponent.TakeDamageProcess += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdsfld("RoR2.RoR2Content/Items", "ArmorReductionOnHit"),
					x => x.MatchCallOrCallvirt<Inventory>("GetItemCountEffective")
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Action<int, HealthComponent>>((count, self) =>
					{
						if (count > 0)
						{
							CharacterBody body = self.body;
							if (body)
							{
								if (!body.HasBuff(RoR2Content.Buffs.Pulverized))
								{
									EffectManager.SpawnEffect(PulverizedEffectPrefab, new EffectData
									{
										origin = self.body.corePosition,
										scale = self.body.radius
									}, true);

									body.AddTimedBuff(RoR2Content.Buffs.Pulverized, DebuffDuration.Value);
								}
							}
						}
					});
					c.Emit(OpCodes.Ldc_I4, 0);
				}
				else
				{
					LogWarn(itemIdentifier + " :: PulverizeApplicationHook Failed!");
				}
			};
		}
	}
}
