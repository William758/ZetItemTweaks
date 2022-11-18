using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Configuration;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class FrostRelic
	{
		internal static BuffDef IndicatorBuff;

		public static List<string> autoCompatList = new List<string> { "com.RiskyLives.RiskyMod", "Hayaku.VanillaRebalance" };

		public static string itemIdentifier = "FrostRelic";
		public static bool appliedChanges = false;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> OverrideText { get; set; }
		public static ConfigEntry<bool> IcicleIndicator { get; set; }
		public static ConfigEntry<float> IcicleDuration { get; set; }
		public static ConfigEntry<int> BaseIcicle { get; set; }
		public static ConfigEntry<int> StackIcicle { get; set; }
		public static ConfigEntry<float> StormRadius { get; set; }
		public static ConfigEntry<float> IcicleRadius { get; set; }
		public static ConfigEntry<float> RadiusExponent { get; set; }
		public static ConfigEntry<float> StormDamage { get; set; }
		public static ConfigEntry<float> IcicleDamage { get; set; }

		internal static void Init()
		{
			SetupConfig();

			if (EnableChanges.Value > 0)
			{
				OnBuffCatalogPreInit += ModifyBuff;
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
			IcicleIndicator = ConfigEntry(
				itemIdentifier, "IcicleIndicator", true,
				"Display icicle count as a buff."
			);
			IcicleDuration = ConfigEntry(
				itemIdentifier, "IcicleDuration", 8f,
				"Duration of icicles."
			);
			BaseIcicle = ConfigEntry(
				itemIdentifier, "BaseIcicle", 6,
				"Maximum icicle count gained from item."
			);
			StackIcicle = ConfigEntry(
				itemIdentifier, "StackIcicle", 3,
				"Maximum icicle count gained from item per stack."
			);
			StormRadius = ConfigEntry(
				itemIdentifier, "StormRadius", 6f,
				"Base radius of storm."
			);
			IcicleRadius = ConfigEntry(
				itemIdentifier, "IcicleRadius", 2f,
				"Radius added to storm per icicle."
			);
			RadiusExponent = ConfigEntry(
				itemIdentifier, "RadiusExponent", 0.65f,
				"Exponent modifier on storm radius. Default settings [StormRadius] => ([6m] + (2m * 6 icicles)) / [6m] = 3, Pow(3, 0.65) = 2.04 base radius at 6 icicles."
			);
			StormDamage = ConfigEntry(
				itemIdentifier, "StormDamage", 6f,
				"Damage per second of storm."
			);
			IcicleDamage = ConfigEntry(
				itemIdentifier, "IcicleDamage", 2f,
				"Damage per second added to storm per icicle."
			);
		}

		private static void ModifyBuff()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList, Feedback.None)) return;

			if (!IcicleIndicator.Value) return;

			if (!PluginLoaded("com.Wolfo.WolfoQualityOfLife")) return;

			BuffDef buffDef = FindBuffDefPreCatalogInit("visual_FrostRelicGrowth");
			if (buffDef)
			{
				if (!buffDef.isHidden)
				{
					buffDef.isHidden = true;

					LogInfo(itemIdentifier + " :: Hiding buff : visual_FrostRelicGrowth");

					ModifiedBuffDefCount++;
				}
			}
		}

		private static void LateSetup()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, autoCompatList)) return;

			IcicleHook();
			RadiusHook();
			IndicatorHook();

			if (!GenerateOverrideText.Value || OverrideText.Value)
			{
				targetLanguage = "default";

				RegisterFragment("ICE_STORM", "Killing enemies surrounds you with an <style=cIsDamage>ice storm</style>.");
				RegisterFragment("ICE_STORM_GROW", "Killing enemies surrounds you with an <style=cIsDamage>ice storm</style> that <style=cIsDamage>grows with every kill</style>.");
				RegisterFragment("ICICLE_COUNT", "\nGain up to a maximum of {0} icicles.");
				RegisterFragment("ICICLE_RADIUS", "\nStorm radius of {0}.");
				RegisterFragment("ICICLE_RADIUS_GROW", "\nStorm radius of {0} <style=cStack>(+{1} per icicle)</style>.");
				RegisterFragment("ICICLE_DAMAGE", "\nThe storm deals <style=cIsDamage>{0} damage per second</style> and <style=cIsUtility>slows</style> enemies by <style=cIsUtility>80%</style>.");
				RegisterFragment("ICICLE_SCALING", "{0}% <style=cStack>(+{1}% per icicle)</style>");
				RegisterToken("ITEM_ICICLE_DESC", DescriptionText());

				targetLanguage = "pt-BR";

				RegisterFragment("ICE_STORM", "Abater um inimigo cria ao seu redor uma <style=cIsDamage>tempestade de gelo</style>.");
				RegisterFragment("ICE_STORM_GROW", "Abater um inimigo cria ao seu redor uma <style=cIsDamage>tempestade de gelo</style> que <style=cIsDamage>aumenta a cada abate</style>.");
				RegisterFragment("ICICLE_COUNT", "\nGanhe até um máximo de {0} pingentes de gelo.");
				RegisterFragment("ICICLE_RADIUS", "\nRaio da tempestade de {0}.");
				RegisterFragment("ICICLE_RADIUS_GROW", "\nRaio da tempestade de {0} <style=cStack>(+{1} por pingente de gelo)</style>.");
				RegisterFragment("ICICLE_DAMAGE", "\nA tempestade causa <style=cIsDamage>{0} de dano por segundo</style> e causa <style=cIsUtility>lentidão</style> nos inimigos em <style=cIsUtility>80%</style>.");
				RegisterFragment("ICICLE_SCALING", "{0}% <style=cStack>(+{1}% por pingente de gelo)</style>");
				RegisterToken("ITEM_ICICLE_DESC", DescriptionText());

				targetLanguage = "";
			}

			appliedChanges = true;
		}

		private static string DescriptionText()
		{
			bool grows = IcicleRadius.Value > 0f;

			string output;
			if (grows)
			{
				output = TextFragment("ICE_STORM_GROW");
			}
			else
			{
				output = TextFragment("ICE_STORM");
			}
			output += String.Format(
				TextFragment("ICICLE_COUNT"),
				ScalingText(BaseIcicle.Value, StackIcicle.Value, "flat", "cIsDamage")
			);
			if (grows)
			{
				output += String.Format(
					TextFragment("ICICLE_RADIUS_GROW"),
					ScalingText(StormRadius.Value, "distance", "cIsDamage"),
					ScalingText(Mathf.Round((IcicleRadius.Value / StormRadius.Value) * Mathf.Clamp(RadiusExponent.Value, 0.1f, 1f) * 1000f) / 1000f, "percent")
				);
			}
			else
			{
				output += String.Format(
					TextFragment("ICICLE_RADIUS"),
					ScalingText(StormRadius.Value, "distance", "cIsDamage")
				);
			}
			string scalingText;
			if (IcicleDamage.Value > 0)
			{
				scalingText = String.Format(
					TextFragment("ICICLE_SCALING"),
					StormDamage.Value * 100f,
					IcicleDamage.Value * 100f
				);
			}
			else
			{
				scalingText = ScalingText(StormDamage.Value, "percent");
			}
			output += String.Format(
				TextFragment("ICICLE_DAMAGE"),
				scalingText
			);

			return output;
		}



		private static void IcicleHook()
		{
			On.RoR2.IcicleAuraController.Awake += (orig, self) =>
			{
				orig(self);
				/*
				LogInfo(itemIdentifier + " interval : " + self.baseIcicleAttackInterval);//0.25
				LogInfo(itemIdentifier + " baseRadius : " + self.icicleBaseRadius);//6
				LogInfo(itemIdentifier + " icicleRadius : " + self.icicleRadiusPerIcicle);//2
				LogInfo(itemIdentifier + " baseDamagePerTick : " + self.icicleDamageCoefficientPerTick);//3
				LogInfo(itemIdentifier + " icicleDamagePerTick : " + self.icicleDamageCoefficientPerTickPerIcicle);//0
				LogInfo(itemIdentifier + " duration : " + self.icicleDuration);//5
				LogInfo(itemIdentifier + " baseIcicleCount : " + self.baseIcicleMax);//6
				LogInfo(itemIdentifier + " StackIcicleCount : " + self.icicleMaxPerStack);//6
				*/
				self.baseIcicleMax = BaseIcicle.Value;
				self.icicleMaxPerStack = StackIcicle.Value;

				self.icicleDuration = IcicleDuration.Value;

				self.icicleBaseRadius = StormRadius.Value;
				self.icicleRadiusPerIcicle = Mathf.Max(0f, IcicleRadius.Value);

				float interval = self.baseIcicleAttackInterval;
				self.icicleDamageCoefficientPerTick = StormDamage.Value * interval;
				self.icicleDamageCoefficientPerTickPerIcicle = IcicleDamage.Value * interval;
			};
		}

		private static void RadiusHook()
		{
			On.RoR2.IcicleAuraController.UpdateRadius += (orig, self) =>
			{
				if (self.owner)
				{
					if (self.finalIcicleCount > 0)
					{
						float radius = 0f;

						CharacterBody body = self.cachedOwnerInfo.characterBody;
						if (body)
						{
							float baseRadius = self.icicleBaseRadius;
							radius = baseRadius + self.icicleRadiusPerIcicle * self.finalIcicleCount;

							float exponent = Mathf.Max(0.1f, RadiusExponent.Value);
							if (exponent < 1f)
							{
								radius /= baseRadius;
								radius = Mathf.Pow(radius, exponent);
								radius *= baseRadius;
							}

							radius += body.radius;

							self.actualRadius = radius;
						}

						return;
					}

					self.actualRadius = 0f;
				}
			};
		}

		private static void IndicatorHook()
		{
			On.RoR2.IcicleAuraController.OnDestroy += (orig, self) =>
			{
				orig(self);

				if (NetworkServer.active)
				{
					CharacterBody body = self.cachedOwnerInfo.characterBody;
					if (body)
					{
						body.SetBuffCount(IndicatorBuff.buffIndex, 0);
					}
				}
			};

			On.RoR2.IcicleAuraController.FixedUpdate += (orig, self) =>
			{
				orig(self);

				if (NetworkServer.active && IcicleIndicator.Value)
				{
					CharacterBody body = self.cachedOwnerInfo.characterBody;
					if (body)
					{
						int icicleCount = self.finalIcicleCount;
						int buffCount = body.GetBuffCount(IndicatorBuff);

						if (buffCount != icicleCount)
						{
							body.SetBuffCount(IndicatorBuff.buffIndex, icicleCount);
						}
					}
				}
			};
		}
	}
}
