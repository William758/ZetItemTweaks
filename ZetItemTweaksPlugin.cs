using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2;
using RoR2.ContentManagement;

using System.Security;
using System.Security.Permissions;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace TPDespair.ZetItemTweaks
{
	[BepInPlugin(ModGuid, ModName, ModVer)]

	public class ZetItemTweaksPlugin : BaseUnityPlugin
	{
		public const string ModVer = "1.2.0";
		public const string ModName = "ZetItemTweaks";
		public const string ModGuid = "com.TPDespair.ZetItemTweaks";

		public static Dictionary<string, string> LangTokens = new Dictionary<string, string>();
		public static Dictionary<string, string> Fragments = new Dictionary<string, string>();

		public static List<string> ConfigKeys = new List<string>();

		public static ConfigFile configFile;
		public static ManualLogSource logSource;

		public static ConfigEntry<bool> EnableAutoCompat { get; set; }
		public static ConfigEntry<bool> ExplicitEnable { get; set; }
		public static ConfigEntry<bool> GenerateOverrideText { get; set; }

		internal static string SectionEnableDesc = "Enable item changes. 0 = Disabled, 1 = AutoCompat, 2 = Force Enabled";

		public static Action OnItemCatalogPreInit;
		public static Action OnLateSetup;

		internal static string LastPluginChecked = "";
		internal static string SetupPhase = "";
		internal static int ConfigCount = 0;
		internal static int ModifyCount = 0;
		internal static int TweakCount = 0;



		public static AssetBundle Assets;



		public void Awake()
		{
			RoR2Application.isModded = true;
			NetworkModCompatibilityHelper.networkModList = NetworkModCompatibilityHelper.networkModList.Append(ModGuid + ":" + ModVer);

			configFile = Config;
			logSource = Logger;

			SetupConfig();

			On.RoR2.ItemCatalog.Init += ItemCatalogInit;
			RoR2Application.onLoad += LateSetup;

			ContentManager.collectContentPackProviders += ContentManager_collectContentPackProviders;

			EffectManagerHook();

			SetupFragments();

			StatHooks.Init();

			// T1 Items
			//RepulsionArmorPlate.Init();
			//TopazBrooch.Init();
			//TritipDagger.Init();
			LensMakersGlasses.Init();
			//BundleofFireworks.Init();
			BisonSteak.Init();
			CautiousSlug.Init();
			//Gasoline.Init();
			//BustlingFungus.Init();
			FocusCrystal.Init();
			PersonalShieldGenerator.Init();
			//StickyBomb.Init();

			// T1 VoidItems
			//NeedleTick.Init();
			//LostSeersLenses.Init();
			WeepingFungus.Init();

			// T1 Modded Items
			ScratchTicket.Init();

			// T2 Items
			PredatoryInstincts.Init();
			GhorsTome.Init();
			Ukulele.Init();
			DeathMark.Init();
			WarHorn.Init();
			////WillotheWisp.Init();
			KjarosBand.Init();
			HarvestersScythe.Init();
			RenaldsBand.Init();
			AtgMissileMk1.Init();
			HuntersHarpoon.Init();
			OldWarStealthKit.Init();
			Shuriken.Init();
			LeechingSeed.Init();
			ChronoBauble.Init();
			RoseBuckler.Init();
			RedWhip.Init();
			LeptonDaisy.Init();
			RazorWire.Init();
			BerzerkersPauldron.Init();

			// T2 Void Items
			Polylute.Init();
			////VoidsentFlame.Init();
			PlasmaShrimp.Init();

			// T2 Modded Items
			BlackMonolith.Init();
			//EngineersToolbelt.Init();
			//FaultySpotter.Init();

			// T3 Items
			AlienHead.Init();
			ShatteringJustice.Init();
			Aegis.Init();
			BrilliantBehemoth.Init();
			DefensiveMicrobots.Init();
			LaserScope.Init();
			FrostRelic.Init();
			BensRainCoat.Init();
			BrainStalks.Init();
			NkuhanasOpinion.Init();
			UnstableTeslaCoil.Init();

			// Boss Items
			//QueensGland.Init();
			ShatterSpleen.Init();
			TitanicKnurl.Init();
			//DefenseNucleus.Init();
			//GenesisLoop();
			Planula.Init();
			Pearl.Init();
			IrradiantPearl.Init();
			LittleDisciple.Init();

			LanguageOverride();
		}
		/*
		public void Update()
		{
			DebugDrops();
		}
		//*/


		internal static void LoadAssets()
		{
			using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TPDespair.ZetItemTweaks.zetitemtweakbundle"))
			{
				Assets = AssetBundle.LoadFromStream(stream);
			}
		}



		internal static ConfigEntry<bool> ConfigEntry(string section, string key, bool defaultValue, string description)
		{
			ConfigCount++;

			string fullConfigKey = section + "_" + key;
			ValidateConfigKey(fullConfigKey);
			ConfigEntry<bool> configEntry = configFile.Bind(section, fullConfigKey, defaultValue, description);

			return configEntry;
		}

		internal static ConfigEntry<int> ConfigEntry(string section, string key, int defaultValue, string description)
		{
			ConfigCount++;

			string fullConfigKey = section + "_" + key;
			ValidateConfigKey(fullConfigKey);
			ConfigEntry<int> configEntry = configFile.Bind(section, fullConfigKey, defaultValue, description);

			return configEntry;
		}

		internal static ConfigEntry<float> ConfigEntry(string section, string key, float defaultValue, string description)
		{
			ConfigCount++;

			string fullConfigKey = section + "_" + key;
			ValidateConfigKey(fullConfigKey);
			ConfigEntry<float> configEntry = configFile.Bind(section, fullConfigKey, defaultValue, description);

			return configEntry;
		}

		private static void ValidateConfigKey(string configKey)
		{
			if (!ConfigKeys.Contains(configKey))
			{
				ConfigKeys.Add(configKey);
			}
			else
			{
				LogWarn("ConfigEntry for " + configKey + " already exists!");
			}
		}



		internal static void LogInfo(object data)
		{
			logSource.LogInfo(data);
		}

		internal static void LogWarn(object data)
		{
			logSource.LogWarning(data);
		}



		private static void SetupConfig()
		{
			string section = "00_General";

			EnableAutoCompat = ConfigEntry(
				section, "EnableAutoCompat", true,
				"Auto compat will prevent this mod from making changes to an item that another installed mod may have changed. Disabling this setting will cause items set to auto compat to ignore mod list. Individual item changes can be force enabled in each items config section."
			);
			ExplicitEnable = ConfigEntry(
				section, "ExplicitEnable", false,
				"Only enable item changes for sections that are set to force enabled. EnableAutoCompat setting will be ignored."
			);
			GenerateOverrideText = ConfigEntry(
				section, "GenerateOverrideText", false,
				"Create a config entry for each item that allows disabling item description text replacement."
			);
		}


		
		private static void ItemCatalogInit(On.RoR2.ItemCatalog.orig_Init orig)
		{
			Action action = OnItemCatalogPreInit;
			if (action != null)
			{
				SetupPhase = "ModifyItem";
				LogInfo("ItemCatalog Initializing!");

				action();

				SetupPhase = "";
			}

			orig();
		}
		
		private static void LateSetup()
		{
			Action action = OnLateSetup;
			if (action != null)
			{
				SetupPhase = "LateSetup";
				LogInfo("LateSetup Initialized!");

				action();

				PostSetupInfo();

				SetupPhase = "";
			}
		}



		private void ContentManager_collectContentPackProviders(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
		{
			addContentPackProvider(new ZetItemTweaksContent());
		}



		private static void EffectManagerHook()
		{
			On.RoR2.EffectManager.SpawnEffect_EffectIndex_EffectData_bool += (orig, index, data, transmit) =>
			{
				if ((int)index == 1758002 && !transmit)
				{
					if (data.genericUInt == 1u)
					{
						if (OldWarStealthKit.appliedChanges)
						{
							OldWarStealthKit.CreateMissText(data);
						}
					}
					else
					{
						LogWarn("Unknown SpawnEffect : " + data.genericUInt);
					}

					return;
				}

				orig(index, data, transmit);
			};
		}



		private static void SetupFragments()
		{
			RegisterFragment("BASE_STACK_FORMAT", "{0} {1}");

			RegisterFragment("FLAT_VALUE", "{0}");
			RegisterFragment("PERCENT_VALUE", "{0}%");
			RegisterFragment("FLATREGEN_VALUE", "{0} hp/s");
			RegisterFragment("PERCENTREGEN_VALUE", "{0}% hp/s");
			RegisterFragment("DURATION_VALUE", "{0}s");
			RegisterFragment("METER_VALUE", "{0}m");

			RegisterFragment("FLAT_STACK_INC", "<style=cStack>(+{0} per stack)</style>");
			RegisterFragment("PERCENT_STACK_INC", "<style=cStack>(+{0}% per stack)</style>");
			RegisterFragment("FLATREGEN_STACK_INC", "<style=cStack>(+{0} hp/s per stack)</style>");
			RegisterFragment("PERCENTREGEN_STACK_INC", "<style=cStack>(+{0}% hp/s per stack)</style>");
			RegisterFragment("DURATION_STACK_INC", "<style=cStack>(+{0}s per stack)</style>");
			RegisterFragment("METER_STACK_INC", "<style=cStack>(+{0}m per stack)</style>");
			RegisterFragment("FLAT_STACK_DEC", "<style=cStack>(-{0} per stack)</style>");
			RegisterFragment("PERCENT_STACK_DEC", "<style=cStack>(-{0}% per stack)</style>");
			RegisterFragment("FLATREGEN_STACK_DEC", "<style=cStack>(-{0} hp/s per stack)</style>");
			RegisterFragment("PERCENTREGEN_STACK_DEC", "<style=cStack>(-{0}% hp/s per stack)</style>");
			RegisterFragment("DURATION_STACK_DEC", "<style=cStack>(-{0}s per stack)</style>");
			RegisterFragment("METER_STACK_DEC", "<style=cStack>(-{0}m per stack)</style>");

			RegisterFragment("BASE_DAMAGE", "base");
			RegisterFragment("TOTAL_DAMAGE", "TOTAL");

			RegisterFragment("FOR_SECOND", "for {0} second");
			RegisterFragment("FOR_SECONDS", "for {0} seconds");
			RegisterFragment("OVER_SECOND", "over {0} second");
			RegisterFragment("OVER_SECONDS", "over {0} seconds");
			RegisterFragment("AFTER_SECOND", "after {0} second");
			RegisterFragment("AFTER_SECONDS", "after {0} seconds");
			RegisterFragment("EVERY_SECOND", "every second");
			RegisterFragment("EVERY_SECONDS", "every {0} seconds");
			RegisterFragment("SECOND", "{0} second");
			RegisterFragment("SECONDS", "{0} seconds");

			RegisterFragment("STAT_HEALTH_EXTRA_SHIELD", "\nGain {0} of health as extra <style=cIsHealing>shield</style>.");
			RegisterFragment("STAT_EXTRA_JUMP", "\nGain <style=cIsUtility>+1</style> maximum <style=cIsUtility>jump count</style>.");
			RegisterFragment("STAT_MOVESPEED", "\nIncreases <style=cIsUtility>movement speed</style> by {0}.");
			RegisterFragment("STAT_ATKSPEED", "\nIncreases <style=cIsDamage>attack speed</style> by {0}.");
			RegisterFragment("STAT_ARMOR", "\nIncreases <style=cIsHealing>armor</style> by {0}.");
			RegisterFragment("STAT_HEALTH", "\nIncreases <style=cIsHealing>maximum health</style> by {0}.");
			RegisterFragment("STAT_REGENERATION", "\nIncreases <style=cIsHealing>health regeneration</style> by {0}.");
			RegisterFragment("STAT_DAMAGE", "\nIncreases <style=cIsDamage>damage</style> by {0}.");
			RegisterFragment("STAT_COOLDOWN", "\nReduces <style=cIsUtility>skill cooldowns</style> by {0}.");
			RegisterFragment("STAT_CRIT", "\nIncreases <style=cIsDamage>critical strike chance</style> by {0}.");
			RegisterFragment("STAT_CRITMULT", "\nIncreases <style=cIsDamage>critical strike multiplier</style> by {0}.");
		}



		internal static bool ProceedChanges(string identifier, int enabled, List<string> modList, bool silent = false)
		{
			if (enabled == 1)
			{
				if (ExplicitEnable.Value)
				{
					if (!silent) LogInfo(identifier + " :: Disabled because it was not explicitly enabled!");

					return false;
				}

				if (!EnableAutoCompat.Value || modList.Count == 0)
				{
					if (!silent)
					{
						LogInfo(identifier + " :: Proceed with " + SetupPhase + ".");
						if (SetupPhase == "LateSetup") TweakCount++;
					}

					return true;
				}

				foreach (string guid in modList)
				{
					if (PluginLoaded(guid))
					{
						if (!silent) LogWarn(identifier + " :: Disabled because " + LastPluginChecked + " is installed!");

						return false;
					}
				}

				if (!silent)
				{
					LogInfo(identifier + " :: Proceed with " + SetupPhase + ".");
					if (SetupPhase == "LateSetup") TweakCount++;
				}

				return true;
			}
			else if (enabled == 2)
			{
				if (!silent)
				{
					LogInfo(identifier + " :: Proceed with " + SetupPhase + ".");
					if (SetupPhase == "LateSetup") TweakCount++;
				}

				return true;
			}

			if (!silent) LogWarn(identifier + " :: Disabled for unknown reason!");

			return false;
		}

		internal static bool PluginLoaded(string key)
		{
			LastPluginChecked = key;
			return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(key);
		}



		private static void PostSetupInfo()
		{
			LogInfo("LateSetup Complete!");
			LogInfo("Created [" + ConfigCount + "] config entries.");
			LogInfo("Modified [" + ModifyCount + "] itemDefs.");
			LogInfo("Tweaked [" + TweakCount + "] items.");
		}



		internal static ItemDef FindItemDefPreCatalogInit(string identifier)
		{
			foreach (ItemDef itemDef in ContentManager.itemDefs)
			{
				if (itemDef.name == identifier) return itemDef;
			}

			return null;
		}

		internal static void AssignDepricatedTier(ItemDef itemDef, ItemTier itemTier)
		{
			#pragma warning disable CS0618 // Type or member is obsolete
			itemDef.deprecatedTier = itemTier;
			#pragma warning restore CS0618 // Type or member is obsolete
		}

		internal static void RefreshTimedBuffStacks(CharacterBody self, BuffIndex buffIndex, float duration)
		{
			if (duration > 0f)
			{
				for (int i = 0; i < self.timedBuffs.Count; i++)
				{
					CharacterBody.TimedBuff timedBuff = self.timedBuffs[i];
					if (timedBuff.buffIndex == buffIndex)
					{
						if (timedBuff.timer > 0.1f && timedBuff.timer < duration) timedBuff.timer = duration;
					}
				}
			}
		}

		internal static void SetTimedBuffStacks(CharacterBody self, BuffIndex buffIndex, float duration, int count)
		{
			self.ClearTimedBuffs(buffIndex);
			self.SetBuffCount(buffIndex, 0);

			if (duration > 0f)
			{
				for (int i = 0; i < count; i++)
				{
					self.AddTimedBuff(buffIndex, duration);
				}
			}
		}

		internal static void SetCooldownBuffStacks(CharacterBody self, BuffIndex buffIndex, float duration)
		{
			self.ClearTimedBuffs(buffIndex);
			self.SetBuffCount(buffIndex, 0);

			if (duration > 0f)
			{
				float remainingDuration = duration;
				while (remainingDuration > 0f)
				{
					self.AddTimedBuff(buffIndex, remainingDuration);
					remainingDuration -= 1f;
				}
			}
		}



		private static void LanguageOverride()
		{
			On.RoR2.Language.TokenIsRegistered += (orig, self, token) =>
			{
				if (token != null)
				{
					if (LangTokens.ContainsKey(token)) return true;
				}

				return orig(self, token);
			};

			On.RoR2.Language.GetString_string += (orig, token) =>
			{
				if (token != null)
				{
					if (LangTokens.ContainsKey(token)) return LangTokens[token];
				}

				return orig(token);
			};
		}

		public static void RegisterToken(string token, string text)
		{
			if (!LangTokens.ContainsKey(token)) LangTokens.Add(token, text);
			else LangTokens[token] = text;
		}

		public static void RegisterFragment(string token, string text)
		{
			if (!Fragments.ContainsKey(token)) Fragments.Add(token, text);
			else
			{
				if (Fragments[token] != text)
				{
					LogInfo("Replacing fragment (" + token + ")");
					Fragments[token] = text;
				}
			}
		}

		public static string TextFragment(string key, bool trim = false)
		{
			if (Fragments.ContainsKey(key))
			{
				string output = Fragments[key];
				if (trim) output = output.Trim('\n');
				return output;
			}

			LogInfo("Failed to find fragment (" + key + ")");
			return "[" + key + "]";
		}

		public static string ScalingText(float baseValue, float stackValue, string modifier = "", string style = "")
		{
			if (stackValue == 0f)
			{
				return ScalingText(baseValue, modifier, style);
			}

			float mult = (modifier == "percent" || modifier == "percentregen") ? 100f : 1f;
			string sign = stackValue > 0f ? "INC" : "DEC";

			string baseString, stackString;
			if (modifier == "percent" || modifier == "chance")
			{
				baseString = TextFragment("PERCENT_VALUE");
				stackString = TextFragment("PERCENT_STACK_" + sign);
			}
			else if (modifier == "flatregen" || modifier == "regen")
			{
				baseString = TextFragment("FLATREGEN_VALUE");
				stackString = TextFragment("FLATREGEN_STACK_" + sign);
			}
			else if (modifier == "percentregen")
			{
				baseString = TextFragment("PERCENTREGEN_VALUE");
				stackString = TextFragment("PERCENTREGEN_STACK_" + sign);
			}
			else if (modifier == "second" || modifier == "duration")
			{
				baseString = TextFragment("DURATION_VALUE");
				stackString = TextFragment("DURATION_STACK_" + sign);
			}
			else if (modifier == "meter" || modifier == "distance")
			{
				baseString = TextFragment("METER_VALUE");
				stackString = TextFragment("METER_STACK_" + sign);
			}
			else
			{
				baseString = TextFragment("FLAT_VALUE");
				stackString = TextFragment("FLAT_STACK_" + sign);
			}

			string formatString = TextFragment("BASE_STACK_FORMAT");

			baseString = String.Format(baseString, baseValue * mult);
			stackString = String.Format(stackString, Mathf.Abs(stackValue * mult));

			if (style != "") baseString = "<style=" + style + ">" + baseString + "</style>";

			string output = String.Format(formatString, baseString, stackString);

			return output;
		}

		public static string ScalingText(float value, string modifier = "", string style = "")
		{
			if (modifier == "percent" || modifier == "percentregen") value *= 100f;

			string baseString;
			if (modifier == "percent" || modifier == "chance") baseString = TextFragment("PERCENT_VALUE");
			else if (modifier == "flatregen" || modifier == "regen") baseString = TextFragment("FLATREGEN_VALUE");
			else if (modifier == "percentregen") baseString = TextFragment("PERCENTREGEN_VALUE");
			else if (modifier == "second" || modifier == "duration") baseString = TextFragment("DURATION_VALUE");
			else if (modifier == "meter" || modifier == "distance") baseString = TextFragment("METER_VALUE");
			else baseString = TextFragment("FLAT_VALUE");

			string output = String.Format(baseString, value);

			if (style != "") output = "<style=" + style + ">" + output + "</style>";

			return output;
		}

		public static string SecondText(float sec, string modifier = "", string style = "")
		{
			string secText = sec.ToString();

			string targetString;
			if (modifier == "for") targetString = "FOR_SECOND";
			else if (modifier == "over") targetString = "OVER_SECOND";
			else if (modifier == "after") targetString = "AFTER_SECOND";
			else if (modifier == "every") targetString = "EVERY_SECOND";
			else targetString = "SECOND";

			if (sec != 1f) targetString += "S";

			if (style != "") secText = "<style=" + style + ">" + secText + "</style>";

			return String.Format(TextFragment(targetString), secText);
		}


		/*
		private static void DebugDrops()
		{
			if (Input.GetKeyDown(KeyCode.F2))
			{
				var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

				CreateDroplet(RoR2Content.Items.ShieldOnly, transform.position + new Vector3(0f, 0f, 0f));
				CreateDroplet(RoR2Content.Equipment.BurnNearby, transform.position + new Vector3(0f, 0f, 0f));



				CreateDroplet(RoR2Content.Items.FlatHealth, transform.position + new Vector3(-5f, 5f, 5f));
				CreateDroplet(RoR2Content.Items.CritGlasses, transform.position + new Vector3(-10f, 10f, 10f));

				CreateDroplet(DLC1Content.Items.MushroomVoid, transform.position + new Vector3(0f, 5f, 7.5f));
				CreateDroplet(DLC1Content.Items.MissileVoid, transform.position + new Vector3(0f, 10f, 15f));

				CreateDroplet(RoR2Content.Items.SprintArmor, transform.position + new Vector3(5f, 5f, 5f));
				CreateDroplet(RoR2Content.Items.Seed, transform.position + new Vector3(10f, 10f, 10f));

				CreateDroplet(RoR2Content.Items.BarrierOnOverHeal, transform.position + new Vector3(-5f, 5f, -5f));
				CreateDroplet(DLC1Content.Items.ImmuneToDebuff, transform.position + new Vector3(-10f, 10f, -10f));

				CreateDroplet(RoR2Content.Items.Knurl, transform.position + new Vector3(0f, 5f, -7.5f));
				CreateDroplet(RoR2Content.Items.BleedOnHitAndExplode, transform.position + new Vector3(0f, 10f, -15f));

				CreateDroplet(RoR2Content.Items.Pearl, transform.position + new Vector3(5f, 5f, -5f));
				CreateDroplet(RoR2Content.Items.ShinyPearl, transform.position + new Vector3(10f, 10f, -10f));
			}
		}

		private static void CreateDroplet(EquipmentDef def, Vector3 pos)
		{
			if (!def) return;

			PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(def.equipmentIndex), pos, Vector3.zero);
		}

		private static void CreateDroplet(ItemDef def, Vector3 pos)
		{
			if (!def) return;

			PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(def.itemIndex), pos, Vector3.zero);
		}
		//*/
	}
}
