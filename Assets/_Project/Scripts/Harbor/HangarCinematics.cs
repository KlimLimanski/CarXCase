using UnityEngine;

/// Dialogue camera for the hangar: cuts on the speaker (over-the-shoulder shot / reverse shot, close-ups,
/// a wide two-shot now and then), slow push-ins while a shot is held, a tracking shot as Alexey walks in,
/// and a tightening, tilting frame while Kostya reaches for his pocket. All shots stay on one side of the
/// line between the brothers, so screen direction never flips.
public sealed partial class HangarFinale
{
    enum Cover { Walk, Wide, OverAlexey, OverKostya, CloseKostya, CloseAlexey, CloseFather, Authored, Choice, Decision }

    bool directing;
    Cover cover;
    float coverStart, lineSide = 1f;
    int holdLines, cuts;
    bool kostyaAlternate, alexeyAlternate;
    Vector3 authoredEye, authoredLook;

    Vector3 camPos, camLook, camPosVelocity, camLookVelocity;
    Vector3 headA, headK, headF, velA, velK, velF;
    bool snapCamera;
    float roll, rollVelocity, zoom = 1f, zoomVelocity;

    Transform boneA, boneK, boneFather, boneLate;
    readonly RaycastHit[] cameraHits = new RaycastHit[24];

    // ---------- control ----------

    void StartDirecting(Cover first, int hold = 0)
    {
        if (!view) return;
        headA = HeadA(); headK = HeadK(); headF = HeadF();
        velA = velK = velF = Vector3.zero;
        lineSide = PickSide();
        directing = true;
        cuts = 0;
        Cut(first, hold);
        // Glide in from wherever the camera is now; later changes are hard cuts.
        camPos = view.transform.position;
        camLook = view.transform.position + view.transform.forward * 3f;
        snapCamera = false;
    }

    void StopDirecting()
    {
        directing = false;
        roll = 0f;
        zoom = 1f;
    }

    /// Switches to a shot; `hold` keeps it through that many following lines.
    void Cut(Cover next, int hold = 0)
    {
        snapCamera = true;
        cover = next;
        coverStart = Time.time;
        holdLines = hold;
        cuts++;
    }

    /// Called as every line starts. Picks the coverage for the speaker.
    void OnLine(string speaker, string words)
    {
        if (!directing) return;
        // Shots tied to a moment (the choice, the draw, a placed angle) give way to coverage at once.
        bool momentary = cover == Cover.Choice || cover == Cover.Decision || cover == Cover.Authored || cover == Cover.Walk;
        if (holdLines > 0) { holdLines--; return; }
        float age = momentary ? 99f : Time.time - coverStart;
        bool heated = words.Contains("!") || words.Contains("?!");
        bool kostyaDown = headK.y < headA.y - .55f, alexeyDown = headA.y < headK.y - .55f;

        Cover next;
        if (speaker == "Костя")
        {
            if (alexeyDown) next = Cover.OverAlexey;
            else if (heated || kostyaDown) next = Cover.CloseKostya;
            else next = (kostyaAlternate = !kostyaAlternate) ? Cover.OverAlexey : Cover.CloseKostya;
        }
        else if (speaker == "Алексей")
        {
            if (kostyaDown) next = Cover.OverKostya;
            else if (heated || alexeyDown) next = Cover.CloseAlexey;
            else next = (alexeyAlternate = !alexeyAlternate) ? Cover.OverKostya : Cover.CloseAlexey;
        }
        else if (speaker == "Фёдор Павлович") next = Cover.CloseFather;
        else next = Cover.Wide;

        // Every so often a calm line opens up to the wide two-shot to re-establish the room.
        if (!heated && cuts % 6 == 5 && speaker != "Фёдор Павлович") next = Cover.Wide;

        if (next == cover)
        {
            if (age < 6.5f) return;                              // keep a good shot running
            // Held too long: switch to the other angle on the same speaker.
            next = next == Cover.CloseKostya ? Cover.OverAlexey : next == Cover.OverAlexey ? Cover.CloseKostya :
                   next == Cover.CloseAlexey ? Cover.OverKostya : next == Cover.OverKostya ? Cover.CloseAlexey : Cover.Wide;
        }
        if (age < .9f) return;                                  // no strobing on rapid-fire lines
        Cut(next);
    }

    // ---------- per frame ----------

    void DirectCamera()
    {
        if (!directing || !view) return;
        float dt = Time.deltaTime;
        headA = Vector3.SmoothDamp(headA, HeadA(), ref velA, .16f, Mathf.Infinity, dt);
        headK = Vector3.SmoothDamp(headK, HeadK(), ref velK, .16f, Mathf.Infinity, dt);
        headF = Vector3.SmoothDamp(headF, HeadF(), ref velF, .16f, Mathf.Infinity, dt);

        if (stage == Stage.DialogueChoice && cover != Cover.Choice) Cut(Cover.Choice);
        if ((stage == Stage.Decision || stage == Stage.Missed) && cover != Cover.Decision) Cut(Cover.Decision);

        Compose(out Vector3 eye, out Vector3 look, out float wantRoll, out float wantZoom);
        eye = Clear(look, eye);
        float ground = player.transform.position.y + .32f;
        if (eye.y < ground) eye.y = ground;

        if (snapCamera)
        {
            camPos = eye; camLook = look;
            camPosVelocity = camLookVelocity = Vector3.zero;
            roll = wantRoll; zoom = wantZoom;
            rollVelocity = zoomVelocity = 0f;
            snapCamera = false;
        }
        else
        {
            camPos = Vector3.SmoothDamp(camPos, eye, ref camPosVelocity, .14f, Mathf.Infinity, dt);
            camLook = Vector3.SmoothDamp(camLook, look, ref camLookVelocity, .12f, Mathf.Infinity, dt);
            roll = Mathf.SmoothDamp(roll, wantRoll, ref rollVelocity, .35f, Mathf.Infinity, dt);
            zoom = Mathf.SmoothDamp(zoom, wantZoom, ref zoomVelocity, .35f, Mathf.Infinity, dt);
        }
        view.Direct(camPos, camLook, roll, zoom);
    }

    void Compose(out Vector3 eye, out Vector3 look, out float tilt, out float fov)
    {
        float t = Time.time - coverStart;
        float held = Mathf.Min(t, 7f);
        Vector3 up = Vector3.up;
        Vector3 axis = Flat(headK - headA, player.transform.forward);   // Alexey -> Kostya
        Vector3 side = Vector3.Cross(up, axis) * lineSide;
        tilt = 0f;
        fov = 1f;

        switch (cover)
        {
            case Cover.Walk:
            {
                // Follow Alexey from behind his shoulder as he storms in, Kostya ahead in frame.
                Vector3 fwd = Flat(player.transform.forward, axis);
                Vector3 fwdSide = Vector3.Cross(up, fwd) * lineSide;
                eye = headA - fwd * 1.35f + fwdSide * .48f + up * .12f;
                look = Vector3.Lerp(headA + fwd * 3f, headK, .55f) - up * .1f;
                fov = .95f;
                break;
            }
            case Cover.OverAlexey:
                // Over Alexey's shoulder onto Kostya, slow push-in.
                eye = headA - axis * (.78f - held * .03f) + side * .44f + up * .06f;
                look = headK - up * .05f;
                fov = .9f;
                break;
            case Cover.OverKostya:
                // Reverse shot: over Kostya's shoulder onto Alexey.
                eye = headK + axis * (.78f - held * .03f) + side * .44f + up * .04f;
                look = headA - up * .05f;
                fov = .9f;
                break;
            case Cover.CloseKostya:
            {
                // Slightly low close-up: Kostya looms. Drifts in and a touch sideways.
                float d = Mathf.Lerp(1.25f, 1.02f, held / 7f);
                eye = headK - axis * d + side * (.32f + held * .015f) - up * .14f;
                look = headK - up * .04f;
                fov = .78f;
                tilt = -1.2f * lineSide;
                break;
            }
            case Cover.CloseAlexey:
            {
                float d = Mathf.Lerp(1.2f, 1f, held / 7f);
                eye = headA + axis * d + side * (.3f + held * .015f) - up * .06f;
                look = headA - up * .04f;
                fov = .8f;
                break;
            }
            case Cover.CloseFather:
            {
                Vector3 toAlexey = Flat(headA - headF, -axis);
                Vector3 fSide = Vector3.Cross(up, toAlexey) * lineSide;
                eye = headF + toAlexey * Mathf.Lerp(1.15f, .95f, held / 7f) + fSide * .28f + up * .1f;
                look = headF - up * .05f;
                fov = .8f;
                break;
            }
            case Cover.Wide:
            {
                // Two-shot from the side of the line, slow lateral dolly; the father sits in the background.
                Vector3 mid = Vector3.Lerp(headA, headK, .5f);
                eye = mid + side * 2.35f - axis * (.55f - held * .07f) + up * .32f;
                look = Vector3.Lerp(mid, headF, .2f) - up * .2f;
                fov = 1.02f;
                break;
            }
            case Cover.Choice:
                // Holding on Kostya while the player decides what to say.
                eye = headA - axis * (.95f - held * .025f) + side * .5f + up * .08f;
                look = Vector3.Lerp(headK, headA, .12f) - up * .08f;
                fov = .92f;
                break;
            case Cover.Decision:
            {
                // Kostya goes for his pocket: the frame closes in on him and the horizon starts to lean.
                float clock = 1f - Mathf.Clamp01((deadline - Time.time) / DecisionSeconds);
                float tension = Mathf.Max(reach, clock);
                eye = headA - axis * Mathf.Lerp(.7f, .38f, tension) + side * Mathf.Lerp(.42f, .3f, tension) + up * .04f;
                look = Vector3.Lerp(headK, headK - up * .42f, reach * .55f);
                fov = Mathf.Lerp(.86f, .66f, tension);
                tilt = Mathf.Lerp(0f, 5.5f, tension) * lineSide;
                break;
            }
            default: // Authored: a hand-placed angle from the scene, held with a slow push-in.
            {
                Vector3 dir = (authoredLook - authoredEye).normalized;
                eye = authoredEye + dir * Mathf.Min(held * .045f, .3f);
                look = authoredLook;
                break;
            }
        }
    }

    // ---------- helpers ----------

    float PickSide()
    {
        Vector3 axis = Flat(headK - headA, player.transform.forward);
        Vector3 side = Vector3.Cross(Vector3.up, axis);
        Vector3 mid = Vector3.Lerp(headA, headK, .5f);
        float right = Room(mid, side), left = Room(mid, -side);
        return right >= left ? 1f : -1f;
    }

    float Room(Vector3 from, Vector3 dir)
    {
        const float reachDistance = 2.6f;
        float best = reachDistance;
        int count = Physics.SphereCastNonAlloc(from, .2f, dir, cameraHits, reachDistance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (cameraHits[i].distance > 0f && !IsActor(cameraHits[i].collider.transform))
                best = Mathf.Min(best, cameraHits[i].distance);
        return best;
    }

    /// Pulls the lens in front of walls and crates between the subject and the wanted spot.
    Vector3 Clear(Vector3 subject, Vector3 wanted)
    {
        Vector3 path = wanted - subject;
        float length = path.magnitude;
        if (length < .05f) return wanted;
        Vector3 dir = path / length;
        float free = length;
        int count = Physics.SphereCastNonAlloc(subject, .16f, dir, cameraHits, length, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (cameraHits[i].distance > 0f && !IsActor(cameraHits[i].collider.transform))
                free = Mathf.Min(free, cameraHits[i].distance - .1f);
        return subject + dir * Mathf.Max(free, Mathf.Min(.45f, length));
    }

    bool IsActor(Transform t) =>
        (player && t.IsChildOf(player.transform)) || (kostya && t.IsChildOf(kostya)) ||
        (rescue && t.IsChildOf(rescue.transform)) || (late && t.IsChildOf(late.transform)) ||
        (view && t.IsChildOf(view.transform));

    static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        if (v.sqrMagnitude < .0001f) { v = fallback; v.y = 0f; }
        return v.sqrMagnitude < .0001f ? Vector3.forward : v.normalized;
    }

    Vector3 HeadA() => Eyes(player ? player.transform : null, ref boneA, 1.6f);
    Vector3 HeadK() => Eyes(kostya, ref boneK, 1.58f);
    Vector3 HeadF() => rescue && rescue.activeInHierarchy
        ? Eyes(rescue.transform, ref boneFather, 1.1f)
        : Eyes(late ? late.transform : null, ref boneLate, .6f);

    static Vector3 Eyes(Transform root, ref Transform head, float fallbackHeight)
    {
        if (!root) return Vector3.zero;
        if (!head) head = FindHead(root);
        return head ? head.position + Vector3.up * .06f : root.position + Vector3.up * fallbackHeight;
    }

    static Transform FindHead(Transform root)
    {
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            if (animator.isHuman && animator.avatar)
            {
                var bone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone) return bone;
            }
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var bone in skin.bones)
                if (bone && bone.name.EndsWith("head", System.StringComparison.OrdinalIgnoreCase)) return bone;
        return null;
    }
}
