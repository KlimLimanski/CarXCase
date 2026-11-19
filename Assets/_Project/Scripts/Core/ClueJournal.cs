using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DetectiveGame.Core
{
    /// Clues and conclusions the player has found. Static, so it survives scene loads during a play session.
    public static class ClueJournal
    {
        public sealed class Entry { public string id, title, description, thought; }

        static readonly List<Entry> entries = new List<Entry>();
        static readonly HashSet<string> conclusions = new HashSet<string>();
        public static IReadOnlyList<Entry> Entries => entries;
        public static IEnumerable<string> Conclusions => conclusions;
        public static int Count => entries.Count;
        public static event System.Action Changed;
        public static event System.Action Cleared;
        public static string Decision;
        /// Set when returning to the precinct from a location: the entrance cutscene is skipped.
        public static bool SkipPrecinctIntro;

        public static bool Has(string id) => entries.Any(e => e.id == id) || conclusions.Contains(id);

        public static void Collect(string id, string title, string description, string thought = null)
        {
            if (string.IsNullOrEmpty(id) || entries.Any(e => e.id == id)) return;
            entries.Add(new Entry { id = id, title = title, description = description, thought = thought });
            Changed?.Invoke();
        }

        public static void AddConclusion(string id) { if (!string.IsNullOrEmpty(id) && conclusions.Add(id)) Changed?.Invoke(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() { entries.Clear(); conclusions.Clear(); Decision = null; SkipPrecinctIntro = false; Cleared?.Invoke(); }
    }
}
