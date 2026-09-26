namespace Assets.PointShop.Scripts.UI
{
    public class ShopSwitch : ButtonUIElement
    {
        public PugText Value;
        public PugText Shadow;
        private void Update()
        {
            var player = Manager.main.player;
            if (player == null)
                return;
            string amount = player.playerInventoryHandler.GetExistingAmountOfObject(PointShop.Coin).ToString();
            Value.Render(amount, false, true);
            Shadow.Render(amount, false, true);
        }
        public override void OnLeftClicked(bool mod1, bool mod2)
        {
            base.OnLeftClicked(mod1, mod2);
            PointShop.OpenShop();
        }
    }
}
