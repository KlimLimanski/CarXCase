using System;
using System.Linq;
using UnityEngine;
namespace Casebook {
public enum DeskView { Closed, Journal, Board }
[DefaultExecutionOrder(-200)] public class CasebookController : MonoBehaviour {
    public InvestigationCase caseFile;
    public bool standaloneInput=true;
    public Func<bool> BoardActionAvailable;
    public Action BoardAction;
    int openedFrame=-1;bool resetFocus;
    [Tooltip("Контроллеры движения и обзора, которые надо временно отключать")]public Behaviour[] pauseWhileOpen=Array.Empty<Behaviour>();
    public CaseSession Session{get;private set;} public DeskView View{get;private set;}
    public event Action<DeskView> ViewChanged;
    public event Action<Deduction> HintUnlocked;
    internal string selection="", carry="", inspectHint="", inspectEntry="", linking="", toast="", tear="";
    internal float until,tearStart=-1,opened;
    bool[] previous;CursorLockMode cursor;bool visible;
    internal JournalWindow Journal;internal CorkWindow Board;
    public void Start(){if(caseFile&&Session==null)Initialize(caseFile);}
    public void Initialize(InvestigationCase file,string saveFolder=null,CaseState state=null,Action<CaseState> persist=null){if(Session!=null)Close();caseFile=file;Session=new CaseSession(file,saveFolder,state,persist);Journal=new JournalWindow(this);Board=new CorkWindow(this);
        Session.Found+=entry=>{Select(entry.id);Tell("Новая запись   /   "+entry.title);};
        Session.HintUnlocked+=rule=>{Tell("Новая подсказка   /   "+rule.title);HintUnlocked?.Invoke(rule);};
        selection=Session.State.found.LastOrDefault()?.id??"";
    }
    public bool Collect(string id)=>Session!=null&&Session.Collect(id);
    internal void Select(string id){if(selection!=id)Journal.ResetDetail();selection=id;}
    public void OpenJournal(){Open(DeskView.Journal);if(Session?.Get(selection)!=null)Session.Read(selection);}
    public void OpenBoard(){Open(DeskView.Board);}

    // ---- Board on a physical surface ------------------------------------------------------
    // The same CorkWindow is rendered every frame onto a quad laid over the 3D board in the scene,
    // and the mouse is cast onto that surface, so the board in the world is the interface.
    Transform worldSurface;Vector2 worldSize;RenderTexture worldTarget;GameObject worldQuad;Material worldMaterial;Vector2 lastVirtual;
    public bool WorldBoard=>View==DeskView.Board&&worldSurface;
    /// Opens the board on the given surface (local X right, Y up, Z out of the board).
    public void OpenBoardOn(Transform surface,Vector2 size){worldSurface=surface;worldSize=size;OpenBoard();}
    void EnsureWorldTarget(){
        int w=Mathf.Max(64,Screen.width),h=Mathf.Max(64,Screen.height);
        if(!worldTarget||worldTarget.width!=w||worldTarget.height!=h){if(worldTarget)worldTarget.Release();worldTarget=new RenderTexture(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){name="Board surface",filterMode=FilterMode.Trilinear,useMipMap=true,autoGenerateMips=true,anisoLevel=4};worldTarget.Create();if(worldMaterial)Bind();}
        if(!worldQuad){worldQuad=GameObject.CreatePrimitive(PrimitiveType.Quad);worldQuad.name="Board_Live_Surface";Destroy(worldQuad.GetComponent<Collider>());
            var shader=Shader.Find("Universal Render Pipeline/Lit");worldMaterial=new Material(shader){name="Board surface (live)"};
            worldMaterial.SetFloat("_Smoothness",.12f);worldMaterial.EnableKeyword("_EMISSION");worldMaterial.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
            worldQuad.GetComponent<MeshRenderer>().sharedMaterial=worldMaterial;worldQuad.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Bind();}
        // A quad shows its -Z face: point -Z out of the board (towards the viewer), a few millimetres off the cork.
        // Its local +X is then the viewer's right, so the layout reads unmirrored.
        worldQuad.transform.SetPositionAndRotation(worldSurface.position+worldSurface.forward*.004f,Quaternion.LookRotation(-worldSurface.forward,worldSurface.up));
        worldQuad.transform.localScale=new Vector3(worldSize.x,worldSize.y,1);
        worldQuad.SetActive(true);
    }
    void Bind(){worldMaterial.SetTexture("_BaseMap",worldTarget);worldMaterial.SetTexture("_EmissionMap",worldTarget);worldMaterial.SetColor("_BaseColor",Color.white);worldMaterial.SetColor("_EmissionColor",new Color(.55f,.55f,.55f));
        // GUI draws top-down into the target; flip V when the platform stores it the other way up.
        var scale=new Vector2(1,flipV?-1:1);var offset=new Vector2(0,flipV?1:0);
        worldMaterial.SetTextureScale("_BaseMap",scale);worldMaterial.SetTextureOffset("_BaseMap",offset);worldMaterial.SetTextureScale("_EmissionMap",scale);worldMaterial.SetTextureOffset("_EmissionMap",offset);}
    [Tooltip("Flip the live board image vertically if it appears upside down on this platform.")]public bool flipV;
    void HideWorld(){if(worldQuad)worldQuad.SetActive(false);}
    /// Screen pixel (GUI, y down) → board layout point (1440x900 space); false if the ray misses the board.
    bool ToBoard(Vector2 gui,out Vector2 layout){
        layout=new Vector2(-9999,-9999);var cam=Camera.main;if(!cam||!worldQuad)return false;
        var ray=cam.ScreenPointToRay(new Vector3(gui.x,Screen.height-gui.y,0));var plane=new Plane(worldSurface.forward,worldSurface.position);
        if(!plane.Raycast(ray,out float enter))return false;
        Vector3 local=worldQuad.transform.InverseTransformPoint(ray.GetPoint(enter));
        float u=local.x+.5f,v=local.y+.5f;
        var area=CorkWindow.WorldArea;layout=new Vector2(area.x+u*area.width,area.y+(1-v)*area.height);
        return u>=0&&u<=1&&v>=0&&v<=1;}
    void DrawWorldBoard(){
        EnsureWorldTarget();
        var ev=Event.current;Vector2 screenMouse=ev.mousePosition;
        ToBoard(screenMouse,out var layout);
        var area=CorkWindow.WorldArea;
        var toTarget=Matrix4x4.Scale(new Vector3(worldTarget.width/area.width,worldTarget.height/area.height,1))*Matrix4x4.Translate(new Vector3(-area.x,-area.y,0));
        GUI.matrix=Matrix4x4.identity;
        ev.mousePosition=toTarget.MultiplyPoint3x4(layout);
        if(ev.type==EventType.MouseDrag)ev.delta=layout-lastVirtual;
        lastVirtual=layout;
        GUI.matrix=toTarget;
        if(ev.type==EventType.Repaint){var previous=RenderTexture.active;RenderTexture.active=worldTarget;GL.Clear(true,true,new Color(.3f,.24f,.18f,1));Board.Draw(true);RenderTexture.active=previous;}
        else Board.Draw(true);
        GUI.matrix=Matrix4x4.identity;
        if(ev.type!=EventType.Used)ev.mousePosition=screenMouse;
    }
    void Open(DeskView next){if(Session==null)return;if(View==DeskView.Closed){previous=pauseWhileOpen.Select(item=>item&&item.enabled).ToArray();for(int i=0;i<previous.Length;i++)if(pauseWhileOpen[i]&&pauseWhileOpen[i]!=this)pauseWhileOpen[i].enabled=false;cursor=Cursor.lockState;visible=Cursor.visible;}
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;View=next;openedFrame=Time.frameCount;opened=Time.unscaledTime;inspectEntry=inspectHint=linking="";tear="";tearStart=-1;Board.Release();resetFocus=true;ViewChanged?.Invoke(next);}
    // The board keeps its last picture after closing: the pinned sheets and threads stay on the wall.
    public void Close(){if(View==DeskView.Closed)return;worldSurface=null;Session.Save();View=DeskView.Closed;for(int i=0;i<previous.Length&&i<pauseWhileOpen.Length;i++)if(pauseWhileOpen[i])pauseWhileOpen[i].enabled=previous[i];Cursor.lockState=cursor;Cursor.visible=visible;inspectEntry=inspectHint=linking=tear=carry="";tearStart=-1;Board.Release();ViewChanged?.Invoke(View);}
    void OnDisable(){if(Session!=null)Close();}
    void OnDestroy(){if(worldTarget)worldTarget.Release();if(worldQuad)Destroy(worldQuad);if(worldMaterial)Destroy(worldMaterial);}
    void OnApplicationPause(bool paused){if(paused&&Session!=null)Session.Save();}
    public void Notify(string message)=>Tell(message);
    internal void Tell(string message){toast=message;until=Time.unscaledTime+3;}
    internal void TearDone(){var id=tear;if(Session.Detach(id)){OpenBoard();carry=id;}tear="";tearStart=-1;}
    internal void Back(){if(tear!=""){tear="";tearStart=-1;}else if(inspectEntry!=""||inspectHint!=""){inspectEntry=inspectHint="";}else if(carry!=""||linking!=""){carry=linking="";}else Close();}
    void OnGUI(){if(Session==null)return;DeskInk.Init();var original=GUI.matrix;
        var oldColor=GUI.color;var oldContent=GUI.contentColor;var oldBackground=GUI.backgroundColor;var oldEnabled=GUI.enabled;var oldDepth=GUI.depth;
        GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white;GUI.enabled=true;GUI.depth=-100;
        float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1440*scale)/2,(Screen.height-900*scale)/2,0),Quaternion.identity,Vector3.one*scale);
        if(resetFocus){GUI.FocusControl(null);resetFocus=false;}
        var ev=Event.current;bool writing=GUI.GetNameOfFocusedControl()=="CasebookNote";
        if(ev.type==EventType.KeyDown&&Time.frameCount!=openedFrame&&(standaloneInput||View!=DeskView.Closed)){if(ev.keyCode==KeyCode.Escape&&View!=DeskView.Closed){if(writing)GUI.FocusControl(null);else Back();ev.Use();}else if(!writing&&ev.keyCode==(KeyCode)Session.State.journalKey){if(View==DeskView.Journal)Close();else OpenJournal();ev.Use();}else if(!writing&&ev.keyCode==(KeyCode)Session.State.boardKey){if(View==DeskView.Board)Close();else OpenBoard();ev.Use();}}
        if(View==DeskView.Journal){Journal.Draw();}
        else if(WorldBoard){
            // The board itself lives in the scene; only an open sheet is read on screen.
            var screenMatrix=GUI.matrix;DrawWorldBoard();GUI.matrix=screenMatrix;
            if(Board.Modal)Board.DrawModal();
        }
        else if(View==DeskView.Board){Board.Draw();}
        // Notices only inside the diary/board; in play the floating J key announces new pages.
        if(View!=DeskView.Closed&&Time.unscaledTime<until){var r=new Rect(777,26,602,44);DeskInk.Box(r,DeskInk.Dark);DeskInk.Box(new Rect(r.x,r.y,4,r.height),DeskInk.Wine);DeskInk.Label(new Rect(r.x+16,r.y,r.width-22,r.height),toast,16,DeskInk.Cream);}
        if(Session.Error!=null)DeskInk.Label(new Rect(32,850,1340,46),"Сохранение: "+Session.Error,16,Color.white);
        GUI.matrix=original;GUI.color=oldColor;GUI.contentColor=oldContent;GUI.backgroundColor=oldBackground;GUI.enabled=oldEnabled;GUI.depth=oldDepth;
    }
}
}
