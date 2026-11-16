using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Core;
using Assets.BuildingBlueprint.Scripts.Enums;
using Assets.BuildingBlueprint.Scripts.Systems;
using PugTilemap;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.UI
{
    public class SelectHandler : MonoBehaviour
    {
        public GameObject EntityIcon;
        public GameObject TileIcon;
        public Transform ModeMark;
        public Transform OpMark;
        public GameObject OperatorContainer;
        public GameObject BoxContainer;
        public GameObject ClickContainer;
        public GameObject TileFilterButton;
        public UILinearLayoutContainer TileFilterContainer;
        public LinearLayoutUIComponent FilterLayout;
        public UITileTarget FilterTemplate;

        [HideInInspector]
        public HashSet<TileType> ExceptTiles;
        [HideInInspector]
        public SelectionLayer Layer;
        [HideInInspector]
        public SelectionMode Mode;
        [HideInInspector]
        public int Op;
        [HideInInspector]
        public HashSet<TileType> TileTarget;

        [HideInInspector]
        public SelectionOptionCD Option;

        private bool rendered;

        private void Awake()
        {
            OperatorContainer.SetActive(false);
            OperatorContainer.transform.localPosition = new(0, 0, 0);
            TileTarget = new();
            Mode = SelectionMode.Check;
            TileFilterButton.SetActive(false);
            TileFilterContainer.gameObject.SetActive(false);
            FilterTemplate.gameObject.SetActive(false);
            var container = TileFilterContainer.GetComponent<UIScrollWindow>().scrollingContent.GetChild(0);
            Array array = Enum.GetValues(typeof(TileType));
            ExceptTiles = new();
            foreach (var f in array)
                ExceptTiles.Add((TileType)f);
            if (ScriptableData.TryGetDataBlocks<TileTargetDataBlock>(out var dataBlocks))
            {
                var datas = TileTargetDataBlock.GetSortedArray(dataBlocks);
                int i = 0;
                foreach (var dataBlock in datas)
                {
                    var filter = dataBlock.TileType;
                    var f = Instantiate(FilterTemplate, container);
                    f.TileType = filter;
                    f.Index = i++;
                    f.gameObject.SetActive(true);
                    ExceptTiles.Remove(filter);
                }
            }
        }
        public void ChangeSelectionLayer()
        {
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.Layer, (int)(Layer == SelectionLayer.Tile ? SelectionLayer.Entity : SelectionLayer.Tile));
        }
        public void ChangeSelectionMode(GameObject button)
        {
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.Mode, (int)button.GetComponent<UISelectionMode>().Mode);
        }

        public void ChangeBoxOp(GameObject button)
        {
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.Operator, (int)button.GetComponent<UIBoxOperator>().Op);
        }

        public void ChangeClickOp(GameObject button)
        {
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.Operator, (int)button.GetComponent<UIClickOperator>().Op);
        }

        public void ChangeTileFilter(UITileTarget button)
        {
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.TileTarget, button.Index);
        }
        public void ChangeTileFilter(int mode)
        {
            BlueprintStateChangeClient.SwitchState(BlueprintUIAction.TileTarget, mode);
        }
        public void SwitchTileFilter()
        {
            TileFilterContainer.gameObject.SetActive(!TileFilterContainer.gameObject.activeSelf);
            if (rendered)
                return;
            rendered = true;
            FilterLayout.RenderUIComponent(true);
        }
        private void Update()
        {
            var oLayer = Option.Layer;
            if (oLayer != Layer)
            {
                bool entity = oLayer == SelectionLayer.Entity;
                EntityIcon.SetActive(entity);
                TileIcon.SetActive(!entity);
                Layer = oLayer;
                OperatorContainer.transform.localPosition = new(entity ? 0 : -1.25f, 0, 0);
                TileFilterButton.SetActive(!entity);
                TileFilterContainer.gameObject.SetActive(false);
                BlueprintUI.Ins.SwitchPreview(oLayer);
            }
            var oMode = Option.Mode;
            if (oMode != Mode)
            {
                var pos = ModeMark.localPosition;
                pos.x = ((int)oMode - 2) * 1.25f;
                ModeMark.localPosition = pos;
                OperatorContainer.SetActive(true);
                switch (oMode)
                {
                    case SelectionMode.Box:
                        BoxContainer.SetActive(true);
                        ClickContainer.SetActive(false);
                        break;
                    case SelectionMode.Click:
                        ClickContainer.SetActive(true);
                        BoxContainer.SetActive(false);
                        break;
                    default:
                        OperatorContainer.SetActive(false);
                        break;
                }
                Mode = oMode;
                Op = -1;
            }
            var op = Option.Operator;
            if (op != Op)
            {
                Op = op;
                var pos = OpMark.localPosition;
                pos.x = (op - (oMode == SelectionMode.Box ? 4 : 2)) * 1.25f;
                OpMark.localPosition = pos;
            }
        }
        public void RefreshTileTarget(DynamicBuffer<TileTargetStateBuffer> buffer)
        {
            if (!TileFilterContainer.gameObject.activeInHierarchy)
                return;
            foreach (Transform filter in TileFilterContainer.Layout.transform)
            {
                var f = filter.GetComponent<UITileTarget>();
                bool state = buffer[f.Index].State;
                f.Enable.SetActive(state);
                f.Disable.SetActive(!state);
            }
        }
    }
}
