using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Authored, reusable meshes with bevel normals and metric UVs, stored as normal editable assets.
public static class StrelaMeshLibrary
{
    public const string Dir="Assets/_Project/ArtDirection/Strela";
    static Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
    static Mesh Save(string key,List<Vector3> v,List<Vector3> n,List<Vector2> uv,List<int> tri){
        string p=Dir+"/Meshes/"+key+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if(!m){m=new Mesh();AssetDatabase.CreateAsset(m,p);}else m.Clear();
        m.name=key;m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(tri,0);m.RecalculateBounds();m.RecalculateTangents();EditorUtility.SetDirty(m);cache[key]=m;return m;
    }
    public static Mesh Bevel(Vector3 size,float bevel){
        string key="Bevel_"+size.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+size.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+bevel.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        if(cache.TryGetValue(key,out var mesh)&&mesh)return mesh;
        var vv=new List<Vector3>();var nn=new List<Vector3>();var uv=new List<Vector2>();var tt=new List<int>();var half=size*.5f;float b=Mathf.Min(bevel,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.9f);var inner=half-Vector3.one*b;
        Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        foreach(var normal in normals){var u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,normal);var w=Vector3.Cross(normal,u);
            float hu=Vector3.Dot(half,new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z))), hw=Vector3.Dot(half,new Vector3(Mathf.Abs(w.x),Mathf.Abs(w.y),Mathf.Abs(w.z)));
            float hn=Vector3.Dot(half,new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)));
            float[] xs={-hu,-hu+b*.3f,-hu+b,hu-b,hu-b*.3f,hu};float[] ys={-hw,-hw+b*.3f,-hw+b,hw-b,hw-b*.3f,hw};int start=vv.Count;
            for(int j=0;j<6;j++)for(int i=0;i<6;i++){var p=normal*hn+u*xs[i]+w*ys[j];var q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));var no=(p-q).normalized;vv.Add(q+no*b);nn.Add(no);uv.Add(new Vector2(xs[i],ys[j]));}
            for(int j=0;j<5;j++)for(int i=0;i<5;i++){int a=start+j*6+i;tt.AddRange(new[]{a,a+1,a+7,a,a+7,a+6});}
        }return Save(key,vv,nn,uv,tt);
    }
    public static Mesh Lathe(string key,Vector2[] profile,int sides=32){
        if(cache.TryGetValue(key,out var mesh)&&mesh)return mesh;
        var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();float total=0;for(int i=1;i<profile.Length;i++)total+=Vector2.Distance(profile[i],profile[i-1]);float distance=0;
        for(int j=0;j<profile.Length;j++){
            if(j>0)distance+=Vector2.Distance(profile[j],profile[j-1]);var tangent=profile[Mathf.Min(j+1,profile.Length-1)]-profile[Mathf.Max(0,j-1)];
            for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*profile[j].x,profile[j].y,Mathf.Sin(a)*profile[j].x));n.Add(new Vector3(tangent.y*Mathf.Cos(a),-tangent.x,tangent.y*Mathf.Sin(a)).normalized);uv.Add(new Vector2((float)i/sides,distance/Mathf.Max(total,.001f)));}
        }
        for(int j=0;j<profile.Length-1;j++)for(int i=0;i<sides;i++){int a=j*(sides+1)+i;t.AddRange(new[]{a,a+sides+1,a+1,a+1,a+sides+1,a+sides+2});}
        return Save(key,v,n,uv,t);
    }
    public static Mesh Torus(float radius,float tube){
        string key="Ring_"+radius.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+tube.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);if(cache.TryGetValue(key,out var mesh)&&mesh)return mesh;
        var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();const int N=40,M=8;
        for(int i=0;i<=N;i++)for(int j=0;j<=M;j++){float a=i*Mathf.PI*2/N,b=j*Mathf.PI*2/M;var normal=new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(b),Mathf.Sin(a)*Mathf.Cos(b));v.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)+normal*tube);n.Add(normal);uv.Add(new Vector2((float)i/N,(float)j/M));}
        for(int i=0;i<N;i++)for(int j=0;j<M;j++){int a=i*(M+1)+j;t.AddRange(new[]{a,a+1,a+M+2,a,a+M+2,a+M+1});}return Save(key,v,n,uv,t);
    }
    public static Transform Item(Transform p,string name,Vector3 pos,Mesh mesh,Material mat){var go=new GameObject(name);go.transform.SetParent(p,false);go.transform.localPosition=pos;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;}
    public static Transform Box(Transform p,string n,Vector3 pos,Vector3 size,Material m,float bevel=.015f)=>Item(p,n,pos,Bevel(size,bevel),m);
    public static Transform Rod(Transform p,string n,Vector3 a,Vector3 b,float radius,Material m){float len=Vector3.Distance(a,b);var t=Item(p,n,(a+b)*.5f,Lathe("Rod_"+radius.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+len.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),new[]{new Vector2(0,-len/2),new Vector2(radius,-len/2),new Vector2(radius,len/2),new Vector2(0,len/2)},16),m);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return t;}
}
