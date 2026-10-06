using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
using Path = System.IO.Path;

namespace CustomPosters
{
    public class CustomPosters : IModApi
    {

        public static ModConfig config;
        public static CustomPosters context;
        public static Mod mod;

        public static Dictionary<string, Texture2D> postersDict = new Dictionary<string, Texture2D>();
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(mod.Name);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Dbgl($"Mod awake");

        }

        [HarmonyPatch(typeof(XUiC_SpawnSelectionWindow), nameof(XUiC_SpawnSelectionWindow.Open))]
        static class XUiC_SpawnSelectionWindow_Open_Patch
        {
            static void Postfix()
            {
                if (!config.modEnabled)
                    return;
                Dbgl($"Adding Posters");
                GameManager.Instance.StartCoroutine(LoadPosters());
            }
        }

        [HarmonyPatch(typeof(BlockShapeModelEntity), nameof(BlockShapeModelEntity.getPrefab))]
        static class BlockShapeModelEntity_getPrefab_Patch
        {
            static void Postfix(BlockShapeModelEntity __instance, Transform __result)
            {
                if (!postersDict.TryGetValue(__instance.block.blockName, out var tex))
                    return;
                Dbgl($"editing prefab for {__instance.block.blockName}");

                foreach (var mr in __result.gameObject.GetComponentsInChildren<MeshRenderer>())
                {
                    Dbgl($"mr {mr.name}");
                    foreach(var m in mr.materials)
                    {
                        Dbgl($"{string.Join(", ", m.GetTexturePropertyNames())}");
                    }
                    mr.material.SetTexture("_MainTex", tex);
                }
            }
        }
        public static IEnumerator LoadPosters()
        {
            postersDict.Clear();
            List<string> uniques = new List<string>();
            string root = Path.Combine(mod.Path, "Posters");
            if(!Directory.Exists(root))
                Directory.CreateDirectory(root);
            int rootPaths = root.Split(Path.DirectorySeparatorChar).Length;
            var all = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            var sorted = new Dictionary<string, string>();

            Dbgl($"Got {all.Length} files");

            foreach (var path in all)
            {
                Dbgl($"Checking file {path.Substring(root.Length)}");

                var dir = Path.GetDirectoryName(path);
                if (dir == root)
                {
                    Dbgl($"In root folder, skipping");
                    continue;
                }
                if (!path.EndsWith(".png") && !path.EndsWith(".jpg") && !path.EndsWith(".gif") && !path.EndsWith(".jpeg"))
                {
                    Dbgl($"Not supported file extension, skipping");
                    continue;
                }
                var name = Path.GetFileNameWithoutExtension(path);
                if (uniques.Contains(name))
                {
                    Dbgl($"Duplicate, skipping");
                    continue;
                }
                uniques.Add(name);

                name = "CustomPoster" + name;

                string type = Path.GetFileName(dir);
                if (!Block.nameToBlock.ContainsKey(type))
                {
                    Dbgl($"Template block {type} not found, skipping");
                    continue;
                }
                CoroutineTask<Texture2D> r = GetImageAsync(path, name);
                yield return r;
                var tex = r.GetResult();
                if (tex == null)
                {
                    Dbgl($"Couldn't load texture, skipping");
                    continue;
                }
                Dbgl($"Creating Poster {name}");
                try
                {
                    CreatePosterBlock(name, type, tex);
                    Dbgl($"Created Poster {name}");
                }
                catch(Exception ex)
                {
                    Dbgl($"Error creating poster {name}:\n\n{ex}");
                }
            }
            if (postersDict.Count == 0)
                yield break;
            Block.AssignIds();
            
            Dbgl($"Created {postersDict.Count} posters");

            Dbgl($"Creating items");

            foreach (var name in postersDict.Keys)
            {
                ItemClassBlock itemClassBlock = new ItemClassBlock();
                itemClassBlock.SetId(Block.nameToBlock[name].blockID);
                itemClassBlock.SetName(Block.nameToBlock[name].GetBlockName());
                itemClassBlock.Stacknumber = new DataItem<int>(Block.nameToBlock[name].Stacknumber);
                ItemClass.list[itemClassBlock.Id] = itemClassBlock;
                itemClassBlock.Init();
                Dbgl($"Created item {itemClassBlock.GetItemName()}");
            }

            Dbgl($"Creating recipes");

            foreach (var name in postersDict.Keys)
            {
                CreatePosterRecipe(name);
            }
            MicroStopwatch msw = new MicroStopwatch(true);
            List<Recipe> allRecipes = CraftingManager.GetAllRecipes();
            Dictionary<string, List<Recipe>> recipesByName = new Dictionary<string, List<Recipe>>();
            foreach (Recipe recipe in allRecipes)
            {
                string name = recipe.GetName();
                List<Recipe> list;
                if (!recipesByName.TryGetValue(name, out list))
                {
                    list = new List<Recipe>();
                    recipesByName[name] = list;
                }
                list.Add(recipe);
                if (msw.ElapsedMilliseconds > (long)Constants.cMaxLoadTimePerFrameMillis)
                {
                    yield return null;
                    msw.ResetAndRestart();
                }
            }
            CraftingManager.PostInit();

            List<string> recipeCalcStack = new List<string>();
            foreach (ItemClass itemClass in ItemClass.list)
            {
                if (itemClass != null)
                {
                    recipeCalcStack.Clear();
                    itemClass.AutoCalcWeight(recipesByName);
                    if (itemClass.AutoCalcEcoVal(recipesByName, recipeCalcStack) < 0f)
                    {
                        Log.Warning("Loading recipes: Could not calculate eco value for item " + itemClass.GetItemName() + ": Only recursive recipes found");
                    }
                    if (recipeCalcStack.Count > 0)
                    {
                        Log.Warning("Loading recipes: Eco value calculation stack not empty for item " + itemClass.GetItemName() + ": " + string.Join(" > ", recipeCalcStack));
                    }
                    if (msw.ElapsedMilliseconds > (long)Constants.cMaxLoadTimePerFrameMillis)
                    {
                        yield return null;
                        msw.ResetAndRestart();
                    }
                }
            }
            yield break;
        }

        public static void CreatePosterBlock(string name, string type, Texture2D tex)
        {
            Block template = Block.nameToBlock[type];
            XmlFile file = new XmlFile($"<block name=\"{name}\"><property name=\"Extends\" value=\"{type}\"/></block>", "CustomPosters", name);
            BlocksFromXml.ParseBlock(GameManager.Instance != null && GameManager.Instance.IsEditMode(), file.XmlDoc.Root);

            postersDict[name] = tex;
        }

        public static void CreatePosterRecipe(string name)
        {
            Dbgl($"Creating recipe {name}");


            var item = ItemClass.GetItem(name, false);
            if (item.type == 0)
            {
                Dbgl($"Item {name} not found");
                return;
            }
            Recipe recipe = new Recipe()
            {
                itemValueType = item.type,
                count = 1,
                craftingTime = -1,
                unlockExpGain = -1,
                craftExpGain = -1,
                IsTrackable = true,
                UseIngredientModifier = true,
                craftingArea = "workbench",
                ingredients = new List<ItemStack>()
                {
                    new ItemStack(ItemClass.GetItem("resourceWood", false), 10),
                    new ItemStack(ItemClass.GetItem("resourcePaint", false), 1)
                }
            };
            recipe.Init();
            CraftingManager.AddRecipe(recipe);
            Dbgl($"Added recipe for block item {item.ItemClass.Name}");
        }

        public static CoroutineTask<Texture2D> GetImageAsync(string path, string name)
        {
            TaskResult<Texture2D> result = new TaskResult<Texture2D>();
            return new CoroutineTask<Texture2D>(GetImageAsync(path, name, result), result);
        }

        public static IEnumerator GetImageAsync(string path, string name, IOut<Texture2D> result)
        {
            Uri uri = new Uri(path);
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(uri))
            {
                yield return uwr.SendWebRequest();

                DownloadHandlerTexture.GetContent(uwr);
                if (!string.IsNullOrEmpty(uwr.error))
                {
                    Dbgl(uwr.error);
                    result.Set(null);
                }
                else
                {
                    var tex = DownloadHandlerTexture.GetContent(uwr);
                    tex.name = name;
                    result.Set(tex);
                }
            }
            yield break;
        }
        public static void LoadConfig()
        {
            config = new ModConfig();
            var path = Path.Combine(mod.Path, "config.json");
            if (File.Exists(path))
            {
                config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void SaveConfig()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

    }
}
