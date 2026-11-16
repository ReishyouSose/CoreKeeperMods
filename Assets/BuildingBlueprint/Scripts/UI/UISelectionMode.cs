using Assets.BuildingBlueprint.Scripts.Enums;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.UI
{
    [RequireComponent(typeof(ButtonUIElement))]
    public class UISelectionMode : MonoBehaviour
    {
        public SelectionMode Mode;
        private void Awake()
        {
            var button = GetComponent<ButtonUIElement>();
            button.showHoverTitle = true;
            button.optionalTitle = new()
            {
                mTerm = BuildingBlueprint.Key + $"Mode/{Mode}"
            };
            button.optionalHoverDesc = new()
            {
                mTerm = BuildingBlueprint.Key + $"Mode/{Mode}Desc"
            };
        }
    }
}
