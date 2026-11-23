using System.Collections;
using CarXCase.UI;
using DetectiveGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

/// Review menu for the hangar finale (Ё or F8 in the harbour scene): replays any ending straight away.
/// The player is put inside the hangar, the dialogue answers are picked automatically to reach
/// the chosen outcome, and the decision is pressed for you, so every cutscene can be watched in turn.
public sealed partial class HangarFinale
{
    bool debugMenu;
    CursorLockMode debugLock;
    bool debugCursor;

    static readonly (string label, string ending, bool late)[] DebugEndings =
    {
        ("Подслушивание у двери", "door", false),
        ("Успел · выбор «Выстрелить»", "shot", false),
        ("Успел · выбор «Обезвредить»", "arrest", false),
        ("Успел · не успел нажать (промах)", "missed", false),
        ("Успел · «Последняя ошибка»", "mistake", false),
        ("Успел · «Грехи отцов»", "sins", false),
        ("Успел · «Десять лет молчания»", "silence", false),
        ("Опоздал · выбор «Выстрелить»", "shot", true),
        ("Опоздал · выбор «Обезвредить»", "arrest", true),
        ("Опоздал · «Последняя ошибка»", "mistake", true),
        ("Опоздал · «Десять лет молчания»", "silence", true),
        ("Титры", "credits", false),
    };

    void DebugKeys()
    {
        var keys = Keyboard.current;
        // F8, or the key left of 1 (Ё / `) for laptops where F8 is a system key.
        if (keys == null || !(keys.f8Key.wasPressedThisFrame || keys.backquoteKey.wasPressedThisFrame)) return;
        debugMenu = !debugMenu;
        if (debugMenu)
        {
            debugLock = Cursor.lockState;
            debugCursor = Cursor.visible;
            var cam = FindAnyObjectByType<ThirdPersonCamera>();
            if (cam) cam.SetCursorLock(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = debugLock;
            Cursor.visible = debugCursor;
        }
    }

    /// Draws the menu; returns true while it is open so nothing else draws over it.
    bool DebugMenu()
    {
        if (!debugMenu) return false;
        float w = Mathf.Min(460f, Screen.width - 40f), row = 38f;
        float h = 70f + DebugEndings.Length * (row + 6f);
        var box = new Rect((Screen.width - w) * .5f, Mathf.Max(20f, (Screen.height - h) * .5f), w, h);
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .55f));
        Fill(box, new Color(.06f, .07f, .07f, .96f));
        var head = new GUIStyle(GameTypography.Label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
        head.normal.textColor = new Color(.95f, .92f, .84f); head.Steady();
        GUI.Label(new Rect(box.x + 16, box.y + 10, w - 32, 30), "Финал в ангаре — показать сцену (Ё или F8 — закрыть)", head);
        for (int i = 0; i < DebugEndings.Length; i++)
        {
            var e = DebugEndings[i];
            if (ChoiceButton.Draw(new Rect(box.x + 16, box.y + 50 + i * (row + 6f), w - 32, row), e.label, 17))
                DebugPlay(e.ending, e.late);
        }
        return true;
    }

    void DebugPlay(string ending, bool lateArrival)
    {
        debugMenu = false;
        Leave();
        if (ending == "credits") { stage = Stage.Report; FinishCase(); return; }
        foreach (var id in new[] { Shot, Arrest, Mistake, Sins, Silence }) ClueJournal.Forget(id);
        if (lateArrival) ClueJournal.AddConclusion(LateRoute); else ClueJournal.Forget(LateRoute);
        if (!HangarKey.Found) ClueJournal.Collect(HangarKey.Id, "Ключ от ангара", "Латунный ключ с грузового ящика на причале.");
        SetBranch(lateArrival);
        staging.ResetActors();
        var officer = FindAnyObjectByType<OfficerController>();
        if (!officer) return;
        if (ending == "door")
        {
            ClueJournal.Forget(Overheard);
            Place(officer, new Vector3(-.75f, .1f, -2.0f), new Vector3(.6f, .1f, -.7f));
            Listen(officer);
            return;
        }
        ClueJournal.AddConclusion(Overheard);
        Place(officer, new Vector3(-.35f, .1f, 1.2f), kostya.localPosition);
        Enter(officer);
        StartCoroutine(DebugDrive(ending, officer));
    }

    void Place(OfficerController officer, Vector3 local, Vector3 lookLocal)
    {
        var controller = officer.GetComponent<CharacterController>();
        bool was = controller && controller.enabled;
        if (was) controller.enabled = false;
        Vector3 at = transform.TransformPoint(local), look = transform.TransformPoint(lookLocal) - at;
        look.y = 0;
        officer.transform.SetPositionAndRotation(at, look.sqrMagnitude > .001f ? Quaternion.LookRotation(look) : officer.transform.rotation);
        if (was) controller.enabled = true;
    }

    IEnumerator DebugDrive(string ending, OfficerController officer)
    {
        // After the opening confrontation, step up to Kostya so the negotiation starts.
        while (stage != Stage.Approach) yield return null;
        yield return new WaitForSeconds(.4f);
        Place(officer, new Vector3(.2f, .1f, 5.6f), kostya.localPosition);

        int answer = ending switch { "mistake" => 2, "sins" => 3, "silence" => 0, _ => 1 };
        for (int picks = 0; picks < 2; picks++)
        {
            while (stage != Stage.DialogueChoice) yield return null;
            yield return new WaitForSeconds(1.6f);
            Pick(answer);
        }
        if (ending != "shot" && ending != "arrest" && ending != "missed") yield break;
        while (stage != Stage.Decision) yield return null;
        if (ending == "missed") yield break;
        yield return new WaitForSeconds(1.9f);   // let Kostya start going for his pocket
        Resolve(ending == "shot");
    }
}
