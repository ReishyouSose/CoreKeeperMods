using System.Collections.Generic;
using UnityEngine;

namespace TomeOfMimicry
{
    public static class BlueprintLibraryStore
    {
        private static readonly Dictionary<string, Blueprint> _cache = new();
        private static readonly List<string> _staleKeys = new();
        private static float _nextRefresh;

        public static List<string> Names = new();
        public static readonly Dictionary<string, Blueprint> Blueprints = new();

        public static void Invalidate() => _nextRefresh = 0f;

        public static void Put(Blueprint bp)
        {
            if (bp != null) _cache[bp.name] = bp;
        }

        public static void Forget(string name) => _cache.Remove(name);

        public static bool HasTag(Blueprint bp, string tag)
        {
            if (bp?.tags == null) return false;
            foreach (string existing in bp.tags)
                if (string.Equals(existing, tag, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool Refresh(string tagFilter, string search, int sortMode)
        {
            if (Time.realtimeSinceStartup < _nextRefresh) return false;
            var names = BlueprintSerializer.ListSaved();

            if (_cache.Count > 0)
            {
                var present = new HashSet<string>(names);
                _staleKeys.Clear();
                foreach (var key in _cache.Keys)
                    if (!present.Contains(key)) _staleKeys.Add(key);
                foreach (var key in _staleKeys) _cache.Remove(key);
            }

            Blueprints.Clear();
            foreach (string name in names)
            {
                if (!_cache.TryGetValue(name, out var bp))
                {
                    bp = BlueprintSerializer.Load(name);
                    if (bp == null) continue;
                    _cache[name] = bp;
                }
                if (tagFilter != "ALL" && !HasTag(bp, tagFilter)) continue;
                if (!string.IsNullOrWhiteSpace(search) &&
                    name.IndexOf(search.Trim(), System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Blueprints[name] = bp;
            }

            Names = new List<string>(Blueprints.Keys);
            Names.Sort((a, b) =>
            {
                var left = Blueprints[a];
                var right = Blueprints[b];
                return sortMode switch
                {
                    1 => string.Compare(left.name, right.name, System.StringComparison.OrdinalIgnoreCase),
                    2 => (right.width * right.height).CompareTo(left.width * left.height),
                    _ => string.Compare(right.modifiedUtc, left.modifiedUtc, System.StringComparison.Ordinal),
                };
            });
            _nextRefresh = Time.realtimeSinceStartup + 1f;
            return true;
        }
    }
}
