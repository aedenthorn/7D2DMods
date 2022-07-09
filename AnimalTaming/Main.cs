using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace AnimalTaming
{
	public class Main : IModApi
	{
		public static ModConfig config;
		public static Main context;
		public static Mod mod;
		public static List<string> tranqNames = new List<string>
		{
			"ammoCrossbowBoltTranq",
			"Tranq Crossbow Bolt"
		};

		public void InitMod(Mod modInstance)
		{
            context = this;
            mod = modInstance;
            LoadConfig();
			Harmony harmony = new Harmony(GetType().ToString());
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}

		public static void LoadConfig()
		{
			string path = Path.Combine(AedenthornUtils.GetAssetPath(context, true), "config.json");
			bool flag = !File.Exists(path);
			if (flag)
			{
                config = new ModConfig();
			}
			else
			{
                config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
			}
			File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
		}

		public static void SaveConfig()
		{
			string path = Path.Combine(AedenthornUtils.GetAssetPath(context, true), "config.json");
			File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
		}

		public static void Dbgl(object str, bool prefix = true)
		{
			bool isDebug = config.isDebug;
			if (isDebug)
			{
				Debug.Log((prefix ? (mod.ModInfo.Name.Value + " ") : "") + ((str != null) ? str.ToString() : null));
			}
		}

		[HarmonyPatch(typeof(ProjectileMoveScript), "checkCollision")]
		private static class ProjectileMoveScript_checkCollision_Patch
		{
			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				List<CodeInstruction> list = new List<CodeInstruction>(instructions);
                Dbgl("Transpiling ProjectileMoveScript.checkCollision", true);
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i].opcode == OpCodes.Call && list[i].operand is MethodInfo && (MethodInfo)list[i].operand == AccessTools.Method(typeof(ItemActionAttack), "Hit", null, null))
					{
                        Dbgl("Adding method to check for tranq", true);
						list.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Main), "CheckForTranq2")));
						list.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
						break;
					}
				}
				return list.AsEnumerable();
			}
		}

		public static void CheckForTranq2(ProjectileMoveScript pms)
		{
			Dbgl("Hit on check collision");

			if (!Voxel.voxelRayHitInfo.tag.StartsWith("E_"))
				return;

			CheckForTranq(Voxel.voxelRayHitInfo, pms.itemValueLauncher);
		}
		public static void CheckForTranq(WorldRayHitInfo hitInfo, ItemValue itemValue)
		{
			Entity entity = ItemActionAttack.FindHitEntityNoTagCheck(hitInfo, out string text);
			Dbgl("Hit entity");

			if (entity == null || !(entity is EntityAlive) || FactionManager.Instance.GetFaction((entity as EntityAlive).factionId).Name != "animals")
				return;

			Dbgl("Hit animal");

			ItemClass itemClass = itemValue.ItemClass;
			if (itemClass == null)
				return;

			Dbgl("Hit something with something");


			string[] magazineItemNames = (itemClass.Actions[0] as ItemActionRanged).MagazineItemNames;
			Dbgl($"Hit animal with {magazineItemNames[(int)itemValue.SelectedAmmoTypeIndex]}");

			if (!tranqNames.Contains(magazineItemNames[(int)itemValue.SelectedAmmoTypeIndex]))
				return;
			Dbgl("Hit animal with tranq: " + magazineItemNames[(int)itemValue.SelectedAmmoTypeIndex], true);
			(entity as EntityAlive).SetDead();
			//(entity as EntityAlive).factionId = FactionManager.Instance.GetFactionByName("trader").ID;
		}
	}
}
