using System;
using System.Collections.Generic;
using UnityEngine;

namespace Casebook {
public enum Section { Evidence, Notes, People, Places, Timeline }

[Serializable] public class Attachment {
    public Texture2D image;
    public string caption;
}

[Serializable] public class Entry {
    public string id, title;
    public Section section;
    public string location, time;
    [TextArea] public string observation, narrator;
    public List<Attachment> attachments = new List<Attachment>();
    [Tooltip("Для людей, мест и времени: запись сама появится в дневнике, когда найдены все эти улики. Пусто — только через игровое событие сбора.")]
    public List<string> revealedBy = new List<string>();
    public bool Derived => revealedBy != null && revealedBy.Count > 0;
}

[Serializable] public class Deduction {
    public string id, first, second, title;
    [TextArea] public string hint;
    public bool Matches(string left,string right) => first==left && second==right || first==right && second==left;
}

[CreateAssetMenu(menuName="Investigation/Case file",fileName="InvestigationCase")]
public class InvestigationCase : ScriptableObject {
    [Tooltip("Отдельный стабильный идентификатор сохранения")] public string caseId="case-01";
    public KeyCode journalKey=KeyCode.J, boardKey=KeyCode.Tab;
    public string heading="Дневник", narratorName="Алексей";
    public List<Entry> entries=new List<Entry>();
    public List<Deduction> deductions=new List<Deduction>();
    public Entry Find(string id)=>entries.Find(entry=>entry!=null && entry.id==id);
    public string ValidateCase(){
        var ids=new HashSet<string>();
        if(string.IsNullOrWhiteSpace(caseId))return "Нужно указать ID дела.";
        foreach(var entry in entries)if(entry==null || string.IsNullOrWhiteSpace(entry.id)||string.IsNullOrWhiteSpace(entry.title)||!ids.Add(entry.id))return "У каждой улики должны быть название и уникальный ID.";
        var seen=new HashSet<string>();var pairs=new HashSet<string>();
        foreach(var rule in deductions){
            if(rule==null||string.IsNullOrWhiteSpace(rule.id)||!seen.Add(rule.id)||rule.first==rule.second||!ids.Contains(rule.first)||!ids.Contains(rule.second)||string.IsNullOrWhiteSpace(rule.title)||string.IsNullOrWhiteSpace(rule.hint))return "Проверьте ID, пару улик, название и текст подсказки.";
            string left=rule.first,right=rule.second;if(string.CompareOrdinal(left,right)>0){left=rule.second;right=rule.first;}
            if(!pairs.Add(left.Length+":"+left+right))return "Одна пара назначена нескольким подсказкам.";
        }
        return null;
    }
}
}
