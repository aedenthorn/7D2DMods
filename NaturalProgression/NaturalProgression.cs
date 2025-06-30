using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UAI;
using UnityEngine;

namespace NaturalProgression
{
    public class NaturalProgression : IModApi
    {

        public static ModConfig config;
        public static NaturalProgression context;
        public static Mod mod;
        
        public enum SkillType
        {
            none,
            craftingHarvestingTools,
            craftingRepairTools,
            craftingSalvageTools,
            craftingKnuckles,
            craftingBlades,
            craftingClubs,
            craftingSledgehammers,
            craftingBows,
            craftingSpears,
            craftingHandguns,
            craftingShotguns,
            craftingRifles,
            craftingMachineGuns,
            craftingExplosives,
            craftingRobotics,
            craftingArmor,
            craftingMedical,
            craftingFood,
            craftingSeeds,
            craftingElectrician,
            craftingTraps,
            craftingWorkstations,
            craftingVehicles
        }

        public static SkillType currentSkillType = SkillType.none;

        public static Dictionary<int, Dictionary<SkillType, int>> skillDict = new Dictionary<int, Dictionary<SkillType, int>>();
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }



        [HarmonyPatch(typeof(BlockProjectileMoveScript), nameof(BlockProjectileMoveScript.checkCollision))]
        static class BlockProjectileMoveScript_checkCollision_Patch
        {
            static void Prefix(BlockProjectileMoveScript __instance)
            {
                if (!config.modEnabled)
                    return;
                EntityPlayer entityPlayer = GameManager.Instance.World.GetEntity(__instance.ProjectileOwnerID) as EntityPlayer;
                if(entityPlayer != null)
                {
                    float value = EffectManager.GetValue(PassiveEffects.ElectricalTrapXP, entityPlayer.inventory.holdingItemItemValue, 0f, entityPlayer, null, default(FastTags<TagGroup.Global>), true, true, true, true, true, 1, true, false);
                    if (value > 0f)
                    {
                        currentSkillType = SkillType.craftingTraps;
                    }
                }
            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.GiveExp))]
        static class EntityPlayerLocal_GiveExp_Patch
        {
            static void Prefix(EntityPlayerLocal __instance, CraftCompleteData data)
            {
                if (!config.modEnabled)
                    return;
                currentSkillType = SkillType.none;
                if (data.CraftedItemStack?.itemValue is null)
                {
                    return;
                }
                SetSkillType(data.CraftedItemStack.itemValue.ItemClass);
            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.giveExp))]
        static class XUiC_RecipeStack_giveExp_Patch
        {
            static void Prefix(XUiC_RecipeStack __instance, ItemClass _ic)
            {
                if (!config.modEnabled)
                    return;
                SetSkillType(_ic);
            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(ItemActionMelee), nameof(ItemActionMelee.hitTheTarget))]
        static class ItemActionMelee_hitTheTarget_Patch
        {
            static void Prefix(ItemActionMelee __instance, ItemActionMelee.InventoryDataMelee _actionData)
            {
                if (!config.modEnabled || !(_actionData.invData.holdingEntity is EntityPlayerLocal))
                    return;
                SetSkillType(_actionData.invData.item);

            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(ItemActionDynamic), nameof(ItemActionDynamic.hitTarget))]
        static class ItemActionDynamic_hitTarget_Patch
        {
            static void Prefix(ItemActionData _actionData)
            {
                if (!config.modEnabled || !(_actionData.invData.holdingEntity is EntityPlayerLocal))
                    return;
                SetSkillType(_actionData.invData.item);

            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(ItemActionMelee), nameof(ItemActionMelee.checkHarvesting))]
        static class ItemActionMelee_checkHarvesting_Patch
        {
            static void Prefix(ItemActionMelee __instance, ItemActionMelee.InventoryDataMelee _actionData)
            {
                if (!config.modEnabled || !(_actionData.invData.holdingEntity is EntityPlayerLocal))
                    return;
                SetSkillType(_actionData.invData.item);

            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.fireShot))]
        static class ItemActionRanged_fireShot_Patch
        {
            static void Prefix(ItemActionMelee __instance, ItemActionRanged.ItemActionDataRanged _actionData)
            {
                if (!config.modEnabled || !(_actionData.invData.holdingEntity is EntityPlayerLocal))
                    return;
                SetSkillType(_actionData.invData.item);
            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.ExecuteAction))]
        static class ItemActionRepair_ExecuteAction_Patch
        {
            static void Prefix(ItemActionData _actionData)
            {
                if (!config.modEnabled || !(_actionData.invData.holdingEntity is EntityPlayerLocal))
                    return;
                SetSkillType(_actionData.invData.item);

            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(ItemActionRepair), nameof(ItemActionRepair.OnHoldingUpdate))]
        static class ItemActionRepair_OnHoldingUpdate_Patch
        {
            static void Prefix(ItemActionRepair __instance, ItemActionData _actionData)
            {
                if (!config.modEnabled || !__instance.bUpgradeCountChanged || !(_actionData.invData.holdingEntity is EntityPlayerLocal))
                    return;
                SetSkillType(_actionData.invData.item);

            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }
        [HarmonyPatch(typeof(GameUtils), nameof(GameUtils.collectHarvestedItem))]
        static class GameUtils_collectHarvestedItem_Patch
        {
            static void Prefix(ItemActionData _actionData)
            {
                if(!config.modEnabled) 
                    return;
                if(!_actionData.attackDetails.blockBeingDamaged.isair && _actionData.attackDetails.blockBeingDamaged.Block.blockName.ToLower().StartsWith("planted") && (_actionData.attackDetails.blockBeingDamaged.Block.blockName.ToLower().EndsWith("harvestplayer") || _actionData.attackDetails.blockBeingDamaged.Block.blockName.ToLower().EndsWith("harvest")))
                {
                    currentSkillType  = SkillType.craftingSeeds;
                }
                else
                {
                    SetSkillType(_actionData.invData.item);
                }
            }
            static void Postfix()
            {
                currentSkillType = SkillType.none;
            }
        }

        [HarmonyPatch(typeof(Progression), nameof(Progression.AddLevelExp))]
        static class Progression_AddLevelExp_Patch
        {
            static void Postfix(Progression __instance, int _exp, Progression.XPTypes _xpType, int __result)
            {
                //Dbgl($"adding exp {_exp}, {_xpType}, {currentSkillType}, {__instance.parent?.GetType()}");
                //Dbgl(Environment.StackTrace);
                if (!config.modEnabled || currentSkillType == SkillType.none || !(__instance.parent is EntityPlayerLocal))
                    return;
                switch (_xpType)
                {
                    case Progression.XPTypes.Kill:
                    case Progression.XPTypes.Crafting:
                    case Progression.XPTypes.Harvesting:
                    case Progression.XPTypes.Repairing:
                    case Progression.XPTypes.Upgrading:
                        AddSkillXP(__instance.parent as EntityPlayerLocal, currentSkillType, __result);
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(World), nameof(World.SetupTraders))]
        static class World_SetupTraders_Patch
        {
            static void Prefix()
            {
                if (!config.modEnabled)
                    return;
                LoadExpFile();
            }
        }
        [HarmonyPatch(typeof(PlayerDataFile), nameof(PlayerDataFile.Save))]
        static class PlayerDataFile_Save_Patch
        {
            static void Prefix()
            {
                if (!config.modEnabled)
                    return;
                SaveExpFile();
            }
        }

        private static void SetSkillType(ItemClass itemClass)
        {
            if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("rifleSkill")))
            {
                currentSkillType = SkillType.craftingRifles;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("machinegunSkill")))
            {
                currentSkillType = SkillType.craftingMachineGuns;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("explosivesSkill,Mine")))
            {
                currentSkillType = SkillType.craftingExplosives;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("bowSkill")))
            {
                currentSkillType = SkillType.craftingBows;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("roboticsSkill")))
            {
                currentSkillType = SkillType.craftingRobotics;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("handgunSkill")))
            {
                currentSkillType = SkillType.craftingHandguns;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("shotgunSkill")))
            {
                currentSkillType = SkillType.craftingShotguns;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("bladeSkill")))
            {
                currentSkillType = SkillType.craftingBlades;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("spearSkill")))
            {
                currentSkillType = SkillType.craftingSpears;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("clubSkill")))
            {
                currentSkillType = SkillType.craftingClubs;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("sledgeSkill")))
            {
                currentSkillType = SkillType.craftingSledgehammers;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("knuckleSkill")))
            {
                currentSkillType = SkillType.craftingKnuckles;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("repairingSkill")))
            {
                currentSkillType = SkillType.craftingRepairTools;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("harvestingSkill")))
            {
                currentSkillType = SkillType.craftingHarvestingTools;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("salvagingSkill")))
            {
                currentSkillType = SkillType.craftingSalvageTools;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("medicalSkill")))
            {
                currentSkillType = SkillType.craftingMedical;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("medicalSkill")))
            {
                currentSkillType = SkillType.craftingMedical;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("foodSkill")))
            {
                currentSkillType = SkillType.craftingFood;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("foodSkill")))
            {
                currentSkillType = SkillType.craftingFood;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("electricianSkill")))
            {
                currentSkillType = SkillType.craftingElectrician;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("workstationSkill")))
            {
                currentSkillType = SkillType.craftingWorkstations;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("vehiclesSkill")))
            {
                currentSkillType = SkillType.craftingVehicles;
            }
            else if (itemClass.HasAnyTags(FastTags<TagGroup.Global>.Parse("trapsSkill")))
            {
                currentSkillType = SkillType.craftingTraps;
            }
            else if (itemClass.pArmor != null)
            {
                currentSkillType = SkillType.craftingArmor;
            }
            else
            {
                currentSkillType = SkillType.none;
            }
        }

        public static void AddSkillXP(EntityPlayer player, SkillType sType, int num)
        {

            num = Mathf.RoundToInt(num * config.xpMult);
            if(skillDict is null)
            {
                skillDict = new Dictionary<int, Dictionary<SkillType, int>>();
            }
            if (!skillDict.TryGetValue(player.entityId, out var dict))
            {
                dict = new Dictionary<SkillType, int>()
                {
                    { sType, num }
                };
                skillDict[player.entityId] = dict;
            }
            else
            {
                if(!dict.TryGetValue(sType, out int cur))
                {
                    dict[sType] = num;
                }
                else
                {
                    dict[sType] = cur + num;
               
                }
            }



            var progressionValue = player.Progression.GetProgressionValue(sType.ToString());
            if (progressionValue == null)
            {
                Dbgl($"Couldn't get progression value for {sType}");
                return;
            }
            var currentLevel = progressionValue.GetCalculatedLevel(player);
            var levelXP = player.Progression.getExpForLevel(currentLevel + 1);
            int remain = dict[sType] - levelXP;
            Dbgl($"Added xp {num} to {sType}, total {dict[sType]}, next level {levelXP}");
            if (remain < 0)
                return;
            while (remain >= 0)
            {
                if (progressionValue.Level + 1 <= progressionValue.ProgressionClass.MaxLevel)
                {
                    player.MinEventContext.ProgressionValue = progressionValue;
                    MinEventActionAddProgressionLevel minEvent = new MinEventActionAddProgressionLevel()
                    {
                        progressionName = sType.ToString(),
                        level = 1
                    };
                    minEvent.targets.Clear();
                    minEvent.targets.Add(player);
                    minEvent.Execute(player.MinEventContext);
                    Dbgl($"level up {sType}");
                }
                else
                {
                    break;
                }
                dict[sType] = remain;
                currentLevel = progressionValue.GetCalculatedLevel(player);
                levelXP = player.Progression.getExpForLevel(currentLevel + 1);
                remain = dict[sType] - levelXP;
            }
        }

        public static void SaveExpFile()
        {
            var json = JsonConvert.SerializeObject(skillDict, Formatting.Indented);
            //Dbgl("Saving exp file");
            //Dbgl($"{json}");
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), GameManager.Instance.World.Guid + ".json");

            File.WriteAllText(path, json);

        }
        public static void LoadExpFile()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), GameManager.Instance.World.Guid + ".json");
            if (File.Exists(path))
            {
                skillDict = JsonConvert.DeserializeObject<Dictionary<int, Dictionary<SkillType, int>>>(File.ReadAllText(path)) ?? new Dictionary<int, Dictionary<SkillType, int>>();
            }
            else
            {
                skillDict = new Dictionary<int, Dictionary<SkillType, int>>();
                SaveExpFile();
            }
        }

        public static void LoadConfig()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            if (!File.Exists(path))
            {
                config = new ModConfig();
            }
            else
            {
                config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

    }
}
