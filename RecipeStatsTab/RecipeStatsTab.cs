using HarmonyLib;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using Path = System.IO.Path;

namespace RecipeStatsTab
{
    public class RecipeStatsTab : IModApi
    {

        public static ModConfig config;
        public static RecipeStatsTab context;
        public static Mod mod;
        public static bool showStats;
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
                UnityEngine.Debug.Log((prefix ? mod.Name + " " : "") + str);
        }
        [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.Init))]
        public static class XUiC_CraftingInfoWindow_Init_Patch
        {

            public static void Postfix(XUiC_CraftingInfoWindow __instance)
            {
                if (!config.modEnabled)
                    return;
                showStats = false;
                __instance.GetChildById("recipeStatsButton").OnPress += StatsButton_OnPress;
                ((XUiV_Button)__instance.GetChildById("recipeStatsButton").ViewComponent).Selected = false;
                __instance.IsDirty = true;
            }
        }
        private static void StatsButton_OnPress(XUiController _sender, int _mouseButton)
        {
            var windows = _sender.xui.GetWindowsByType<XUiC_CraftingInfoWindow>();
            foreach (var craftingWindow in windows)
            {
                if (craftingWindow.IsOpen)
                {
                    craftingWindow.TabType = XUiC_CraftingInfoWindow.TabTypes.UnlockedBy;
                    craftingWindow.SetSelectedButtonByType(craftingWindow.TabType);
                    showStats = true;
                    craftingWindow.IsDirty = true;
                    return;
                }
            }
        }
        [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.OnClose))]
        public static class XUiC_CraftingInfoWindow_OnClose_Patch
        {
            public static void Postfix(XUiC_CraftingInfoWindow __instance)
            {
                if (!config.modEnabled)
                    return;
                showStats = false;
                ((XUiV_Button)__instance.GetChildById("recipeStatsButton").ViewComponent).Selected = false;
            }
        }
        [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.SetSelectedButtonByType))]
        public static class XUiC_CraftingInfoWindow_SetSelectedButtonByType_Patch
        {
            public static void Postfix(XUiC_CraftingInfoWindow __instance)
            {
                if (!config.modEnabled)
                    return;
                showStats = __instance.TabType == XUiC_CraftingInfoWindow.TabTypes.UnlockedBy && (__instance.recipe == null || XUiM_Recipes.GetRecipeIsUnlocked(__instance.xui, __instance.recipe));
                ((XUiV_Button)__instance.GetChildById("recipeStatsButton").ViewComponent).Selected = showStats;
            }
        }
        [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.SetRecipe))]
        public static class XUiC_CraftingInfoWindow_SetRecipe_Patch
        {
            public static void Prefix(XUiC_CraftingInfoWindow __instance, ref bool __state)
            {
                if (!config.modEnabled || !showStats || __instance.recipe != null || !XUiM_Recipes.GetRecipeIsUnlocked(__instance.xui, __instance.recipe))
                    return;
                __state = true;
            }
            public static void Postfix(XUiC_CraftingInfoWindow __instance, ref bool __state)
            {
                if (!__state)
                    return;
                StatsButton_OnPress(__instance, 0);
            }
        }
        [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.GetBindingValue))]
        public static class XUiC_CraftingInfoWindow_GetBindingValue_Patch
        {
            public static bool Prefix(XUiC_CraftingInfoWindow __instance, ref string value, string bindingName, ref bool __result)
            {
                if (!config.modEnabled)
                    return true;
                if(showStats && (bindingName == "showingredients" || bindingName == "showdescription" || bindingName == "showunlockedby"))
                {
                    value = false.ToString();
                    __result = true;
                    return false;
                }
                if(bindingName == "showrecipestats")
                {
                    value = showStats.ToString();
                    __result = true;
                    return false;
                }
                if(bindingName.StartsWith("recipestattitle") && int.TryParse(bindingName.Substring("recipestattitle".Length), out int result))
                {
                    value = GetStatTitle(__instance, result);
                    __result = true;
                    return false;
                }
                if (bindingName.StartsWith("recipestat") && int.TryParse(bindingName.Substring("recipestat".Length), out int result2))
                {
                    value = GetStatValue(__instance, result2);
                    __result = true;
                    return false;
                }
                return true;
            }
        }
        public static string GetStatTitle(XUiC_CraftingInfoWindow craftingWindow, int index)
        {
            if (craftingWindow?.recipe == null)
                return "";
            if (index > 0)
                index--;
            var itemClass = ItemClass.GetItemClass(craftingWindow.recipe.GetName());
            var itemDisplayEntry = UIDisplayInfoManager.Current.GetDisplayStatsForTag(itemClass.IsBlock() ? Block.list[ItemClass.GetItem(craftingWindow.recipe.GetName(), false).type].DisplayType : itemClass.DisplayType);

            if (itemDisplayEntry == null || itemDisplayEntry.DisplayStats.Count <= index)
            {
                return "";
            }
            if (itemDisplayEntry.DisplayStats[index].TitleOverride != null)
            {
                return itemDisplayEntry.DisplayStats[index].TitleOverride;
            }
            var result = UIDisplayInfoManager.Current.GetLocalizedName(itemDisplayEntry.DisplayStats[index].StatType);
            return result;
        }
        public static string GetStatValue(XUiC_CraftingInfoWindow craftingWindow, int index)
        {
            if (craftingWindow?.recipe == null)
                return "";
            if (index > 0)
                index--;
            var itemClass = ItemClass.GetItemClass(craftingWindow.recipe.GetName());
            var itemValue = ItemClass.GetItem(craftingWindow.recipe.GetName(), false);
            var itemDisplayEntry = UIDisplayInfoManager.Current.GetDisplayStatsForTag(itemClass.IsBlock() ? Block.list[itemValue.type].DisplayType : itemClass.DisplayType);

            if (itemDisplayEntry == null || itemDisplayEntry.DisplayStats.Count <= index)
            {
                return "";
            }
            DisplayInfoEntry displayInfoEntry = itemDisplayEntry.DisplayStats[index];
            var result = XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(new ItemStack(itemValue, 1), ItemStack.Empty, craftingWindow.xui.playerUI.entityPlayer, displayInfoEntry, false, true);
            return result;
        }
    }
}
