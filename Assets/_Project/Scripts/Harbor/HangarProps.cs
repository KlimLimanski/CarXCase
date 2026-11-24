using System.Collections.Generic;
using System.IO;
using CarXCase.Hero;
using DetectiveGame.Core;
using UnityEngine;

/// Hand props and procedural acting for the hangar finale, applied after the Animator and HeroFace:
/// Alexey's handheld service pistol, Kostya's pistol drawn from the jacket,
/// guns dropping to the floor, linked cuffs that follow Kostya's wrists, Alexey's collapse when shot,
/// and the father's twisted jute rope (built once from the old placeholder rings).
[DefaultExecutionOrder(9600)]
public sealed class HangarProps : MonoBehaviour
{
    HangarStaging staging;
    Transform kostya;
    Transform hero, heroVisual;
    Animator heroRig;
    Transform gunAlexey, gunKostya;
    GameObject bloodPool;
    Material bloodMaterial;
    Texture2D bloodTexture;
    float bloodSpread;
    static readonly Vector3 BloodPoolStartSize = new(1.41f, 1.58f, 1f);
    static readonly Vector3 BloodPoolSize = new(1.72f, 1.95f, 1f);

    float aimGoal, aim, aimVelocity;
    Transform aimAt;
    bool kostyaArmed;

    sealed class Fall { public Transform item; public float start; public Vector3 from, to; public Quaternion fromRot, toRot; }
    readonly List<Fall> falling = new();
    readonly HashSet<Transform> grounded = new();

    float collapse = -1f;
    bool visualSaved;
    Vector3 visualPosition;
    Quaternion visualRotation;
    readonly Transform[] aimBones = new Transform[8];
    readonly Quaternion[] locomotionArms = new Quaternion[8];
    SkinnedMeshRenderer gripSkin;
    Mesh originalGripMesh, gripMesh;
    int gripShape;
    bool gripPrepared;
    // Grip space measured on the retargeted Pistol Aim pose.
    static readonly Quaternion AimGripRotation = new(.69940f, .27256f, .33313f, .57059f);
    static readonly Vector3 AimGripOffset = new(.0434f, -.0332f, -.0094f);
    static readonly Vector3 PropMeshOffset = new(.006f, .034f, .034f);
    static readonly Vector3 KostyaWristOffset = new(-.053f, .096f, .099f);
    static readonly Vector3 KostyaPalmOffset = new(.038f, .091f, .019f);

    float Floor => transform.position.y + .06f;

    // ---------- setup ----------

    public void Setup(HangarStaging stage, Transform brother, Transform rescue)
    {
        staging = stage;
        kostya = brother;
        var steel = GunMaterial(stage.CuffMaterial);
        gunAlexey = NewGun("Alexey service pistol", steel);
        gunKostya = NewGun("Kostya pistol", steel);
        FatherRope.Build(rescue, stage);
    }

    public void SetHero(OfficerController officer)
    {
        if (!officer || hero == officer.transform) return;
        ReleaseGrip();
        hero = officer.transform;
        heroRig = officer.GetComponentInChildren<Animator>();
        heroBones.Clear();
        palmsKnown = false;
        heroVisual = hero.Find("Character_Visual");
        if (!heroVisual && heroRig) heroVisual = heroRig.transform;
        System.Array.Clear(aimBones, 0, aimBones.Length);
    }

    bool BindAimArms()
    {
        if (heroRig && aimBones[1] && aimBones[2] && aimBones[3] && aimBones[5] && aimBones[6] && aimBones[7]) return true;
        if (!heroRig) heroRig = hero.GetComponentInChildren<Animator>();
        aimBones[0] = Bone(HumanBodyBones.RightShoulder);
        aimBones[1] = Bone(HumanBodyBones.RightUpperArm);
        aimBones[2] = Bone(HumanBodyBones.RightLowerArm);
        aimBones[3] = Bone(HumanBodyBones.RightHand);
        aimBones[4] = Bone(HumanBodyBones.LeftShoulder);
        aimBones[5] = Bone(HumanBodyBones.LeftUpperArm);
        aimBones[6] = Bone(HumanBodyBones.LeftLowerArm);
        aimBones[7] = Bone(HumanBodyBones.LeftHand);
        return heroRig && aimBones[1] && aimBones[2] && aimBones[3] && aimBones[5] && aimBones[6] && aimBones[7];
    }

    void PrepareGrip()
    {
        if (!staging.HeroGripData) return;
        var data = staging.HeroGripData.bytes;
        if (data.Length < 8) return;
        using var stream = new BinaryReader(new MemoryStream(data));
        int vertexCount = stream.ReadInt32();
        int count = stream.ReadInt32();
        // Older grip assets contain only positions. The shaded pose also stores normal and tangent deltas.
        bool shaded = data.Length == 8L + 40L * count;
        if (count < 0 || (!shaded && data.Length != 8L + 16L * count)) return;
        Vector3 ReadDelta() => new Vector3(stream.ReadSingle(), stream.ReadSingle(), stream.ReadSingle());

        foreach (var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.sharedMesh || skin.sharedMesh.vertexCount != vertexCount) continue;
            var offsets = new Vector3[vertexCount];
            var normals = shaded ? new Vector3[vertexCount] : null;
            var tangents = shaded ? new Vector3[vertexCount] : null;
            for (int i = 0; i < count; i++)
            {
                int vertex = stream.ReadInt32();
                if (vertex < 0 || vertex >= vertexCount) return;
                offsets[vertex] = ReadDelta();
                if (shaded)
                {
                    normals[vertex] = ReadDelta();
                    tangents[vertex] = ReadDelta();
                }
            }
            gripSkin = skin;
            originalGripMesh = skin.sharedMesh;
            gripMesh = Instantiate(originalGripMesh);
            gripShape = gripMesh.blendShapeCount;
            gripMesh.AddBlendShapeFrame("Hangar pistol grip", 100f, offsets, normals, tangents);
            gripSkin.sharedMesh = gripMesh;
            return;
        }
    }

    void ReleaseGrip()
    {
        if (gripSkin && gripSkin.sharedMesh == gripMesh) gripSkin.sharedMesh = originalGripMesh;
        if (gripMesh) Destroy(gripMesh);
        gripSkin = null;
        gripMesh = null;
        gripPrepared = false;
    }

    /// Alexey's gun: 0 holstered, .5 low ready (muzzle at the floor in front of the target), 1 aimed at the chest.
    public void Aim(float level, Transform target)
    {
        aimGoal = Mathf.Clamp01(level);
        if (target) aimAt = target;
    }

    public void AimImmediate(float level, Transform target) { Aim(level, target); aim = aimGoal; aimVelocity = 0f; }

    public void ArmKostya(bool armed)
    {
        if (armed && grounded.Contains(gunKostya)) return;
        kostyaArmed = armed;
    }

    public bool KostyaArmed => kostyaArmed;

    public void DropKostyaGun()
    {
        if (!kostyaArmed) return;
        kostyaArmed = false;
        Drop(gunKostya, .9f);
    }

    /// 0..1 progress of Alexey going down after being shot.
    public void Collapse(float t)
    {
        if (collapse < 0f && aim > .02f) { Drop(gunAlexey, .5f); aimGoal = aim = 0f; }
        collapse = Mathf.Clamp01(t);
    }

    public void ResetAll()
    {
        aimGoal = aim = aimVelocity = 0f;
        if (bloodPool) Destroy(bloodPool);
        bloodPool = null;
        kostyaArmed = false;
        falling.Clear();
        grounded.Clear();
        if (gunAlexey) gunAlexey.gameObject.SetActive(false);
        if (gripSkin) gripSkin.SetBlendShapeWeight(gripShape, 0);
        if (gunKostya) gunKostya.gameObject.SetActive(false);
        collapse = -1f;
        if (visualSaved && heroVisual) heroVisual.SetLocalPositionAndRotation(visualPosition, visualRotation);
        visualSaved = false;
        if (staging) staging.SetCuff(false);
    }

    void OnDisable() => ResetAll();
    void OnDestroy()
    {
        ReleaseGrip();
        if (bloodMaterial) Destroy(bloodMaterial);
        if (bloodTexture) Destroy(bloodTexture);
    }

    public void ShowBlood(bool alexey)
    {
        if (bloodPool) return;
        var chest = alexey ? Bone(HumanBodyBones.Chest) : staging.KostyaBone("chest");
        if (!chest) return;
        if (!bloodMaterial)
        {
            var template = Resources.Load<Material>("Hangar blood pool");
            if (!template) return;
            bloodTexture = MakeBloodTexture();
            bloodMaterial = new Material(template) { mainTexture = bloodTexture };
            bloodMaterial.SetTexture("_EmissionMap", bloodTexture);
        }

        // Project the final chest pose onto the walkable slab, below the character and any props.
        var center = new Vector3(chest.position.x, Floor, chest.position.z);
        var normal = Vector3.up;
        var slab = transform.Find("Worn concrete slab");
        if (slab && slab.TryGetComponent<Collider>(out var floor) &&
            floor.Raycast(new Ray(center + Vector3.up, Vector3.down), out var hit, 2f))
        {
            center = hit.point;
            normal = hit.normal;
        }
        var hips = alexey ? Bone(HumanBodyBones.Hips) : staging.KostyaBone("pelvis");
        Vector3 along = Vector3.ProjectOnPlane(hips ? chest.position - hips.position : chest.up, normal);
        if (along.sqrMagnitude < .001f) along = Vector3.ProjectOnPlane(transform.forward, normal);

        bloodPool = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bloodPool.name = "Blood pool on concrete";
        bloodPool.transform.SetParent(transform, false);
        bloodPool.transform.SetPositionAndRotation(center + normal * .014f, Quaternion.LookRotation(-normal, along));
        bloodSpread = 0f;
        bloodPool.transform.localScale = BloodPoolStartSize;
        var collider = bloodPool.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var renderer = bloodPool.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = bloodMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Texture2D MakeBloodTexture()
    {
        const int size = 192;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Blood pooled on concrete",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
        };
        var pixels = new Color32[size * size];
        var core = new Color(.215f, .015f, .028f);
        var thin = new Color(.445f, .043f, .062f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var p = new Vector2(x, y) * (2f / (size - 1)) - Vector2.one;
            float warp = Mathf.PerlinNoise(p.x * 5.1f + 7.7f, p.y * 5.1f + 9.3f) - .5f;
            float grain = Mathf.PerlinNoise(p.x * 37f + 17.4f, p.y * 37f + 26.1f);
            float edge = Lobe(p, -.08f, -.06f, .68f, .61f);
            edge = MergeLobes(edge, Lobe(p, .36f, .24f, .43f, .40f));
            edge = MergeLobes(edge, Lobe(p, -.38f, -.35f, .44f, .37f));
            edge = MergeLobes(edge, Lobe(p, .12f, .45f, .41f, .35f));
            edge += .22f * warp + .07f * (grain - .5f);

            float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.54f, .02f, edge));
            var color = Color.Lerp(core, thin, Mathf.Clamp01(.13f + rim * .78f + warp * .29f + grain * .14f));
            color.a = Mathf.Lerp(.98f, .67f, rim) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.12f, .047f, edge)));
            pixels[y * size + x] = color;
        }
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }

    static float Lobe(Vector2 p, float x, float y, float width, float height) =>
        new Vector2((p.x - x) / width, (p.y - y) / height).magnitude - 1f;

    static float MergeLobes(float a, float b)
    {
        const float blend = .24f;
        float overlap = Mathf.Max(blend - Mathf.Abs(a - b), 0f) / blend;
        return Mathf.Min(a, b) - .25f * blend * overlap * overlap;
    }

    // ---------- per frame ----------

    void LateUpdate()
    {
        if (NoirMenu.GameSession.Paused) return;
        aim = Mathf.SmoothDamp(aim, aimGoal, ref aimVelocity, .28f);
        if (bloodPool && bloodSpread < 1f)
        {
            bloodSpread = Mathf.MoveTowards(bloodSpread, 1f, Time.deltaTime / 2.4f);
            float spread = Mathf.SmoothStep(0f, 1f, bloodSpread);
            bloodPool.transform.localScale = Vector3.Lerp(BloodPoolStartSize, BloodPoolSize, spread);
        }
        Apply();
    }

    /// Everything that has to happen after the Animator; public so editor review can pose stills.
    public void Apply()
    {
        if (!staging) return;
        staging.UpdateCuff();
        AlexeyArms();
        KostyaGun();
        Falling();
        AlexeyCollapse();
    }

    readonly Dictionary<HumanBodyBones, Transform> heroBones = new();
    Vector3 palmR, palmL;
    bool palmsKnown;

    /// Alexey's rig names bones "L_UpperArm", "R_Hand", "Head"...; the Animator mapping is a fallback.
    Transform Bone(HumanBodyBones bone)
    {
        if (!hero) return null;
        if (heroBones.TryGetValue(bone, out var known) && known) return known;
        string name = bone.ToString();
        if (name.StartsWith("Left")) name = "L_" + name.Substring(4);
        else if (name.StartsWith("Right")) name = "R_" + name.Substring(5);
        Transform found = null;
        // Only bones that actually deform the visible body (the hierarchy also holds helper copies).
        foreach (var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            foreach (var t in skin.bones)
                if (t && t.name == name) { found = t; break; }
            if (found) break;
        }
        if (!found && heroRig && heroRig.isHuman) found = heroRig.GetBoneTransform(bone);
        heroBones[bone] = found;
        return found;
    }

    void AlexeyArms()
    {
        bool show = aim > .03f && aimAt && hero && collapse < 0f && !grounded.Contains(gunAlexey);
        if (gunAlexey && !IsFalling(gunAlexey)) gunAlexey.gameObject.SetActive(show);
        if (gripSkin && !show) gripSkin.SetBlendShapeWeight(gripShape, 0);
        if (!show) return;
        if (staging.HeroAimPose && BindAimArms())
        {
            ApplyAimPose();
            return;
        }
        Transform chest = Bone(HumanBodyBones.UpperChest) ?? Bone(HumanBodyBones.Chest);
        Transform shR = Bone(HumanBodyBones.RightUpperArm), elR = Bone(HumanBodyBones.RightLowerArm), hR = Bone(HumanBodyBones.RightHand);
        Transform shL = Bone(HumanBodyBones.LeftUpperArm), elL = Bone(HumanBodyBones.LeftLowerArm), hL = Bone(HumanBodyBones.LeftHand);
        if (!chest || !shR || !elR || !hR || !shL || !elL || !hL) return;
        if (!palmsKnown)
        {
            // Taken from the Animator pose before any IK: arms hang, palms turned to the thighs.
            palmR = hR.InverseTransformDirection(-hero.right);
            palmL = hL.InverseTransformDirection(hero.right);
            palmsKnown = true;
        }

        float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(aim * 2f));
        float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(aim * 2f - 1f));
        Vector3 up = Vector3.up;
        Vector3 target = Vector3.Lerp(aimAt.position + up * .1f, aimAt.position + up * 1.28f, raise);
        Vector3 center = chest.position;
        Vector3 dir = (target - center).normalized;
        Vector3 right = Vector3.Cross(up, dir).normalized;
        Vector3 gunUp = Vector3.Cross(dir, right);

        // Weaver-ish grip: gun hand forward on the centre line, support hand wrapped under it.
        Vector3 grip = center + dir * Mathf.Lerp(.36f, .5f, raise) + right * .03f - up * Mathf.Lerp(.16f, .05f, raise);
        HeroArmIK.Solve(shR, elR, hR, grip, shR.position + right * .32f - up * .35f, weight);
        RollPalm(hR, palmR, -right, dir, weight);                 // thumb up, palm round the grip
        Vector3 support = hR.position - gunUp * .06f - right * .018f + dir * .01f;
        HeroArmIK.Solve(shL, elL, hL, support, shL.position - right * .32f - up * .35f, weight);
        RollPalm(hL, palmL, gunUp, dir, weight);                  // support hand cups it from below

        gunAlexey.SetPositionAndRotation(hR.position + dir * .055f - right * .022f - gunUp * .01f, Quaternion.LookRotation(dir, gunUp));
    }

    void ApplyAimPose()
    {
        if (!gripPrepared)
        {
            gripPrepared = true;
            PrepareGrip();
        }
        if (gripSkin) gripSkin.SetBlendShapeWeight(gripShape, Mathf.SmoothStep(0f, 100f, Mathf.InverseLerp(-.05f, .15f, aim)));
        float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .46f, aim));
        for (int i = 0; i < aimBones.Length; i++)
            if (aimBones[i]) locomotionArms[i] = aimBones[i].localRotation;
        staging.HeroAimPose.SampleAnimation(heroRig.gameObject, 0f);

        Transform right = aimBones[3];
        for (int i = 0; i < aimBones.Length; i++)
        {
            if (!aimBones[i]) continue;
            // The free arm keeps its locomotion pose throughout the shot.
            aimBones[i].localRotation = i < 4
                ? Quaternion.Slerp(locomotionArms[i], aimBones[i].localRotation, weight)
                : locomotionArms[i];
        }

        Vector3 up = Vector3.up;
        Vector3 shoulderCenter = (aimBones[1].position + aimBones[5].position) * .5f;
        float raised = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.48f, 1f, aim));
        Vector3 target = aimAt.position + up * Mathf.Lerp(.16f, 1.25f, raised);
        Vector3 flat = Vector3.ProjectOnPlane(target - shoulderCenter, up);
        Vector3 forward = flat.sqrMagnitude > .01f
            ? Vector3.RotateTowards(hero.forward, flat.normalized, 25f * Mathf.Deg2Rad, 0f)
            : hero.forward;
        Vector3 across = Vector3.Cross(up, forward).normalized;
        // Keep the prop on the right side of the silhouette as the arm rises.
        Vector3 centerGrip = shoulderCenter + forward * Mathf.Lerp(.30f, .57f, raised)
            + across * .13f - up * Mathf.Lerp(.28f, .015f, raised);
        Vector3 direction = (target - centerGrip).normalized;
        Quaternion gunRotation = Quaternion.LookRotation(direction.sqrMagnitude > .01f ? direction : forward, up);
        Quaternion rightPalm = gunRotation * Quaternion.Inverse(AimGripRotation);
        Vector3 rightWrist = centerGrip - rightPalm * AimGripOffset;
        HeroArmIK.Solve(aimBones[1], aimBones[2], right, rightWrist,
            shoulderCenter + across * .43f - up * .27f + forward * .08f, weight);
        right.rotation = Quaternion.Slerp(right.rotation, rightPalm, weight);

        Vector3 gripPoint = right.position + right.TransformDirection(AimGripOffset);
        Quaternion propRotation = right.rotation * AimGripRotation;
        gunAlexey.SetPositionAndRotation(gripPoint + propRotation * PropMeshOffset, propRotation);
    }

    /// Twists the hand about the barrel line so its palm (a direction in hand space) faces `want`.
    static void RollPalm(Transform hand, Vector3 palmLocal, Vector3 want, Vector3 along, float weight)
    {
        Vector3 now = Vector3.ProjectOnPlane(hand.TransformDirection(palmLocal), along);
        Vector3 goal = Vector3.ProjectOnPlane(want, along);
        if (now.sqrMagnitude < 1e-5f || goal.sqrMagnitude < 1e-5f) return;
        hand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(now, goal, along) * weight, along) * hand.rotation;
    }

    void KostyaGun()
    {
        if (!gunKostya || IsFalling(gunKostya) || grounded.Contains(gunKostya)) return;
        gunKostya.gameObject.SetActive(kostyaArmed);
        if (!kostyaArmed) return;
        var hand = staging.KostyaBone("hand.R");
        Quaternion rotation = Quaternion.LookRotation(staging.HandAxis, staging.ThumbAxis);
        Vector3 grip = hand.position + hand.TransformDirection(KostyaWristOffset) + rotation * KostyaPalmOffset;
        gunKostya.SetPositionAndRotation(grip, rotation);
    }

    void Drop(Transform item, float scatter)
    {
        if (!item || !item.gameObject.activeSelf || IsFalling(item)) return;
        Vector3 from = item.position;
        var away = Random.insideUnitCircle * .25f * scatter;
        var to = new Vector3(from.x + away.x, Floor + .016f, from.z + away.y);
        falling.Add(new Fall
        {
            item = item, start = Time.time, from = from, to = to, fromRot = item.rotation,
            toRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f)
        });
    }

    bool IsFalling(Transform item) { foreach (var f in falling) if (f.item == item) return true; return false; }

    void Falling()
    {
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            var f = falling[i];
            float t = Mathf.Clamp01((Time.time - f.start) / .5f);
            Vector3 p = Vector3.Lerp(f.from, f.to, t);
            p.y = Mathf.Lerp(f.from.y, f.to.y, t * t) + Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.25f - .25f)) * .03f * (1f - t);
            f.item.SetPositionAndRotation(p, Quaternion.Slerp(f.fromRot, f.toRot, Mathf.SmoothStep(0f, 1f, t)));
            f.item.gameObject.SetActive(true);
            if (t >= 1f)
            {
                if (f.item == gunKostya || f.item == gunAlexey) LocationAudio.Effect("Lock key press", .25f);
                grounded.Add(f.item);
                falling.RemoveAt(i);
            }
        }
    }

    /// Knees give, the body folds and goes over backwards onto the concrete: one knee stays up,
    /// arms fall open, the head rolls aside. The lowest point of the body is kept on the floor.
    void AlexeyCollapse()
    {
        if (collapse < 0f || !heroVisual) return;
        if (!visualSaved)
        {
            visualPosition = heroVisual.localPosition;
            visualRotation = heroVisual.localRotation;
            visualSaved = true;
        }
        float t = collapse;
        float buckle = Ease(0f, .38f, t), fall = Ease(.28f, 1f, t);
        Vector3 right0 = hero.right, forward0 = hero.forward;

        heroVisual.localPosition = visualPosition;
        heroVisual.rotation = Quaternion.AngleAxis(-84f * fall, right0) * Quaternion.AngleAxis(13f * fall, forward0) *
                              (hero.rotation * visualRotation);

        Vector3 right = heroVisual.right, fwd = heroVisual.forward, up = heroVisual.up;
        void Turn(HumanBodyBones b, float degrees, Vector3 axis) { var t2 = Bone(b); if (t2) t2.rotation = Quaternion.AngleAxis(degrees, axis) * t2.rotation; }

        float kept = 1f - fall;
        Turn(HumanBodyBones.RightUpperLeg, -58f * buckle * kept - 48f * fall, right);
        Turn(HumanBodyBones.RightLowerLeg, 100f * buckle * kept + 82f * fall, right);
        Turn(HumanBodyBones.LeftUpperLeg, -52f * buckle * kept - 10f * fall, right);
        Turn(HumanBodyBones.LeftLowerLeg, 92f * buckle * kept + 16f * fall, right);
        Turn(HumanBodyBones.Spine, 16f * buckle * kept - 4f * fall, right);
        Turn(HumanBodyBones.Chest, 10f * buckle * kept, right);
        Turn(HumanBodyBones.RightUpperArm, 34f * fall, fwd);
        Turn(HumanBodyBones.LeftUpperArm, -44f * fall, fwd);
        Turn(HumanBodyBones.RightUpperArm, -20f * buckle * kept, right);
        Turn(HumanBodyBones.LeftUpperArm, -26f * buckle * kept, right);
        Turn(HumanBodyBones.RightLowerArm, -22f * fall - 30f * buckle * kept, right);
        Turn(HumanBodyBones.LeftLowerArm, -35f * fall - 25f * buckle * kept, right);
        Turn(HumanBodyBones.Head, -12f * fall, right);
        Turn(HumanBodyBones.Head, 28f * fall, up);
        Turn(HumanBodyBones.Neck, 10f * buckle * kept, right);

        // Rest on the floor: lift or lower the whole body so its lowest part just touches the concrete.
        float lowest = float.MaxValue;
        foreach (var (b, r) in new[] { (HumanBodyBones.Hips, .11f), (HumanBodyBones.Chest, .12f), (HumanBodyBones.UpperChest, .12f),
                     (HumanBodyBones.Head, .1f), (HumanBodyBones.LeftHand, .03f), (HumanBodyBones.RightHand, .03f),
                     (HumanBodyBones.LeftFoot, .05f), (HumanBodyBones.RightFoot, .05f), (HumanBodyBones.LeftToes, .02f),
                     (HumanBodyBones.RightToes, .02f), (HumanBodyBones.LeftLowerLeg, .06f), (HumanBodyBones.RightLowerLeg, .06f),
                     (HumanBodyBones.LeftLowerArm, .04f), (HumanBodyBones.RightLowerArm, .04f) })
        {
            var bone = Bone(b);
            if (bone) lowest = Mathf.Min(lowest, bone.position.y - r);
        }
        if (lowest < float.MaxValue) heroVisual.position += Vector3.up * (Floor - lowest);

        float settle = Ease(.67f, 1f, t);
        if (settle > 0f)
        {
            SettleArm(true, settle);
            SettleArm(false, settle);
        }
    }

    void SettleArm(bool left, float weight)
    {
        var shoulder = Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        var elbow = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        var wrist = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        if (!shoulder || !elbow || !wrist) return;

        Vector3 outward = hero.right * (left ? -1f : 1f);
        Vector3 towardFeet = hero.forward;
        Vector3 forearmDirection = (towardFeet + outward * (left ? .40f : .62f)).normalized;
        LowerSegment(shoulder, elbow, (outward + towardFeet * .42f).normalized, Floor + .045f, weight);
        LowerSegment(elbow, wrist, forearmDirection, Floor + .0325f, weight);

        // The hand meshes extend along opposite local X axes. Keep the fingers parallel to the slab.
        Vector3 localX = left ? -forearmDirection : forearmDirection;
        var flatHand = Quaternion.LookRotation(Vector3.Cross(localX, Vector3.up), Vector3.up);
        wrist.rotation = Quaternion.Slerp(wrist.rotation, flatHand, weight);
    }

    static void LowerSegment(Transform joint, Transform tip, Vector3 groundDirection, float tipHeight, float weight)
    {
        Vector3 segment = tip.position - joint.position;
        float length = segment.magnitude;
        if (length < .001f) return;
        float rise = Mathf.Clamp(tipHeight - joint.position.y, -length, length);
        float run = Mathf.Sqrt(Mathf.Max(0f, length * length - rise * rise));
        Vector3 target = Vector3.up * rise + Vector3.ProjectOnPlane(groundDirection, Vector3.up).normalized * run;
        var rotation = Quaternion.FromToRotation(segment, target) * joint.rotation;
        joint.rotation = Quaternion.Slerp(joint.rotation, rotation, weight);
    }

    static float Ease(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

    // ---------- pistol ----------

    static Material GunMaterial(Material template)
    {
        var material = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = "Pistol polymer and blued steel";
        material.SetTexture("_BaseMap", null);
        material.SetTexture("_MetallicGlossMap", null);
        material.SetColor("_BaseColor", new Color(.045f, .046f, .05f));
        material.SetFloat("_Metallic", .55f);
        material.SetFloat("_Smoothness", .52f);
        return material;
    }

    Transform NewGun(string name, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = PistolMesh();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        go.SetActive(false);
        return go.transform;
    }

    static Mesh pistol;

    /// A compact 9 mm service pistol. Origin: centre of the grip where the hand closes; +Z barrel, +Y up.
    static Mesh PistolMesh()
    {
        if (pistol) return pistol;
        var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cube = probe.GetComponent<MeshFilter>().sharedMesh;
        if (Application.isPlaying) Destroy(probe); else DestroyImmediate(probe);
        var parts = new List<CombineInstance>();
        void Box(float x, float y, float z, float w, float h, float d, float tilt = 0f) =>
            parts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(tilt, 0, 0), new Vector3(w, h, d)) });
        Box(0, .061f, .052f, .026f, .029f, .184f);          // slide
        Box(0, .067f, .052f, .021f, .02f, .178f);           // rounded slide top
        Box(0, .038f, .056f, .024f, .018f, .148f);          // frame / dust cover
        Box(0, -.006f, -.006f, .028f, .1f, .05f, 17f);      // grip, raked back
        Box(0, -.054f, -.022f, .03f, .012f, .054f, 17f);    // magazine base
        Box(0, .011f, .062f, .008f, .006f, .056f);          // trigger guard, bottom
        Box(0, .022f, .088f, .008f, .026f, .007f, -8f);     // trigger guard, front
        Box(0, .021f, .042f, .006f, .02f, .006f, 14f);      // trigger
        Box(0, .079f, .136f, .005f, .007f, .007f);          // front sight
        Box(-.006f, .079f, -.03f, .005f, .007f, .008f);     // rear sight, left
        Box(.006f, .079f, -.03f, .005f, .007f, .008f);      // rear sight, right
        Box(0, .05f, -.042f, .02f, .02f, .012f);            // beavertail
        pistol = new Mesh { name = "Service pistol" };
        pistol.CombineMeshes(parts.ToArray(), true, true);
        pistol.RecalculateBounds();
        return pistol;
    }
}

/// Replaces the father's placeholder rope rings with a proper three-strand jute rope: three tight turns
/// round his chest, arms and the chair back, a knot at the back with two loose ends, and a lashing round
/// each ankle that moves with his legs.
public static class FatherRope
{
    const float Radius = .0105f;

    public static void Build(Transform rescue, HangarStaging staging)
    {
        var jute = staging.RopeMaterial;
        var old = new List<Renderer>();
        foreach (Transform part in rescue)
            if (part.name == "Rope" && part.TryGetComponent<Renderer>(out var r)) old.Add(r);
        if (old.Count == 0) return;
        if (!jute) jute = old[0].sharedMaterial;

        // Fit the turns to the placeholder rings (local to the father).
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        foreach (var r in old)
        {
            Vector3 p = rescue.InverseTransformPoint(r.bounds.center);
            min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            r.enabled = false;
        }
        Vector3 c = (min + max) * .5f;
        float ex = (max.x - min.x) * .5f * .9f, ez = (max.z - min.z) * .5f * .86f;   // pulled tight against the body
        float halfDepth = (max.z - min.z) * .5f;

        // Back of the chair = opposite the way his knees point.
        Vector3 knees = (staging.FatherBone("shin.L").position + staging.FatherBone("shin.R").position) * .5f;
        Vector3 back = rescue.InverseTransformDirection(staging.FatherBone("pelvis").position - knees);
        back.y = 0f;
        float knotAngle = Mathf.Atan2(back.z / Mathf.Max(ez, .01f), back.x / Mathf.Max(ex, .01f));

        // Fit the front and the sides to his actual arms and chest at rope height, so the turns
        // bite into the jacket instead of floating; behind him the rope keeps riding the chair back.
        Vector3 fwd = -new Vector3(back.x, 0f, back.z).normalized;
        bool frontAlongZ = Mathf.Abs(fwd.z) >= Mathf.Abs(fwd.x);
        float ringY = (min.y + max.y) * .5f, front = 0f, flank = 0f;
        void Probe(Vector3 world, float pad)
        {
            Vector3 p = rescue.InverseTransformPoint(world);
            if (Mathf.Abs(p.y - ringY) > .1f) return;
            p -= c; p.y = 0f;
            front = Mathf.Max(front, Vector3.Dot(p, fwd) + pad);
            flank = Mathf.Max(flank, Mathf.Abs(frontAlongZ ? p.x : p.z) + pad);
        }
        foreach (var s in new[] { ".L", ".R" })
        {
            Transform a = staging.FatherBone("upper_arm" + s), b = staging.FatherBone("forearm" + s), h = staging.FatherBone("hand" + s);
            if (!a || !b || !h) continue;
            for (int i = 0; i <= 12; i++)
            {
                Probe(Vector3.Lerp(a.position, b.position, i / 12f), .05f);
                Probe(Vector3.Lerp(b.position, h.position, i / 12f), .045f);
            }
        }
        foreach (var name in new[] { "spine", "spine.001", "spine.002", "spine.003" })
            if (staging.FatherBone(name)) Probe(staging.FatherBone(name).position, .12f);
        float xPos = ex, xNeg = ex, zPos = halfDepth, zNeg = halfDepth;
        if (front > .05f)
        {
            if (frontAlongZ) { if (fwd.z > 0) zPos = front; else zNeg = front; }
            else { if (fwd.x > 0) xPos = front; else xNeg = front; }
        }
        if (flank > .05f)
        {
            if (frontAlongZ) xPos = xNeg = flank;
            else zPos = zNeg = flank;
        }

        var path = new List<Vector3>();
        const int turns = 3;
        float y0 = min.y - .012f, y1 = max.y + .03f;
        int steps = 260 * turns;
        for (int i = 0; i <= steps; i++)
        {
            float u = (float)i / steps;
            float a = knotAngle + u * turns * Mathf.PI * 2f;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            // Squarish wrap: a rope pulled round a torso and a chair back is flat on the sides.
            float x = Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), .72f) * (cs >= 0f ? xPos : xNeg);
            float z = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), .72f) * (sn >= 0f ? zPos : zNeg);
            float y = Mathf.Lerp(y0, y1, u) + Mathf.Sin(a * 2f) * .004f;
            path.Add(new Vector3(c.x + x, y, c.z + z));
        }
        var mesh = new MeshBuilder();
        mesh.Tube(path, Radius, false, false);

        // Knot and loose ends at the back.
        Vector3 knot = path[0] + (path[path.Count - 1] - path[0]) * .5f;
        Vector3 outward = new Vector3(back.x, 0, back.z).normalized;
        Vector3 along = Vector3.Cross(Vector3.up, outward);
        for (int k = 0; k < 2; k++)
        {
            var loop = new List<Vector3>();
            for (int i = 0; i <= 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                loop.Add(knot + outward * (.012f + .016f * (1 + Mathf.Cos(a)) * .5f) + along * (Mathf.Sin(a) * .024f + (k - .5f) * .018f) +
                         Vector3.up * (Mathf.Cos(a) * .02f + (k - .5f) * .012f));
            }
            mesh.Tube(loop, Radius * .95f, false, false);
        }
        for (int k = 0; k < 2; k++)
        {
            var tail = new List<Vector3>();
            float length = k == 0 ? .3f : .21f;
            for (int i = 0; i <= 30; i++)
            {
                float s = i / 30f;
                tail.Add(knot + outward * (.02f + .05f * s * (1 - s * .5f)) + along * ((k - .5f) * .03f + Mathf.Sin(s * 3f + k) * .02f * s) -
                         Vector3.up * (length * s));
            }
            mesh.Tube(tail, Radius, false, true);
        }
        staging.AddBinding(mesh.Create("Rope (twisted jute)", rescue, jute));

        foreach (var side in new[] { ".L", ".R" })
        {
            var shin = staging.FatherBone("shin" + side);
            var foot = staging.FatherBone("foot" + side);
            Vector3 axis = (shin.position - foot.position).normalized;
            Vector3 ankle = foot.position + axis * .07f;
            Vector3 n1 = Vector3.Cross(axis, Vector3.forward).normalized;
            if (n1.sqrMagnitude < .01f) n1 = Vector3.Cross(axis, Vector3.right).normalized;
            Vector3 n2 = Vector3.Cross(axis, n1);
            var ring = new List<Vector3>();
            for (int i = 0; i <= 120; i++)
            {
                float a = i / 120f * Mathf.PI * 4f;
                ring.Add(ankle + (n1 * Mathf.Cos(a) * .058f + n2 * Mathf.Sin(a) * .05f) + axis * (i / 120f * .03f));
            }
            var ankleMesh = new MeshBuilder();
            ankleMesh.Tube(ring, Radius * .9f, false, false);
            var tail = new List<Vector3>();
            for (int i = 0; i <= 16; i++) tail.Add(ring[ring.Count - 1] + n1 * (.01f * i / 16f) - Vector3.up * (.12f * i / 16f));
            ankleMesh.Tube(tail, Radius * .9f, false, true);
            var go = ankleMesh.Create("Rope (ankle" + side + ")", null, jute);
            go.transform.SetParent(shin, true);
            staging.AddBinding(go);
        }
    }

    sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new(), normals = new();
        readonly List<Vector2> uv = new();
        readonly List<int> triangles = new();
        const int Sides = 10;

        /// A rope tube with three twisted strands along the path (points in the final object's space).
        public void Tube(List<Vector3> points, float radius, bool capStart, bool frayEnd)
        {
            if (points.Count < 2) return;
            int start = vertices.Count;
            Vector3 normal = Vector3.zero;
            float length = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                if (i == 0)
                {
                    normal = Vector3.Cross(tangent, Vector3.up);
                    if (normal.sqrMagnitude < .001f) normal = Vector3.Cross(tangent, Vector3.right);
                }
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                if (i > 0) length += Vector3.Distance(points[i], points[i - 1]);
                float taper = frayEnd ? Mathf.Lerp(1f, .55f, Mathf.InverseLerp(points.Count - 4, points.Count - 1, i)) : 1f;
                for (int j = 0; j <= Sides; j++)
                {
                    float a = j / (float)Sides * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    float strand = 1f + .2f * Mathf.Cos(3f * a + length * (Mathf.PI * 2f / .03f));
                    vertices.Add(points[i] + dir * radius * strand * taper);
                    normals.Add(dir);
                    uv.Add(new Vector2(j / (float)Sides, length / .06f));
                }
                if (i == 0) continue;
                int row = start + i * (Sides + 1), prev = row - (Sides + 1);
                for (int j = 0; j < Sides; j++)
                {
                    triangles.Add(prev + j); triangles.Add(row + j); triangles.Add(prev + j + 1);
                    triangles.Add(prev + j + 1); triangles.Add(row + j); triangles.Add(row + j + 1);
                }
            }
        }

        public GameObject Create(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            // Points were produced in parent space (or world space when there is no parent).
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }
    }
}
