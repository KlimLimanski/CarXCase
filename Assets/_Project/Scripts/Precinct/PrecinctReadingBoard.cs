using System.Linq;
using Casebook;
using CarXCase.Investigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CarXCase.Precinct
{
    // All positions are relative to the existing cork board in the detective's office.
    public sealed class PrecinctReadingBoard : MonoBehaviour
    {
        static readonly Vector2 NoteSize = new Vector2(235, 137);
        static readonly Color Ink = new Color(.15f, .125f, .11f);
        readonly Texture2D[] stationery = new Texture2D[3];
        Texture2D pencilStroke;
        static readonly Vector2[] Offsets = {
            new Vector2(-6,18), new Vector2(4,-26), new Vector2(0,8), new Vector2(6,-9),
            new Vector2(-8,-15), new Vector2(12,23), new Vector2(5,-9)
        };
        DeductionBoard board;
        RectTransform sheetLayer;
        Font font;
        CaseSession lastSession;
        int lastCount = -1;
        bool reading;
        string selected;
        readonly System.Collections.Generic.Dictionary<string, RectTransform> notes = new();
        readonly System.Collections.Generic.List<GameObject> yarn = new();


        void Start()
        {
            board = GetComponent<DeductionBoard>();
            if (!board.surface) { enabled = false; return; }
            var model = Resources.Load<GameObject>("PrecinctCork");
            if (!model) { Debug.LogError("Missing PrecinctCork model", this); enabled = false; return; }
            var mount = new GameObject("Blender cork board").transform;
            mount.SetParent(board.surface, false);
            var boardMesh = Instantiate(model, mount, false);
            var cork = board.transform.Find("Cork")?.GetComponent<Renderer>();
            var frame = board.transform.Find("Frame")?.GetComponent<Renderer>();
            var trim = board.transform.Find("Walnut_Front_Trim");
            var wood = trim ? trim.GetComponentInChildren<Renderer>() : frame;
            foreach (var part in boardMesh.GetComponentsInChildren<MeshRenderer>())
            {
                if (part.name.StartsWith("Cork grain"))
                {
                    // The imported grain mesh has five material slots. Retain their variation.
                    var slots = part.sharedMaterials;
                    for(int i=0;i<slots.Length;i++)
                    {
                        if(cork)slots[i]=cork.sharedMaterial;
                    }
                    part.sharedMaterials=slots;
                    for(int i=0;i<slots.Length;i++)
                    {
                        var tint=new MaterialPropertyBlock();
                        tint.SetColor("_BaseColor",Color.Lerp(new Color(.48f,.36f,.24f),new Color(.92f,.77f,.58f),i/4f));
                        part.SetPropertyBlock(tint,i);
                    }
                }
                else if(part.name.StartsWith("Cork") && cork)part.sharedMaterial=cork.sharedMaterial;
                else if(part.name.StartsWith("Brass") && board.pinMaterial)part.sharedMaterial=board.pinMaterial;
                else if(wood)part.sharedMaterial=wood.sharedMaterial;
            }
            if(cork)cork.enabled=false;
            if(frame)frame.enabled=false;
            if(trim)foreach(var oldRail in trim.GetComponentsInChildren<Renderer>())oldRail.enabled=false;
            // Fit exported model units before orienting the front toward the detective.
            var filters = boardMesh.GetComponentsInChildren<MeshFilter>();
            var bounds = new Bounds(); bool first = true;
            foreach (var filter in filters)
            {
                var meshBounds = filter.sharedMesh.bounds;
                for (int corner=0;corner<8;corner++)
                {
                    Vector3 signs = new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                    var point=mount.InverseTransformPoint(filter.transform.TransformPoint(meshBounds.center+Vector3.Scale(meshBounds.extents,signs)));
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            var grain = filters.FirstOrDefault(f=>f.name.StartsWith("Cork grain"));
            if(grain && mount.InverseTransformPoint(grain.transform.TransformPoint(grain.sharedMesh.bounds.center)).z < bounds.center.z)
                mount.localRotation=Quaternion.Euler(0,180,0);
            float scale = board.surfaceSize.x / Mathf.Max(.001f,bounds.size.x);
            mount.localScale=Vector3.one*scale;
            mount.localPosition=Vector3.forward*(.04f+scale*bounds.extents.z)-mount.localRotation*(bounds.center*scale);

            sheetLayer=(RectTransform)new GameObject("Pinned diary entries", typeof(RectTransform),typeof(Canvas)).transform;
            sheetLayer.SetParent(board.surface,false);
            sheetLayer.localRotation=Quaternion.Euler(0,180,0);
            sheetLayer.localPosition=Vector3.forward*(.044f+scale*bounds.size.z);
            sheetLayer.localScale=Vector3.one*(board.surfaceSize.x/1340f);
            sheetLayer.sizeDelta=new Vector2(1200,680);
            sheetLayer.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            font = GameTypography.Face;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pencilStroke = MakePencilStroke();
            for (int i = 0; i < stationery.Length; i++) stationery[i] = MakeStationery(i);
        }

        void LateUpdate()
        {
            var desk = InvestigationIntegration.Controller;
            reading = desk && desk.View == DeskView.Board && board.isActiveAndEnabled;
            if (!reading) selected = null;
            if (reading && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                PickNote(desk.Session);
            if (reading && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && board.DecisionReady)
                board.OpenDecision();

            if (!desk || desk.Session == null) return;
            var session = desk.Session;
            if (session == lastSession && lastCount == session.State.found.Count) return;
            selected = null;
            notes.Clear();
            yarn.Clear();
            foreach (Transform child in sheetLayer)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            var entries = session.State.found.Select(record => desk.caseFile.Find(record.id))
                .Where(entry => entry != null && entry.section == Section.Evidence).ToList();
            for (int i = 0; i < entries.Count; i++) DrawNote(entries[i], i, entries.Count);
            DrawThreads(session);
            lastSession = session;
            lastCount = session.State.found.Count;
        }

        void PickNote(CaseSession session)
        {
            var point = Mouse.current.position.ReadValue();
            foreach (var item in notes)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(item.Value, point, Camera.main)) continue;
                if (selected == null) { selected = item.Key; return; }
                string first = selected;
                selected = null;
                if (first == item.Key) return;
                var rule = session.File.deductions.Find(d => d.Matches(first, item.Key));
                if (rule == null)
                {
                    InvestigationIntegration.Controller.Notify("Эти улики пока нечем связать.");
                    return;
                }
                foreach (string id in new[] { first, item.Key })
                {
                    if (session.Pinned(id) != null) continue;
                    session.Detach(id);
                    var pos = notes[id].anchoredPosition;
                    session.Place(id, new Vector2(pos.x, -pos.y));
                }
                session.Tie(first, item.Key);
                DrawThreads(session);
                return;
            }
        }

        Vector2 PinPosition(RectTransform note)
        {
            bool carbon = note.GetComponent<RawImage>().texture == stationery[2];
            return sheetLayer.InverseTransformPoint(note.TransformPoint(new Vector3(
                note.rect.xMin + note.rect.width * (carbon ? .79f : .47f),
                note.rect.yMax - note.rect.height * 11f / 304f, 0)));
        }

        void DrawThreads(CaseSession session)
        {
            foreach (var line in yarn) Destroy(line);
            yarn.Clear();
            foreach (string id in session.State.threads)
            {
                var rule = session.File.deductions.Find(d => d.id == id);
                if (rule == null || !notes.TryGetValue(rule.first, out var first) ||
                    !notes.TryGetValue(rule.second, out var second)) continue;
                Vector2 a = PinPosition(first), b = PinPosition(second);
                var line = Node(sheetLayer, "Красная нить", Vector2.zero, new Vector2((b-a).magnitude, 3));
                line.pivot = new Vector2(0, .5f);
                line.localPosition = a;
                line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y-a.y, b.x-a.x) * Mathf.Rad2Deg);
                var image = line.gameObject.AddComponent<Image>();
                image.color = new Color(.55f,.045f,.035f);
                image.raycastTarget = false;
                yarn.Add(line.gameObject);
            }
        }

        void DrawNote(Entry entry, int index, int count)
        {
            int columns = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(count * 1.45f)));
            int rows = Mathf.CeilToInt(count / (float)columns);
            float cellWidth = Mathf.Min(272f, 1140f / columns);
            float cellHeight = Mathf.Min(221f, 625f / rows);
            float scale = Mathf.Min(1f, cellWidth / 261f, cellHeight / 210f);
            int row = index / columns;
            int rowCount = Mathf.Min(columns, count - row * columns);
            var pos = new Vector2((1200 - rowCount * cellWidth) * .5f + (index % columns + .5f) * cellWidth,
                                  (680 - rows * cellHeight) * .5f + (row + .5f) * cellHeight);
            float[] tilts = { -3.5f, 2f, -1f, 3.2f, -2f };
            int stock = index % 3;
            Vector2 size = stock == 1 ? new Vector2(226,164) : stock == 2 ? new Vector2(241,145) : new Vector2(248,151);
            var note = Node(sheetLayer, entry.id, pos, size);
            notes.Add(entry.id, note);
            note.pivot = new Vector2(.5f, .5f);
            note.anchoredPosition += Offsets[index % Offsets.Length] * scale;
            note.localScale = Vector3.one * scale;
            note.localRotation = Quaternion.Euler(0, 0, tilts[index % tilts.Length]);

            var paper = note.gameObject.AddComponent<RawImage>();
            paper.texture = stationery[stock];
            paper.raycastTarget = false;
            var shadow = note.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.075f,.06f,.05f,.57f);
            shadow.effectDistance = new Vector2(3,-4);

            float left=stock==1?28:16;
            string heading = entry.title;
            if (entry.id=="clue_answering_machine") heading="Автоответчик";
            if (entry.id=="clue_bar_matches") heading="Коробок «Стерлядь»";
            float width = size.x - left - 13f;
            var title = TextLine(note, heading, new Vector2(left, 14), new Vector2(width, 44), 28, Ink);
            title.font = font;
            title.resizeTextForBestFit = false;
            float naturalWidth = title.preferredWidth;
            title.fontSize = Mathf.Clamp(Mathf.FloorToInt(28f * width / Mathf.Max(1f, naturalWidth)), 21, 28);
            float titleHeight = Mathf.Min(53f, Mathf.Ceil(title.preferredHeight));
            title.rectTransform.sizeDelta = new Vector2(width, titleHeight);
            title.resizeTextMaxSize = title.fontSize;
            title.resizeTextForBestFit = true;
            title.alignment = TextAnchor.LowerLeft;

            float lineTop = 14f + titleHeight + 1f;
            float lineWidth = Mathf.Min(width, naturalWidth * title.fontSize / 28f + 4f);
            var stroke = Node(note, "Красный карандаш", new Vector2(left - 1f, lineTop),
                new Vector2(lineWidth, 5.8f)).gameObject.AddComponent<RawImage>();
            stroke.texture = pencilStroke;
            stroke.raycastTarget = false;

            float bodyTop = lineTop + 13f;
            var body = TextLine(note, Summary(entry), new Vector2(left, bodyTop),
                new Vector2(width, size.y - bodyTop - 10f), 22, Ink);
            body.fontStyle = FontStyle.Italic;
        }

        // A broad pencil mark, with a grainy edge and lighter pressure at the tip.
        static Texture2D MakePencilStroke()
        {
            const int width = 256, height = 12;
            var pixels = new Color32[width * height];
            var grain = new System.Random(129);
            for (int x = 0; x < width; x++)
            {
                float u = x / (width - 1f);
                float tip = Mathf.Clamp01(Mathf.Min(u * 25f, (1f - u) * 7f));
                float center = 5f + 1.8f * u + .7f * Mathf.Sin(u * 9f);
                float radius = tip * (3.2f + .65f * Mathf.Sin(u * 15f));
                for (int y = 0; y < height; y++)
                {
                    float coverage = Mathf.Clamp01(radius + .5f - Mathf.Abs(y - center));
                    float density = .70f + .30f * (float)grain.NextDouble();
                    pixels[y * width + x] = new Color32(113, 31, 25, (byte)(255f * coverage * density));
                }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static string Summary(Entry entry)
        {
            switch(entry.id)
            {
                case "clue_record": return "Джазовая пластинка лежит стороной Б вверх.";
                case "clue_door_lock": return "Замок цел. Ключи отца остались внутри.";
                case "clue_valuables": return "Ящики вывернуты. Часы и серебро на месте.";
                case "clue_bar_matches": return "На тумбочке — спички из «Стерляди».";
                case "clue_answering_machine": return "19:15 — портовый гудок. 21:40 — угроза Дмитрию.";
                case "clue_warehouse": return "На Ван-Брант люди Резо держат должников.";
                case "clue_receipts": return "Михаил трижды закладывал отцовские часы.";
            }
            // New evidence automatically uses a short excerpt from the actual discovery.
            string text = (entry.observation ?? "").Trim();
            if (text.Length == 0) return entry.location ?? "";
            int stop = text.IndexOfAny(new[] { '.', '!', '?' });
            if (stop >= 0 && stop < 83) return text.Substring(0,stop+1);
            if (text.Length <= 84) return text;
            int space = text.LastIndexOf(' ',83);
            return text.Substring(0,space > 0 ? space : 83).TrimEnd() + "…";
        }

        // Three stocks: an index slip, a torn ruled notebook leaf and a folded carbon copy.
        static Texture2D MakeStationery(int stock)
        {
            const int w=480,h=304;
            Color[] tones={new Color(.84f,.775f,.675f),new Color(.86f,.81f,.72f),new Color(.80f,.735f,.635f)};
            var pixels=new Color32[w*h];
            var rng=new System.Random(417+stock);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float t=h-1-y;
                float edge=Mathf.Min(Mathf.Min(x,w-1-x),Mathf.Min(y,t));
                float grain=(float)rng.NextDouble()*.036f + Mathf.PerlinNoise(x*.032f,y*.047f)*.04f;
                float variation = Mathf.PerlinNoise(x * .012f + stock * 5f, y * .016f);
                float value=.92f+grain+.075f*variation-.18f*Mathf.Exp(-edge/8f);
                Color c=tones[stock]*value;
                if(stock==1)
                {
                    if(t>102 && (y%35)<2)c=Color.Lerp(c,new Color(.37f,.48f,.49f),.3f);
                    if(Mathf.Abs(x-43)<1.5f)c=Color.Lerp(c,new Color(.54f,.34f,.31f),.50f);
                }
                if(stock==2)
                {
                    float crease=x-w*.69f+t*.026f;
                    c*=1f+.037f*Mathf.Exp(-Mathf.Abs(crease-3)/4)-.092f*Mathf.Exp(-Mathf.Abs(crease)/2);
                }
                float rough=Mathf.PerlinNoise(x*.09f,t*.08f)*2.8f;
                float tornEdge = 2f + 6f * Mathf.PerlinNoise(x * .032f, stock + .3f);
                float alpha = Mathf.Clamp01(Mathf.Min(edge, y - tornEdge) - rough);
                if(stock==0)alpha*=Mathf.Clamp01(x+t-16); // clipped upper corner
                if(stock==1 && x<23)
                {
                    float dist=Vector2.Distance(new Vector2(x,t%44),new Vector2(12,20));
                    alpha*=Mathf.Clamp01(dist-5);
                    if(x<12 && Mathf.Abs(t%44-20)<1.3f)alpha=0;
                }
                if(stock==2 && w-x+t<26)
                    c=Color.Lerp(c,new Color(.58f,.56f,.49f),.55f);
                // A domed drawing pin with its own contact shadow and metal glint.
                var point=new Vector2(x,t);
                var pin=new Vector2(stock==2?w*.79f:w*.47f,11);
                float d=Vector2.Distance(point,pin);
                c*=1-.32f*Mathf.Exp(-.07f*(point-pin-new Vector2(2,3)).sqrMagnitude);
                if(d<7.3f)
                {
                    Color metal=stock==0?new Color(.40f,.16f,.11f):new Color(.35f,.32f,.26f);
                    c=Color.Lerp(metal*.45f,metal*1.7f,Mathf.Clamp01(1-Vector2.Distance(point,pin-new Vector2(2,2))/9));
                    c+=Color.white*(.50f*Mathf.Exp(-.44f*(point-pin+new Vector2(2,2)).sqrMagnitude));
                }
                c.a=alpha;pixels[y*w+x]=c;
            }
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Clamp};
            tex.SetPixels32(pixels);tex.Apply(true,true);return tex;
        }
        void OnDestroy()
        {
            if (pencilStroke) Destroy(pencilStroke);
            foreach(var tex in stationery) if(tex)Destroy(tex);
        }

        static RectTransform Node(Transform parent,string name,Vector2 pos,Vector2 size)
        {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(pos.x,-pos.y);rect.sizeDelta=size;return rect;
        }
        Text TextLine(Transform parent,string value,Vector2 pos,Vector2 size,int pt,Color color)
        {
            var text=Node(parent,"Text",pos,size).gameObject.AddComponent<Text>();
            text.font=font;text.fontSize=pt;text.resizeTextForBestFit=true;text.resizeTextMinSize=10;text.resizeTextMaxSize=pt;
            text.text=value;text.color=color;text.raycastTarget=false;text.supportRichText=false;return text;
        }
        GUIStyle controlsStyle;

        void OnGUI()
        {
            if (!reading) return;
            string hint = selected == null ? "Сопоставьте две улики"
                : "Найдите пару для улики «" + InvestigationIntegration.Controller.caseFile.Find(selected).title + "»";
            var previousColor = GUI.color;
            var previousContent = GUI.contentColor;
            GUI.color = GUI.contentColor = Color.white;
            DetectiveGame.Core.SimpleUIHUD.DrawObjectiveCard(hint, Screen.height * .085f);

            controlsStyle ??= new GUIStyle(GameTypography.Label) { alignment = TextAnchor.MiddleCenter };
            controlsStyle.normal.textColor = Color.white;
            float scale = Mathf.Clamp(Screen.height / 900f, .75f, 2f);
            controlsStyle.fontSize = Mathf.RoundToInt(15 * scale);
            string controls = board.DecisionReady ? "Esc — отойти     F — куда отправиться" : "Esc — отойти";
            GUI.Label(new Rect(0, Screen.height - 38 * scale, Screen.width, 24 * scale), controls, controlsStyle);
            GUI.contentColor = previousContent;
            GUI.color = previousColor;
        }
    }
}
