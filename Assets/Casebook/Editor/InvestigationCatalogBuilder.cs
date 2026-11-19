using System;
using System.Collections.Generic;
using System.Linq;
using Casebook;
using CarXCase.Precinct;
using DetectiveGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarXCase.Editor
{
    /// Creates a reusable catalog from authored game scenes without altering those scenes.
    public sealed class InvestigationCatalogBuilder : IPreprocessBuildWithReport
    {
        const string Path = "Assets/Casebook/Resources/CarXInvestigation.asset";
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Rebuild();

        [MenuItem("Tools/Investigation/Rebuild game catalog")]
        public static void Rebuild()
        {
            var file = AssetDatabase.LoadAssetAtPath<InvestigationCase>(Path);
            if (!file) { file = ScriptableObject.CreateInstance<InvestigationCase>(); AssetDatabase.CreateAsset(file, Path); }
            // Preserve user-assigned extra images and categories on catalog entries.
            var old = file.entries.ToDictionary(e => e.id, e => e);
            var entries = new Dictionary<string, Casebook.Entry>();
            var rules = new Dictionary<string, Casebook.Deduction>();
            var photos = new Dictionary<string, Texture2D>();
            void Add(string id, string title, string text, string thought, string location, bool authoritative)
            {
                if (string.IsNullOrEmpty(id) || (!authoritative && entries.ContainsKey(id))) return;
                var entry = entries.TryGetValue(id, out var current) ? current :
                    old.TryGetValue(id, out var previous) ? previous : new Casebook.Entry { id = id };
                entry.title = title; entry.observation = text; entry.narrator = thought ?? "";
                entry.location = location;
                entries[id] = entry;
            }
            foreach (var config in EditorBuildSettings.scenes.Where(s => s.enabled && NoirMenu.GameSession.IsStoryScene(System.IO.Path.GetFileNameWithoutExtension(s.path))))
            {
                var scene = SceneManager.GetSceneByPath(config.path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(config.path, OpenSceneMode.Additive);
                try
                {
                    string location = scene.name == "01_FatherApartment" ? "Квартира отца" :
                        scene.name == "02_Precinct" ? "Участок" : scene.name == "03_Bar" ? "Бар «Стерлядь»" :
                        scene.name == "04_Pawnshop" ? "Ломбард" : scene.name == "05_Mikhail" ? "Квартира Михаила" : "Брайтон";
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var clue in root.GetComponentsInChildren<ClueObject>(true))
                            if (clue.addToJournal)
                            {
                                Add(clue.clueId, clue.clueTitle, clue.clueDescription, clue.journalThought, location, true);
                                foreach (var attachment in clue.journalAttachments ?? Array.Empty<Attachment>())
                                    if (attachment != null && attachment.image && !entries[clue.clueId].attachments.Any(a => a.image == attachment.image))
                                        entries[clue.clueId].attachments.Add(attachment);
                            }
                        foreach (var talk in root.GetComponentsInChildren<Interrogation>(true))
                            Add(talk.grantId, talk.grantTitle, talk.grantText, "", location, true);
                        foreach (var board in root.GetComponentsInChildren<DeductionBoard>(true))
                        {
                            foreach (var clue in board.fallbackClues ?? Array.Empty<DeductionBoard.ClueDef>())
                                Add(clue.id, clue.title, clue.description, "", "", false);
                            foreach (var rule in board.deductions ?? Array.Empty<DeductionBoard.Deduction>())
                            {
                                rules[rule.id] = new Casebook.Deduction { id = rule.id, first = rule.clueA, second = rule.clueB, title = rule.title, hint = rule.text };
                                Add(rule.id, rule.title, rule.text, "", "Вывод по делу", true);
                                entries[rule.id].section = Section.Notes;
                            }
                            foreach (var photo in board.cluePhotos ?? Array.Empty<Texture2D>())
                                if (photo) photos[photo.name] = photo;
                        }
                    }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            foreach (var entry in entries.Values)
                if (photos.TryGetValue(entry.id, out var photo) && !entry.attachments.Any(a => a.image == photo))
                    entry.attachments.Add(new Attachment { image = photo, caption = entry.title });
            file.caseId = "carx-story";
            file.entries = entries.Values.ToList(); file.deductions = rules.Values.ToList();
            var error = file.ValidateCase();
            if (error != null) throw new BuildFailedException(error);
            EditorUtility.SetDirty(file);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Investigation] Catalog: {file.entries.Count} entries, {file.deductions.Count} actual game deductions.");
        }
    }
}
