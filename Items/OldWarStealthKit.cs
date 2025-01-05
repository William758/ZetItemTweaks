using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class OldWarStealthKit
	{
		private static FieldInfo DamageInfoRejectedField;
		private static FieldInfo PhasingBuffDurationField;
		private static EffectDef RejectTextDef;
		private static ArtifactIndex DiluvianArtifactIndex = ArtifactIndex.None;
		internal static BuffDef EvadeBuff;

		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "com.kking117.FlatItemBuff", "noodlegemo.SeekingItemReworks" };

		public static string itemIdentifier = "OldWarStealthKit";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<float> BaseAvoidanceChance { get; set; }
		public static ConfigEntry<float> StackAvoidanceChance { get; set; }
		public static ConfigEntry<float> BaseDuration { get; set; }
		public static ConfigEntry<float> StackDuration { get; set; }
		public static ConfigEntry<float> BaseRecharge { get; set; }
		public static ConfigEntry<float> StackReduction { get; set; }


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
			BaseAvoidanceChance = ConfigEntry(
				itemIdentifier, "BaseAvoidanceChance", 35f,
				"Damage avoidance chance while cloaked. 35 is 35%"
			);
			StackAvoidanceChance = ConfigEntry(
				itemIdentifier, "StackAvoidanceChance", 15f,
				"Damage avoidance chance while cloaked per stack."
			);
			BaseDuration = ConfigEntry(
				itemIdentifier, "BaseDuration", 5f,
				"Cloak duration."
			);
			StackDuration = ConfigEntry(
				itemIdentifier, "StackDuration", 0f,
				"Cloak duration per stack."
			);
			BaseRecharge = ConfigEntry(
				itemIdentifier, "BaseRecharge", 20f,
				"Recharge interval for cloak availability."
			);
			StackReduction = ConfigEntry(
				itemIdentifier, "StackReduction", 0.25f,
				"Recharge interval reduction per stack."
			);
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			DamageInfoRejectedField = typeof(DamageInfo).GetField("rejected");
			PhasingBuffDurationField = typeof(RoR2.Items.PhasingBodyBehavior).GetField("buffDuration", BindingFlags.Instance | BindingFlags.NonPublic);

			GameObject RejectTextPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/DamageRejected");
			EffectIndex effectIndex = EffectCatalog.FindEffectIndexFromPrefab(RejectTextPrefab);
			RejectTextDef = EffectCatalog.GetEffectDef(effectIndex);

			DiluvianArtifactIndex = ArtifactCatalog.FindArtifactIndex("ARTIFACT_DILUVIFACT");

			RechargeTimeHook();
			BuffDurationHook();
			DodgeHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("STEALTHKIT_ACTIVATION", "Falling below <style=cIsHealth>25%</style> health</style> {0} for {1}.");
				RegisterFragment("STEALTHKIT_EFFECT", "causes you to gain <style=cIsUtility>40% movement speed</style> and <style=cIsUtility>invisibility</style>");
				RegisterFragment("STEALTHKIT_DODGE", "causes you to gain {0} chance to <style=cIsHealing>dodge</style>, <style=cIsUtility>40% movement speed</style>, and <style=cIsUtility>invisibility</style>");
				RegisterFragment("STEALTHKIT_RECHARGE", "\nRecharges {0}.");
				RegisterFragment("STEALTHKIT_RECHARGE_REDUCE", "\nRecharges {0} <style=cStack>(-{1} per stack)</style>.");
				RegisterToken("ITEM_PHASING_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("STEALTHKIT_ACTIVATION", "Atingir menos de <style=cIsHealth>25%</style> de saúde</style> {0} por {1}.");
				RegisterFragment("STEALTHKIT_EFFECT", "faz com que você ganhe <style=cIsUtility>40% de velocidade de movimento</style> e <style=cIsUtility>invisibilidade</style>");
				RegisterFragment("STEALTHKIT_DODGE", "faz com que você ganhe {0} de chance de <style=cIsHealing>desviar</style>, <style=cIsUtility>40% de velocidade de movimento</style>, e <style=cIsUtility>invisibilidade</style>");
				RegisterFragment("STEALTHKIT_RECHARGE", "\nRecarrega {0}.");
				RegisterFragment("STEALTHKIT_RECHARGE_REDUCE", "\nRecarrega {0} <style=cStack>(-{1} por acúmulo)</style>.");
				RegisterToken("ITEM_PHASING_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			string stealthEffects;
			if (BaseAvoidanceChance.Value > 0f)
			{
				stealthEffects = String.Format(
					TextFragment("STEALTHKIT_DODGE"),
					ScalingText(BaseAvoidanceChance.Value, StackAvoidanceChance.Value, "chance", "cIsHealing")
				);
			}
			else
			{
				stealthEffects = TextFragment("STEALTHKIT_EFFECT");
			}

			string output = String.Format(
				TextFragment("STEALTHKIT_ACTIVATION"),
				stealthEffects,
				ScalingText(BaseDuration.Value, StackDuration.Value, "duration", "cIsUtility")
			);

			if (StackReduction.Value == 0f)
			{
				output += String.Format(
					TextFragment("STEALTHKIT_RECHARGE"),
					SecondText(BaseRecharge.Value, "every", "cIsUtility")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("STEALTHKIT_RECHARGE_REDUCE"),
					SecondText(BaseRecharge.Value, "every", "cIsUtility"),
					ScalingText(StackReduction.Value, "percent")
				);
			}

			return output;
		}



		private static void RechargeTimeHook()
		{
			On.RoR2.Items.PhasingBodyBehavior.Start += (orig, self) =>
			{
				self.baseRechargeSeconds = BaseRecharge.Value;
				self.rechargeReductionMultiplierPerStack = 1f - StackReduction.Value;

				orig(self);
			};
		}

		private static void BuffDurationHook()
		{
			IL.RoR2.Items.PhasingBodyBehavior.FixedUpdate += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdfld(PhasingBuffDurationField),
					x => x.MatchCallOrCallvirt<CharacterBody>("AddTimedBuff")
				);

				if (found)
				{
					c.Emit(OpCodes.Ldarg, 0);// used to store field after delegate
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<RoR2.Items.PhasingBodyBehavior, float>>((phasingBodyBehavior) =>
					{
						float duration = 5f;

						int count = phasingBodyBehavior.stack;
						if (count > 0)
						{
							duration = BaseDuration.Value + StackDuration.Value * (count - 1);
						}

						phasingBodyBehavior.body.AddTimedBuff(EvadeBuff, duration);

						return duration;
					});
					c.Emit(OpCodes.Stfld, PhasingBuffDurationField);
				}
				else
				{
					LogWarn(itemIdentifier + " :: BuffDurationHook Failed!");
				}
			};
		}

		private static void DodgeHook()
		{
			IL.RoR2.HealthComponent.TakeDamageProcess += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdfld<DamageInfo>("rejected"),
					x => x.MatchBrtrue(out _),
					x => x.MatchLdarg(0),
					x => x.MatchLdfld<HealthComponent>("body"),
					x => x.MatchLdsfld(typeof(JunkContent.Buffs).GetField("BodyArmor"))
				);

				if (found)
				{
					// loc.1 DamageInfo on stack
					c.Emit(OpCodes.Dup);
					c.Emit(OpCodes.Dup);
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<DamageInfo, HealthComponent, bool>>((damageInfo, healthComponent) =>
					{
						if (damageInfo.rejected) return true;

						if (BaseAvoidanceChance.Value > 0f)
						{
							CharacterBody body = healthComponent.body;
							if (body && body.HasBuff(EvadeBuff) && body.hasCloakBuff)
							{
								int count = 0;

								Inventory inventory = body.inventory;
								if (inventory)
								{
									count = inventory.GetItemCount(RoR2Content.Items.Phasing);
								}

								count = Mathf.Max(count, 1);
								float avoidChance = BaseAvoidanceChance.Value + StackAvoidanceChance.Value * (count - 1);
								avoidChance = Util.ConvertAmplificationPercentageIntoReductionPercentage(avoidChance);

								if (DiluvianArtifactIndex != ArtifactIndex.None && body.teamComponent.teamIndex == TeamIndex.Player)
								{
									RunArtifactManager artifactManager = RunArtifactManager.instance;
									if (artifactManager && artifactManager.IsArtifactEnabled(DiluvianArtifactIndex)) avoidChance *= 0.5f;
								}

								if (Util.CheckRoll(avoidChance, 0f, null))
								{
									EffectManager.SpawnEffect((EffectIndex)1758002, new EffectData { genericUInt = 1u, origin = damageInfo.position }, true);

									return true;
								}
							}
						}

						return false;
					});
					c.Emit(OpCodes.Stfld, DamageInfoRejectedField);
				}
				else
				{
					Debug.LogWarning(itemIdentifier + " :: DodgeHook Failed");
				}
			};
		}



		internal static void CreateMissText(EffectData effectData)
		{
			EffectDef effectDef = RejectTextDef;

			if (effectDef == null) return;

			if (!VFXBudget.CanAffordSpawn(effectDef.prefabVfxAttributes)) return;
			if (effectDef.cullMethod != null && !effectDef.cullMethod(effectData)) return;

			EffectData effectDataClone = effectData.Clone();
			GameObject gameObject = UnityEngine.Object.Instantiate(effectDef.prefab, effectDataClone.origin, effectDataClone.rotation);
			if (gameObject)
			{
				EffectComponent effectComponent = gameObject.GetComponent<EffectComponent>();
				if (effectComponent)
				{
					effectComponent.effectData = effectDataClone.Clone();

					Transform textTransform = effectComponent.transform.Find("TextMeshPro");
					if (textTransform)
					{
						TextMeshPro textMesh = textTransform.GetComponent<TextMeshPro>();
						if (textMesh)
						{
							LanguageTextMeshController controller = textMesh.gameObject.GetComponent<LanguageTextMeshController>();
							if (controller)
							{
								controller.token = "MISS";
								textMesh.text = "MISS";
								textMesh.fontSize = 1.75f;
							}
						}
					}
				}
			}
		}
	}
}
