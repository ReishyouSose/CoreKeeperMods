using Assets.BuildingBlueprint.Scripts.Core;
using System.Collections.Generic;
using static PugDatabase;

namespace Assets.BuildingBlueprint.Scripts.UI
{
    public class UIBuildingName : UIInputBox, IOverrideHoverMaterialVariation
    {
        private UIBuildingInfo info;
        private List<int> variations;
        private void Start()
        {
            info = GetComponentInParent<UIBuildingInfo>();
        }
        public override List<MaterialInfo> GetRequiredMaterials(bool isRepairing, bool isReinforcing)
        {
            return info.Info.GetTooltipMaterials(out variations);
        }
        public override List<TextAndFormatFields> GetHoverDescription()
        {
            var lines = new List<TextAndFormatFields>
            {
                new()
                {
                   text = "BuildingBlueprint/MaterialTipDesc",
                },
                new()
                {
                    text = info.Info.Description,
                    dontLocalize = true
                }
            };
            // Item kinds that did not fit in the tooltip's rows; no words, so it needs no translation
            int hidden = info.Info.GetHiddenTooltipCount();
            if (hidden > 0)
                lines.Add(new() { text = $"+{hidden} …", dontLocalize = true });
            return lines;
        }
        public override bool ShowRequiredMaterialsAmountNumberColor() => true;

        public int GetOverrideVariation(int index) => variations[index];
    }
}
