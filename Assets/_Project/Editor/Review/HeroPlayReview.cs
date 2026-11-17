using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// Review-only Play Mode driver: injects keyboard/mouse input, logs telemetry and captures frames.
public static class HeroPlayReview
{
    public class Step
    {
        public string label = "";
        public float duration;
        public Key[] keys = new Key[0];
        public Key[] tap = new Key[0];
        public float mouseX;
        public float shotEvery;
        public string view = "main";
    }

    static List<Step> steps;
    static int index = -1;
    static float stepStart, nextShot;
    static bool firstFrame;
    static string runName;
    static StringBuilder csv;
    static Dictionary<string, List<Texture2D>> shots;
    static Transform player, hero, head, lFoot, rFoot, lToe, rToe;
    static Animator anim;
    public static string LastReport = "";
    public static bool Running => steps != null;

    public static void Begin(string name, IEnumerable<Step> sequence)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first");
        runName = name;
        steps = sequence.ToList();
        index = -1;
        csv = new StringBuilder("t,step,px,py,pz,yaw,speedParam,blendIdle,officer,lfx,lfy,lfz,rfx,rfy,rfz,ltx,lty,ltz,rtx,rty,rtz,cx,cy,cz,cyaw,cpitch,hx,hy,hz\n");
        shots = new Dictionary<string, List<Texture2D>>();
        player = GameObject.Find("Player").transform;
        hero = player.Find("Character_Visual/Alexey");
        anim = hero.GetComponent<Animator>();
        var bones = hero.GetComponentsInChildren<Transform>(true);
        Transform B(string n) => bones.First(b => b.name == n);
        head = B("Head"); lFoot = B("L_Foot"); rFoot = B("R_Foot"); lToe = B("L_Toes"); rToe = B("R_Toes");
        LastReport = "running";
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Advance(Time.time);
    }

    static void Advance(float now)
    {
        index++;
        stepStart = now;
        nextShot = now;
        firstFrame = true;
        if (index >= steps.Count) Finish();
    }

    static void Tick()
    {
        if (steps == null) return;
        if (!EditorApplication.isPlaying || EditorApplication.isPaused && !EditorApplication.isPlaying) { Finish(); return; }
        float now = Time.time;
        while (steps != null && now - stepStart >= steps[index].duration) Advance(now);
        if (steps == null) return;
        var step = steps[index];

        var held = new List<Key>(step.keys);
        if (firstFrame) held.AddRange(step.tap);
        firstFrame = false;
        if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(held.ToArray()));
        if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState { delta = new Vector2(step.mouseX, 0f) });

        var cam = Camera.main;
        Vector3 p = player.position, lf = lFoot.position, rf = rFoot.position, lt = lToe.position, rt = rToe.position, h = head.position;
        Vector3 c = cam ? cam.transform.position : Vector3.zero;
        Vector3 ce = cam ? cam.transform.eulerAngles : Vector3.zero;
        var officer = player.GetComponent<DetectiveGame.Core.OfficerController>();
        csv.AppendLine(string.Join(",", new[]
        {
            F(now), step.label, F(p.x), F(p.y), F(p.z), F(player.eulerAngles.y), F(anim.GetFloat("Speed")), "0", officer && officer.InputLocked ? "1" : "0",
            F(lf.x), F(lf.y), F(lf.z), F(rf.x), F(rf.y), F(rf.z), F(lt.x), F(lt.y), F(lt.z), F(rt.x), F(rt.y), F(rt.z),
            F(c.x), F(c.y), F(c.z), F(ce.y), F(ce.x), F(h.x), F(h.y), F(h.z)
        }));

        if (step.shotEvery > 0 && now >= nextShot && cam)
        {
            nextShot = now + step.shotEvery;
            Texture2D tex;
            Vector3 hip = hero.position + Vector3.up * .95f;
            switch (step.view)
            {
                case "side": tex = HeroReviewCapture.Render(cam, hip + player.right * 1.25f, hip, 74, 300, 400); break;
                case "front": tex = HeroReviewCapture.Render(cam, hip + player.forward * 2.4f, hip, 50, 300, 400); break;
                case "face": tex = HeroReviewCapture.Render(cam, h + Vector3.up * .07f + player.forward * .85f, h + Vector3.up * .07f, 28, 400, 400); break;
                case "face34": tex = HeroReviewCapture.Render(cam, h + Vector3.up * .07f + (player.forward + player.right * .9f).normalized * .85f, h + Vector3.up * .07f, 28, 400, 400); break;
                default: tex = HeroReviewCapture.RenderCamera(cam, 480, 270); break;
            }
            if (!shots.TryGetValue(step.label, out var list)) shots[step.label] = list = new List<Texture2D>();
            list.Add(tex);
        }
    }

    static string F(float v) => v.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);

    static void Finish()
    {
        EditorApplication.update -= Tick;
        if (steps == null) return;
        steps = null;
        try { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); } catch { }
        Directory.CreateDirectory(HeroReviewCapture.Folder);
        File.WriteAllText(HeroReviewCapture.Folder + runName + ".csv", csv.ToString());
        var report = new StringBuilder(runName + " done\n");
        foreach (var pair in shots)
            if (pair.Value.Count > 0)
                report.AppendLine(HeroReviewCapture.Sheet(runName + "_" + pair.Key, pair.Value, pair.Value[0].width > 400 ? 3 : 8));
        LastReport = report.ToString();
        Debug.Log("[HeroPlayReview] " + LastReport);
    }
}
