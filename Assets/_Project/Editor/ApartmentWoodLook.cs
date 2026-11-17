using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ApartmentWoodLook
{
    const string Folder = "Assets/_Project/ArtDirection/ApartmentWood/";
    const string LeafName = "Wood_Joinery_Visual";
    const string FittedLeafName = "Fitted_Bedroom_Door";

    [MenuItem("Tools/Brighton/Apartment: refresh doors, wood and light")]
    public static void Save()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/01_FatherApartment.unity")
            throw new InvalidOperationException("Open the father apartment in Edit Mode.");
        Apply(GameObject.Find("BrooklynApartment").transform);
        ApartmentNoirLook.RefreshIfPresent(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Apartment could not be saved.");
    }

    public static void Apply(Transform apartment)
    {
        var roomModel = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Apartment_Room_Leaf.glb");
        var cabinetModel = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Apartment_Cabinet_Leaf.glb");
        var fittedModel = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Bedroom_Door_Fitted.glb");
        if (!roomModel || !cabinetModel || !fittedModel) throw new InvalidOperationException("Import the Blender door models first.");
        var nodes = apartment.gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform Find(string name) => nodes.First(t => t && t.name == name);
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Quiet_Oak_Albedo.png");
        var normalPath = Folder + "Quiet_Oak_Normal.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        if (importer.textureType != TextureImporterType.NormalMap || importer.convertToNormalmap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.convertToNormalmap = false;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        var oak = Finish("Quiet_Matte_Oak", albedo, normal, new Color(.94f,.93f,.91f), .26f);
        var brass = Finish("Brass_Door_Hardware", null, null, new Color(.58f,.43f,.25f), .37f, .71f);
        var latch = Finish("Bedroom_Latch_Recess", null, null, new Color(.105f,.087f,.064f), .25f, .15f);
        ApplyFurnitureFinishes(nodes, oak);

        foreach (var t in nodes)
        {
            if (!t || !t.gameObject.activeInHierarchy || !t.TryGetComponent<ApartmentInteractable>(out var interaction) || interaction.kind != "hinge") continue;
            bool entry = t.name=="INT_EntryDoor", bath = t.name=="INT_BathroomDoor", bed = t.name=="INT_Bedroom_Door";
            if (bed)
            {
                FitBedroomDoor(t, fittedModel, oak, brass, latch,
                    Find("Shell_BedDoor_Jamb_B").GetComponent<Renderer>().bounds.max.z,
                    Find("Shell_BedDoor_Jamb_A").GetComponent<Renderer>().bounds.min.z,
                    Find("Shell_BedDoor_Lintel").GetComponent<Renderer>().bounds.min.y,
                    Find("Shell_Floor_Bedroom").GetComponent<Renderer>().bounds.max.y);
                continue;
            }
            bool room = entry || bath;
            bool kitchenDoor = t.name.StartsWith("INT_Kitchen") && t.name.Contains("Door");
            bool bedroomCabinet = t.name.StartsWith("INT_Bedroom_Wardrobe");
            bool cabinet = kitchenDoor || bedroomCabinet || t.name.StartsWith("INT_Wardrobe_Door") || t.name.StartsWith("INT_TVStand_Door");
            if (!room && !cabinet) continue;
            var existing = t.Find(LeafName);
            if (existing)
            {
                FinishLeaf(existing, oak, brass);
                SetHandleHeight(existing.GetComponentsInChildren<MeshRenderer>(), t.name, existing);
                continue;
            }
            var renderers = t.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) continue;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            var facing = entry ? Vector3.back : bath ? Vector3.right : bedroomCabinet ? Vector3.back : kitchenDoor ? Vector3.forward : Vector3.right;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(room ? roomModel : cabinetModel, t);
            model.name = LeafName;
            var mesh = model.GetComponentsInChildren<MeshRenderer>();
            var knob = mesh.First(r => r.name.StartsWith("Knob"));
            if (!room && model.transform.InverseTransformPoint(knob.bounds.center).z < 0)
                facing = -facing;
            var m = model.transform;
            m.rotation = Quaternion.LookRotation(facing,Vector3.up);
            m.position = bounds.center;
            var width = Mathf.Abs(facing.x)>.5f ? bounds.size.z : bounds.size.x;
            var depth = room ? .064f : .045f;
            float roomHeight = bounds.size.y - .009f;
            m.localScale = new Vector3((width-.009f)/t.lossyScale.x,roomHeight/t.lossyScale.y,(depth/.064f)/t.lossyScale.z);
            var widthAxis = m.right;
            if (Vector3.Dot(knob.bounds.center-m.position,widthAxis)*Vector3.Dot(t.position-m.position,widthAxis)>0)
            {
                var scale = m.localScale;
                scale.x *= -1;
                m.localScale = scale;
            }
            FinishLeaf(m, oak, brass);
            SetHandleHeight(mesh,t.name,m);
            foreach (var r in renderers) r.enabled = false;
        }

        FitHallWardrobe(apartment);

        var crt = Find("CRT_White_Noise_Light").GetComponent<Light>();
        crt.shadows = LightShadows.Soft;
        crt.shadowStrength = .94f;
        crt.shadowBias = .10f;
        crt.shadowNormalBias = .16f;
        crt.intensity = 7.4f;
        foreach (var renderer in nodes.Where(t => t && t.root.name=="Player").SelectMany(t => t.GetComponents<SkinnedMeshRenderer>()))
            renderer.shadowCastingMode = ShadowCastingMode.On;

        ConfigureBedroomWindowLight(Find("Street_Sodium_Bedroom").GetComponent<Light>(), Find("Bedroom_Kitchen_Window"));
        Find("Bedroom_Ceiling_Front").GetComponent<Light>().intensity = 1.04f;
        Find("Bedroom_Ceiling_Back").GetComponent<Light>().intensity = .73f;
        var sofa = Find("Sofa_Dad");
        var collider = Find("COL_Sofa");
        var radiator = Find("Radiator_Assembly").GetComponentsInChildren<Renderer>();
        float end = radiator.Max(r => r.bounds.max.z);
        float gap = sofa.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.z) - end;
        if (gap < .18f)
        {
            var shift = Vector3.forward * (.18f-gap);
            sofa.position += shift;
            collider.position += shift;
        }
        RefreshCabinetDoors(apartment);
    }

    public static void RefreshCabinetDoors(Transform apartment)
    {
        var oak = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Quiet_Matte_Oak.mat");
        var dark = Finish("Dark_Brown_Cabinet_Doors", oak.GetTexture("_BaseMap") as Texture2D,
            oak.GetTexture("_BumpMap") as Texture2D, new Color(.64f,.52f,.44f), .28f);
        var nodes = apartment.GetComponentsInChildren<Transform>(true);
        foreach (var renderer in nodes.SelectMany(t => t.GetComponents<MeshRenderer>()))
        {
            var t = renderer.transform;
            var leaf = t.parent;
            while (leaf && leaf.name != LeafName) leaf = leaf.parent;
            bool movingDoor = leaf && leaf.parent &&
                (leaf.parent.name.StartsWith("INT_Kitchen") || leaf.parent.name.StartsWith("INT_TVStand_Door")
                || leaf.parent.name.StartsWith("INT_Wardrobe_Door") || leaf.parent.name.StartsWith("INT_Bedroom_Wardrobe"));
            bool cabinetFront = (Under(t,"Walnut_Cabinet") && (t.name.StartsWith("Panel_Door") || t.name.StartsWith("Raised_Door_Panel")
                || t.name.StartsWith("Shallow_Drawer"))) || t.name is "CutleryDrawer_Visual" or "TVStand_Assembly_Visual";
            if (!movingDoor && !cabinetFront) continue;
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i=0;i<materials.Length;i++)
            {
                if (!materials[i] || materials[i]==dark) continue;
                if (materials[i].name is not ("Quiet_Matte_Oak" or "OldWalnut" or "Details_Walnut")) continue;
                materials[i] = dark;
                changed = true;
            }
            if (!changed) continue;
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        var door = nodes.First(t=>t.name=="INT_KitchenBase_Prep_Door00");
        var visual = door.Find(LeafName);
        if (!visual) return;
        var sideA = nodes.First(t=>t.name=="KitchenBase_Prep_Side").GetComponent<Renderer>().bounds;
        var sideB = nodes.First(t=>t.name=="KitchenBase_Prep_Side.001").GetComponent<Renderer>().bounds;
        const float reveal = .005f;
        float min = Mathf.Min(sideA.min.x,sideB.min.x) + reveal;
        float max = Mathf.Max(sideA.max.x,sideB.max.x) - reveal;
        float center = (max+min)*.5f;
        var position = visual.position;
        position.x = center;
        visual.position = position;
        var scale = visual.localScale;
        scale.x = Mathf.Sign(scale.x)*(max-min)/Mathf.Abs(door.lossyScale.x);
        visual.localScale = scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);

        var collider = door.GetComponentInChildren<BoxCollider>();
        var boxCenter = collider.bounds.center;
        boxCenter.x = center;
        boxCenter.z = position.z;
        collider.center = collider.transform.InverseTransformPoint(boxCenter);
        var boxSize = collider.size;
        boxSize.x = (max-min)/Mathf.Abs(collider.transform.lossyScale.x);
        collider.size = boxSize;
        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
    }

    public static void RefreshFurnitureFinishes(Transform apartment)
    {
        var oak = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Quiet_Matte_Oak.mat");
        if (!oak) throw new InvalidOperationException("Import the matte oak material first.");
        var nodes = apartment.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        ApplyFurnitureFinishes(nodes, oak);
    }

    public static void FitHallWardrobe(Transform apartment)
    {
        var wardrobe = apartment.Find("Furniture/Wardrobe_Assembly");
        var frame = wardrobe.Find("Wardrobe_Assembly_Visual").GetComponent<Renderer>().bounds;
        FitHallLeaf(wardrobe.Find("INT_Wardrobe_Door00"), frame.min.z + .008f, frame.center.z - .0045f, frame.max.x, true);
        FitHallLeaf(wardrobe.Find("INT_Wardrobe_Door01"), frame.center.z + .0045f, frame.max.z - .008f, frame.max.x, false);
    }

    static void FitHallLeaf(Transform hinge, float minZ, float maxZ, float frameFront, bool lowZ)
    {
        var leaf = hinge.Find(LeafName);
        var stiles = leaf.GetComponentsInChildren<MeshRenderer>().Where(renderer =>
            renderer.name.StartsWith("Stile.", StringComparison.Ordinal)).ToArray();

        Vector3 leafPosition = leaf.position;
        float width = maxZ - minZ;
        float oldWidth = stiles.Max(r => r.bounds.max.z) - stiles.Min(r => r.bounds.min.z);
        var scale = leaf.localScale;
        scale.x *= width / oldWidth;
        leaf.localScale = scale;
        var scaledMin = stiles.Min(r => r.bounds.min.z);
        var scaledMax = stiles.Max(r => r.bounds.max.z);
        leafPosition.z += (minZ + maxZ - scaledMin - scaledMax) * .5f;
        leafPosition.x += frameFront + .006f - stiles.Min(r => r.bounds.min.x);

        // Keep the pivot at the outer edge of the widened leaf so it still swings away from the cabinet.
        hinge.position = new Vector3(frameFront + .019f, hinge.position.y, lowZ ? minZ - .003f : maxZ + .003f);
        leaf.position = leafPosition;
        var hitbox = hinge.GetComponentInChildren<BoxCollider>();
        var center = hitbox.bounds.center;
        center.z = (minZ + maxZ) * .5f;
        center.x = (stiles.Min(r => r.bounds.min.x) + stiles.Max(r => r.bounds.max.x)) * .5f;
        hitbox.center = hitbox.transform.InverseTransformPoint(center);
        var widthAxis = hitbox.transform.InverseTransformVector(Vector3.forward);
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(widthAxis[i]) < widthAxis.magnitude * .99f) continue;
            var size = hitbox.size;
            size[i] = width * Mathf.Abs(widthAxis[i]);
            hitbox.size = size;
            break;
        }
    }

    static void ApplyFurnitureFinishes(Transform[] nodes, Material oak)
    {
        const string art = "Assets/_Project/Arts/Apartaments/";
        var walnut = AssetDatabase.LoadAssetAtPath<Material>(art + "Materials/OldWalnut.mat");
        var parquet = AssetDatabase.LoadAssetAtPath<Material>(art + "Materials/RedOakFloor.mat");
        var clueWood = AssetDatabase.LoadAssetAtPath<Material>(art + "Evidence/Materials/Clue_Walnut.mat");
        var clueEdge = AssetDatabase.LoadAssetAtPath<Material>(art + "Evidence/Materials/Clue_WoodEdge.mat");
        var record = AssetDatabase.LoadAssetAtPath<Material>(art + "RecordPlayer/Materials/RP_Walnut.mat");
        var kitchen = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/ArtDirection/ApartmentKitchen/Kitchen_Satin_Walnut.mat");
        foreach (var t in nodes)
        {
            if (Under(t, LeafName) || Under(t, FittedLeafName)) continue;
            foreach (var renderer in t.GetComponents<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i=0;i<slots.Length;i++)
                {
                    if (!slots[i]) continue;
                    var current = slots[i].name;
                    Material finish = null;
                    if (UseOakOnFurniture(t, current))
                        finish = oak;
                    else if (current=="Oak_Aged_Parquet") finish = parquet;
                    else if (current=="Walnut_Kitchen_Varnish") finish = kitchen;
                    else if (current=="Walnut_Furniture_Aged")
                    {
                        if (Under(t, "Evidence_Visual")) finish = clueWood;
                        else if (t.name=="RP_Plinth") finish = record;
                        else if (t.name=="Wall_Shelf") finish = PrefabMaterial(renderer,i,"Wall_Shelf_Mat");
                        else finish = walnut;
                    }
                    else if (current=="Walnut_Handled_Edges" && Under(t,"Evidence_Visual"))
                        finish = clueEdge;
                    else if (current=="Walnut_Handled_Edges" || current=="Walnut_Inset_Veneer" || current=="Walnut_Stiles_Satin")
                    {
                        var original = current=="Walnut_Handled_Edges" ? "Walnut_Trim" : current=="Walnut_Inset_Veneer" ? "Walnut_Panel" : "Walnut_Solid";
                        finish = PrefabMaterial(renderer,i,original) ?? walnut;
                    }
                    if (!finish || slots[i] == finish) continue;
                    slots[i] = finish;
                    changed = true;
                }
                if (changed)
                {
                    renderer.sharedMaterials = slots;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
        }
    }

    static bool UseOakOnFurniture(Transform mesh, string finish)
    {
        if (Under(mesh,"Dresser_Ransacked"))
            return finish is "Clue_Walnut" or "Clue_WoodEdge" or "Walnut_Furniture_Aged" or "Walnut_Handled_Edges"
                || (mesh.name=="Dresser_Body" && finish=="Clue_Shadow");

        bool kitchenCabinet = mesh.name.StartsWith("KitchenBase_") || mesh.name.StartsWith("KitchenUpper_")
            || mesh.name.StartsWith("Kitchen_ToeKick") || mesh.name is "Sink_Front_Rail" or "CutleryDrawer_Visual";
        if (kitchenCabinet)
            return finish is "OldWalnut" or "Kitchen_Satin_Walnut" or "Walnut_Kitchen_Varnish" or "Walnut_Furniture_Aged";

        bool storage = Under(mesh,"Walnut_Cabinet") || Under(mesh,"Bedroom_Wardrobe") || Under(mesh,"Wardrobe_Assembly")
            || mesh.name is "Hall_Wardrobe_Door_Stop" or "Bedroom_Wardrobe_Door_Stop";
        return storage && (finish is "OldWalnut" or "Details_Walnut" or "Walnut_Solid" or "Walnut_Trim" or "Walnut_Panel"
            or "Walnut_Furniture_Aged" or "Walnut_Handled_Edges" or "Walnut_Inset_Veneer" or "Walnut_Stiles_Satin");
    }

    static Material PrefabMaterial(Renderer renderer, int index, string name)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
        var materials = source ? source.sharedMaterials : Array.Empty<Material>();
        return index < materials.Length && materials[index] && materials[index].name == name ? materials[index] : null;
    }

    static bool Under(Transform node, string name)
    {
        for (var n=node;n;n=n.parent)
            if (n.name==name) return true;
        return false;
    }

    static void FinishLeaf(Transform leaf, Material oak, Material brass)
    {
        foreach (var r in leaf.GetComponentsInChildren<MeshRenderer>())
        {
            r.sharedMaterials = r.sharedMaterials.Select(x=>x && x.name.Contains("Brass",StringComparison.OrdinalIgnoreCase) ? brass : oak).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
    }

    static void FitBedroomDoor(Transform hinge, GameObject prefab, Material oak, Material brass, Material latch,
        float jambMin, float jambMax, float lintel, float floor)
    {
        var generic = hinge.Find(LeafName);
        if (generic) UnityEngine.Object.DestroyImmediate(generic.gameObject);
        var model = hinge.Find(FittedLeafName);
        if (!model)
        {
            foreach (var r in hinge.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
            model = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, hinge)).transform;
            model.name = FittedLeafName;
        }
        const float sideGap=.006f, bottomGap=.011f, topGap=.009f, depth=.047f;
        float width=jambMax-jambMin-2*sideGap, height=lintel-floor-bottomGap-topGap;
        var center=new Vector3(hinge.position.x,floor+bottomGap+height*.5f,(jambMin+jambMax)*.5f);
        var knob=model.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.StartsWith("Knob"));
        float handleX=model.InverseTransformPoint(knob.bounds.center).x;
        model.SetPositionAndRotation(center,Quaternion.LookRotation(handleX < 0 ? Vector3.right : Vector3.left));
        model.localScale=new Vector3(width/1.009f/hinge.lossyScale.x,height/2.372f/hinge.lossyScale.y,1f/hinge.lossyScale.z);
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
        {
            r.sharedMaterial = r.name.StartsWith("Keyhole") ? latch :
                r.name.StartsWith("Brass") || r.name.StartsWith("Knob") ? brass : oak;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        var hitbox = hinge.Find("Solid_Hitbox").GetComponent<BoxCollider>();
        hitbox.center = hitbox.transform.InverseTransformPoint(center);
        var scale=hitbox.transform.lossyScale;
        hitbox.size=new Vector3(depth/scale.x,height/scale.y,width/scale.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
    }

    static void ConfigureBedroomWindowLight(Light light, Transform window)
    {
        var glass = window.Find("Window_Glazing").GetComponent<Renderer>().bounds;
        var cookiePath = Folder + "Bedroom_Blinds_Projection.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(cookiePath);
        if (importer.textureType!=TextureImporterType.Cookie || importer.wrapMode!=TextureWrapMode.Clamp)
        {
            importer.textureType = TextureImporterType.Cookie;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
        // Position at the room-facing plane of the blinds. The cookie supplies their occlusion.
        var source = new Vector3(glass.center.x+.251f,glass.center.y+.27f,glass.center.z-.18f);
        light.transform.SetPositionAndRotation(source,Quaternion.LookRotation(new Vector3(-5.94f,.07f,-3.50f)-source,Vector3.up));
        light.type = LightType.Spot;
        light.color = new Color(.64f,.77f,1f);
        light.intensity = 22f;
        light.range = 6.9f;
        light.spotAngle = 49f;
        light.innerSpotAngle = 43f;
        light.cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(cookiePath);
        light.shadows = LightShadows.Soft;
        light.shadowBias = .11f;
        light.shadowNormalBias = .19f;
    }

    static void SetHandleHeight(MeshRenderer[] meshes, string door, Transform leaf)
    {
        float? height = door.StartsWith("INT_KitchenUpper") ? -.30f :
            door.Contains("Wardrobe") ? -.04f : null;
        if (!height.HasValue) return;
        foreach (var part in meshes.Where(r => r.name.StartsWith("Knob") || r.name.StartsWith("Escutcheon")))
        {
            var at = leaf.InverseTransformPoint(part.transform.position);
            at.y = height.Value;
            part.transform.position = leaf.TransformPoint(at);
            PrefabUtility.RecordPrefabInstancePropertyModifications(part.transform);
        }
    }

    static Material Finish(string name, Texture2D albedo, Texture2D normal, Color tint, float smoothness, float metal = 0)
    {
        var path = Folder + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m,path);
        }
        m.SetColor("_BaseColor",tint);
        m.SetFloat("_Smoothness",smoothness);
        m.SetFloat("_Metallic",metal);
        if (albedo)
        {
            m.SetTexture("_BaseMap",albedo);
            m.SetTexture("_BumpMap",normal);
            m.SetFloat("_BumpScale",.68f);
            m.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
