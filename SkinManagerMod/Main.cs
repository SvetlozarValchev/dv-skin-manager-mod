﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityModManagerNet;
using Harmony12;
using UnityEngine;
using Newtonsoft.Json.Linq;
using DV;
using DV.JObjectExtstensions;

namespace SkinManagerMod
{
    public class Main
    {
        public static Dictionary<string, string> trainCarState = new Dictionary<string, string>();

        public static string modPath;

        static Vector2 scrollViewVector = Vector2.zero;

        public static Dictionary<TrainCarType, string> prefabMap;

        static TrainCarType trainCarSelected = TrainCarType.NotSet;

        static bool showDropdown = false;

        public static Dictionary<TrainCarType, SkinGroup> skinGroups = new Dictionary<TrainCarType, SkinGroup>();

        public static Dictionary<TrainCarType, string> selectedSkin = new Dictionary<TrainCarType, string>();

        static List<string> excludedExports = new List<string>
        {
            "car_lods",
            "SH_glass_01d",
            "windows_01d",
            "window_d"
        };

        static Dictionary<string, string> aliasNames = new Dictionary<string, string>
        {
            { "exterior_d", "body" },
            { "LocoDiesel_exterior_d", "body" },
            { "SH_exterior_d", "body" },
            { "SH_tender_01d", "body" }
        };

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Type type = typeof(CarTypes);
            FieldInfo fieldInfo = type.GetField("prefabMap", BindingFlags.Static | BindingFlags.NonPublic);

            prefabMap = fieldInfo.GetValue(null) as Dictionary<TrainCarType, string>;

            modPath = modEntry.Path;

            LoadSkins();

            foreach (TrainCarType carType in prefabMap.Keys)
            {
                selectedSkin.Add(carType, "Random");
            }

            modEntry.OnGUI = OnGUI;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("Export Textures");

            GUILayout.BeginHorizontal(GUILayout.Width(250));

            GUILayout.BeginVertical();

            if (GUILayout.Button(trainCarSelected == TrainCarType.NotSet ? "Select Train Car" : prefabMap[trainCarSelected], GUILayout.Width(220)))
            {
                showDropdown = !showDropdown;
            }

            if (showDropdown)
            {
                scrollViewVector = GUILayout.BeginScrollView(scrollViewVector, GUILayout.Height(350));

                foreach (var entry in prefabMap)
                {
                    if (GUILayout.Button(entry.Value, GUILayout.Width(220)))
                    {
                        showDropdown = false;
                        trainCarSelected = entry.Key;
                    }
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();

            if (trainCarSelected != TrainCarType.NotSet)
            {
                if (GUILayout.Button("Export Textures", GUILayout.Width(180)))
                {
                    DumpTextures(trainCarSelected);
                }
            }

            GUILayout.EndHorizontal();
        }

        public static void DumpTextures(TrainCarType trainCar)
        {
            var obj = CarTypes.GetCarPrefab(trainCar);

            var path = modPath + "Exported\\" + obj.name;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var cmps = obj.GetComponentsInChildren<MeshRenderer>();
            var textureList = new Dictionary<string, Texture>();

            foreach (var cmp in cmps)
            {
                if (!cmp.material || !cmp.material.mainTexture)
                {
                    continue;
                }

                var materials = cmp.materials;

                foreach(var m in materials)
                {
                    Debug.Log("Material : " + m.mainTexture);
                }

                var diffuse = GetMaterialTexture(cmp, "_MainTex");
                var normal = GetMaterialTexture(cmp, "_BumpMap");
                var specular = GetMaterialTexture(cmp, "_MetallicGlossMap");

                ExportTexture(path, diffuse, textureList);
                ExportTexture(path, normal, textureList);
                ExportTexture(path, specular, textureList);
            }
        }

        public static Texture2D GetMaterialTexture(MeshRenderer cmp, string materialName)
        {
            if (cmp.material == null || !cmp.material.HasProperty(materialName))
            {
                return null;
            }

            var texture = cmp.material.GetTexture(materialName);

            if (texture is Texture2D)
            {
                return (Texture2D)texture;
            } else
            {
                return null;
            }
        }

        public static void ExportTexture(string path, Texture2D texture, Dictionary<string, Texture> textureList)
        {
            if (texture && texture is Texture2D && !textureList.ContainsKey(texture.name))
            {
                textureList.Add(texture.name, texture);

                if (!excludedExports.Contains(texture.name))
                {
                    var tex = DuplicateTexture(texture as Texture2D);

                    SaveTextureAsPNG(tex, path + "\\" + texture.name + ".png");
                }
            }
        }

        public static void SaveTextureAsPNG(Texture2D _texture, string _fullPath)
        {
            byte[] _bytes = _texture.EncodeToPNG();
            File.WriteAllBytes(_fullPath, _bytes);
            Debug.Log(_bytes.Length / 1024 + "Kb was saved as: " + _fullPath);
        }

        static Texture2D DuplicateTexture(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.sRGB);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }

        static void LoadSkins()
        {
            foreach (var prefab in prefabMap)
            {
                var dir = modPath + "Skins\\" + prefab.Value;

                if (Directory.Exists(dir))
                {
                    var subDirectories = Directory.GetDirectories(dir);
                    var skinGroup = new SkinGroup(prefab.Key);
                    var carPrefab = CarTypes.GetCarPrefab(prefab.Key);
                    var cmps = carPrefab.gameObject.GetComponentsInChildren<MeshRenderer>();

                    foreach (var subDir in subDirectories)
                    {
                        var dirInfo = new DirectoryInfo(subDir);
                        var files = Directory.GetFiles(subDir);
                        var skin = new Skin(dirInfo.Name);

                        foreach (var file in files)
                        {
                            FileInfo fileInfo = new FileInfo(file);
                            string fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                            byte[] fileData = File.ReadAllBytes(file);

                            foreach (var cmp in cmps)
                            {
                                if (!cmp.material)
                                    continue;

                                var diffuse = GetMaterialTexture(cmp, "_MainTex");
                                var normal = GetMaterialTexture(cmp, "_BumpMap");
                                var specular = GetMaterialTexture(cmp, "_MetallicGlossMap");

                                if (diffuse != null)
                                {                                   
                                    if ((diffuse.name == fileName || aliasNames.ContainsKey(diffuse.name) && aliasNames[diffuse.name] == fileName) && !skin.ContainsTexture(diffuse.name)) {

                                        var texture = DuplicateTexture((Texture2D)diffuse);
                                        texture.LoadImage(fileData);

                                        skin.skinTextures.Add(new SkinTexture(diffuse.name, texture));
                                    }
                                }

                                if (normal != null)
                                {
                                    if ((normal.name == fileName || aliasNames.ContainsKey(normal.name) && aliasNames[normal.name] == fileName) && !skin.ContainsTexture(normal.name)) {
                                        var texture = DuplicateTexture((Texture2D)normal);
                                        texture.LoadImage(fileData);

                                        skin.skinTextures.Add(new SkinTexture(normal.name, texture));
                                    }
                                }


                                if (specular != null)
                                {   
                                    if ((specular.name == fileName || aliasNames.ContainsKey(specular.name) && aliasNames[specular.name] == fileName) && !skin.ContainsTexture(specular.name))
                                    {
                                        var texture = DuplicateTexture((Texture2D)specular);
                                        texture.LoadImage(fileData);

                                        skin.skinTextures.Add(new SkinTexture(specular.name, texture));
                                    }
                                }
                            }
                        }

                        skinGroup.skins.Add(skin);
                    }

                    skinGroups.Add(prefab.Key, skinGroup);
                }
            }
        }

        public static Skin FindTrainCarSkin(TrainCar trainCar)
        {
            if (!skinGroups.ContainsKey(trainCar.carType))
            {
                return null;
            }

            var skinGroup = skinGroups[trainCar.carType];

            if (skinGroup.skins.Count == 0)
            {
                return null;
            }

            Skin skin;

            if (trainCarState.ContainsKey(trainCar.logicCar.carGuid))
            {
                string skinName = trainCarState[trainCar.logicCar.carGuid];

                if (skinName == "__default")
                {
                    return null;
                }

                skin = skinGroup.GetSkin(skinName);

                if (skin == null)
                {
                    return null;
                }
            }
            else
            {
                string selectedSkin = Main.selectedSkin[trainCar.carType];

                if (selectedSkin == "Random")
                {
                    var range = UnityEngine.Random.Range(0, skinGroup.skins.Count);

                    skin = skinGroup.skins[range];

                    SetCarState(trainCar.logicCar.carGuid, skin.name);
                }
                else if (selectedSkin == "Default")
                {
                    SetCarState(trainCar.logicCar.carGuid, "__default");

                    return null;
                }
                else
                {
                    skin = skinGroup.GetSkin(selectedSkin);

                    SetCarState(trainCar.logicCar.carGuid, skin.name);
                }
            }

            return skin;
        }

        public static void ReplaceTexture(TrainCar trainCar)
        {
            var skin = FindTrainCarSkin(trainCar);

            if (skin == null)
            {
                return;
            }

            var cmps = trainCar.gameObject.GetComponentsInChildren<MeshRenderer>();

            foreach (var cmp in cmps)
            {
                if (!cmp.material)
                {
                    continue;
                }

                var diffuse = GetMaterialTexture(cmp, "_MainTex");
                var normal = GetMaterialTexture(cmp, "_BumpMap");
                var specular = GetMaterialTexture(cmp, "_MetallicGlossMap");

                if (diffuse != null && skin.ContainsTexture(diffuse.name))
                {
                    var skinTexture = skin.GetTexture(diffuse.name);

                    cmp.material.SetTexture("_MainTex", skinTexture.textureData);
                }

                if (normal != null && skin.ContainsTexture(normal.name))
                {
                    var skinTexture = skin.GetTexture(normal.name);

                    cmp.material.SetTexture("_BumpMap", skinTexture.textureData);
                }

                if (specular != null && skin.ContainsTexture(specular.name))
                {
                    var skinTexture = skin.GetTexture(specular.name);

                    cmp.material.SetTexture("_MetallicGlossMap", skinTexture.textureData);
                }
            }


        }

        public static void ReplaceInteriorTexture(TrainCar trainCar)
        {
            var skin = FindTrainCarSkin(trainCar);

            if (skin == null)
            {
                return;
            }

            var cmps = trainCar.interior.GetComponentsInChildren<MeshRenderer>();

            foreach (var cmp in cmps)
            {
                if (!cmp.material)
                {
                    continue;
                }

                var diffuse = GetMaterialTexture(cmp, "_MainTex");
                var normal = GetMaterialTexture(cmp, "_BumpMap");
                var specular = GetMaterialTexture(cmp, "_MetallicGlossMap");

                if (diffuse != null && skin.ContainsTexture(diffuse.name))
                {
                    var skinTexture = skin.GetTexture(diffuse.name);

                    cmp.material.SetTexture("_MainTex", skinTexture.textureData);
                }

                if (normal != null && skin.ContainsTexture(normal.name))
                {
                    var skinTexture = skin.GetTexture(normal.name);

                    cmp.material.SetTexture("_BumpMap", skinTexture.textureData);
                }

                if (specular != null && skin.ContainsTexture(specular.name))
                {
                    var skinTexture = skin.GetTexture(specular.name);

                    cmp.material.SetTexture("_MetallicGlossMap", skinTexture.textureData);
                }
            }
        }

        public static void SetCarState(string guid, string name)
        {
            if (trainCarState.ContainsKey(guid))
            {
                trainCarState[guid] = name;
            } else
            {
                trainCarState.Add(guid, name);
            }
        }
    }

    public class SkinGroup
    {
        TrainCarType trainCarType;
        public List<Skin> skins = new List<Skin>();

        public SkinGroup(TrainCarType trainCarType)
        {
            this.trainCarType = trainCarType;
        }

        public Skin GetSkin(string name)
        {
            foreach(var skin in skins)
            {
                if (skin.name == name)
                {
                    return skin;
                }
            }

            return null;
        }
    }

    public class Skin
    {
        public string name;
        public List<SkinTexture> skinTextures = new List<SkinTexture>();

        public Skin(string name)
        {
            this.name = name;
        }

        public bool ContainsTexture(string name)
        {
            foreach(var tex in skinTextures)
            {
                if (tex.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        public SkinTexture GetTexture(string name)
        {
            foreach (var tex in skinTextures)
            {
                if (tex.name == name)
                {
                    return tex;
                }
            }

            return null;
        }
    }

    public class SkinTexture
    {
        public string name;
        public Texture2D textureData;

        public SkinTexture(string name, Texture2D textureData)
        {
            this.name = name;
            this.textureData = textureData;
        }
    }

    [HarmonyPatch(typeof(CarSpawner), "SpawnCar")]
    class CarSpawner_SpawnCar_Patch
    {
        static void Postfix(TrainCar __result)
        {
            Main.ReplaceTexture(__result);
        }
    }

    [HarmonyPatch(typeof(CarSpawner), "SpawnLoadedCar")]
    class CarSpawner_SpawnExistingCar_Patch
    {
        static void Postfix(TrainCar __result)
        {
            Main.ReplaceTexture(__result);
        }
    }

    [HarmonyPatch(typeof(TrainCar), "LoadInterior")]
    class TrainCar_LoadInterior_Patch
    {
        static void Postfix(TrainCar __instance)
        {
            Main.ReplaceInteriorTexture(__instance);
        }
    }

    [HarmonyPatch(typeof(SaveGameManager), "Save")]
    class SaveGameManager_Save_Patch
    {
        static void Prefix(SaveGameManager __instance)
        {
            JObject carsSaveData = GetCarsSaveData();

            SaveGameManager.data.SetJObject("Mod_Skins", carsSaveData);
        }

        static JObject GetCarsSaveData()
        {
            JObject carsSaveData = new JObject();

            JObject[] array = new JObject[Main.trainCarState.Count];

            int i = 0;

            foreach (KeyValuePair<string, string> entry in Main.trainCarState)
            {
                JObject dataObject = new JObject();

                dataObject.SetString("guid", entry.Key);
                dataObject.SetString("name", entry.Value);

                array[i] = dataObject;

                i++;
            }

            JObject[] skinArray = new JObject[Main.selectedSkin.Count];

            i = 0;

            foreach (KeyValuePair<TrainCarType, string> entry in Main.selectedSkin)
            {
                JObject dataObject = new JObject();

                dataObject.SetInt("type", (int)entry.Key);
                dataObject.SetString("skin", entry.Value);

                skinArray[i] = dataObject;
                i++;
            }

            carsSaveData.SetJObjectArray("carsData", array);
            carsSaveData.SetJObjectArray("carSkins", skinArray);

            return carsSaveData;
        }
    }

    [HarmonyPatch(typeof(CarsSaveManager), "Load")]
    class CarsSaveManager_Load_Patch
    {
        static void Prefix(JObject savedData)
        {
            if (savedData == null)
            {
                Debug.LogError((object)"Given save data is null, loading will not be performed");
                return;
            }

            JObject carsSaveData = SaveGameManager.data.GetJObject("Mod_Skins");

            if (carsSaveData != null)
            {
                JObject[] jobjectArray = carsSaveData.GetJObjectArray("carsData");

                if (jobjectArray != null)
                {
                    foreach (JObject jobject in jobjectArray)
                    {
                        var guid = jobject.GetString("guid");
                        var name = jobject.GetString("name");

                        if (!Main.trainCarState.ContainsKey(guid))
                        {
                            Main.trainCarState.Add(guid, name);
                        }
                    }
                }

                JObject[] jobjectSkinArray = carsSaveData.GetJObjectArray("carSkins");

                if (jobjectArray != null)
                {
                    foreach (JObject jobject in jobjectSkinArray)
                    {
                        TrainCarType type = (TrainCarType)jobject.GetInt("type").Value;
                        string skin = jobject.GetString("skin");

                        if (Main.selectedSkin.ContainsKey(type))
                        {
                            Main.selectedSkin[type] = skin;
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(UnityModManager.UI), "OnGUI")]
    class UnityModManagerUI_OnGUI_Patch
    {
        private static void Postfix(UnityModManager.UI __instance)
        {
            ModUI.DrawModUI();
        }
    }

    class ModUI
    {
        public static bool isEnabled;

        static bool showDropdown;

        static Vector2 scrollViewVector;

       
        //static Dictionary<TrainCarType, string> prefabMap;
        public static void Init()
        {

        }

        public static void DrawModUI()
        {
            if (!isEnabled)
            {
                showDropdown = false;
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameObject carToSpawn = SingletonBehaviour<CarSpawner>.Instance.carToSpawn;
            TrainCar trainCar = carToSpawn.GetComponent<TrainCar>();
            TrainCarType carType = trainCar.carType;

            SkinGroup skinGroup = Main.skinGroups[carType];
            string selectSkin = Main.selectedSkin[carType];

            float menuHeight = 60f;

            float menuWidth = 270f;
            float buttonWidth = 240f;
            bool isMaxHeight = false;
            float maxHeight = Screen.height - 200f;

            if (showDropdown)
            {
                float totalItemHeight = skinGroup.skins.Count * 23f;

                if (totalItemHeight > maxHeight)
                {
                    totalItemHeight = maxHeight;
                    isMaxHeight = true;
                }

                menuHeight += totalItemHeight + 46f;
            }

            if (isMaxHeight)
            {
                buttonWidth -= 20f;
            }

            GUI.skin = DVGUI.skin;
            GUI.skin.label.fontSize = 11;
            GUI.skin.button.fontSize = 10;
            GUI.color = new Color32(0, 0, 0, 200);
            GUI.Box(new Rect(20f, 20f, menuWidth, menuHeight), "");
            GUILayout.BeginArea(new Rect(30f, 20f, menuWidth, menuHeight));
            GUI.color = Color.yellow;
            GUILayout.Label("Skin Manager Menu :: " + carToSpawn.name);
            GUI.color = Color.white;
            GUILayout.Space(5f);

            if (showDropdown)
            {
                if (GUILayout.Button("=== " + selectSkin + " ===", GUILayout.Width(240f)))
                {
                    showDropdown = false;
                }

                if (isMaxHeight)
                {
                    scrollViewVector = GUILayout.BeginScrollView(scrollViewVector, GUILayout.Width(245f), GUILayout.Height(menuHeight - 55f));
                }

                if (GUILayout.Button("Random", GUILayout.Width(buttonWidth)))
                {
                    showDropdown = false;
                    Main.selectedSkin[carType] = "Random";
                }

                if (GUILayout.Button("Default", GUILayout.Width(buttonWidth)))
                {
                    showDropdown = false;
                    Main.selectedSkin[carType] = "Default";
                }

                foreach (Skin skin in skinGroup.skins)
                {
                    if (GUILayout.Button(skin.name, GUILayout.Width(buttonWidth)))
                    {
                        showDropdown = false;
                        Main.selectedSkin[carType] = skin.name;
                    }
                }

                if (isMaxHeight)
                {
                    GUILayout.EndScrollView();
                }
            } else
            {
                if (GUILayout.Button("=== " + selectSkin + " ===", GUILayout.Width(240f)))
                {
                    showDropdown = true;
                }
            }

            GUILayout.EndArea();
        }
    }

    [HarmonyPatch(typeof(CarSpawner), "SpawnModeEnable")]
    class CarSpawner_SpawnModeEnable_Patch
    {
        static void Prefix(bool turnOn)
        {
            ModUI.isEnabled = turnOn;
        }
    }

    class CarsSkinSaveData
    {
        public string guid;
        public string name;

        public CarsSkinSaveData(string guid, string name)
        {
            this.guid = guid;
            this.name = name;
        }
    }
}