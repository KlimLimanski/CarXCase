using System.Collections;
using System.Collections.Generic;
using DetectiveGame.Core;
using UnityEngine;

/// A hasp and a brass padlock on the outside of hangar No. 4. Built at runtime on the door leaves
/// (found with a ray from the door's interaction point), opened with the key from the pier:
/// the shackle springs, the lock drops to the concrete and the hasp swings back. Stays open once opened.
public sealed class HangarPadlock : MonoBehaviour
{
    public const string OpenedId = "hangar_padlock_open";
    public bool Open => ClueJournal.Has(OpenedId);
    public Vector3 Point => body ? body.position : transform.position;
    public Vector3 Outward => transform.forward;

    Transform body, shackle, haspFlap;
    Vector3 bodyRest, shackleRest;
    Quaternion bodyRestRot, flapRest;
    float ground;

    /// Builds the lock on the door surface in front of `interaction`, facing the side the player stands on.
    public static HangarPadlock Build(Transform interaction, Transform inside)
    {
        Vector3 into = inside ? inside.position - interaction.position : interaction.forward;
        into.y = 0f;
        into = into.sqrMagnitude > .001f ? into.normalized : Vector3.forward;
        Vector3 from = interaction.position - into * .6f;
        Vector3 point = interaction.position + into * .25f, normal = -into;
        float best = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(from, into, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<OfficerController>()) continue;
            if (hit.distance < best) { best = hit.distance; point = hit.point; normal = hit.normal; }
        }
        // The visible boards can stand in front of the door's collider: take the nearest door-sized mesh too.
        var ray = new Ray(from, into);
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var b = r.bounds;
            if (!r.enabled || b.Contains(from)) continue;
            if (r.GetComponentInParent<OfficerController>()) continue;
            if (b.IntersectRay(ray, out float d) && d > .05f && d < best)
            {
                best = d;
                point = from + into * d;
                normal = -into;
            }
        }
        normal.y = 0f;
        normal = normal.sqrMagnitude > .001f ? normal.normalized : -into;

        var go = new GameObject("Hangar padlock");
        go.transform.SetPositionAndRotation(point + normal * .004f, Quaternion.LookRotation(normal, Vector3.up));
        var mount = interaction.Find("Exterior hasp mount");
        if (mount) go.transform.SetPositionAndRotation(mount.position, mount.rotation);
        go.transform.SetParent(interaction, true);
        var lockOn = go.AddComponent<HangarPadlock>();
        lockOn.ground = interaction.position.y - 1.1f;
        if (Physics.Raycast(point + normal * .3f, Vector3.down, out var floor, 3f, ~0, QueryTriggerInteraction.Ignore))
            lockOn.ground = floor.point.y;
        lockOn.Assemble();
        return lockOn;
    }

    // ---------- geometry ----------

    static Mesh cube;
    static Mesh Cube()
    {
        if (cube) return cube;
        var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube = probe.GetComponent<MeshFilter>().sharedMesh;
        if (Application.isPlaying) Destroy(probe); else DestroyImmediate(probe);
        return cube;
    }

    static Material Metal(Color color, float metallic, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(shader) { name = "Padlock metal" };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    static Transform Part(string name, Transform parent, Mesh mesh, Material material, Vector3 local, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go.transform;
    }

    /// A bent rod (the shackle / the hasp staple) as a tube through the given points.
    static Mesh Rod(IList<Vector3> points, float radius)
    {
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var tris = new List<int>();
        const int sides = 10;
        Vector3 n = Vector3.forward;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 t = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
            n = Vector3.ProjectOnPlane(n, t).normalized;
            if (n.sqrMagnitude < .1f) n = Vector3.ProjectOnPlane(Vector3.up, t).normalized;
            Vector3 b = Vector3.Cross(t, n);
            for (int j = 0; j <= sides; j++)
            {
                float a = j * Mathf.PI * 2f / sides;
                Vector3 d = n * Mathf.Cos(a) + b * Mathf.Sin(a);
                vertices.Add(points[i] + d * radius); normals.Add(d);
            }
            if (i == 0) continue;
            int row = i * (sides + 1), prev = row - sides - 1;
            for (int j = 0; j < sides; j++)
            {
                tris.Add(prev + j); tris.Add(row + j); tris.Add(prev + j + 1);
                tris.Add(prev + j + 1); tris.Add(row + j); tris.Add(row + j + 1);
            }
        }
        var mesh = new Mesh { name = "Rod" };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    void Assemble()
    {
        var steel = Metal(new Color(.32f, .33f, .34f), .85f, .42f);
        var brass = Metal(new Color(.62f, .47f, .22f), .9f, .55f);
        var paint = Metal(new Color(.13f, .15f, .15f), .35f, .3f);

        // Hasp: a fixed plate with a staple on one leaf, a hinged flap over the seam from the other.
        Part("Hasp plate", transform, Cube(), paint, new Vector3(-.045f, 0f, .002f), new Vector3(.08f, .06f, .004f));
        var staplePath = new List<Vector3>();
        for (int i = 0; i <= 16; i++)
        {
            float a = Mathf.PI * i / 16f;
            staplePath.Add(new Vector3(-.012f + Mathf.Cos(a) * -.012f, -.004f + Mathf.Sin(a) * .0f, .004f + Mathf.Sin(a) * .016f));
        }
        Part("Hasp staple", transform, Rod(staplePath, .0035f), steel, Vector3.zero, Vector3.one);
        var hinge = new GameObject("Hasp hinge").transform;
        hinge.SetParent(transform, false);
        hinge.localPosition = new Vector3(.1f, 0f, .004f);
        haspFlap = hinge;
        flapRest = hinge.localRotation;
        Part("Hasp flap", hinge, Cube(), paint, new Vector3(-.047f, 0f, .002f), new Vector3(.084f, .042f, .004f));
        Part("Hasp knuckle", hinge, Cube(), steel, new Vector3(0f, 0f, .002f), new Vector3(.012f, .05f, .012f));

        // Padlock: the shackle hangs through the staple, the brass body below it.
        body = new GameObject("Padlock").transform;
        body.SetParent(transform, false);
        body.localPosition = bodyRest = new Vector3(-.012f, -.052f, .021f);
        bodyRestRot = body.localRotation;
        Part("Body", body, Cube(), brass, Vector3.zero, new Vector3(.05f, .056f, .022f));
        Part("Body rim", body, Cube(), brass, new Vector3(0f, -.03f, 0f), new Vector3(.046f, .006f, .02f));
        Part("Keyway", body, Cube(), Metal(new Color(.05f, .04f, .03f), .2f, .2f), new Vector3(0f, -.008f, .0115f), new Vector3(.005f, .016f, .002f));
        var bow = new List<Vector3>();
        for (int i = 0; i <= 20; i++)
        {
            float a = Mathf.PI * i / 20f;
            bow.Add(new Vector3(Mathf.Cos(a) * .015f, .028f + Mathf.Sin(a) * .026f + (i == 0 || i == 20 ? -.004f : 0f), 0f));
        }
        bow.Insert(0, new Vector3(.015f, .022f, 0f));
        shackle = Part("Shackle", body, Rod(bow, .0042f), steel, Vector3.zero, Vector3.one);
        shackleRest = shackle.localPosition;

        if (Open) Rest();
    }

    /// The opened state: lock lying on the concrete under the door, hasp folded back.
    void Rest()
    {
        haspFlap.localRotation = flapRest * Quaternion.Euler(0f, -150f, 0f);
        shackle.localPosition = shackleRest + Vector3.up * .016f;
        body.SetParent(null, true);
        body.position = new Vector3(Point.x, ground + .012f, Point.z) + Outward * .22f + transform.right * .08f;
        body.rotation = Quaternion.LookRotation(Vector3.up, Outward) * Quaternion.Euler(0f, 0f, 35f);
    }

    /// Key turns, shackle springs, the lock falls to the ground and the hasp swings back.
    public IEnumerator Unlock()
    {
        LocationAudio.Effect("Lock key press", .6f);
        var key = new GameObject("Pier key in cylinder").transform;
        key.SetParent(body, false);
        var metal = body.Find("Body").GetComponent<Renderer>().sharedMaterial;
        Part("Blade", key, Cube(), metal, Vector3.forward * .018f, new Vector3(.004f,.008f,.044f));
        var ring = new List<Vector3>();
        for (int i = 0; i <= 20; i++)
        {
            float a = Mathf.PI * 2f * i / 20f;
            ring.Add(new Vector3(0, .012f * Mathf.Sin(a), .052f + .013f * Mathf.Cos(a)));
        }
        Part("Key bow", key, Rod(ring,.0025f), metal, Vector3.zero, Vector3.one);
        Vector3 slot = new Vector3(0,-.008f,.012f);
        for (float t = 0; t < 1f; t += Time.deltaTime / .6f)
        {
            key.localPosition = slot + Vector3.forward * (.035f * (1f - Mathf.SmoothStep(0,1,Mathf.Clamp01(t*2.5f))));
            key.localRotation = Quaternion.Euler(0,0,75f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,1f,t)));
            yield return null;
        }
        Destroy(key.gameObject);
        LocationAudio.Effect("Gate unlocked", .5f);
        for (float t = 0; t < 1f; t += Time.deltaTime / .18f)
        {
            shackle.localPosition = shackleRest + Vector3.up * (.016f * Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        yield return Wait(.25f);

        // Unhook: tilt out of the staple, then drop with a tumble.
        Vector3 start = body.position;
        Quaternion startRot = body.rotation;
        body.SetParent(null, true);
        Vector3 landing = new Vector3(Point.x, ground + .012f, Point.z) + Outward * .22f + transform.right * .08f;
        Quaternion lying = Quaternion.LookRotation(Vector3.up, Outward) * Quaternion.Euler(0f, 0f, 35f);
        Vector3 lift = Outward * .03f + Vector3.up * .02f;
        for (float t = 0; t < 1f; t += Time.deltaTime / .22f)
        {
            body.position = start + lift * Mathf.SmoothStep(0, 1, t);
            body.rotation = startRot * Quaternion.Euler(-35f * t, 0f, 0f);
            yield return null;
        }
        Vector3 top = body.position;
        Quaternion tumble = body.rotation;
        float fall = Mathf.Sqrt(2f * Mathf.Max(.05f, top.y - landing.y) / 9.81f);
        for (float t = 0; t < fall; t += Time.deltaTime)
        {
            float u = t / fall;
            body.position = Vector3.Lerp(top, landing, u) + Vector3.up * ((top.y - landing.y) * (1f - u * u) - (top.y - landing.y) * (1f - u));
            body.rotation = Quaternion.Slerp(tumble, lying, u) * Quaternion.Euler(0f, 540f * u, 0f);
            haspFlap.localRotation = flapRest * Quaternion.Euler(0f, -150f * Mathf.SmoothStep(0, 1, u), 0f);
            yield return null;
        }
        body.SetPositionAndRotation(landing, lying);
        haspFlap.localRotation = flapRest * Quaternion.Euler(0f, -150f, 0f);
        LocationAudio.Effect("Lock key press", .3f);
        ClueJournal.AddConclusion(OpenedId);
        yield return Wait(.45f);
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime) yield return NoirMenu.GameSession.NextStoryFrame;
    }

    void OnDestroy()
    {
        if (body && body.parent != transform) Destroy(body.gameObject);
    }
}
