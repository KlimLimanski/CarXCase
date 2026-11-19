using System.Linq;
using UnityEngine;
namespace Casebook {
internal class JournalWindow {
 readonly CasebookController desk;
 CaseSession Case=>desk.Session;
 int filter,attachment;Vector2 indexScroll,detailScroll;string hint="";
 string[] sections={"Все","Улики","Заметки","Люди","Места","Время","Выводы"};
 public JournalWindow(CasebookController value){desk=value;}
 public void ResetDetail(){attachment=0;detailScroll=Vector2.zero;hint="";}
 public void Draw(){
    DeskInk.Box(new Rect(-2000,-2000,5440,4900),new Color(.04f,.05f,.048f,.79f));
    bool modal=desk.tear!="";GUI.enabled=!modal;
    var old=GUI.matrix;float open=Mathf.SmoothStep(.96f,1,Mathf.Clamp01((Time.unscaledTime-desk.opened)*5));GUIUtility.ScaleAroundPivot(new Vector2(open,open),new Vector2(720,460));
    ArchiveArt.Back(true);
    ArchiveArt.Write(new Rect(177,135,460,31),"АРХИВ НАБЛЮДЕНИЙ    /    ЛИЧНЫЙ ЭКЗЕМПЛЯР",13,false,true,DeskInk.Wine);
    ArchiveArt.Write(new Rect(174,169,478,68),Case.File.heading,46,true,true);
    ArchiveArt.Write(new Rect(177,239,480,32),"Указатель материалов",22,true);
    ArchiveArt.Under(179,277,445);
    for(int i=0;i<sections.Length;i++){
        var tag=new Rect(177+i%4*113,289+i/4*32,109,29);
        if(ArchiveArt.Inscription(tag,sections[i],17,filter==i)){filter=i;indexScroll=Vector2.zero;ResetDetail();
           if(filter<6){var first=Case.State.found.FirstOrDefault(rec=>filter==0||(int)Case.File.Find(rec.id).section==filter-1);desk.Select(first?.id??"");}else hint=Case.State.insights.LastOrDefault()??"";}
    }
    bool conclusions=filter==6;
    var ids=conclusions?Case.State.insights.ToArray():Case.State.found.Where(rec=>filter==0||(int)Case.File.Find(rec.id).section==filter-1).Select(rec=>rec.id).ToArray();
    if(!conclusions&&filter!=0&&desk.selection!=""&&!ids.Contains(desk.selection)){filter=0;ids=Case.State.found.Select(rec=>rec.id).ToArray();}
    if(conclusions&&hint==""&&ids.Length>0)hint=ids[ids.Length-1];
    indexScroll=GUI.BeginScrollView(new Rect(174,366,460,145),indexScroll,new Rect(0,0,434,Mathf.Max(143,ids.Length*49)));
    if(ids.Length==0)ArchiveArt.Write(new Rect(4,7,398,88),conclusions?"Вернуться сюда, когда появится первая связь.":"Оставить место для первой находки.",22,true);
    for(int i=0;i<ids.Length;i++){var id=ids[i];var title=conclusions?Case.File.deductions.Find(rule=>rule.id==id).title:Case.File.Find(id).title;var row=new Rect(1,i*49,424,46);bool active=conclusions?hint==id:desk.selection==id;
        ArchiveArt.Write(new Rect(4,row.y,41,44),(i+1).ToString("00"),16,false,false,active?DeskInk.Wine:DeskInk.Ink);
        ArchiveArt.Under(48,row.yMax-2,359,.23f);
        if(ArchiveArt.Inscription(new Rect(48,row.y,365,46),title,20,active)){if(conclusions){hint=id;detailScroll=Vector2.zero;}else{desk.Select(id);Case.Read(id);}}}
    GUI.EndScrollView();
    if(ArchiveArt.RoomPhoto(new Rect(405,588,209,143)))desk.OpenBoard();
    var sheet=new Rect(754,150,503,537);
    if(conclusions){var rule=Case.File.deductions.Find(item=>item.id==hint);if(rule!=null)WriteHint(sheet,rule);else Empty(sheet,"Выводы по делу","Соедините кнопки связанных улик на доске.");}
    else if(Case.Get(desk.selection)!=null){Case.Read(desk.selection);WriteEntry(sheet,Case.File.Find(desk.selection),true);}
    else Empty(sheet,"Первая запись","Осмотреть кабинет. Записать, что лежит не на своём месте. Сохранить всё, к чему захочется вернуться.");
    if(!conclusions&&Case.Get(desk.selection)!=null){var rec=Case.Get(desk.selection);
       if(ArchiveArt.Inscription(new Rect(794,672,385,39),rec.studied?"✓  Изучено":"□  Отметить изученной",18)){rec.studied=!rec.studied;Case.Save();}
       if(ArchiveArt.Tab(new Rect(930,715,289,44),Case.Pinned(rec.id)!=null?"Найти лист на доске →":rec.detached?"Взять отделённый лист →":"Отделить лист для доски →",true)){if(rec.detached){desk.OpenBoard();if(Case.Pinned(rec.id)==null)desk.carry=rec.id;else desk.Board.Focus(rec.id);}else desk.tear=rec.id;}}
    ArchiveArt.Write(new Rect(53,848,460,30),"Esc   /   закрыть дневник",15,false,false,DeskInk.Cream);
    GUI.matrix=old;GUI.enabled=true;
    if(modal){DeskInk.Box(new Rect(0,0,1440,900),new Color(0,0,0,.52f));
        if(desk.tearStart<0){var r=new Rect(450,323,540,210);DeskInk.Sheet(r);DeskInk.Label(new Rect(481,343,480,84),"Отделить лист? Запись останется в дневнике.",23,DeskInk.Ink,true);
            if(DeskInk.Button(new Rect(482,455,209,48),"Оставить",true))desk.tear="";if(DeskInk.Button(new Rect(709,455,248,48),"Отделить лист",false,true))desk.tearStart=Time.unscaledTime;}
        else{float t=Mathf.Clamp01((Time.unscaledTime-desk.tearStart)/.55f);var m=GUI.matrix;var r=new Rect(609+155*t,185-88*t,520,430);GUIUtility.RotateAroundPivot(-9*t,r.center);DrawSlip(r,Case.File.Find(desk.tear),true);GUI.matrix=m;if(t>=1)desk.TearDone();}}
 }
 void Empty(Rect r,string title,string text){ArchiveArt.Write(new Rect(r.x+28,r.y+27,r.width-54,50),"ИЗ ЛИЧНЫХ ЗАПИСЕЙ",14,false,true,DeskInk.Wine);ArchiveArt.Write(new Rect(r.x+27,r.y+94,r.width-52,90),title,34,true,true);ArchiveArt.Under(r.x+30,r.y+183,r.width-66);ArchiveArt.Write(new Rect(r.x+30,r.y+199,r.width-64,154),text,22,true);}
 public void WriteHint(Rect r,Deduction rule){Empty(r,rule.title,rule.hint);DeskInk.Label(new Rect(r.x+42,r.yMax-90,r.width-84,66),Case.File.Find(rule.first).title+"    ↔    "+Case.File.Find(rule.second).title,16,DeskInk.Wine,true);}
 public void WriteEntry(Rect r,Entry entry,bool edit){var rec=Case.Get(entry.id);float width=r.width-68;
    var body=DeskInk.Style(21,DeskInk.Ink);float obs=Mathf.Max(48,body.CalcHeight(new GUIContent(entry.observation),width)+8),voice=Mathf.Max(48,body.CalcHeight(new GUIContent(entry.narrator),width-40)+8);
    var media=entry.attachments?.Where(item=>item!=null&&item.image!=null).ToList();bool has=media!=null&&media.Count>0;
    float header=90+Mathf.Max(57,ArchiveArt.Type(30,DeskInk.Ink,true,true).CalcHeight(new GUIContent(entry.title),width));
    float height=header+obs+voice+(has?230:0)+158;
    detailScroll=GUI.BeginScrollView(new Rect(r.x+18,r.y+16,r.width-26,r.height-36),detailScroll,new Rect(0,0,width+18,height));
    ArchiveArt.Write(new Rect(10,5,width,32),"ЛИСТ    №  "+(Case.State.found.IndexOf(rec)+1).ToString("00"),14,false,true,DeskInk.Wine);
    ArchiveArt.Write(new Rect(10,48,width,header-90),entry.title,30,true,true);DeskInk.Label(new Rect(10,header-34,width,29),string.Join("   ·   ",new[]{entry.location,entry.time}.Where(value=>!string.IsNullOrEmpty(value))),16,DeskInk.Ink);
    float y=header+8;DeskInk.Label(new Rect(10,y,width,obs),entry.observation,21,DeskInk.Ink);y+=obs+10;
    if(has){attachment=Mathf.Clamp(attachment,0,media.Count-1);var photo=media[attachment];var pm=GUI.matrix;GUIUtility.RotateAroundPivot(-2,new Vector2(145,y+98));DeskInk.Mount(new Rect(16,y+7,268,198),photo.image,photo.caption);GUI.matrix=pm;
        DeskInk.Label(new Rect(298,y+18,width-303,52),"ВЛОЖЕНИЕ  "+(attachment+1)+" / "+media.Count,15,DeskInk.Ink,true);if(media.Count>1&&DeskInk.Button(new Rect(296,y+82,127,40),"Следующее  →",true))attachment=(attachment+1)%media.Count;y+=230;}
    ArchiveArt.Leaf(new Rect(5,y,width+4,voice+43));ArchiveArt.Tape(new Rect(width-56,y+1,42,10));DeskInk.Label(new Rect(23,y+3,width-44,30),Case.File.narratorName.ToUpperInvariant()+"   /   НА ПОЛЯХ",14,DeskInk.Wine,true);y+=30;ArchiveArt.Write(new Rect(23,y,width-40,voice),entry.narrator,20,true);y+=voice+25;
    ArchiveArt.Write(new Rect(10,y,width,26),"На полях:",18,true);y+=30;
    if(edit){ArchiveArt.Under(12,y+28,width-10);ArchiveArt.Under(12,y+53,width-10);GUI.SetNextControlName("CasebookNote");var result=GUI.TextArea(new Rect(18,y+4,width-16,50),rec.note??"",800,DeskInk.Style(17,DeskInk.Ink));if(result!=rec.note){rec.note=result;Case.Save();}}else DeskInk.Label(new Rect(10,y,width,58),rec.note,17,DeskInk.Ink);
    GUI.EndScrollView();}
 public static void DrawSlip(Rect r,Entry entry,bool full=false){
    ArchiveArt.Leaf(r);var photo=entry.attachments?.FirstOrDefault(item=>item!=null&&item.image);
    float pad=r.width*.065f;
    if(photo!=null){DeskInk.DrawTexture(new Rect(r.x+pad,r.y+pad,r.width-2*pad,r.height*.58f),photo.image,ScaleMode.ScaleToFit);DeskInk.Label(new Rect(r.x+pad,r.y+r.height*.62f,r.width-2*pad,r.height*.13f),photo.caption,Mathf.RoundToInt(r.width*.055f),DeskInk.Ink);}
    else {ArchiveArt.Write(new Rect(r.x+pad,r.y+r.height*.08f,r.width-2*pad,r.height*.15f),"ПРОТОКОЛ  /  ОПИСЬ",Mathf.RoundToInt(r.width*.054f),false,true,DeskInk.Wine);ArchiveArt.Under(r.x+pad,r.y+r.height*.24f,r.width-2*pad,.36f);
       ArchiveArt.Write(new Rect(r.x+pad,r.y+r.height*.27f,r.width-2*pad,r.height*.41f),entry.observation,Mathf.RoundToInt(r.width*.071f),true);}
    ArchiveArt.Write(new Rect(r.x+pad,r.y+r.height*.71f,r.width-2*pad,r.height*.26f),entry.title,Mathf.RoundToInt(r.width*.079f),true,true);
 }
}
}
