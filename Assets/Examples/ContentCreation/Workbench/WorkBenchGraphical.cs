using UnityEngine;

public class WorkBenchGraphical : CraftingBuilding
{
    public SpriteRenderer SRShadow;
    private Sprite defaultShadowSprite;
    public Transform particleSpawnLocation;
    private bool isWideCraftingBuilding => this is WorkBenchGraphical;

    protected override void Awake()
    {
        base.Awake();
        if (SRShadow)
            defaultShadowSprite = SRShadow.sprite;
    }

    public override void UpdateGraphicsFromObjectInfo(ObjectInfo info)
    {
        // Change shadow sprite if it's an anvil
        if (SRShadow)
        {
            if (!isWideCraftingBuilding && info.additionalSprites.Count > 0)
                SRShadow.sprite = info.additionalSprites[0];
            else
                SRShadow.sprite = defaultShadowSprite;
        }
        base.UpdateGraphicsFromObjectInfo(info);
    }

    protected override void OnDeath()
    {
        base.OnDeath();
        Manager.effects.PlayPuff(PuffID.DirtBlockDust, particleSpawnLocation.position, 5);
    }
}