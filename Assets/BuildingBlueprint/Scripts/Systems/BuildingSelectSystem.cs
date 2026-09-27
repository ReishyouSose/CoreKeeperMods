using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Core;
using Assets.BuildingBlueprint.Scripts.UI;
using PlayerEquipment;
using Pug.UnityExtensions;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.Systems
{
    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(BlockInputSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class BuildingSelectClient : PugSimulationSystemBase
    {
        internal static BuildingSelectClient Ins { get; private set; }
        private BlueprintUI ui;
        private BufferLookup<SelectedEntityBuffer> entityLookup;
        private BufferLookup<SelectedTileBuffer> tileLookup;
        private ComponentLookup<SelectionOptionCD> optionLookup;
        private ComponentLookup<ClientInput> inputLookup;
        private BufferLookup<TileTargetStateBuffer> stateLookup;
        private EntityQuery query;
        private float2? startPos;
        private float2 currentPos;
        private ComponentLookup<DirectionCD> directionLookup;
        private ComponentLookup<ObjectDataCD> objLookup;
        private ComponentLookup<LocalTransform> transLookup;
        private ComponentLookup<PaintableObjectCD> paintLookup;
        private TileAccessor tileAccessor;

        protected override void OnCreate()
        {
            Ins = this;
            ui = BlueprintUI.Ins;
            entityLookup = SystemAPI.GetBufferLookup<SelectedEntityBuffer>();
            tileLookup = SystemAPI.GetBufferLookup<SelectedTileBuffer>();
            optionLookup = SystemAPI.GetComponentLookup<SelectionOptionCD>();
            inputLookup = SystemAPI.GetComponentLookup<ClientInput>();
            stateLookup = SystemAPI.GetBufferLookup<TileTargetStateBuffer>();
            query = EntityManager.CreateEntityQuery(typeof(PlaceableCD));
            directionLookup = SystemAPI.GetComponentLookup<DirectionCD>();
            objLookup = SystemAPI.GetComponentLookup<ObjectDataCD>();
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>();
            paintLookup = SystemAPI.GetComponentLookup<PaintableObjectCD>();
            var e = EntityManager.CreateSingletonBuffer<TileTargetBuffer>("TileTargetBuffer");
            var buffer = EntityManager.GetBuffer<TileTargetBuffer>(e);
            if (ScriptableData.TryGetDataBlocks<TileTargetDataBlock>(out var dataBlocks))
            {
                var datas = TileTargetDataBlock.GetSortedArray(dataBlocks);
                foreach (var dataBlock in datas)
                {
                    var t = dataBlock.TileType;
                    buffer.Add(new() { TileType = t });
                }
            }
            RequireForUpdate<TileTargetBuffer>();
            NeedDatabase();
            base.OnCreate();
        }
        protected override void OnStartRunning()
        {
            tileAccessor = CreateTileAccessor();
            base.OnStartRunning();
        }
        protected override void OnUpdate()
        {
            if (!Manager.main.player)
                return;
            var player = Manager.main.player.entity;
            var option = optionLookup[player];
            var tileState = stateLookup[player];
            ui.SelectHandler.Option = option;
            ui.SelectHandler.RefreshTileTarget(tileState);
            if (!option.Open)
                return;
            if (option.Place)
                return;
            var input = inputLookup[player];
            bool click = input.IsButtonStateSet(CommandInputButtonStateNames.Interact_Pressed);
            var layer = option.Layer;
            switch (option.Mode)
            {
                case SelectionMode.Box:
                    {
                        BoxOperator op = option;
                        var mouse = input.mouseOrJoystickWorldPoint;
                        if (click)
                        {
                            startPos = mouse;
                            currentPos = startPos.Value;
                            ui.StartSelect();
                        }
                        else if (startPos != null && !Manager.input.singleplayerInputModule.IsButtonCurrentlyDown(PlayerInput.InputType.INTERACT, false))
                        {
                            startPos = null;
                            ui.EndSelect();
                            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.Release, 0);
                        }
                        if (startPos == null)
                            return;
                        if (!currentPos.Equals(mouse))
                        {
                            var entities = entityLookup[player];
                            var tiles = tileLookup[player];
                            currentPos = mouse;
                            float2 min = math.min(startPos.Value, mouse);
                            float2 max = math.max(startPos.Value, mouse);
                            Rect selectArea = new(min.x, min.y, max.x - min.x, max.y - min.y);
                            if (layer == SelectionLayer.Entity)
                            {
                                List<EntityCD> list = new();
                                for (int i = 0; i < entities.Length; i++)
                                {
                                    list.Add(entities[i]);
                                }
                                ui.BoxOperate(selectArea, list, op);
                            }
                            else
                            {
                                Dictionary<int2, TilesInfo> dict = new();
                                for (int i = 0; i < tiles.Length; i++)
                                {
                                    var tile = tiles[i];
                                    var pos = tile.Position;
                                    if (!dict.TryGetValue(pos, out var info))
                                        info = dict[pos] = new() { Position = pos, Tiles = new() };
                                    info.Tiles.Add(tile.Tile, tile.State);
                                }
                                ui.BoxOperate(selectArea, dict.Select(x => x.Value).ToList(), op);
                            }
                        }
                    }
                    break;
                case SelectionMode.Click:
                    {
                        var mouse = input.mouseOrJoystickWorldPoint.RoundToInt2();
                        bool hover = false;
                        ClickOperator op = option;
                        if (layer == SelectionLayer.Entity)
                        {
                            using var queryE = query.ToEntityArray(Allocator.Temp);
                            EntityInfo info = new()
                            {
                                Position = mouse,
                                Entities = new()
                            };
                            foreach (var entity in queryE)
                            {
                                directionLookup.TryGetComponent(entity, out var direction);
                                if (!objLookup.TryGetComponent(entity, out var obj))
                                    continue;
                                if (!transLookup.TryGetComponent(entity, out var trans))
                                    continue;
                                paintLookup.TryGetComponent(entity, out var paint);
                                var size = direction.GetPrefabTileSize(PugDatabase.GetEntityObjectInfo(obj.objectID, database, obj.variation).prefabTileSize);
                                var cd = new SelectedEntityBuffer()
                                {
                                    ObjectID = obj.objectID,
                                    X = size.x,
                                    Y = size.y,
                                    Direction = direction.direction.RoundToInt2(),
                                    Position = trans.Position.RoundToInt2(),
                                    Variation = obj.variation,
                                    Color = paint.color
                                };
                                var box = MiscHelper.GetEntityRect(cd, database);
                                if (box.Contains(mouse.ToVec2Int()))
                                {
                                    info.Entities.Add(cd);
                                    hover = true;
                                }
                            }
                            ui.ClickOperate(hover, info, click, op);
                        }
                        else
                        {
                            TilesInfo info = new() { Position = mouse, Tiles = new() };
                            var tileTarget = SystemAPI.GetSingletonBuffer<TileTargetBuffer>();
                            NativeHashSet<int> targets = new(tileTarget.Length, Allocator.Temp);
                            for (int i = 0; i < tileTarget.Length; i++)
                            {
                                if (tileState[i].State)
                                    targets.Add((int)tileTarget[i].TileType);
                            }
                            using var queryT = tileAccessor.Get(mouse, Allocator.Temp);
                            foreach (var tile in queryT)
                            {
                                info.Tiles.Add(tile, targets.Contains((int)tile.tileType));
                                hover = true;
                            }
                            ui.ClickOperate(hover, info, click, op);
                        }
                    }
                    break;
            }
            base.OnUpdate();
        }
    }

    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(BlockInputSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class BuildingSelectServer : PugSimulationSystemBase
    {
        private EntityQuery query;
        private ComponentLookup<DirectionCD> directionLookup;
        private ComponentLookup<ObjectDataCD> objLookup;
        private ComponentLookup<LocalTransform> transLookup;
        private ComponentLookup<PaintableObjectCD> paintLookup;
        private TileAccessor tileAccessor;
        protected override void OnCreate()
        {
            EntityQueryDesc desc = new()
            {
                All = new ComponentType[]
                {
                    typeof(PlaceableCD)
                },
                Options = EntityQueryOptions.IncludeDisabledEntities
            };
            query = EntityManager.CreateEntityQuery(desc);
            directionLookup = SystemAPI.GetComponentLookup<DirectionCD>();
            objLookup = SystemAPI.GetComponentLookup<ObjectDataCD>();
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>();
            paintLookup = SystemAPI.GetComponentLookup<PaintableObjectCD>();
            NeedDatabase();
            var e = EntityManager.CreateSingletonBuffer<TileTargetBuffer>("TileTargetBuffer");
            var buffer = EntityManager.GetBuffer<TileTargetBuffer>(e);
            if (ScriptableData.TryGetDataBlocks<TileTargetDataBlock>(out var dataBlocks))
            {
                var datas = TileTargetDataBlock.GetSortedArray(dataBlocks);
                foreach (var dataBlock in datas)
                {
                    var t = dataBlock.TileType;
                    buffer.Add(new() { TileType = t });
                }
            }
            RequireForUpdate<TileTargetBuffer>();
            base.OnCreate();
        }
        protected override void OnStartRunning()
        {
            tileAccessor = CreateTileAccessor();
            base.OnStartRunning();
        }
        protected override void OnUpdate()
        {
            var database = this.database;
            var query = this.query;
            var directionLookup = this.directionLookup;
            var objLookup = this.objLookup;
            var transLookup = this.transLookup;
            var paintLookup = this.paintLookup;
            var tileTarget = SystemAPI.GetSingletonBuffer<TileTargetBuffer>();
            var tileAccessor = this.tileAccessor;
            var delta = World.Time.DeltaTime;
            Entities.ForEach((ref SelectionOptionCD option, ref DynamicBuffer<SelectedEntityBuffer> entities, ref DynamicBuffer<SelectedTileBuffer> tiles,
                in DynamicBuffer<TileTargetStateBuffer> tileState, in ClientInput input) =>
            {
                if (!option.Open)
                    return;
                if (option.Place)
                    return;
                if (option.Mode != SelectionMode.Box)
                    return;
                var mouse = input.mouseOrJoystickWorldPoint.RoundToInt2();
                var layer = option.Layer;
                ref var startPos = ref option.Start;
                ref var currentPos = ref option.Current;
                BoxOperator op = option;
                if (input.IsButtonStateSet(CommandInputButtonStateNames.Interact_Pressed))
                {
                    startPos = mouse;
                    currentPos = startPos.Value;
                    option.InteractHeld = true;
                }
                else if (startPos != null && !option.InteractHeld)
                {
                    startPos = null;
                    entities.Clear();
                    tiles.Clear();
                }
                if (startPos == null)
                    return;
                if (!currentPos.Equals(mouse))
                {
                    currentPos = mouse;
                    float2 min = math.min(startPos.Value, mouse);
                    float2 max = math.max(startPos.Value, mouse);
                    Rect selectArea = new(min.x, min.y, max.x - min.x, max.y - min.y);
                    if (layer == SelectionLayer.Entity)
                    {
                        entities.Clear();
                        using var queryE = query.ToEntityArray(Allocator.Temp);
                        int count = 0;
                        foreach (var entity in queryE)
                        {
                            if (!objLookup.TryGetComponent(entity, out var obj))
                                continue;
                            ref var info = ref PugDatabase.GetEntityObjectInfo(obj.objectID, database, obj.variation);
                            directionLookup.TryGetComponent(entity, out var direction);
                            if (!transLookup.TryGetComponent(entity, out var trans))
                                continue;
                            paintLookup.TryGetComponent(entity, out var paint);
                            var size = direction.GetPrefabTileSize(info.prefabTileSize);
                            var cd = new SelectedEntityBuffer()
                            {
                                ObjectID = obj.objectID,
                                X = size.x,
                                Y = size.y,
                                Direction = direction.direction.RoundToInt2(),
                                Position = trans.Position.RoundToInt2(),
                                Variation = obj.variation,
                                Color = paint.color,
                                Entity = entity,
                            };
                            var box = MiscHelper.GetEntityRect(cd, database);
                            if (selectArea.Contains(box))
                            {
                                entities.Add(cd);
                                count++;
                            }
                        }
                    }
                    else
                    {
                        tiles.Clear();
                        int left = (int)math.ceil(min.x);
                        int right = (int)math.floor(max.x);
                        int bottom = (int)math.ceil(min.y);
                        int top = (int)math.floor(max.y);
                        NativeHashSet<int> targets = new(tileTarget.Length, Allocator.Temp);
                        for (int i = 0; i < tileTarget.Length; i++)
                        {
                            if (tileState[i].State)
                                targets.Add((int)tileTarget[i].TileType);
                        }
                        for (int x = left; x <= right; x++)
                        {
                            for (int y = bottom; y <= top; y++)
                            {
                                int2 pos = new(x, y);
                                using var queryT = tileAccessor.Get(pos, Allocator.Temp);
                                foreach (var tile in queryT)
                                {
                                    tiles.Add(new()
                                    {
                                        Position = pos,
                                        Tile = tile,
                                        State = targets.Contains((int)tile.tileType)
                                    });
                                }
                            }
                        }
                        targets.Dispose();
                    }
                }
            })
                .WithName("BuildingSelect")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
        public static float2 MouseWorld => EntityMonoBehaviour.ToWorldFromRender(Manager.ui.mouse.GetMouseGameViewPosition()).ToFloat2();
        internal static void MarkPlaceable(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            if (authoringData.TryGetComponent<TileAuthoring>(out _))
                return;
            if (authoringData.TryGetComponent<MineableAuthoring>(out _) || authoringData.TryGetComponent<DiggableAuthoring>(out _))
            {
                if (authoringData.TryGetComponent(out EntityMonoBehaviourData data))
                {
                    if (data.objectInfo.icon == null)
                        return;
                    if (data.objectInfo.objectType == ObjectType.Critter)
                        return;
                }
                else if (!authoringData.TryGetComponent(out InventoryItemAuthoring _))
                    return;
                entityManager.AddComponent<PlaceableCD>(entity);
            }
        }
        internal static void AddSelectComponent(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            if (authoringData.TryGetComponent(out PlayerAuthoring _))
            {
                entityManager.AddComponent<SelectionOptionCD>(entity);
                entityManager.AddBuffer<SelectedEntityBuffer>(entity);
                entityManager.AddBuffer<SelectedTileBuffer>(entity);
                var target = entityManager.AddBuffer<TileTargetStateBuffer>(entity);
                if (ScriptableData.TryGetDataBlocks<TileTargetDataBlock>(out var dataBlocks))
                    target.Capacity = target.Length = dataBlocks.Count;
            }
        }
    }
}
