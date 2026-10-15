using Assets.BuildingBlueprint.Scripts.Core;
using Assets.BuildingBlueprint.Scripts.Systems;
using CoreLib.Submodule.UserInterface.Interface;
using Pug.UnityExtensions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.UI
{
    public class BlueprintUI : MonoBehaviour, IModUI
    {
        private static WaitForSeconds waitForSeconds0_1 = new WaitForSeconds(0.1f);

        internal static BlueprintUI Ins { get; private set; }
        public SpriteRenderer SelectBorder;
        public SpriteRenderer MarkTemplate;
        public SpriteRenderer HoverMark;
        public GameObject DestoryMark;
        public Transform MarkContainer;
        public Transform Camera;
        public SelectHandler SelectHandler;
        public UISingleSlot EntitySlot;
        public LinearLayoutUIComponent EntityLayout;
        public UITileTypeSelector TileSelector;
        public LinearLayoutUIComponent TileLayout;
        public GameObject LeaderContainer;
        public GameObject InfoContainer;
        public SaveHandler SavesHandler;
        public GameObject Exit;
        public UIBuildingPreviewWindow PreviewWindow;
        public UIBuildingInfo BuildingTemplate;
        public LinearLayoutUIComponent BuildingsLayout;
        public PlacePreviewHanlder PlaceHanlder;
        private List<UISingleSlot> entityList;
        private List<UITileTypeSelector> tileSelectors;
        private List<SpriteRenderer> marks;
        private Dictionary<int2, EntityInfo> entityPreview;
        private Dictionary<int2, EntityInfo> entityRecord;
        private Dictionary<int2, TilesInfo> tilePreview;
        private Dictionary<int2, TilesInfo> tileRecord;
        private Dictionary<TileCD, bool> currentTile;
        private bool open;
        private const string PopKey = "BuildingBlueprint/NoSelected";
        public GameObject Root => transform.GetChild(0).gameObject;
        public bool ShowWithPlayerInventory => false;

        public bool ShouldPlayerCraftingShow => false;
        private void Awake()
        {
            Ins = this;
            marks = new();
            MarkTemplate.gameObject.SetActive(false);
            SelectBorder.gameObject.SetActive(false);
            HoverMark.gameObject.SetActive(false);
            Exit.SetActive(false);
            entityPreview = new();
            entityRecord = new();
            tilePreview = new();
            tileRecord = new();
            entityList = new()
            {
               EntitySlot
            };
            tileSelectors = new()
            {
                TileSelector
            };
            EntitySlot.gameObject.SetActive(false);
            TileLayout.gameObject.SetActive(false);
            BuildingTemplate.gameObject.SetActive(false);
            ShowInfoContainer();
            HideUI();
        }
        public void HideUI()
        {
            open = false;
            Root.SetActive(false);
            SavesHandler.ClearCut();
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.SwitchUIState, 0);
            if (BuildingSelectClient.Ins == null)
                return;
            BuildingSelectClient.Ins.Enabled = false;
        }

        public void ShowUI()
        {
            open = true;
            Root.SetActive(true);
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.SwitchUIState, 1);
            BuildingSelectClient.Ins.Enabled = true;
            DestoryMark.SetActive(false);

            if (BuildingBlueprint.ClearSelected.Value)
                ClearRecord();
            if (BuildingBlueprint.ExitPlaceMode.Value)
                ExitPlaceMode();
        }

        public void StartSelect()
        {
            SelectBorder.gameObject.SetActive(true);
            SelectBorder.transform.localPosition = Vector3.zero;
            SelectBorder.size = Vector2.zero;
        }

        public void EndSelect()
        {
            SelectBorder.gameObject.SetActive(false);
            entityRecord = entityPreview.ToDictionary(x => x.Key, x => x.Value);
            tileRecord = tilePreview.ToDictionary(x => x.Key, x => x.Value);
        }
        public void BoxOperate(Rect rect, List<EntityCD> selected, BoxOperator op)
        {
            Dictionary<int2, EntityInfo> s = new();
            foreach (var select in selected)
            {
                var pos = select.Position;
                if (!s.TryGetValue(pos, out var info))
                    info = s[pos] = new() { Position = pos, Entities = new() };
                info.Entities.Add(select);
            }
            entityPreview = entityRecord.ToDictionary(x => x.Key, x => x.Value);
            switch (op)
            {
                case BoxOperator.New:
                    entityPreview = s;
                    break;
                case BoxOperator.Add:
                    foreach (var (k, v) in s)
                    {
                        if (!entityPreview.TryGetValue(k, out var info))
                            info = entityPreview[k] = new() { Position = k, Entities = new() };
                        var list = info.Entities;
                        foreach (var e in v.Entities)
                            list.Add(e);
                    }
                    break;
                case BoxOperator.Subtract:
                    foreach (var (k, v) in s)
                    {
                        if (!entityPreview.TryGetValue(k, out var info))
                            continue;
                        var list = info.Entities;
                        foreach (var e in v.Entities)
                            list.Remove(e);
                    }
                    break;
                case BoxOperator.Intersect:
                    var dict = new Dictionary<int2, EntityInfo>(entityPreview.Count);
                    foreach (var (k, v) in s)
                    {
                        if (entityPreview.TryGetValue(k, out var previewInfo))
                        {
                            var intersected = previewInfo.Entities.Intersect(v.Entities);
                            if (intersected.Any())
                            {
                                dict[k] = new EntityInfo
                                {
                                    Position = k,
                                    Entities = intersected.ToHashSet()
                                };
                            }
                        }
                    }
                    entityPreview = dict;
                    break;
            }
            RefreshVisual(entityPreview);
            SelectBorder.transform.localPosition = new(rect.x, rect.y, 0);
            SelectBorder.size = rect.size;
        }
        public void BoxOperate(Rect rect, List<TilesInfo> selected, BoxOperator op)
        {
            var s = selected.ToDictionary(x => x.Position);
            tilePreview = tileRecord.ToDictionary(x => x.Key, x => x.Value);
            switch (op)
            {
                case BoxOperator.New:
                    tilePreview = s;
                    break;
                case BoxOperator.Add:
                    foreach (var (k, v) in s)
                        tilePreview[k] = v;
                    break;
                case BoxOperator.Subtract:
                    foreach (var key in s.Keys)
                        tilePreview.Remove(key);
                    break;
                case BoxOperator.Intersect:
                    tilePreview = tilePreview.Where(kv => s.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                    break;
            }
            RefreshVisual(tilePreview);
            SelectBorder.transform.localPosition = new(rect.x, rect.y, 0);
            SelectBorder.size = rect.size;
        }

        public void ClickOperate(bool hover, EntityInfo info, bool click, ClickOperator op)
        {
            if (Manager.ui.currentSelectedUIElement || !hover)
            {
                HoverMark.gameObject.SetActive(false);
                return;
            }
            HoverMark.gameObject.SetActive(true);
            SetPosition(HoverMark, info.Entities.Aggregate((x, y) => x.X * x.Y > y.X * y.Y ? x : y));
            if (click)
            {
                switch (op)
                {
                    case ClickOperator.Add:
                        entityRecord.TryAdd(info.Position, info);
                        break;
                    case ClickOperator.Remove:
                        entityRecord.Remove(info.Position);
                        break;
                }
                RefreshVisual(entityRecord);
            }
        }
        public void ClickOperate(bool hover, TilesInfo info, bool click, ClickOperator op)
        {
            if (Manager.ui.currentSelectedUIElement || !hover)
            {
                HoverMark.gameObject.SetActive(false);
                return;
            }
            HoverMark.gameObject.SetActive(true);
            SetPosition(HoverMark, info);
            if (click)
            {
                switch (op)
                {
                    case ClickOperator.Add:
                        tileRecord.TryAdd(info.Position, info);
                        break;
                    case ClickOperator.Remove:
                        tileRecord.Remove(info.Position);
                        break;
                }
                RefreshVisual(tileRecord);
            }
        }
        private void RefreshVisual(Dictionary<int2, EntityInfo> entities)
        {
            int i = 0;
            foreach (var info in entities.Values)
            {
                foreach (var cd in info.Entities)
                {
                    var mark = GetOrCreateSlot(i++);
                    SetPosition(mark, cd);
                }
            }
            DeactiveExcessSlot(i);
        }
        private void RefreshVisual(Dictionary<int2, TilesInfo> tiles)
        {
            int i = 0;
            foreach (var info in tiles.Values)
            {
                var mark = GetOrCreateSlot(i++);
                // 转换世界坐标到渲染坐标
                SetPosition(mark, info);
            }
            DeactiveExcessSlot(i);
        }
        public void SwitchPreview(SelectionLayer layer)
        {
            if (layer == SelectionLayer.Entity)
                RefreshVisual(entityRecord);
            else
                RefreshVisual(tileRecord);
        }
        public void SetPosition(SpriteRenderer mark, EntityCD info)
        {
            SetPosition(mark, info.Position, info.GetEntityOffset(out var size), size);
        }
        public void SetPosition(SpriteRenderer mark, TilesInfo info)
        {
            SetPosition(mark, info.Position, float2.zero, new(1, 1));
        }
        private void SetPosition(SpriteRenderer mark, int2 pos, float2 offset, int2 size)
        {
            mark.GetComponent<RecordPosition>().Position = pos;
            mark.transform.localPosition = new Vector3(pos.x + offset.x, pos.y + offset.y, 0);
            mark.size = new(size.x, size.y);
        }

        private void Update()
        {
            if (open && (Manager.ui.isAnyInventoryShowing || Manager.menu.menuStackCount > 0))
            {
                HideUI();
                return;
            }
            var camera = -Manager.camera.smoothedCameraPosition;
            Camera.localPosition = new(camera.x, camera.z, 0);
            if (SelectHandler.Mode != SelectionMode.Click)
                HoverMark.gameObject.SetActive(false);
            if (PreviewWindow.gameObject.activeInHierarchy || SelectHandler.Mode != SelectionMode.Check)
                return;
            if (Manager.ui.currentSelectedUIElement)
                return;
            bool hover = false;
            var mouse = EntityMonoBehaviour.ToWorldFromRender(Manager.ui.mouse.GetMouseGameViewPosition()).ToFloat2();
            bool click = Input.GetMouseButtonDown(0);
            foreach (var sr in marks)
            {
                if (!sr.gameObject.activeInHierarchy)
                    continue;
                var center = sr.transform.localPosition;
                var size = sr.size;
                Rect rect = new(center.x - size.x / 2f, center.y - size.y / 2f, size.x, size.y);
                if (!rect.Contains(mouse))
                    continue;
                hover = true;
                HoverMark.gameObject.SetActive(true);
                HoverMark.size = sr.size;
                HoverMark.transform.localPosition = sr.transform.localPosition;
                if (click)
                {
                    switch (SelectHandler.Layer)
                    {
                        case SelectionLayer.Entity:
                            CheckEntityInfo(entityRecord[sr.GetComponent<RecordPosition>()]);
                            break;
                        case SelectionLayer.Tile:
                            CheckTilesInfo(tileRecord[sr.GetComponent<RecordPosition>()]);
                            break;
                    }
                }
                break;
            }
            if (!hover)
                HoverMark.gameObject.SetActive(false);
        }
        private void CheckTilesInfo(TilesInfo info)
        {
            ShowInfoContainer();
            EntityLayout.gameObject.SetActive(false);
            TileLayout.gameObject.SetActive(true);
            currentTile = info.Tiles;
            int i = 0;
            var except = SelectHandler.ExceptTiles;
            foreach (var (tile, state) in currentTile)
            {
                if (tileSelectors.Count <= i)
                {
                    tileSelectors.Add(Instantiate(TileSelector, TileLayout.transform));
                }
                var selector = tileSelectors[i];
                selector.gameObject.SetActive(true);
                selector.Set(tile, i, except.Contains(tile.tileType), state);
                i++;
            }
            for (int j = i; j < tileSelectors.Count; j++)
                tileSelectors[j].gameObject.SetActive(false);
            TileLayout.RenderUIComponent(true);
        }
        private void CheckEntityInfo(EntityInfo info)
        {
            ShowInfoContainer();
            TileLayout.gameObject.SetActive(false);
            EntityLayout.gameObject.SetActive(true);
            int i = 0;
            foreach (var entity in info.Entities)
            {
                if (entityList.Count <= i)
                {
                    entityList.Add(Instantiate(EntitySlot, EntityLayout.transform));
                }
                var slot = entityList[i];
                slot.gameObject.SetActive(true);
                slot.Contained = new()
                {
                    objectData = new()
                    {
                        objectID = entity.ObjectID,
                        variation = entity.Variation,
                        amount = 1
                    }
                };
                i++;
            }
            for (int j = i; j < entityList.Count; j++)
                entityList[j].gameObject.SetActive(false);
            EntityLayout.RenderUIComponent(true);
        }
        public void ClearSelection(bool single)
        {
            if (single)
            {
                switch (SelectHandler.Layer)
                {
                    case SelectionLayer.Entity:
                        entityRecord.Clear();
                        break;
                    case SelectionLayer.Tile:
                        tileRecord.Clear();
                        break;
                }
            }
            else
            {
                entityRecord.Clear();
                tileRecord.Clear();
            }
            DeactiveExcessSlot();
        }
        public void ChangeTileState(UITileTypeSelector selector)
        {
            currentTile[selector.TileCD] = !currentTile[selector.TileCD];
        }
        public void Save()
        {
            var player = Manager.main.player;
            var pop = player.RenderPosition + Vector3.up * 0.7f;
            if (entityRecord.Count == 0 && tileRecord.Count == 0)
            {
                CombatText.SpawnCombatText(PopKey, CombatText.NumberColor.White, pop, false, false, true);
                return;
            }
            float minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (pos, entity) in entityRecord)
            {
                var x = pos.x;
                var y = pos.y;
                minX = Mathf.Min(x, minX);
                minY = Mathf.Min(y, minY);
                maxX = Mathf.Max(x, maxX);
                maxY = Mathf.Max(y, maxY);
            }
            var remove = tileRecord.Where(x => x.Value.Tiles.All(v => !v.Value)).Select(x => x.Key).ToArray();
            foreach (var r in remove)
                tileRecord.Remove(r);
            foreach (var (pos, tile) in tileRecord)
            {
                var x = pos.x;
                var y = pos.y;
                minX = Mathf.Min(x, minX);
                minY = Mathf.Min(y, minY);
                maxX = Mathf.Max(x, maxX);
                maxY = Mathf.Max(y, maxY);
            }
            int oriX = (int)minX;
            int oriY = (int)minY;
            List<EntityInfo> entityInfos = new();
            bool any = false;
            foreach (var (pos, entity) in entityRecord)
            {
                var info = entity;
                var origin = info.Position = new(pos.x - oriX, pos.y - oriY);
                var list = info.Entities = new();
                foreach (var e in entity.Entities)
                {
                    var ori = e;
                    ori.Position = origin;
                    list.Add(ori);
                    any = true;
                }
                entityInfos.Add(info);
            }
            List<SerializeTileInfo> tileInfos = new();
            var except = SelectHandler.ExceptTiles;
            foreach (var (pos, tile) in tileRecord)
            {
                SerializeTileInfo info = new()
                {
                    Position = pos,
                };
                var tiles = info.Tiles = new();
                info.Position = new(pos.x - oriX, pos.y - oriY);
                foreach (var (t, state) in tile.Tiles)
                {
                    if (!state)
                        continue;
                    if (except.Contains(t.tileType))
                        continue;
                    tiles.Add(t);
                    any = true;
                }
                tileInfos.Add(info);
            }
            if (!any)
            {
                CombatText.SpawnCombatText(PopKey, CombatText.NumberColor.White, pop, false, false, true);
                return;
            }
            SavesHandler.gameObject.SetActive(true);
            SavesHandler.Save(new()
            {
                EntityInfos = entityInfos,
                TileInfos = tileInfos,
                Size = new((int)(maxX - minX), (int)(maxY - minY)),
            });
            ClearRecord();
        }
        public void Destory()
        {
            var player = Manager.main.player;
            if (player.guestMode || BuildingBlueprint.DestoryPrivileges.Value && player.adminPrivileges <= 0)
            {
                StartCoroutine(DestoryWarning());
                return;
            }
            var pe = player.entity;
            var pop = player.RenderPosition + Vector3.up * 0.7f;
            var sys = BuildingPlaceClient.Ins;
            if (entityRecord.Count == 0 && tileRecord.Count == 0)
            {
                CombatText.SpawnCombatText(PopKey, CombatText.NumberColor.White, pop, false, false, true);
                return;
            }
            var remove = tileRecord.Where(x => x.Value.Tiles.All(v => !v.Value)).Select(x => x.Key).ToArray();
            foreach (var r in remove)
                tileRecord.Remove(r);
            bool any = false;
            foreach (var entity in entityRecord.Values)
            {
                foreach (var e in entity.Entities)
                {
                    sys.Destory(e, pe);
                    any = true;
                }
            }
            var except = SelectHandler.ExceptTiles;
            foreach (var (pos, tile) in tileRecord)
            {
                foreach (var (t, state) in tile.Tiles.Reverse())
                {
                    if (!state)
                        continue;
                    if (except.Contains(t.tileType))
                        continue;
                    sys.Destory(t, pos, pe);
                    any = true;
                }
            }
            if (!any)
            {
                CombatText.SpawnCombatText(PopKey, CombatText.NumberColor.White, pop, false, false, true);
            }
            ClearRecord();
        }
        private void ClearRecord()
        {
            entityRecord.Clear();
            entityPreview.Clear();
            tileRecord.Clear();
            tilePreview.Clear();
            DeactiveExcessSlot();
            InfoContainer.SetActive(false);
            HoverMark.gameObject.SetActive(false);
        }
        private SpriteRenderer GetOrCreateSlot(int index)
        {
            if (index >= marks.Count)
                marks.Add(Instantiate(MarkTemplate, MarkContainer));
            marks[index].gameObject.SetActive(true);
            return marks[index];
        }
        private void DeactiveExcessSlot(int index = 0)
        {
            for (int i = index; i < marks.Count; i++)
            {
                marks[i].gameObject.SetActive(false);
            }
        }
        public void SwitchBuildingList()
        {
            if (SavesHandler.gameObject.activeSelf)
            {
                SavesHandler.gameObject.SetActive(false);
                InfoContainer.SetActive(true);
            }
            else
            {
                SavesHandler.gameObject.SetActive(true);
                InfoContainer.SetActive(false);
                HoverMark.gameObject.SetActive(false);
                SavesHandler.RenderSaves();
            }
            ExitPlaceMode();
        }
        public void CloseBuildingList()
        {
            if (SavesHandler.gameObject.activeSelf)
            {
                SavesHandler.gameObject.SetActive(false);
                InfoContainer.SetActive(true);
            }
        }
        public void SelectBuilding()
        {
            SavesHandler.gameObject.SetActive(false);
            PreviewWindow.gameObject.SetActive(true);
            PlaceHanlder.gameObject.SetActive(true);
            var building = SavesHandler.Current;
            PreviewWindow.Refresh(building);
            PlaceHanlder.RefreshPreview(building);
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.Place, 1);
            Exit.SetActive(true);
            SelectHandler.gameObject.SetActive(false);
        }
        public void ExitPlaceMode()
        {
            PreviewWindow.gameObject.SetActive(false);
            PlaceHanlder.gameObject.SetActive(false);
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.Place, 0);
            Exit.SetActive(false);
            SelectHandler.gameObject.SetActive(true);
        }
        public void ShowInfoContainer()
        {
            InfoContainer.SetActive(true);
            SavesHandler.gameObject.SetActive(false);
            PreviewWindow.gameObject.SetActive(false);
            PlaceHanlder.gameObject.SetActive(false);
        }
        private IEnumerator DestoryWarning()
        {
            for (int i = 0; i < 3; i++)
            {
                DestoryMark.SetActive(true);
                yield return waitForSeconds0_1;
                DestoryMark.SetActive(false);
                yield return waitForSeconds0_1;
            }
        }
    }
}
