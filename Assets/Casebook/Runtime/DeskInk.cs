using UnityEngine;
namespace Casebook {
internal static class DeskInk {
    static readonly System.Collections.Generic.Dictionary<Texture,Texture> guiTextures = new System.Collections.Generic.Dictionary<Texture,Texture>();
    public static Color DisplayColor(Color color)=>QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
    // IMGUI's default texture shader treats samples as GUI (linear) data. Normalize authored
    // sRGB artwork for both IMGUI sampling and the Linear backbuffer, without changing the game's URP color space or the source images.
    public static Texture DisplayTexture(Texture source){
        if(!source||QualitySettings.activeColorSpace!=ColorSpace.Linear||source==Texture2D.whiteTexture)return source;
        if(guiTextures.TryGetValue(source,out var cached))return cached;
        if(!(source is Texture2D readable)||!readable.isReadable)return source;
        var pixels=readable.GetPixels();
        for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].linear.linear;
        var copy=new Texture2D(source.width,source.height,TextureFormat.RGBAHalf,false,true){wrapMode=source.wrapMode,filterMode=source.filterMode};
        copy.SetPixels(pixels);copy.Apply(false,true);guiTextures[source]=copy;return copy;
    }
    public static void DrawTexture(Rect r,Texture texture,ScaleMode scale=ScaleMode.StretchToFill)=>GUI.DrawTexture(r,DisplayTexture(texture),scale);

    public static readonly Color Ink=C("302e2c"), Cream=C("ebe9de"), Muted=C("a9aaa0"), Wine=C("8f393a"), Panel=C("282925"), Light=C("d4d2c7"), Dark=C("222421");
    public static Texture2D Cork,Wall,Paper,Wood,Metal;
    static GUIStyle basis;static Font face;
    public static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
    public static void Init(){if(Cork)return;face=Font.CreateDynamicFontFromOSFont(new[]{"Avenir Next","Arial","Liberation Sans","DejaVu Sans"},22);
        basis=new GUIStyle{font=face,wordWrap=true,clipping=TextClipping.Clip};Cork=Grain(0);Wall=Grain(1);Paper=Grain(2);Wood=Grain(3);
        Metal=new Texture2D(32,32,TextureFormat.RGBA32,false);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float radius=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15f;float light=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(11,23))/22f);var col=Color.Lerp(C("282827"),C("d1d1c9"),light*light);col.a=Mathf.Clamp01((1-radius)*16);Metal.SetPixel(x,y,col);}Metal.Apply();}
    static Texture2D Grain(int kind){const int n=256;var tex=new Texture2D(n,n,TextureFormat.RGB24,false);var colors=new Color32[n*n];var random=new System.Random(107+kind);
        Color lo=C(new[]{"42362d","494a46","c9c9bf","332d2a"}[kind]),hi=C(new[]{"897058","737570","e4e4da","55453d"}[kind]);
        for(int y=0;y<n;y++)for(int x=0;x<n;x++){float noise=(float)random.NextDouble();float grain=Mathf.PerlinNoise(x*.16f,y*.16f);float broad=Mathf.PerlinNoise(x*.025f,y*.025f);
        if(kind==1){float fx=2*Mathf.PI*x/n,fy=2*Mathf.PI*y/n;broad=.5f+.10f*Mathf.Sin(fx+2*Mathf.Cos(fy))+.08f*Mathf.Cos(2*fy+Mathf.Sin(fx));}
        float value=kind==0?.52f*grain+.37f*noise+.11f*broad:kind==1?.82f*broad+.18f*noise:kind==2?.70f+.15f*noise+.15f*broad:.6f*Mathf.PerlinNoise(x*.016f,y*.38f)+.4f*noise;
        colors[y*n+x]=Color.Lerp(lo,hi,value);}tex.SetPixels32(colors);tex.wrapMode=TextureWrapMode.Repeat;tex.Apply();return tex;}
    public static void Box(Rect rect,Color color){var old=GUI.color;GUI.color=DisplayColor(color);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
    public static void Tile(Rect r,Texture tex,float size){GUI.DrawTextureWithTexCoords(r,DisplayTexture(tex),new Rect(0,0,r.width/size,r.height/size));}
    public static GUIStyle Style(int size,Color color,bool bold=false,TextAnchor align=TextAnchor.MiddleLeft){return new GUIStyle(basis){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,alignment=align,normal={textColor=DisplayColor(color)}};}
    public static void Label(Rect r,string value,int size,Color color,bool bold=false,TextAnchor align=TextAnchor.MiddleLeft){GUI.Label(r,value??"",Style(size,color,bold,align));}
    public static bool Button(Rect r,string value,bool light=false,bool active=false){var hovered=r.Contains(Event.current.mousePosition);Box(r,active?Wine:hovered?(light?C("c6c7bb"):C("484a44")):(light?C("d5d5ca"):C("323430")));Label(r,value,16,active||!light?Cream:Ink,active,TextAnchor.MiddleCenter);return GUI.Button(r,GUIContent.none,GUIStyle.none);}
    public static void Rule(Vector2 a,Vector2 b,Color color,float width){
        if(Event.current.type!=EventType.Repaint)return;
        if(Mathf.Abs(a.y-b.y)<2){Box(new Rect(Mathf.Min(a.x,b.x),a.y,Mathf.Abs(b.x-a.x),Mathf.Max(width,1)),color);return;}
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*1.5f));
        for(int i=0;i<=steps;i++){var p=Vector2.Lerp(a,b,i/(float)steps);Box(new Rect(p.x-width*.5f,p.y-width*.5f,Mathf.Max(width,1.1f),Mathf.Max(width,1.1f)),color);}
    }
    public static void Tack(Vector2 p,bool red=false){Box(new Rect(p.x-4,p.y+4,10,5),new Color(0,0,0,.28f));var old=GUI.color;GUI.color=red?DisplayColor(C("df7474")):Color.white;DrawTexture(new Rect(p.x-6,p.y-6,12,12),Metal);GUI.color=old;}
    public static void Sheet(Rect r){Box(new Rect(r.x+4,r.y+6,r.width,r.height),new Color(0,0,0,.28f));Tile(r,Paper,265);}
    public static void Mount(Rect rect,Texture image,string caption){Sheet(rect);DrawTexture(new Rect(rect.x+8,rect.y+8,rect.width-16,rect.height-45),image,ScaleMode.ScaleToFit);Label(new Rect(rect.x+8,rect.yMax-36,rect.width-16,32),caption,13,Ink,false,TextAnchor.MiddleCenter);Box(new Rect(rect.center.x-25,rect.y-3,50,10),new Color(.66f,.65f,.59f,.85f));}
}
}
