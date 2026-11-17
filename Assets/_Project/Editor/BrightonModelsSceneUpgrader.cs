using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CarXCase.Intro;

namespace DetectiveGame.Editor
{
    [InitializeOnLoad]
    public static class BrightonModelsSceneUpgrader
    {
        const string ModelsScenePath = "Assets/_Project/Scenes/00_BrightonDrive_Models.unity";
        const string RequestPath = "Temp/UpgradeModels.request";
        const string LogPath = "Logs/BrightonModelsUpgrade.txt";

        static BrightonModelsSceneUpgrader()
        {
            EditorApplication.update += CheckRequest;
        }

        static void CheckRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(RequestPath))
            {
                return;
            }

            File.Delete(RequestPath);

            try
            {
                UpgradeScene();
            }
            catch (Exception ex)
            {
                File.WriteAllText(LogPath, ex.ToString());
                Debug.LogException(ex);
            }
        }

        [MenuItem("Tools/Brighton/1. Расширить дорогу и добавить трафик (Models Scene)")]
        public static void UpgradeScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ModelsScenePath)
            {
                if (!File.Exists(ModelsScenePath))
                {
                    throw new FileNotFoundException("Сцена не найдена: " + ModelsScenePath);
                }

                EditorSceneManager.SaveOpenScenes();
                activeScene = EditorSceneManager.OpenScene(ModelsScenePath, OpenSceneMode.Single);
            }

            var street = GameObject.Find("Brighton_Street");
            if (street == null)
            {
                throw new Exception("Не найден объект Brighton_Street в сцене!");
            }

            var road = street.transform.Find("Road");
            if (road == null)
            {
                throw new Exception("Не найден объект Road в Brighton_Street!");
            }

            float currentWidth = road.localScale.x;
            float targetRoadWidth = 16f;
            float widthDelta = Mathf.Max(0f, (targetRoadWidth - currentWidth) / 2f);

            road.localScale = new Vector3(targetRoadWidth, 0.25f, 850f);
            road.position = new Vector3(0f, -0.1f, 350f);

            var sidewalkRight = street.transform.Find("Sidewalk_1");
            if (sidewalkRight != null)
            {
                sidewalkRight.position = new Vector3(11.5f, 0.05f, 350f);
                sidewalkRight.localScale = new Vector3(7.0f, 0.35f, 850f);
            }

            var sidewalkLeft = street.transform.Find("Sidewalk_-1");
            if (sidewalkLeft != null)
            {
                sidewalkLeft.position = new Vector3(-11.5f, 0.05f, 350f);
                sidewalkLeft.localScale = new Vector3(7.0f, 0.35f, 850f);
            }

            var curbRight = street.transform.Find("Curb_1");
            if (curbRight != null)
            {
                curbRight.position = new Vector3(8.05f, 0.12f, 350f);
                curbRight.localScale = new Vector3(0.2f, 0.3f, 850f);
            }

            var curbLeft = street.transform.Find("Curb_-1");
            if (curbLeft != null)
            {
                curbLeft.position = new Vector3(-8.05f, 0.12f, 350f);
                curbLeft.localScale = new Vector3(0.2f, 0.3f, 850f);
            }

            foreach (Transform child in street.transform)
            {
                string name = child.name;
                float side = Mathf.Sign(child.localPosition.x);

                if (name.StartsWith("Rail_Crossbeam_"))
                {
                    child.localScale = new Vector3(17f, child.localScale.y, child.localScale.z);
                }
                else if (name.StartsWith("Rail_Column_"))
                {
                    child.position = new Vector3(side * 7.8f, child.position.y, child.position.z);
                }
                else if (name.StartsWith("ParkedCar_"))
                {
                    child.position = new Vector3(side * 6.8f, child.position.y, child.position.z);
                }
                else if (name.StartsWith("Father_House_") || name == "HouseNumber")
                {
                    if (widthDelta > 0.05f)
                    {
                        child.position += Vector3.right * widthDelta;
                    }
                }
            }

            var assembledModels = GameObject.Find("Brighton_Models_Assembled");
            if (assembledModels != null && widthDelta > 0.05f)
            {
                foreach (Transform bld in assembledModels.transform)
                {
                    float side = Mathf.Sign(bld.position.x);
                    bld.position += Vector3.right * side * widthDelta;
                }
            }

            FixTextDepth(activeScene);

            var existingTraffic = GameObject.Find("Oncoming_Traffic");
            if (existingTraffic != null)
            {
                UnityEngine.Object.DestroyImmediate(existingTraffic);
            }

            var trafficRoot = new GameObject("Oncoming_Traffic").transform;

            var matBlue = GetOrCreateColorMat("CarPaint_Navy", new Color(0.18f, 0.25f, 0.38f));
            var matRed = GetOrCreateColorMat("CarPaint_Maroon", new Color(0.48f, 0.14f, 0.12f));
            var matTaxiYellow = GetOrCreateColorMat("CarPaint_Taxi", new Color(0.85f, 0.72f, 0.12f));
            var matWhite = GetOrCreateColorMat("CarPaint_WhiteVan", new Color(0.82f, 0.82f, 0.80f));
            var matDarkGreen = GetOrCreateColorMat("CarPaint_SUVGreen", new Color(0.14f, 0.28f, 0.16f));
            var matBlack = GetOrCreateColorMat("CarPaint_SedanBlack", new Color(0.08f, 0.09f, 0.10f));
            var matGlass = GetOrCreateColorMat("CarWindow_Tint", new Color(0.20f, 0.28f, 0.35f));
            var matTrim = GetOrCreateColorMat("CarTrim_Dark", new Color(0.06f, 0.07f, 0.08f));
            var matPoliceWhite = GetOrCreateColorMat("CarPaint_PoliceWhite", new Color(0.92f, 0.92f, 0.94f));

            var vehicles = new List<BrightonDriveCutscene.OncomingVehicle>();
            float laneX = -3.8f;

            var car1 = CreateSedan(trafficRoot, "Oncoming_Sedan_Blue", matBlue, matGlass, matTrim, laneX, 140f);
            vehicles.Add(new BrightonDriveCutscene.OncomingVehicle { root = car1, spawnTime = 4f, speed = 14f, startZ = 140f, endZ = -80f });

            var car2 = CreateVan(trafficRoot, "Oncoming_Van_White", matWhite, matGlass, matTrim, laneX - 0.2f, 290f);
            vehicles.Add(new BrightonDriveCutscene.OncomingVehicle { root = car2, spawnTime = 16f, speed = 11f, startZ = 290f, endZ = -80f });

            var car3 = CreateTaxi(trafficRoot, "Oncoming_Taxi_1", matTaxiYellow, matGlass, matTrim, laneX, 390f);
            vehicles.Add(new BrightonDriveCutscene.OncomingVehicle { root = car3, spawnTime = 30f, speed = 15f, startZ = 390f, endZ = -80f });

            var car4 = CreateStationWagon(trafficRoot, "Oncoming_Wagon_Maroon", matRed, matGlass, matTrim, laneX + 0.1f, 460f);
            vehicles.Add(new BrightonDriveCutscene.OncomingVehicle { root = car4, spawnTime = 46f, speed = 13f, startZ = 460f, endZ = -80f });

            var car5 = CreatePoliceCruiser(trafficRoot, "Oncoming_Police_Cruiser", matPoliceWhite, matBlue, matGlass, matTrim, laneX, 520f);
            vehicles.Add(new BrightonDriveCutscene.OncomingVehicle { root = car5, spawnTime = 62f, speed = 16f, startZ = 520f, endZ = -80f });

            var car6 = CreateSUV(trafficRoot, "Oncoming_SUV_Green", matDarkGreen, matGlass, matTrim, laneX - 0.2f, 570f);
            vehicles.Add(new BrightonDriveCutscene.OncomingVehicle { root = car6, spawnTime = 80f, speed = 12f, startZ = 570f, endZ = -80f });

            var car7 = CreateTaxi(trafficRoot, "Oncoming_Taxi_2", matTaxiYellow, matGlass, matTrim, laneX, 600f);
            vehicles.Add(new BrightonDriveCutscene.OncomingVehicle { root = car7, spawnTime = 96f, speed = 14f, startZ = 600f, endZ = -80f });

            var car8 = CreateSedan(trafficRoot, "Oncoming_Sedan_Black", matBlack, matGlass, matTrim, laneX + 0.1f, 620f);
            vehicles.Add(new BrightonDriveCutscene.OncomingVehicle { root = car8, spawnTime = 112f, speed = 11f, startZ = 620f, endZ = -80f });

            var cutsceneDirector = UnityEngine.Object.FindAnyObjectByType<BrightonDriveCutscene>();
            if (cutsceneDirector != null)
            {
                cutsceneDirector.oncomingVehicles = vehicles;
                EditorUtility.SetDirty(cutsceneDirector);
            }

            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            BrightonReferencePass.Apply();
            string report = "Успешно обновлена сцена: " + ModelsScenePath + "\n";
            report += "Ширина дороги: " + targetRoadWidth + "м (дельта = " + widthDelta + "м)\n";
            report += "Тротуары расширены до 7.0м без разрывов\n";
            report += "Добавлено встречных машин: " + vehicles.Count;

            File.WriteAllText(LogPath, report);
            Debug.Log("<color=#00FF88><b>[Brighton Models] Сцена обновлена! Дорога 16м, тротуары 7м, 8 встречных машин добавлены!</b></color>");
        }

        static void FixTextDepth(Scene scene)
        {
            var shader = Shader.Find("CarXCase/World Text Depth");
            if (shader == null || !shader.isSupported)
            {
                return;
            }

            foreach (var label in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if (label.gameObject.scene != scene) continue;

                var renderer = label.GetComponent<Renderer>();
                if (renderer == null) continue;

                string fontName = label.font != null ? label.font.name : "default";
                string matPath = "Assets/Materials/BrightonIntro/TextDepth_" + fontName + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, matPath);
                }

                material.shader = shader;
                material.mainTexture = label.font != null ? label.font.material.mainTexture : renderer.sharedMaterial.mainTexture;
                material.renderQueue = 2450;
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(material);
            }
        }

        static Transform CreateSedan(Transform parent, string name, Material body, Material glass, Material trim, float x, float z)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(x, 0f, z);
            root.rotation = Quaternion.Euler(0f, 180f, 0f);

            MakeBox(root, "Body", new Vector3(0f, 0.52f, 0f), new Vector3(1.8f, 0.62f, 4.3f), body);
            MakeBox(root, "Cabin", new Vector3(0f, 1.15f, -0.15f), new Vector3(1.6f, 0.62f, 2.1f), glass);
            MakeBox(root, "Bumper_Front", new Vector3(0f, 0.35f, 2.2f), new Vector3(1.7f, 0.22f, 0.16f), trim);
            MakeBox(root, "Bumper_Rear", new Vector3(0f, 0.35f, -2.2f), new Vector3(1.7f, 0.22f, 0.16f), trim);

            AddHeadlights(root, 0.65f, 0.55f, 2.18f);

            root.gameObject.SetActive(false);
            return root;
        }

        static Transform CreateTaxi(Transform parent, string name, Material body, Material glass, Material trim, float x, float z)
        {
            var root = CreateSedan(parent, name, body, glass, trim, x, z);
            var taxiSignMat = GetOrCreateColorMat("Taxi_Sign_Amber", new Color(1f, 0.75f, 0.1f));
            MakeBox(root, "Taxi_Roof_Sign", new Vector3(0f, 1.55f, 0.1f), new Vector3(0.55f, 0.18f, 0.25f), taxiSignMat);
            return root;
        }

        static Transform CreateVan(Transform parent, string name, Material body, Material glass, Material trim, float x, float z)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(x, 0f, z);
            root.rotation = Quaternion.Euler(0f, 180f, 0f);

            MakeBox(root, "Van_Chassis", new Vector3(0f, 0.75f, 0f), new Vector3(1.9f, 1.1f, 5.0f), body);
            MakeBox(root, "Van_Cabin_Glass", new Vector3(0f, 1.7f, 1.3f), new Vector3(1.7f, 0.8f, 1.6f), glass);
            MakeBox(root, "Van_Cargo_Box", new Vector3(0f, 1.72f, -0.6f), new Vector3(1.85f, 0.85f, 2.9f), body);
            MakeBox(root, "Van_Bumper_F", new Vector3(0f, 0.35f, 2.55f), new Vector3(1.8f, 0.25f, 0.16f), trim);

            AddHeadlights(root, 0.7f, 0.65f, 2.52f);

            root.gameObject.SetActive(false);
            return root;
        }

        static Transform CreateStationWagon(Transform parent, string name, Material body, Material glass, Material trim, float x, float z)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(x, 0f, z);
            root.rotation = Quaternion.Euler(0f, 180f, 0f);

            MakeBox(root, "Body", new Vector3(0f, 0.52f, 0f), new Vector3(1.8f, 0.62f, 4.5f), body);
            MakeBox(root, "Cabin_Long", new Vector3(0f, 1.15f, -0.45f), new Vector3(1.6f, 0.62f, 2.7f), glass);
            MakeBox(root, "Roof_Rack", new Vector3(0f, 1.5f, -0.45f), new Vector3(1.3f, 0.08f, 2.2f), trim);
            MakeBox(root, "Bumper_Front", new Vector3(0f, 0.35f, 2.3f), new Vector3(1.7f, 0.22f, 0.16f), trim);

            AddHeadlights(root, 0.65f, 0.55f, 2.28f);

            root.gameObject.SetActive(false);
            return root;
        }

        static Transform CreatePoliceCruiser(Transform parent, string name, Material bodyWhite, Material bodyBlue, Material glass, Material trim, float x, float z)
        {
            var root = CreateSedan(parent, name, bodyWhite, glass, trim, x, z);
            MakeBox(root, "Police_Blue_Door_L", new Vector3(-0.91f, 0.65f, 0f), new Vector3(0.04f, 0.5f, 1.8f), bodyBlue);
            MakeBox(root, "Police_Blue_Door_R", new Vector3(0.91f, 0.65f, 0f), new Vector3(0.04f, 0.5f, 1.8f), bodyBlue);

            var matLightRed = GetOrCreateColorMat("Police_Light_Red", Color.red);
            var matLightBlue = GetOrCreateColorMat("Police_Light_Blue", Color.blue);
            MakeBox(root, "Lightbar_Base", new Vector3(0f, 1.5f, 0.05f), new Vector3(1.1f, 0.08f, 0.2f), trim);
            MakeBox(root, "Lightbar_Red", new Vector3(-0.35f, 1.58f, 0.05f), new Vector3(0.4f, 0.12f, 0.18f), matLightRed);
            MakeBox(root, "Lightbar_Blue", new Vector3(0.35f, 1.58f, 0.05f), new Vector3(0.4f, 0.12f, 0.18f), matLightBlue);

            return root;
        }

        static Transform CreateSUV(Transform parent, string name, Material body, Material glass, Material trim, float x, float z)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(x, 0f, z);
            root.rotation = Quaternion.Euler(0f, 180f, 0f);

            MakeBox(root, "SUV_Body", new Vector3(0f, 0.7f, 0f), new Vector3(1.9f, 0.85f, 4.4f), body);
            MakeBox(root, "SUV_Cabin", new Vector3(0f, 1.45f, -0.2f), new Vector3(1.7f, 0.7f, 2.5f), glass);
            MakeBox(root, "Bumper_Front", new Vector3(0f, 0.42f, 2.25f), new Vector3(1.8f, 0.3f, 0.2f), trim);

            AddHeadlights(root, 0.68f, 0.72f, 2.22f);

            root.gameObject.SetActive(false);
            return root;
        }

        static void AddHeadlights(Transform parent, float offsetX, float heightY, float posZ)
        {
            var matHeadlight = GetOrCreateColorMat("Car_Headlight_Glow", new Color(1f, 0.95f, 0.8f));

            var lampL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lampL.name = "Headlight_L";
            lampL.transform.SetParent(parent, false);
            lampL.transform.localPosition = new Vector3(-offsetX, heightY, posZ);
            lampL.transform.localScale = new Vector3(0.28f, 0.15f, 0.08f);
            lampL.GetComponent<Renderer>().sharedMaterial = matHeadlight;
            UnityEngine.Object.DestroyImmediate(lampL.GetComponent<Collider>());

            var lampR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lampR.name = "Headlight_R";
            lampR.transform.SetParent(parent, false);
            lampR.transform.localPosition = new Vector3(offsetX, heightY, posZ);
            lampR.transform.localScale = new Vector3(0.28f, 0.15f, 0.08f);
            lampR.GetComponent<Renderer>().sharedMaterial = matHeadlight;
            UnityEngine.Object.DestroyImmediate(lampR.GetComponent<Collider>());
        }

        static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static Material GetOrCreateColorMat(string name, Color color)
        {
            string path = "Assets/Materials/BrightonIntro/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}

