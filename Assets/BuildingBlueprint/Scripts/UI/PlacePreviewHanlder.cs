using Assets.BuildingBlueprint.Scripts.Core;
using Assets.BuildingBlueprint.Scripts.Systems;
using Pug.UnityExtensions;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.UI
{
    public class PlacePreviewHanlder : MonoBehaviour
    {
        public Sprite Entity;
        public Sprite Tile;
        public SpriteRenderer Template;
        public Transform Camera;
        public Transform MarkContainer;
        public PlacePivot Pivot;

        private List<SpriteRenderer> marks;
        private BuildingInfo current;
        private void Awake()
        {
            marks = new()
            {
                Template
            };
        }
        public void RefreshPreview(BuildingInfo info)
        {
            int i = 0;
            current = info;

            // 计算pivot偏移量
            int2 pivot = GetPivotOffset(info);

            foreach (var entity in info.EntityInfos)
            {
                var pos = entity.Position;
                var x = pos.x;
                var y = pos.y;
                foreach (var e in entity.Entities)
                {
                    var offset = new Vector2(e.X - 1, e.Y - 1) / 2f;
                    Vector2 size = new(e.X, e.Y);
                    var slot = GetOrCreateSlot(i++);
                    slot.sprite = Entity;
                    slot.size = size;
                    slot.transform.localPosition = new Vector3(x + offset.x + pivot.x, y + offset.y + pivot.y, 0);
                }
            }
            foreach (var tile in info.TileInfos)
            {
                var pos = tile.Position;
                var slot = GetOrCreateSlot(i++);
                slot.sprite = Tile;
                slot.size = Vector2.one;
                slot.transform.localPosition = new Vector3(pos.x + pivot.x, pos.y + pivot.y, 0);
            }
            DeactiveExcessSlot(i);
        }

        private int2 GetPivotOffset(BuildingInfo info)
        {
            int w = -info.Size.x;
            int h = -info.Size.y;
            int halfW = w / 2;
            int halfH = h / 2;

            return Pivot switch
            {
                PlacePivot.BottomLeft => new int2(0, 0),
                PlacePivot.BottomCenter => new int2(halfW, 0),
                PlacePivot.BottomRight => new int2(w, 0),
                PlacePivot.LeftCenter => new int2(0, halfH),
                PlacePivot.Center => new int2(halfW, halfH),
                PlacePivot.RightCenter => new int2(w, halfH),
                PlacePivot.TopLeft => new int2(0, h),
                PlacePivot.TopCenter => new int2(halfW, h),
                PlacePivot.TopRight => new int2(w, h),
                _ => int2.zero,
            };
        }

        private SpriteRenderer GetOrCreateSlot(int index)
        {
            if (index >= marks.Count)
                marks.Add(Instantiate(Template, MarkContainer));
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
        private void Update()
        {
            var camera = -Manager.camera.smoothedCameraPosition;
            Camera.localPosition = new(camera.x, camera.z, 0);
            var mouse = EntityMonoBehaviour.ToWorldFromRender(Manager.ui.mouse.GetMouseGameViewPosition()).ToFloat2().RoundToInt2();
            MarkContainer.localPosition = new(mouse.x, mouse.y, 0);
            if (Manager.ui.currentSelectedUIElement)
                return;
            if (Input.GetMouseButtonDown(1))
            {
                BlueprintUI.Ins.ExitPlaceMode();
                return;
            }
            if (!Input.GetMouseButtonDown(0))
                return;
            BuildingPlaceClient.Ins.Place(current, mouse + GetPivotOffset(current));
        }

        public void SwitchPivot(UIPlacePivot go)
        {
            Pivot = go.Pivot;
            RefreshPreview(current);
        }
    }
}
