using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Casebook {
[Serializable] public class Record {public string id,note="";public bool read,studied,detached;}
[Serializable] public class Pin {public string id;public Vector2 position;}
[Serializable] public class CaseState {
    public int version=1; public string caseId;
    public List<Record> found=new List<Record>(); public List<Pin> pins=new List<Pin>();
    public List<string> threads=new List<string>(), insights=new List<string>();
    public float zoom=1; public Vector2 pan; public Vector2 noteA=new Vector2(105,70),noteB=new Vector2(1050,82);
    public int journalKey=(int)KeyCode.J,boardKey=(int)KeyCode.Tab;
}
public enum Match { Missing, Same, Unsupported, Existing, Linked, Discovered }
public sealed class CaseSession {
    public readonly InvestigationCase File;
    public CaseState State {get;private set;}
    public string Error {get;private set;}
    public readonly string PathName;
    readonly Action<CaseState> persist;
    public event Action<Entry> Found;
    public event Action<Deduction> HintUnlocked;
    public CaseSession(InvestigationCase file,string folder=null,CaseState initialState=null,Action<CaseState> save=null){
        persist=save;
        File=file;var invalid=file.ValidateCase();if(invalid!=null)throw new ArgumentException(invalid);
        string safe=string.Concat(System.Text.Encoding.UTF8.GetBytes(file.caseId).Select(val=>val.ToString("x2")));
        PathName=Path.Combine(folder??Application.persistentDataPath,"casebook-"+safe+".json");
        State=new CaseState{caseId=file.caseId,journalKey=(int)file.journalKey,boardKey=(int)file.boardKey};
        if(initialState!=null)State=initialState;
        foreach(var path in save==null?new[]{PathName,PathName+".bak"}:Array.Empty<string>()){
            if(!System.IO.File.Exists(path))continue;
            try {var saved=JsonUtility.FromJson<CaseState>(System.IO.File.ReadAllText(path));if(saved==null||saved.version!=1||saved.caseId!=file.caseId||saved.found==null||saved.pins==null||saved.threads==null||saved.insights==null)continue;State=saved;break;}catch(Exception ex){Error=ex.Message;}
        }
        State.found.RemoveAll(rec=>rec==null||file.Find(rec.id)==null);
        State.pins.RemoveAll(pin=>pin==null||Get(pin.id)==null);
        State.threads.RemoveAll(id=>!file.deductions.Exists(rule=>rule.id==id&&Pinned(rule.first)!=null&&Pinned(rule.second)!=null));
        State.insights.RemoveAll(id=>!file.deductions.Exists(rule=>rule.id==id));
        State.zoom=Mathf.Clamp(State.zoom,.75f,2.2f);
    }
    public Record Get(string id)=>State.found.Find(rec=>rec.id==id);
    public Pin Pinned(string id)=>State.pins.Find(pin=>pin.id==id);
    public bool Collect(string id){var entry=File.Find(id);if(entry==null||Get(id)!=null)return false;State.found.Add(new Record{id=id});Save();Found?.Invoke(entry);return true;}
    public void Read(string id){var rec=Get(id);if(rec!=null&&!rec.read){rec.read=true;Save();}}
    public bool Detach(string id){var rec=Get(id);if(rec==null||rec.detached)return false;rec.detached=true;Save();return true;}
    public bool Place(string id,Vector2 where){var rec=Get(id);if(rec==null||!rec.detached||Pinned(id)!=null)return false;State.pins.Add(new Pin{id=id,position=where});Save();return true;}
    public void Return(string id){if(Get(id)==null)return;Get(id).detached=false;State.pins.RemoveAll(pin=>pin.id==id);State.threads.RemoveAll(ruleId=>File.deductions.Exists(rule=>rule.id==ruleId&&(rule.first==id||rule.second==id)));Save();}
    public Match Tie(string first,string second){
        if(Pinned(first)==null||Pinned(second)==null)return Match.Missing;if(first==second)return Match.Same;
        var rule=File.deductions.Find(item=>item.Matches(first,second));if(rule==null)return Match.Unsupported;
        if(State.threads.Contains(rule.id))return Match.Existing;State.threads.Add(rule.id);
        bool fresh=!State.insights.Contains(rule.id);if(fresh)State.insights.Add(rule.id);Save();
        if(fresh)HintUnlocked?.Invoke(rule);return fresh?Match.Discovered:Match.Linked;
    }
    public void Untie(string id){if(State.threads.Remove(id))Save();}
    public void Save(){try{if(persist!=null){persist(State);Error=null;return;}Directory.CreateDirectory(Path.GetDirectoryName(PathName));var temp=PathName+".tmp";System.IO.File.WriteAllText(temp,JsonUtility.ToJson(State,true));if(System.IO.File.Exists(PathName))System.IO.File.Replace(temp,PathName,PathName+".bak");else System.IO.File.Move(temp,PathName);Error=null;}catch(Exception ex){Error=ex.Message;Debug.LogWarning("Casebook: "+Error);}}
}
}
