using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using BepInEx.Configuration;
using RoR2;

using static TPDespair.ZetItemTweaks.ZetItemTweaksPlugin;

namespace TPDespair.ZetItemTweaks
{
	public static class PixieTube
	{
		internal static BuffDef PixieBuff;

		public static List<string> autoCompatList = new List<string> { };

		public static string itemIdentifier = "PixieTube";
		public static bool appliedChanges = false;

		public static List<BuffDef> modifiedBuffs = new List<BuffDef> { };

		public static BuffIndex PixieArmor = BuffIndex.None;
		public static BuffIndex PixieAttackSpeed = BuffIndex.None;
		public static BuffIndex PixieDamage = BuffIndex.None;
		public static BuffIndex PixieMoveSpeed = BuffIndex.None;

		public static ConfigEntry<int> EnableChanges { get; set; }
		public static ConfigEntry<bool> CombineBuffs { get; set; }



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
				itemIdentifier, "EnableChanges", 0,
				SectionEnableDesc
			);
			CombineBuffs = ConfigEntry(
				itemIdentifier, "CombineBuffs", true,
				"Hide all Pixie Tube buffs and display them as 1 buff."
			);
		}

		private static void ModifyBuff()
		{
			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, "com.ThinkInvisible.TinkersSatchel", Feedback.LogAll | Feedback.Invert)) return;

			if (CombineBuffs.Value)
			{
				AttemptHideBuff("TKSATPixieArmor");
				AttemptHideBuff("TKSATPixieAttackSpeed");
				AttemptHideBuff("TKSATPixieDamage");
				AttemptHideBuff("TKSATPixieMoveSpeed");

				if (modifiedBuffs.Count == 4)
				{
					LogInfo(itemIdentifier + " :: Hiding 4 associated buffs!");

					ModifiedBuffDefCount += 4;
				}
				else
				{
					foreach (BuffDef buffDef in modifiedBuffs)
					{
						buffDef.isHidden = false;
					}

					modifiedBuffs.Clear();

					LogWarn(itemIdentifier + " :: Could Not Find All BuffDefs - Aborting!");
				}
			}
		}

		private static void AttemptHideBuff(string identifier)
		{
			BuffDef buffDef = FindBuffDefPreCatalogInit(identifier);
			if (buffDef)
			{
				if (!buffDef.isHidden)
				{
					buffDef.isHidden = true;

					modifiedBuffs.Add(buffDef);
				}
			}
			else
			{
				LogWarn(itemIdentifier + " :: Could Not Find BuffDef : " + identifier);
			}
		}

		private static void LateSetup()
		{
			if (modifiedBuffs.Count != 4) return;

			if (!ProceedChanges(itemIdentifier, EnableChanges.Value, "com.ThinkInvisible.TinkersSatchel", Feedback.Default | Feedback.Invert)) return;

			if (CombineBuffs.Value)
			{
				GatherBuffIndexes();
				On.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += BuffInterceptHook;

				appliedChanges = true;
			}
		}



		private static void GatherBuffIndexes()
		{
			BuffIndex buffIndex = BuffCatalog.FindBuffIndex("TKSATPixieArmor");
			if (buffIndex != BuffIndex.None)
			{
				PixieArmor = buffIndex;
			}
			else
			{
				LogWarn(itemIdentifier + " :: Could Not Find BuffIndex : TKSATPixieArmor");
			}
			buffIndex = BuffCatalog.FindBuffIndex("TKSATPixieAttackSpeed");
			if (buffIndex != BuffIndex.None)
			{
				PixieAttackSpeed = buffIndex;
			}
			else
			{
				LogWarn(itemIdentifier + " :: Could Not Find BuffIndex : TKSATPixieAttackSpeed");
			}
			buffIndex = BuffCatalog.FindBuffIndex("TKSATPixieDamage");
			if (buffIndex != BuffIndex.None)
			{
				PixieDamage = buffIndex;
			}
			else
			{
				LogWarn(itemIdentifier + " :: Could Not Find BuffIndex : TKSATPixieDamage");
			}
			buffIndex = BuffCatalog.FindBuffIndex("TKSATPixieMoveSpeed");
			if (buffIndex != BuffIndex.None)
			{
				PixieMoveSpeed = buffIndex;
			}
			else
			{
				LogWarn(itemIdentifier + " :: Could Not Find BuffIndex : TKSATPixieMoveSpeed");
			}
		}

		private static void BuffInterceptHook(On.RoR2.CharacterBody.orig_AddTimedBuff_BuffDef_float orig, CharacterBody self, BuffDef buffDef, float duration)
		{
			if (NetworkServer.active)
			{
				BuffIndex buffIndex = buffDef.buffIndex;
				if (buffIndex != BuffIndex.None)
				{
					if (buffIndex == PixieArmor || buffIndex == PixieAttackSpeed || buffIndex == PixieDamage || buffIndex == PixieMoveSpeed)
					{
						orig(self, PixieBuff, duration);
					}
				}
			}

			orig(self, buffDef, duration);
		}
	}
}
