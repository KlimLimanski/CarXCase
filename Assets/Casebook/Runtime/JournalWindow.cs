using System.Linq;
using UnityEngine;
namespace Casebook {
internal class JournalWindow {
 readonly CasebookController desk;
 CaseSession Case=>desk.Session;
 int attachment;
 Vector2 indexScroll,detailScroll;
 Section category=Section.Evidence;
 static readonly Section[] Categories={Section.Evidence,Section.Places,Section.People};
 static readonly string[] Titles={"Улики","Места","Люди"};
 public JournalWindow(CasebookController controller){desk=controller;}
 public void ResetDetail(){attachment=0;detailScroll=Vector2.zero;}
 public static bool Visible(Entry entry)=>entry!=null && System.Array.IndexOf(Categories,entry.section)>=0;
 public void Draw(){
    DeskInk.Box(new Rect(-4000,-4000,9440,8900),new Color(.042f,.042f,.04f));
    // A close crop inside the outer binding leaves only the open book in the frame.
    var cover=new Rect(22,50,1396,800);
    DeskInk.Box(cover,DeskInk.C("382b24"));
    var texture=Resources.Load<Texture2D>("Archive/Fieldbook");
    if(texture)GUI.DrawTextureWithTexCoords(new Rect(34,62,1372,776),DeskInk.DisplayTexture(texture),new Rect(48f/720,81f/450,612f/720,319f/450));
    ArchiveArt.Write(new Rect(108,98,510,72),Case.File.heading,48,true,true);
    for(int i=0;i<Categories.Length;i++){
        if(ArchiveArt.Inscription(new Rect(108+i*164,190,146,45),Titles[i],24,category==Categories[i])){
            category=Categories[i];indexScroll=Vector2.zero;ResetDetail();
        }
    }
    var entries=Case.State.found.Select(record=>Case.File.Find(record.id)).Where(entry=>Visible(entry)&&entry.section==category).ToArray();
    if(!entries.Any(entry=>entry.id==desk.selection))desk.Select(entries.FirstOrDefault()?.id??"");
    indexScroll=GUI.BeginScrollView(new Rect(109,268,505,335),indexScroll,new Rect(0,0,478,Mathf.Max(333,entries.Length*58)),false,false,GUIStyle.none,GUIStyle.none);
    for(int i=0;i<entries.Length;i++){
        var item=entries[i];
        if(ArchiveArt.Inscription(new Rect(5,i*58,466,54),item.title,22,item.id==desk.selection))desk.Select(item.id);
    }
    GUI.EndScrollView();
    var selected=entries.FirstOrDefault(entry=>entry.id==desk.selection);
    if(selected!=null){Case.Read(selected.id);WriteEntry(new Rect(750,120,576,660),selected,false);}
    else ArchiveArt.Write(new Rect(810,265,410,98),"Записей пока нет",27,true);
    ArchiveArt.Write(new Rect(46,848,420,31),"Esc  —  закрыть",17,false,false,DeskInk.Cream);
 }
 void Empty(Rect r,string title,string text){ArchiveArt.Write(new Rect(r.x+28,r.y+27,r.width-54,50),"ИЗ ЛИЧНЫХ ЗАПИСЕЙ",14,false,true,DeskInk.Wine);ArchiveArt.Write(new Rect(r.x+27,r.y+94,r.width-52,90),title,34,true,true);ArchiveArt.Under(r.x+30,r.y+183,r.width-66);ArchiveArt.Write(new Rect(r.x+30,r.y+199,r.width-64,154),text,22,true);}
 public void WriteHint(Rect r,Deduction rule){Empty(r,rule.title,rule.hint);DeskInk.Label(new Rect(r.x+42,r.yMax-90,r.width-84,66),Case.File.Find(rule.first).title+"    ↔    "+Case.File.Find(rule.second).title,16,DeskInk.Wine,true);}
 public void WriteEntry(Rect r,Entry entry,bool edit){var rec=Case.Get(entry.id);float width=r.width-68;
    var body=DeskInk.Style(21,DeskInk.Ink);float obs=Mathf.Max(48,body.CalcHeight(new GUIContent(entry.observation),width)+8),voice=Mathf.Max(48,body.CalcHeight(new GUIContent(entry.narrator),width-40)+8);
    var media=entry.attachments?.Where(item=>item!=null&&item.image!=null).ToList();bool has=media!=null&&media.Count>0;
    float header=90+Mathf.Max(57,ArchiveArt.Type(30,DeskInk.Ink,true,true).CalcHeight(new GUIContent(entry.title),width));
    float height=header+obs+voice+(has?230:0)+158;
    detailScroll=GUI.BeginScrollView(new Rect(r.x+18,r.y+16,r.width-26,r.height-36),detailScroll,new Rect(0,0,width+18,height),false,false,GUIStyle.none,GUIStyle.none);
    ArchiveArt.Write(new Rect(10,48,width,header-90),entry.title,30,true,true);DeskInk.Label(new Rect(10,header-34,width,29),string.Join("   ·   ",new[]{entry.location,entry.time}.Where(value=>!string.IsNullOrEmpty(value))),16,DeskInk.Ink);
    float y=header+8;DeskInk.Label(new Rect(10,y,width,obs),entry.observation,21,DeskInk.Ink);y+=obs+10;
    if(has){attachment=Mathf.Clamp(attachment,0,media.Count-1);var photo=media[attachment];var pm=GUI.matrix;GUIUtility.RotateAroundPivot(-2,new Vector2(145,y+98));DeskInk.Mount(new Rect(16,y+7,268,198),photo.image,photo.caption);GUI.matrix=pm;
        DeskInk.Label(new Rect(298,y+18,width-303,52),"ВЛОЖЕНИЕ  "+(attachment+1)+" / "+media.Count,15,DeskInk.Ink,true);if(media.Count>1&&DeskInk.Button(new Rect(296,y+82,127,40),"Следующее  →",true))attachment=(attachment+1)%media.Count;y+=230;}
    if(!string.IsNullOrWhiteSpace(entry.narrator)){ArchiveArt.Leaf(new Rect(5,y,width+4,voice+43));ArchiveArt.Tape(new Rect(width-56,y+1,42,10));DeskInk.Label(new Rect(23,y+3,width-44,30),Case.File.narratorName.ToUpperInvariant()+"   /   НА ПОЛЯХ",14,DeskInk.Wine,true);y+=30;ArchiveArt.Write(new Rect(23,y,width-40,voice),entry.narrator,20,true);y+=voice+25;}
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
