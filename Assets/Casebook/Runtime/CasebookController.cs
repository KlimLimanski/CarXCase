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
    public Action WorldBoardDismissed;
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
    public void OpenJournal(){Open(DeskView.Journal);}
    public void OpenBoard(){/* Opening the board requires interacting with its scene object. */}

    Transform worldSurface;
    public void OpenBoardOn(Transform surface,Vector2 size){if(!surface)return;worldSurface=surface;Open(DeskView.Board);}
    void Open(DeskView next){if(Session==null)return;if(View==DeskView.Closed){previous=pauseWhileOpen.Select(item=>item&&item.enabled).ToArray();for(int i=0;i<previous.Length;i++)if(pauseWhileOpen[i]&&pauseWhileOpen[i]!=this)pauseWhileOpen[i].enabled=false;cursor=Cursor.lockState;visible=Cursor.visible;}
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;View=next;openedFrame=Time.frameCount;opened=Time.time;inspectEntry=inspectHint=linking="";tear="";tearStart=-1;Board.Release();resetFocus=true;ViewChanged?.Invoke(next);}
    // The board keeps its last picture after closing: the pinned sheets and threads stay on the wall.
    public void Close(){if(View==DeskView.Closed)return;worldSurface=null;Session.Save();View=DeskView.Closed;for(int i=0;i<previous.Length&&i<pauseWhileOpen.Length;i++)if(pauseWhileOpen[i])pauseWhileOpen[i].enabled=previous[i];Cursor.lockState=cursor;Cursor.visible=visible;inspectEntry=inspectHint=linking=tear=carry="";tearStart=-1;Board.Release();ViewChanged?.Invoke(View);}
    void OnDisable(){if(Session!=null)Close();}
    void OnApplicationPause(bool paused){if(paused&&Session!=null)Session.Save();}
    public void Notify(string message)=>Tell(message);
    internal void Tell(string message){toast=message;until=Time.time+3;}
    internal void TearDone(){var id=tear;if(Session.Detach(id)){OpenBoard();carry=id;}tear="";tearStart=-1;}
    public void DismissWorldBoard()
    {
        if (View != DeskView.Board || !worldSurface) return;
        Close();
        WorldBoardDismissed?.Invoke();
    }
    internal void Back(){if(tear!=""){tear="";tearStart=-1;}else if(inspectEntry!=""||inspectHint!=""){inspectEntry=inspectHint="";}else if(carry!=""||linking!=""){carry=linking="";}else if(worldSurface)DismissWorldBoard();else Close();}
    void OnGUI(){
        if (NoirMenu.GameSession.Paused) return;
        if(Session==null)return;DeskInk.Init();var original=GUI.matrix;
        var oldColor=GUI.color;var oldContent=GUI.contentColor;var oldBackground=GUI.backgroundColor;var oldEnabled=GUI.enabled;var oldDepth=GUI.depth;
        GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white;GUI.enabled=true;GUI.depth=-100;
        float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1440*scale)/2,(Screen.height-900*scale)/2,0),Quaternion.identity,Vector3.one*scale);
        if(resetFocus){GUI.FocusControl(null);resetFocus=false;}
        var ev=Event.current;bool writing=GUI.GetNameOfFocusedControl()=="CasebookNote";
        if(ev.type==EventType.KeyDown&&Time.frameCount!=openedFrame&&(standaloneInput||View!=DeskView.Closed)){if(ev.keyCode==KeyCode.Escape&&View!=DeskView.Closed){if(writing)GUI.FocusControl(null);else Back();ev.Use();}else if(View!=DeskView.Board&&!writing&&ev.keyCode==(KeyCode)Session.State.journalKey){if(View==DeskView.Journal)Close();else OpenJournal();ev.Use();}}
        if(View==DeskView.Journal)Journal.Draw();
        // Notices only inside the diary/board; in play the floating J key announces new pages.
        if(View!=DeskView.Closed&&Time.time<until){var r=new Rect(777,26,602,44);DeskInk.Box(r,DeskInk.Dark);DeskInk.Box(new Rect(r.x,r.y,4,r.height),DeskInk.Wine);DeskInk.Label(new Rect(r.x+16,r.y,r.width-22,r.height),toast,16,DeskInk.Cream);}
        if(Session.Error!=null)DeskInk.Label(new Rect(32,850,1340,46),"Сохранение: "+Session.Error,16,Color.white);
        GUI.matrix=original;GUI.color=oldColor;GUI.contentColor=oldContent;GUI.backgroundColor=oldBackground;GUI.enabled=oldEnabled;GUI.depth=oldDepth;
    }
}
}
