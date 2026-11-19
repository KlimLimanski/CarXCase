using UnityEngine;

namespace Casebook {
internal static class ArchiveArt {
    static Texture2D book, room, rag, shade;
    static Font serif, mono;
    internal static Texture2D Room { get { Init(); return room; } }
    static Color ink=DeskInk.C("342d28"), pencil=DeskInk.C("504943"), red=DeskInk.C("783e39");
    public static void Init(){
        if(rag)return;
        book=Resources.Load<Texture2D>("Archive/Fieldbook");room=Resources.Load<Texture2D>("Archive/Study");
        serif=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman","Liberation Serif","Arial"},28);
        mono=Font.CreateDynamicFontFromOSFont(new[]{"Courier New","Courier","Liberation Mono","Arial"},17);
        int w=256,h=224;rag=new Texture2D(w,h,TextureFormat.RGBA32,false);shade=new Texture2D(w,h,TextureFormat.RGBA32,false);var colors=new Color32[w*h];var shadows=new Color32[w*h];var rng=new System.Random(415);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            float fiber=Mathf.PerlinNoise(x*.047f,y*.054f),grain=(float)rng.NextDouble();
            float edge=Mathf.Min(Mathf.Min(x-(1+Mathf.PerlinNoise(10,y*.14f)*3),w-x-1-(1+Mathf.PerlinNoise(4,y*.12f)*4)),Mathf.Min(y-(1+Mathf.PerlinNoise(x*.16f,21)*4),h-y-1-(1+Mathf.PerlinNoise(x*.15f,50)*3)));
            float dog=(x-w+9)+(y-h+9);if(dog>7)edge=Mathf.Min(edge,7-dog);
            float alpha=Mathf.Clamp01(edge+.5f);float v=.28f*fiber+.2f*grain+.18f*Mathf.Clamp01(edge/10f);
            var c=Color.Lerp(DeskInk.C("adab98"),DeskInk.C("e6e3d7"),v+.22f);c.a=alpha;colors[y*w+x]=c;shadows[y*w+x]=new Color(0,0,0,.40f*alpha);}
        rag.SetPixels32(colors);rag.Apply();shade.SetPixels32(shadows);shade.Apply();
    }
    public static void Back(bool journal){Init();var source=journal?book:room;if(source)DeskInk.DrawTexture(new Rect(0,0,1440,900),source,ScaleMode.StretchToFill);}
    public static void Leaf(Rect r){Init();DeskInk.DrawTexture(new Rect(r.x+3,r.y+5,r.width,r.height),shade);DeskInk.DrawTexture(r,rag);}
    public static GUIStyle Type(int size,Color color,bool script=false,bool bold=false,TextAnchor align=TextAnchor.MiddleLeft){Init();var style=DeskInk.Style(size,color,bold,align);style.font=script?serif:mono;style.fontStyle=script?(bold?FontStyle.BoldAndItalic:FontStyle.Italic):(bold?FontStyle.Bold:FontStyle.Normal);return style;}
    public static void Write(Rect r,string text,int size,bool script=false,bool bold=false,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft){GUI.Label(r,text??"",Type(size,color??ink,script,bold,align));}
    public static void Under(float x,float y,float width,float alpha=.35f){DeskInk.Rule(new Vector2(x,y),new Vector2(x+width,y-.7f),new Color(.22f,.18f,.16f,alpha),1);}
    public static bool Inscription(Rect r,string text,int size=18,bool active=false){var hover=r.Contains(Event.current.mousePosition);Write(r,text,size,true,active,active||hover?red:ink);if(hover||active)Under(r.x,r.yMax-5,r.width*.8f,.5f);return GUI.Button(r,GUIContent.none,GUIStyle.none);}
    public static bool Tab(Rect r,string text,bool active=false){Leaf(r);if(active)DeskInk.Box(new Rect(r.x+5,r.y+6,2,r.height-12),red);Write(r,text,15,false,active,active?red:ink,TextAnchor.MiddleCenter);return GUI.Button(r,GUIContent.none,GUIStyle.none);}
    public static void Tape(Rect r){var m=GUI.matrix;GUIUtility.RotateAroundPivot(-6,r.center);DeskInk.Box(r,new Color(.65f,.60f,.47f,.64f));DeskInk.Rule(r.position,new Vector2(r.xMax,r.y),new Color(1,1,1,.20f),1);GUI.matrix=m;}
    public static bool RoomPhoto(Rect r){var before=GUI.matrix;GUIUtility.RotateAroundPivot(-6,r.center);Leaf(r);if(Room)DeskInk.DrawTexture(new Rect(r.x+10,r.y+10,r.width-20,r.height-47),Room,ScaleMode.ScaleAndCrop);Write(new Rect(r.x+10,r.yMax-40,r.width-20,30),"Кабинет  /  к доске →",16,true,false,pencil,TextAnchor.MiddleCenter);Tape(new Rect(r.center.x-30,r.y-1,60,13));bool open=GUI.Button(r,GUIContent.none,GUIStyle.none);GUI.matrix=before;return open;}
    public static void Thread(Vector2 a,Vector2 b,float scale,string id)=>ThreadStroke.Draw(a,b,scale,id);
}
}
