using System;
using System.Linq;
using CarXCase.Apartment;
using DetectiveGame.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class ApartmentStoryArt
{
    const string Models = "Assets/_Project/Arts/Apartaments/StoryProps/";
    const string Surfaces = "Assets/_Project/ArtDirection/ApartmentStory/";
    const int LandingLightLayer = 1 << 2;

    internal static void Apply(Scene scene, Transform story, Transform[] nodes)
    {
        Transform Find(string name) => nodes.First(node => node && node.name == name);
        var landing = story.Find("Second_Floor_Landing");
        var frontDoor = Find("INT_EntryDoor");
        ApplyLanding(landing);
        Find("PlayerCamera").GetComponent<UniversalAdditionalCameraData>().requiresDepthTexture = true;
        RemoveOldEvidence(nodes);

        var keys = story.Find("Evidence_Lock_And_Keys");
        RemoveChildren(keys, "Hall key ledge", "Shelf lip", "Intact strike plate", "Anchor_Keyring");
        var ring = keys.Find("Anchor_Keyring");
        if (!ring) ring = Model(keys, "Anchor_Keyring", new Vector3(.015f, -.009f, -.010f), Quaternion.Euler(0, 21, 0));
        var cylinder = Find("Intact lock cylinder");
        cylinder.rotation = Quaternion.LookRotation(Vector3.back);
        RemoveChildren(cylinder, "Cylinder_Aim", "Door_Lock_Cylinder");
        var cylinderModel = Model(cylinder, "Door_Lock_Cylinder", Vector3.zero, Quaternion.identity);
        var lockPlate = frontDoor.Find("Intact strike plate") ?? keys.Find("Intact strike plate");
        SetHighlights(keys.GetComponent<ClueObject>(), ring, cylinderModel, lockPlate);

        var cabinet = story.Find("Evidence_Valuables_By_Sony_CRT");
        RemoveNamedChild(cabinet, "Pobeda wristwatch");
        RemoveNamedChild(cabinet, "Silver cigarette case");
        var watch = Model(cabinet, "Pobeda_Wristwatch", new Vector3(-.068f, -.012f, -.087f), Quaternion.Euler(0, 16, 0));
        var caseModel = Model(cabinet, "Silver_Cigarette_Case", new Vector3(-.162f, -.012f, .093f), Quaternion.Euler(0, -17, 0));
        SetHighlights(cabinet.GetComponent<ClueObject>(), watch, caseModel);

        var matches = story.Find("Evidence_Sterlyad_Matchbox");
        RemoveChildren(matches, "Sterlyad_Matchbox");
        var matchbox = Model(matches, "Sterlyad_Matchbox", Vector3.zero, Quaternion.identity);
        SetHighlights(matches.GetComponent<ClueObject>(), matchbox);

        var answer = story.Find("Evidence_Answering_Machine");
        RemoveChildren(answer, "Walnut telephone shelf", "Shelf support", "Play_Aim",
            "Blinking message indicator", "Answering_Machine", "Microcassette_MC60");
        var baseModel = Model(answer, "Answering_Machine", new Vector3(0, .012f, 0), Quaternion.identity);
        var well = baseModel.GetComponentsInChildren<Transform>().First(t => t.name == "Cassette well floor");
        Vector3 cassetteSeat = answer.InverseTransformPoint(well.position) + new Vector3(0, .010f, 0);
        var cassette = Model(answer, "Microcassette_MC60", cassetteSeat, Quaternion.identity);
        SetHighlights(answer.GetComponent<ClueObject>(), baseModel, cassette);
        var lens = baseModel.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Message_LED", StringComparison.Ordinal));
        var point = answer.Find("Blinking message indicator").GetComponent<Light>();
        point.transform.position = lens.bounds.center + Vector3.up * .018f;
        point.intensity = .046f;
        point.range = .14f;
        Bind(answer.GetComponent<ApartmentAnsweringMachine>(), "indicatorLens", lens);

        var lightSwitch = story.Find("Entry_Bakelite_Light_Switch");
        var entranceFill = lightSwitch.Find("Entrance bounce during the bulb flash");
        if (entranceFill) entranceFill.name = "Apartment entrance fill";
        RemoveChildren(lightSwitch, "Switch_Reach", "Entry_Light_Switch", "Apartment entrance fill");
        var switchModel = Model(lightSwitch, "Entry_Light_Switch", Vector3.zero, Quaternion.identity);
        Bind(lightSwitch.GetComponent<ApartmentLightSwitch>(), "rocker",
            switchModel.GetComponentsInChildren<Transform>().First(t => t.name == "Rocker"));

        var record = Find("RP_Record");
        record.GetComponent<Renderer>().enabled = false;
        var pivot = record.parent;
        var spin = pivot.GetComponent<RecordSpin>();
        if (spin) spin.playing = false;
        RemoveNamedChild(record, "Fathers_Vinyl");
        // The imported turntable hierarchy uses centimetre scaling. Scene props are authored in metres.
        var disc = Model(story, "Fathers_Vinyl", Vector3.zero, Quaternion.identity);
        disc.SetPositionAndRotation(pivot.position + Vector3.up * .002f, Quaternion.Euler(0, 30, 0));
        var sleeveAnchor = story.Find("Record_Sleeve_Anchor");
        if (!sleeveAnchor)
        {
            sleeveAnchor = new GameObject("Record_Sleeve_Anchor").transform;
            sleeveAnchor.SetParent(story, false);
            sleeveAnchor.position = new Vector3(-6.37f, .543f, -5.79f);
            sleeveAnchor.rotation = Quaternion.Euler(0, 102, 0);
            var aim = sleeveAnchor.gameObject.AddComponent<BoxCollider>();
            aim.isTrigger = true;
            aim.center = new Vector3(0, .025f, 0);
            aim.size = new Vector3(.316f, .092f, .316f);
        }
        var sleeve = Model(sleeveAnchor, "Fathers_Record_Sleeve", Vector3.zero, Quaternion.identity);
        var clue = record.GetComponent<ClueObject>();
        var aimTarget = sleeveAnchor.GetComponent<ClueAimTarget>();
        if (!aimTarget) aimTarget = sleeveAnchor.gameObject.AddComponent<ClueAimTarget>();
        aimTarget.clue = clue;
        var data = FatherCase.Evidence.Single(item => item.id == FatherCase.Record);
        clue.clueDescription = data.observation;
        clue.journalThought = data.thought;
        clue.inspectionComment = data.thought;
        SetHighlights(clue, disc, sleeve);

        var recordLight = story.Find("Window_Reflected_Light").Find("Window bounce on the record");
        if (!recordLight)
        {
            recordLight = new GameObject("Window bounce on the record", typeof(Light)).transform;
            recordLight.SetParent(story.Find("Window_Reflected_Light"), false);
        }
        recordLight.SetPositionAndRotation(new Vector3(-7.00f, 1.59f, -4.97f),
            Quaternion.LookRotation(new Vector3(.57f, -.86f, -.57f)));
        var spill = recordLight.GetComponent<Light>();
        spill.type = LightType.Spot;
        spill.color = new Color(.73f, .77f, .88f);
        spill.intensity = .85f;
        spill.range = 2.26f;
        spill.spotAngle = 90f;
        spill.innerSpotAngle = 58f;

        var threshold = story.Find("Enter_Father_Apartment").GetComponent<ApartmentThreshold>();
        ConfigureDoors(threshold, frontDoor, Find("INT_BathroomDoor"), cylinder);
        MatchLandingDoors(landing, frontDoor);
        foreach (var renderer in landing.GetComponentsInChildren<Renderer>(true))
            renderer.renderingLayerMask |= (uint)LandingLightLayer;
        foreach (var renderer in frontDoor.GetComponentsInChildren<Renderer>(true))
            renderer.renderingLayerMask |= (uint)LandingLightLayer;
        var hint = story.Find("Journal_Hint_Inside_Threshold");
        if (!hint)
        {
            hint = new GameObject("Journal_Hint_Inside_Threshold").transform;
            hint.SetParent(story, false);
            hint.SetPositionAndRotation(new Vector3(-3.22f, 0, -.47f), Quaternion.Euler(0, 180, 0));
        }
        Bind(threshold, "journalHintPoint", hint);
    }

    static void ConfigureDoors(ApartmentThreshold threshold, Transform entry, Transform bathroom, Transform insideLock)
    {
        var hinge = entry.GetComponent<ApartmentInteractable>();
        hinge.enabled = false; // ApartmentThreshold owns the first opening.
        hinge.prioritizeWhenAimed = true;

        var bathHinge = bathroom.GetComponent<ApartmentInteractable>();
        bathHinge.enabled = true;
        bathHinge.prioritizeWhenAimed = true;

        const string anchorName = "Exterior lock cylinder";
        var exteriorLock = entry.Find(anchorName);
        if (!exteriorLock)
        {
            exteriorLock = new GameObject(anchorName).transform;
            exteriorLock.SetParent(entry, false);
        }

        Vector3 outward = entry.TransformDirection(Vector3.down);
        exteriorLock.SetPositionAndRotation(insideLock.position + outward * .074f, Quaternion.LookRotation(outward));
        Model(exteriorLock, "Door_Lock_Cylinder", Vector3.zero, Quaternion.identity);
        var settings = new SerializedObject(threshold);
        settings.FindProperty("exteriorLock").objectReferenceValue = exteriorLock;
        var angle = settings.FindProperty("openAngle");
        if (angle.floatValue < 0f) angle.floatValue = 86f;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ApplyLanding(Transform landing)
    {
        var plaster = Surface("Stairwell_Plaster", new Vector2(4.4f, 1.8f), .15f, .72f);
        var ceiling = CeilingPlaster();
        var returns = Surface("Stairwell_Plaster", new Vector2(.7f, 1.65f), .15f, .72f, "Doorway");
        var paint = Surface("Stairwell_Enamel", new Vector2(3.3f, .73f), .29f, .64f);
        var paintReturn = Surface("Stairwell_Enamel", new Vector2(.72f, .73f), .29f, .64f, "Doorway");
        var terrazzo = Surface("Stairwell_Terrazzo", new Vector2(2.5f, 3.75f), .23f, .61f);
        var tread = Surface("Stairwell_Terrazzo", new Vector2(2.5f, .38f), .23f, .61f, "Tread");

        foreach (Transform part in landing)
        {
            var renderer = part.GetComponent<Renderer>();
            if (renderer)
            {
                string name = part.name;
                if (name == "Stairwell wall") renderer.sharedMaterial = plaster;
                else if (name == "Stairwell ceiling") renderer.sharedMaterial = ceiling;
                else if (name == "Landing wainscot") renderer.sharedMaterial = paint;
                else if (name is "Terrazzo landing" or "Lower landing") renderer.sharedMaterial = terrazzo;
                else if (name.StartsWith("Terrazzo stair ", StringComparison.Ordinal)) renderer.sharedMaterial = tread;
            }
        }

        ApplyLowerFlight(landing, returns, tread);

        // Cover the apartment shell from the outside, independently of its indoor wallpaper.
        var facade = landing.Find("Landing_Entrance_Facade");
        if (!facade)
        {
            facade = new GameObject("Landing_Entrance_Facade").transform;
            facade.SetParent(landing, false);
        }
        AddPanel(facade, "West plaster return", new Vector3(-4.185f, 1.803f, .213f), new Vector3(.87f, 1.91f, .055f), returns);
        AddPanel(facade, "East plaster return", new Vector3(-2.262f, 1.803f, .213f), new Vector3(.853f, 1.91f, .055f), returns);
        AddPanel(facade, "West enamel return", new Vector3(-4.185f, .435f, .214f), new Vector3(.87f, .87f, .055f), paintReturn);
        AddPanel(facade, "East enamel return", new Vector3(-2.262f, .435f, .214f), new Vector3(.853f, .87f, .055f), paintReturn);
        AddPanel(facade, "Plaster above casing", new Vector3(-3.22f, 2.605f, .213f), new Vector3(1.09f, .34f, .055f), returns);
        var jamb = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/ArtDirection/ApartmentWood/Quiet_Matte_Oak.mat");
        AddPanel(facade, "West exterior jamb", new Vector3(-3.756f, 1.19f, .141f), new Vector3(.046f, 2.38f, .217f), jamb);
        AddPanel(facade, "East exterior jamb", new Vector3(-2.685f, 1.19f, .141f), new Vector3(.046f, 2.38f, .217f), jamb);
        AddPanel(facade, "Exterior head casing", new Vector3(-3.22f, 2.403f, .142f), new Vector3(1.119f, .056f, .218f), jamb);

        var bowl = landing.Find("Landing fixture opal bowl");
        var diffuser = bowl.GetComponent<Renderer>().sharedMaterial;
        var warm = new Color(1f, .79f, .58f);
        SetEmission(diffuser, new Color(2.8f, 1.85f, 1.08f));

        var fixture = FindOrAddLight(landing, "Landing light on jamb and threshold");
        fixture.transform.position = bowl.position + Vector3.down * .12f;
        fixture.enabled = true;
        fixture.type = LightType.Point;
        fixture.color = warm;
        fixture.intensity = 8.2f;
        fixture.range = 6.1f;
        fixture.shadows = LightShadows.Soft;
        fixture.GetUniversalAdditionalLightData().renderingLayers = (uint)LandingLightLayer;

        var stairBounce = landing.Find("Light fading down the staircase").GetComponent<Light>();
        stairBounce.color = new Color(1f, .86f, .71f);
        stairBounce.intensity = .95f;
        stairBounce.range = 3.6f;
        stairBounce.GetUniversalAdditionalLightData().renderingLayers = (uint)LandingLightLayer;
        RemoveNamedChild(landing, "Warm spill through door");
        RemoveNamedChild(landing.parent.Find("Window_Reflected_Light"), "Warm return on lock and keys");
    }

    static void MatchLandingDoors(Transform landing, Transform entry)
    {
        var marker = entry.Find("Apartment_2B_Marker");
        var outward = entry.TransformDirection(Vector3.down);
        marker.rotation = Quaternion.LookRotation(outward);
        RemoveNamedChild(marker, "2B");
        Model(marker, "DoorPlaque_2B", new Vector3(0, 0, .003f), Quaternion.identity);

        var facade = landing.Find("Landing_Entrance_Facade");
        var leaf = entry.Find("Wood_Joinery_Visual");
        if (!leaf) leaf = entry.Find("EntryDoor_Visual");
        var exteriorLock = entry.Find("Exterior lock cylinder");
        Vector3 reference = new Vector3(marker.position.x, entry.position.y, marker.position.z);
        Quaternion intoDoorSpace = Quaternion.Inverse(marker.rotation);
        const float offsetFromWall = .083f;

        foreach (Transform neighbor in landing)
        {
            if (!neighbor.name.StartsWith("Neighbor_Door_", StringComparison.Ordinal)) continue;
            string number = neighbor.position.x < reference.x
                ? (neighbor.position.z < 2f ? "2A" : "2C")
                : (neighbor.position.z < 2f ? "2D" : "2E");
            foreach (Transform child in neighbor.Cast<Transform>().ToArray())
            {
                if (child.name is "Door casing" or "Closed door" or "Recessed panel molding" or "Veneer panel" or "Brass doorknob"
                    || child.name == number)
                    Object.DestroyImmediate(child.gameObject);
            }

            CopyDoorPart(leaf);
            CopyDoorPart(exteriorLock);
            CopyDoorPart(facade.Find("West exterior jamb"));
            CopyDoorPart(facade.Find("East exterior jamb"));
            CopyDoorPart(facade.Find("Exterior head casing"));
            var plaque = Model(neighbor, "DoorPlaque_" + number,
                new Vector3(0f, marker.position.y - reference.y, offsetFromWall + .003f * marker.lossyScale.z),
                Quaternion.identity);
            plaque.localScale = marker.lossyScale;
            ConfigureNeighborDoorCollision(neighbor);

            void CopyDoorPart(Transform source)
            {
                var copy = neighbor.Find(source.name);
                if (!copy)
                {
                    copy = Object.Instantiate(source.gameObject, neighbor, false).transform;
                    copy.name = source.name;
                }
                copy.SetLocalPositionAndRotation(Vector3.forward * offsetFromWall + intoDoorSpace * (source.position - reference),
                    intoDoorSpace * source.rotation);
                copy.localScale = source.lossyScale;
            }
        }
    }

    internal static void ConfigureNeighborDoorCollision(Transform door)
    {
        var leaf = door.Find("Wood_Joinery_Visual");
        if (!leaf) leaf = door.Find("EntryDoor_Visual");
        if (!leaf) return;

        var bounds = new Bounds();
        bool first = true;
        foreach (var mesh in leaf.GetComponentsInChildren<MeshFilter>())
        {
            if (!mesh.sharedMesh) continue;
            var toDoor = door.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
            var meshBounds = mesh.sharedMesh.bounds;
            for (int corner = 0; corner < 8; ++corner)
            {
                var local = new Vector3(
                    (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                    (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                    (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                var point = toDoor.MultiplyPoint3x4(local);
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        if (first) return;

        var collider = door.GetComponent<BoxCollider>();
        if (!collider) collider = Undo.AddComponent<BoxCollider>(door.gameObject);
        Undo.RecordObject(collider, "Fit collision to neighboring door");
        collider.center = bounds.center;
        collider.size = bounds.size;
        collider.isTrigger = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
    }

    static Material CeilingPlaster()
    {
        const string textures = "Assets/_Project/Arts/Apartaments/Shell/Textures/Ceiling_Stained_";
        var material = Finish("Stairwell_Ceiling_Plaster", "Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", new Color(.85f, .82f, .76f));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "Albedo.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "Normal.png"));
        material.SetTextureScale("_BaseMap", new Vector2(1.5f, 3.6f));
        material.SetFloat("_BumpScale", .48f);
        material.SetFloat("_Smoothness", .08f);
        material.SetFloat("_SpecularHighlights", 0);
        material.SetFloat("_EnvironmentReflections", 0);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ApplyLowerFlight(Transform landing, Material jamb, Material tread)
    {
        RemoveNamedChild(landing, "Shadow at stair foot");
        var flight = landing.Find("Lower_Stairwell");
        if (!flight)
        {
            flight = new GameObject("Lower_Stairwell").transform;
            flight.SetParent(landing, false);
            flight.localPosition = new Vector3(-3.22f, 0f, 7.04f);
        }

        var dimPlaster = Surface("Stairwell_Plaster", new Vector2(1.65f, 1.6f), .07f, .35f, "LowerFlight");
        dimPlaster.SetColor("_BaseColor", new Color(.42f, .43f, .42f));
        var rail = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/ArtDirection/ApartmentWood/Quiet_Matte_Oak.mat");

        AddPanel(flight, "Left masonry jamb", new Vector3(-1.188f, 1.115f, 0), new Vector3(.552f, 3.29f, .23f), jamb, true);
        AddPanel(flight, "Right masonry jamb", new Vector3(1.188f, 1.115f, 0), new Vector3(.552f, 3.29f, .23f), jamb, true);
        AddPanel(flight, "Masonry lintel", new Vector3(0, 2.315f, 0), new Vector3(1.83f, .93f, .23f), jamb, true);

        for (int i = 0; i < 4; i++)
        {
            float top = -.48f - .115f * (i + 1);
            AddPanel(flight, "Lower stair " + (i + 1), new Vector3(0, top - .075f, .145f + .34f * i),
                new Vector3(1.85f, .15f, .345f), tread, true);
        }

        // Enclose the walkable flight, including the recessed landing beyond the last step.
        AddPanel(flight, "Recessed landing", new Vector3(0, -1.02f, 2.46f), new Vector3(1.85f, .16f, 2.30f), tread, true);
        AddPanel(flight, "Left passage wall", new Vector3(-1.01f, .45f, 1.74f), new Vector3(.19f, 2.98f, 3.65f), dimPlaster, true);
        AddPanel(flight, "Right passage wall", new Vector3(1.01f, .45f, 1.74f), new Vector3(.19f, 2.98f, 3.65f), dimPlaster, true);
        AddPanel(flight, "Passage ceiling", new Vector3(0, 1.92f, 1.74f), new Vector3(2.21f, .15f, 3.65f), dimPlaster, true);
        AddPanel(flight, "Wall beyond the recess", new Vector3(0, .48f, 3.55f), new Vector3(2.21f, 3.04f, .20f), dimPlaster, true);
        var handrail = AddPanel(flight, "Lower stair handrail", new Vector3(-.845f, .35f, .66f),
            new Vector3(.065f, .055f, 1.44f), rail);
        handrail.localRotation = Quaternion.Euler(18.7f, 0, 0);

        var bounce = FindOrAddLight(flight, "Reflected light at the lower opening");
        bounce.transform.SetLocalPositionAndRotation(new Vector3(0, 1.05f, -.36f),
            Quaternion.LookRotation(new Vector3(0, -1f, .62f)));
        bounce.type = LightType.Spot;
        bounce.color = new Color(1f, .87f, .72f);
        bounce.intensity = .80f;
        bounce.GetUniversalAdditionalLightData().renderingLayers = (uint)LandingLightLayer;
        bounce.range = 3.15f;
        bounce.spotAngle = 96f;
        bounce.innerSpotAngle = 63f;
        bounce.shadows = LightShadows.None;

        var fog = Finish("Lower_Stairwell_Fog", "CarXCase/Interiors/Lower Stairwell Fog");
        var volume = AddPanel(flight, "Lower stairwell fog", new Vector3(0f, .37f, 1.86f),
            new Vector3(1.91f, 3.12f, 3.94f), fog).GetComponent<MeshRenderer>();
        volume.shadowCastingMode = ShadowCastingMode.Off;
        volume.receiveShadows = false;
    }

    internal static void ConfigureSwitchLighting(Transform switchRoot, Light[] sceneLights, Renderer[] renderers)
    {
        var ceiling = sceneLights.Single(l => l.name == "Ceiling_Warm");
        ceiling.color = new Color(1f, .79f, .56f);
        ceiling.intensity = 5.8f;
        ceiling.range = 6.3f;
        ceiling.spotAngle = 131f;
        ceiling.innerSpotAngle = 100f;
        ceiling.transform.rotation = Quaternion.LookRotation(new Vector3(-.19f, -1f, .47f));
        ceiling.shadows = LightShadows.Soft;

        var kitchenCeiling = sceneLights.Single(l => l.name == "Ceiling_Warm.001");
        kitchenCeiling.intensity = 3.1f;

        var entrance = FindOrAddLight(switchRoot, "Apartment entrance fill");
        entrance.transform.position = new Vector3(-3.83f, 2.10f, -1.06f);
        entrance.type = LightType.Point;
        entrance.color = ceiling.color;
        entrance.intensity = 1.45f;
        entrance.range = 2.25f;
        entrance.shadows = LightShadows.None;

        // Use the placed kitchen lamps and fitted vanity instead of their older duplicate lights.
        var fixtureNames = new[]
        {
            "Ceiling_Warm", "Ceiling_Warm.001", "Bath_CeilingBounce", "Bedroom_Ceiling_Front", "Bedroom_Ceiling_Back",
            "Room_Bounce", "Kitchen_Bounce", "Bedroom_Wall_Bounce", "Bedroom_Lamp_Light", "ReadingLamp_Light",
            "Sconce_Bounce", "Floor_Lamp_Table_Pool", "Floor_Lamp_Wall_Bounce", "CRT_White_Noise_Light", "CRT_Shelf_Spill"
        };
        var fixtures = sceneLights.Where(light =>
            fixtureNames.Contains(light.name) ||
            (light.name == "Vanity_Warm" && light.transform.parent.name == "Practical_Lights") ||
            (light.name == "Light" && light.transform.parent.name is "Under_Cabinet_Left" or "Under_Cabinet_Right"))
            .Append(entrance).ToArray();

        var data = new SerializedObject(switchRoot.GetComponent<ApartmentLightSwitch>());
        var lights = data.FindProperty("lights");
        lights.arraySize = fixtures.Length;
        for (int i = 0; i < fixtures.Length; i++)
        {
            var light = fixtures[i];
            light.enabled = false;
            if (light.TryGetComponent<CrtScreenLight>(out var flutter)) flutter.enabled = true;
            lights.GetArrayElementAtIndex(i).objectReferenceValue = light;
        }

        var surfaces = data.FindProperty("emissiveSurfaces");
        surfaces.ClearArray();
        foreach (var renderer in renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (!material) continue;
                var litColor = material.name switch
                {
                    "Apartment_Switched_Opal" or "Ceiling_Opal_Diffuser" => new Color(2.7f, 1.77f, .94f),
                    "Floor_Lamp_Warm_Linen" => new Color(1.58f, .82f, .31f),
                    "Kitchen_Warm_Fluorescent_Diffuser" => new Color(2.75f, 1.93f, 1.19f),
                    "Sconce_Warm_Opal" => new Color(1.21f, .79f, .44f),
                    "RP_Lamp" => new Color(2f, .23f, .08f),
                    _ => Color.black
                };
                if (litColor.maxColorComponent <= 0f) continue;
                // Keep the material variant available when the switch overrides its emitted color.
                SetEmission(material, material.GetColor("_EmissionColor"));
                int slot = surfaces.arraySize;
                surfaces.InsertArrayElementAtIndex(slot);
                var surface = surfaces.GetArrayElementAtIndex(slot);
                surface.FindPropertyRelative("renderer").objectReferenceValue = renderer;
                surface.FindPropertyRelative("materialIndex").intValue = i;
                surface.FindPropertyRelative("litColor").colorValue = litColor;
            }
        }

        var tv = renderers.Single(r => r.name == "CRT_Television_Visual");
        var screenMaterials = tv.sharedMaterials;
        int screenIndex = Array.FindIndex(screenMaterials, m => m && m.name is "Unpowered_CRT_Glass" or "Crt_White_Noise" or "CRTGlass");
        if (screenIndex < 0) throw new InvalidOperationException("The apartment TV is missing its glass material.");

        var signal = AssetDatabase.LoadAssetAtPath<Material>(ApartmentFixtures.CrtSignalPath);
        if (!signal) throw new InvalidOperationException("The apartment TV is missing its CRT signal material.");
        var off = Finish("Unpowered_CRT_Glass", "Universal Render Pipeline/Lit");
        off.color = new Color(.077f, .096f, .096f);
        off.SetFloat("_Smoothness", .68f);
        off.SetFloat("_Metallic", .12f);
        EditorUtility.SetDirty(off);
        screenMaterials[screenIndex] = off;
        tv.sharedMaterials = screenMaterials;
        data.FindProperty("television").objectReferenceValue = tv;
        data.FindProperty("televisionSignal").objectReferenceValue = signal;
        data.FindProperty("televisionMaterialIndex").intValue = screenIndex;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static Light FindOrAddLight(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing) return existing.GetComponent<Light>();
        var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(parent, false);
        return light;
    }

    static void SetEmission(Material material, Color color)
    {
        material.SetColor("_EmissionColor", color);
        material.EnableKeyword("_EMISSION");
        // URP derives its emission keyword from these flags when validating the material.
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
    }

    static Material Finish(string name, string shaderName)
    {
        string path = Surfaces + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material) return material;
        material = new Material(Shader.Find(shaderName));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void RemoveOldEvidence(Transform[] nodes)
    {
        var oldProps = new[] { "INT_CoffeeMug", "INT_Envelope_0", "INT_Envelope_1", "INT_Envelope_2",
            "Envelope_Address", "INT_PhotoFrame", "INT_Newspaper_Folded", "INT_Newspaper_Second",
            "INT_ShoulderBag", "Secret_Note" };
        foreach (var node in nodes)
        {
            if (!node || !oldProps.Contains(node.name)) continue;
            RemoveNamedChild(node, "Evidence_Visual");
            RemoveNamedChild(node, "Evidence_AimTarget");
            // Keep named anchors for the older lighting/furniture tools.
            node.gameObject.SetActive(false);
        }
        foreach (var node in nodes.Where(n => n && n.name == "Evidence_AimTarget"))
            Object.DestroyImmediate(node.gameObject);
    }

    static Transform Model(Transform host, string name, Vector3 position, Quaternion rotation)
    {
        var model = host.Find(name);
        if (!model)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".glb");
            if (!source) throw new InvalidOperationException("Missing Blender export " + name);
            model = ((GameObject)PrefabUtility.InstantiatePrefab(source, host.gameObject.scene)).transform;
            model.name = name;
            model.SetParent(host, false);
        }
        model.SetLocalPositionAndRotation(position, rotation);
        model.localScale = Vector3.one;
        return model;
    }

    static Transform AddPanel(Transform root, string label, Vector3 pos, Vector3 size, Material material, bool solid = false)
    {
        var part = root.Find(label);
        if (!part)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!solid) Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = label;
            part = cube.transform;
            part.SetParent(root, false);
        }
        part.localPosition = pos;
        part.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    static Material Surface(string name, Vector2 tiling, float smooth, float strength, string variant = "")
    {
        string map = Surfaces + "Textures/" + name;
        string path = Surfaces + name + (variant.Length > 0 ? "_" + variant : "") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        string normalPath = map + "_Normal.png";
        if (AssetImporter.GetAtPath(normalPath) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(map + "_BaseColor.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        material.SetTextureScale("_BaseMap", tiling);
        material.SetFloat("_BumpScale", strength);
        material.SetFloat("_Smoothness", smooth);
        material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Bind(Component component, string property, Object target)
    {
        var data = new SerializedObject(component);
        data.FindProperty(property).objectReferenceValue = target;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetHighlights(ClueObject clue, params Transform[] models)
    {
        clue.highlightRenderers = models.SelectMany(model => model.GetComponentsInChildren<Renderer>()).ToArray();
        clue.targetRenderer = clue.highlightRenderers[0];
    }

    static void RemoveNamedChild(Transform host, string name)
    {
        var child = host.Find(name);
        if (child) Object.DestroyImmediate(child.gameObject);
    }

    static void RemoveChildren(Transform host, params string[] keep)
    {
        foreach (var child in host.Cast<Transform>().ToArray())
            if (!keep.Contains(child.name))
                Object.DestroyImmediate(child.gameObject);
    }
}
