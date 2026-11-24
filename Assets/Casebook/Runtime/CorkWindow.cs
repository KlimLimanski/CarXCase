using System.Linq;
using UnityEngine;
namespace Casebook {
internal class CorkWindow {
 readonly CasebookController desk;CaseSession Case=>desk.Session;CaseState State=>Case.State;
 readonly Rect cork=new Rect(143,170,1167,471);const float width=202,height=154;
 string selected="",drag="";Vector2 offset;bool pan;int cluePage,pendingIndex;
 public CorkWindow(CasebookController value){desk=value;}
 /// Part of the 1440x900 board layout that is laid onto the physical board in the scene: cork, conclusions, tabs and help line.
 public static readonly Rect WorldArea=new Rect(119,150,1212,720);
 public bool Modal=>desk.inspectEntry!=""||desk.inspectHint!="";
 public void Release(){drag="";pan=false;}
 public void Focus(string id){var pin=Case.Pinned(id);if(pin==null)return;selected=id;State.pan=cork.size*.5f-(pin.position+new Vector2(width/2,height/2))*State.zoom;Case.Save();}
 Vector2 Project(Vector2 board)=>board*State.zoom+State.pan;
 Vector2 Local(Vector2 screen)=>(screen-cork.position-State.pan)/State.zoom;
 Rect Position(Vector2 pos,float w,float h)=>new Rect(Project(pos),new Vector2(w,h)*State.zoom);
 Vector2 PinPoint(Pin pin)=>pin.position+new Vector2(width*.5f,7);
 Vector2 Fit(Vector2 v,float w,float h)=>new Vector2(Mathf.Clamp(v.x,0,cork.width-w),Mathf.Clamp(v.y,0,cork.height-h));
 static float Distance(Vector2 spot,Vector2 a,Vector2 b){var d=b-a;return Vector2.Distance(spot,a+d*Mathf.Clamp01(Vector2.Dot(spot-a,d)/Mathf.Max(.001f,d.sqrMagnitude)));}
 void Link(string id){
    if(desk.linking==""||desk.linking=="?"){desk.linking=id;desk.Tell("Теперь выберите кнопку второй улики");return;}
    var outcome=Case.Tie(desk.linking,id);desk.linking="";if(outcome==Match.Unsupported)desk.Tell("Пока нет фактов, подтверждающих эту связь.");if(outcome==Match.Existing)desk.Tell("Эта связь уже отмечена.");if(outcome==Match.Linked)desk.Tell("Красная нить восстановлена.");}
 void Handle(){
    var ev=Event.current;var mouse=ev.mousePosition;var point=Local(mouse);bool over=cork.Contains(mouse);
    if(ev.type==EventType.MouseUp){if(pan||drag!=""){Release();Case.Save();ev.Use();}return;}
    if(ev.type==EventType.MouseDrag){if(pan){State.pan+=ev.delta;ev.Use();}else if(drag!=""){if(drag=="#a")State.noteA=Fit(point-offset,138,79);else if(drag=="#b")State.noteB=Fit(point-offset,134,91);else {var pin=Case.Pinned(drag);if(pin!=null)pin.position=Fit(point-offset,width,height);}ev.Use();}return;}
    if(!over)return;
    if(ev.type==EventType.ScrollWheel){State.zoom=Mathf.Clamp(State.zoom*Mathf.Pow(.93f,ev.delta.y),.75f,2.2f);State.pan=mouse-cork.position-point*State.zoom;Case.Save();ev.Use();return;}
    if(ev.type!=EventType.MouseDown)return;
    if(ev.button==1){foreach(var id in State.threads.ToArray()){var rule=Case.File.deductions.Find(x=>x.id==id);if(Distance(point,PinPoint(Case.Pinned(rule.first)),PinPoint(Case.Pinned(rule.second)))<10/State.zoom){Case.Untie(id);desk.Tell("Нить снята. Вывод остался в дневнике.");break;}}desk.linking="";ev.Use();return;}
    if(ev.button==2){pan=true;ev.Use();return;}if(ev.button!=0)return;
    if(desk.carry!=""){if(Case.Place(desk.carry,Fit(point-new Vector2(width*.5f,7),width,height))){selected=desk.carry;desk.carry="";}ev.Use();return;}
    var top=State.pins.LastOrDefault(pin=>new Rect(pin.position,new Vector2(width,height)).Contains(point));
    if(top!=null){selected=top.id;if(desk.linking!=""||Vector2.Distance(point,PinPoint(top))<14/State.zoom)Link(top.id);
        else if(ev.clickCount>=2){desk.Journal.ResetDetail();desk.inspectEntry=top.id;Case.Read(top.id);}
        else{drag=top.id;offset=point-top.position;State.pins.Remove(top);State.pins.Add(top);}}
    else if(new Rect(State.noteA,new Vector2(138,79)).Contains(point)){drag="#a";offset=point-State.noteA;}
    else if(new Rect(State.noteB,new Vector2(134,91)).Contains(point)){drag="#b";offset=point-State.noteB;}
    else {selected="";pan=true;}ev.Use();
 }
 void Memo(Vector2 position,float w,float h,string text){var r=Position(position,w,h);var old=GUI.matrix;GUIUtility.RotateAroundPivot(text.StartsWith("К")?-3f:4f,r.position+new Vector2(w/2,6)*State.zoom);ArchiveArt.Leaf(r);ArchiveArt.Write(new Rect(r.x+12*State.zoom,r.y+8*State.zoom,r.width-24*State.zoom,r.height-14*State.zoom),text,Mathf.RoundToInt(16*State.zoom),true);DeskInk.Tack(r.position+new Vector2(w/2,6)*State.zoom);GUI.matrix=old;}
 Texture2D lighting;
 Texture2D NeutralLight(){if(lighting)return lighting;const int w=128,h=80;lighting=new Texture2D(w,h,TextureFormat.RGBA32,false);for(int y=0;y<h;y++)for(int x=0;x<w;x++){float px=(x/(float)w-.51f)*1.5f,py=(1-y/(float)h)*.9f;float level=Mathf.Clamp01(1-Mathf.Sqrt(px*px+py*py));lighting.SetPixel(x,y,new Color(.96f,.97f,.97f,.22f*level*level));}lighting.Apply();return lighting;}
 /// world = drawn onto the 3D board's surface (no room backdrop; reading a sheet opens on screen via DrawModal).
 public void Draw(bool world=false){
    if(world){DeskInk.Box(WorldArea,DeskInk.C("6b5640"));DeskInk.Tile(WorldArea,DeskInk.Cork,240);DeskInk.Box(WorldArea,new Color(0,0,0,.08f));}
    else{DeskInk.Box(new Rect(-2000,-2000,5440,4900),DeskInk.Dark);ArchiveArt.Back(false);}
        bool modal=Modal;if(!modal)Handle();
    GUI.BeginGroup(cork);
    Memo(State.noteA,138,79,"К проверке\n________");Memo(Fit(State.noteB,134,91),134,91,"Сопоставить\nвремя и место");
    foreach(var pin in State.pins){var rect=Position(pin.position,width,height);var old=GUI.matrix;float angle=pin.id.Length%2==0?-2.4f:2.7f;GUIUtility.RotateAroundPivot(angle,Project(PinPoint(pin)));JournalWindow.DrawSlip(rect,Case.File.Find(pin.id));GUI.matrix=old;if(pin.id==selected)DeskInk.Box(new Rect(rect.x,rect.yMax-3,rect.width,3),DeskInk.Wine);}
    foreach(var id in State.threads){var rule=Case.File.deductions.Find(x=>x.id==id);var a=Project(PinPoint(Case.Pinned(rule.first)));var b=Project(PinPoint(Case.Pinned(rule.second)));ArchiveArt.Thread(a,b,State.zoom,id);}
    if(Case.Pinned(desk.linking)!=null)DeskInk.Rule(Project(PinPoint(Case.Pinned(desk.linking))),Event.current.mousePosition,new Color(.62f,.28f,.26f,.75f),2);
    foreach(var pin in State.pins)DeskInk.Tack(Project(PinPoint(pin)),true);GUI.EndGroup();
    GUI.enabled=!modal;
    if(State.insights.Count==0){var empty=new Rect(157,656,294,38);ArchiveArt.Leaf(empty);ArchiveArt.Write(new Rect(175,657,267,31),"Выводы по делу",17,true);DeskInk.Tack(new Vector2(169,661));}
    else {cluePage=Mathf.Clamp(cluePage,0,(State.insights.Count-1)/4);for(int i=cluePage*4;i<Mathf.Min(State.insights.Count,4+cluePage*4);i++){var rule=Case.File.deductions.Find(x=>x.id==State.insights[i]);var r=new Rect(157+(i%4)*249,656,230,41);if(ArchiveArt.Tab(r,rule.title))desk.inspectHint=rule.id;DeskInk.Tack(new Vector2(r.center.x,r.y+2),true);}if(State.insights.Count>4&&DeskInk.Button(new Rect(1155,656,106,38),"Ещё  →"))cluePage=(cluePage+1)%((State.insights.Count+3)/4);}
    
    if(ArchiveArt.Tab(new Rect(143,720,149,42),"← Дневник"))desk.OpenJournal();
    if(ArchiveArt.Tab(new Rect(294,716,175,47),"Красная нить",desk.linking!=""))desk.linking=desk.linking==""?"?":"";
    if(Case.Pinned(selected)!=null&&ArchiveArt.Tab(new Rect(471,720,144,42),"Прочитать")){desk.inspectEntry=selected;Case.Read(selected);desk.Journal.ResetDetail();}
    if(Case.Pinned(selected)!=null&&ArchiveArt.Tab(new Rect(617,719,130,42),"Снять лист")){Case.Return(selected);selected="";desk.linking="";}
    if(ArchiveArt.Tab(new Rect(749,719,140,42),"Вся доска")){State.zoom=1;State.pan=Vector2.zero;Case.Save();}
    var loose=State.found.Where(rec=>rec.detached&&Case.Pinned(rec.id)==null).ToList();
    if(loose.Count>0){pendingIndex%=loose.Count;if(ArchiveArt.Tab(new Rect(914,717,320,44),"Взять: "+Case.File.Find(loose[pendingIndex].id).title))desk.carry=loose[pendingIndex].id;if(loose.Count>1&&DeskInk.Button(new Rect(1236,717,74,44),"Ещё"))pendingIndex=(pendingIndex+1)%loose.Count;}
    DeskInk.Label(new Rect(119,843,1212,26),desk.carry!=""?"Щёлкните по пробке, чтобы приколоть лист.   Esc — отложить":desk.linking!=""?"Выберите две улики. Подтверждённая связь откроет подсказку.":"Тяните за лист, чтобы переместить   ·   За фон — сдвиг   ·   Колесо — масштаб   ·   ПКМ по нити — снять   ·   Esc — закрыть",15,DeskInk.Muted);
    if(desk.BoardActionAvailable?.Invoke()==true&&ArchiveArt.Tab(new Rect(1010,781,300,42),"Что дальше? →",true))desk.BoardAction?.Invoke();
    GUI.enabled=true;if(!modal&&desk.carry!=""){var mouse=Event.current.mousePosition;JournalWindow.DrawSlip(new Rect(mouse.x-85,mouse.y+12,170,129),Case.File.Find(desk.carry));}
    if(modal&&!world)DrawModal();
 }
 /// The open sheet or conclusion, read on screen over the board.
 public void DrawModal(){
    {DeskInk.Box(new Rect(0,0,1440,900),new Color(0,0,0,.74f));var r=new Rect(337,94,766,632);DeskInk.Sheet(r);
        if(desk.inspectEntry!="")desk.Journal.WriteEntry(r,Case.File.Find(desk.inspectEntry),true);else desk.Journal.WriteHint(r,Case.File.deductions.Find(rule=>rule.id==desk.inspectHint));
        if(DeskInk.Button(new Rect(535,747,370,46),"Вернуться к доске"))desk.Back();}
 }
}
}
