using System.Collections.Generic;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;

namespace TomeOfMimicry
{
    public static class UndoManager
    {
        private const int MaxHistoryPerPlayer = 12;

        public struct TileSnapshot
        {
            public int2 pos;

            public bool hadWall;
            public int wallTileset;

            public bool hadFloor;
            public TileType floorKind;
            public int floorTileset;

            public bool hadGround;
            public int groundTileset;

            public bool hadBridge;
            public int bridgeTileset;

            public bool hadRail;
            public int railTileset;

            public bool hadWire;
            public int wireTileset;

            public bool hadWater;
            public bool hadPit;

            public List<int2> extras;
        }

        public class EntityPasteInfo
        {
            public int2 wpos;
            public ObjectID objectId;
            public int variation;
            public float3 direction;
            public bool dirByVariation;
            public int2 dbvDirection;
            public bool hasPaint;
            public int paintColor;
            public bool hasObjectFilter;
            public int objectFilterType;
            public ObjectID objectFilterObject;
            public int objectFilterVariation;
            public bool hasMoverFilter;
            public int moverFilterType;
            public ObjectID moverFilterObject;
            public int moverFilterVariation;
            public int moverFilterCategory;
        }

        public class PasteRecord
        {
            public bool IsCut;
            public bool Cancelled;
            public bool EntitiesSpawned;

            public readonly List<TileSnapshot> Before = new();
            public readonly List<TileSnapshot> After = new();
            public readonly List<Entity> CreatedEntities = new();
            public readonly List<EntityPasteInfo> EntityPastes = new();

            public Dictionary<ObjectID, int> TileCost;
        }

        private static readonly Dictionary<Entity, Stack<PasteRecord>> _undoStacks = new();
        private static readonly Dictionary<Entity, Stack<PasteRecord>> _redoStacks = new();

        public struct UndoRequest
        {
            public Entity player;
            public Entity connection;
            public bool redo;
        }

        private static readonly Queue<UndoRequest> _requests = new();

        public static bool CanUndo(Entity player) =>
            _undoStacks.TryGetValue(player, out var s) && s.Count > 0;

        public static bool CanRedo(Entity player) =>
            _redoStacks.TryGetValue(player, out var s) && s.Count > 0;

        public static void QueueRequest(Entity player, bool redo, Entity connection = default)
        {
            _requests.Enqueue(new UndoRequest { player = player, redo = redo, connection = connection });
        }

        public static void PruneInvalid(System.Func<Entity, bool> stillExists)
        {
            List<Entity> dead = null;
            foreach (var key in _undoStacks.Keys)
            {
                if (stillExists(key)) continue;
                dead ??= new List<Entity>();
                dead.Add(key);
            }
            if (dead == null) return;
            foreach (var key in dead)
            {
                _undoStacks.Remove(key);
                _redoStacks.Remove(key);
                UnityEngine.Debug.Log($"[Tome of Mimicry] Pruned undo history for departed player {key}");
            }
        }

        public static bool TryDequeueRequest(out UndoRequest request)
        {
            if (_requests.Count == 0) { request = default; return false; }
            request = _requests.Dequeue();
            return true;
        }

        private static Stack<PasteRecord> GetStack(
            Dictionary<Entity, Stack<PasteRecord>> stacks, Entity player)
        {
            if (!stacks.TryGetValue(player, out var stack))
            {
                stack = new Stack<PasteRecord>();
                stacks[player] = stack;
            }
            return stack;
        }

        public static void PushPaste(Entity player, PasteRecord record)
        {
            var undo = GetStack(_undoStacks, player);
            undo.Push(record);
            TrimOldest(undo);
            GetStack(_redoStacks, player).Clear();
            UnityEngine.Debug.Log($"[Tome of Mimicry] Undo stack for {player}: {undo.Count} (redo cleared)");
        }

        private static void TrimOldest(Stack<PasteRecord> stack)
        {
            if (stack.Count <= MaxHistoryPerPlayer) return;
            var newestFirst = stack.ToArray();
            stack.Clear();
            for (int i = MaxHistoryPerPlayer - 1; i >= 0; i--)
                stack.Push(newestFirst[i]);
        }

        public static bool TryPopUndo(Entity player, out PasteRecord record)
        {
            var undo = GetStack(_undoStacks, player);
            if (undo.Count == 0) { record = null; return false; }
            record = undo.Pop();
            var redo = GetStack(_redoStacks, player);
            redo.Push(record);
            UnityEngine.Debug.Log($"[Tome of Mimicry] Undo for {player} — restoring {record.Before.Count} positions  (undo left: {undo.Count}, redo: {redo.Count})");
            return true;
        }

        public static bool TryPopRedo(Entity player, out PasteRecord record)
        {
            var redo = GetStack(_redoStacks, player);
            if (redo.Count == 0) { record = null; return false; }
            record = redo.Pop();
            var undo = GetStack(_undoStacks, player);
            undo.Push(record);
            UnityEngine.Debug.Log($"[Tome of Mimicry] Redo for {player} — re-applying {record.After.Count} positions  (undo: {undo.Count}, redo left: {redo.Count})");
            return true;
        }

        public static void PushRedoBack(Entity player, PasteRecord record)
        {
            var undo = GetStack(_undoStacks, player);
            if (undo.Count > 0 && undo.Peek() == record)
                undo.Pop();
            GetStack(_redoStacks, player).Push(record);
            UnityEngine.Debug.Log($"[Tome of Mimicry] Redo pushed back (unaffordable) for {player}");
        }

        public static void Clear()
        {
            _undoStacks.Clear();
            _redoStacks.Clear();
            _requests.Clear();
        }
    }
}
