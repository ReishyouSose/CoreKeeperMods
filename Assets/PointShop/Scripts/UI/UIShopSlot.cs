using Assets.PointShop.Scripts.Systems;
using CommandMinion;
using Pug.Automation;
using PugMod;
using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Entities;
using UnityEngine;
using static PugDatabase;

namespace Assets.PointShop.Scripts.UI
{
    public class UIShopSlot : ButtonUIElement
    {
        #region 本地化键
        private const string WEAPON_MELEE_DAMAGE = "meleeWeapon";
        private const string WEAPON_RANGE_DAMAGE = "rangeWeapon";
        private const string PET_MELEE_DAMAGE = "weaponMeleeDamage";
        private const string PET_RANGE_DAMAGE = "weaponRangeDamage";
        private const string MAGIC_WEAPON_DAMAGE = "magicWeaponDamage";
        private const string PHYSICAL_WEAPON_DAMAGE = "physicalWeaponDamage";
        private const string DAMAGE = "damage";
        private const string WEAPON_SPEED = "WeaponSpeed";
        private const string EXPLOSIVE_DAMAGE = "explosiveDamage";
        private const string MINING_DAMAGE = "ConditionEffect/Mining";
        private const string ADDITIONAL_SLOTS = "additionalSlotsStat";
        private const string ADDITIONAL_SLOTS_POUCH = "additionalSlotsPouchStat";
        private const string PIERCES = "piercingProjectiles";
        private const string BOUNCES = "bouncingProjectiles";
        private const string OFFHAND_USE = "offHandUse";
        private const string OFFHAND_MECHANIC_PREFIX = "OffHandMechanics/";
        private const string COOLDOWN_SEC = "CooldownSec";
        private const string COOLDOWN_MIN = "CooldownMin";
        private const string COOLDOWN_MIN_SEC = "CooldownMinSec";
        private const string MANA_COST = "manaCost";
        private const string WEAPON_SECONDARY = "weaponSecondary";
        private const string WEAPON_SECONDARY_CATEGORY = "WeaponSecondary/";
        private const float DAMAGE_VARIATION = 0.1f;
        #endregion

        [HideInInspector] public ObjectData objectData;
        [HideInInspector] public int Price;
        [HideInInspector] public int Zone;
        [HideInInspector] public ObjectID Boss;
        [HideInInspector] public ObjectID Currency;

        public SpriteRenderer ItemIcon;
        public SpriteRenderer Border;
        public PugText Amount;
        public PugText AmountShadow;

        private ContainedObjectsBuffer objectBuffer;

        public override void OnLeftClicked(bool mod1, bool mod2)
        {
            base.OnLeftClicked(mod1, mod2);
            var player = Manager.main.player;
            if (player == null)
                return;
            PointShopClient.TryBuyItem(player.entity, objectData, Boss, Currency, Price, player.inputModule.IsButtonCurrentlyDown(PlayerInput.InputType.PICK_UP_10));
            AudioManager.Sfx(SfxID.twitch, player.transform.position, 0.1f, 0.55f, 0.1f, true);
        }
        public void SetLimit(int zone, ObjectID boss)
        {
            Zone = zone;
            Boss = boss;
        }
        public void SetItem(ObjectData objData, int sellPrice, ObjectID currency = ObjectID.None)
        {
            ObjectID id = objData.objectID;
            if (id == ObjectID.None)
            {
                return;
            }

            var info = GetObjectInfo(id, objData.variation);
            if (info == null)
                return;
            objectData = objData;
            objectBuffer = new ContainedObjectsBuffer { objectData = objData };
            Price = sellPrice;
            Currency = currency;
            var color = Manager.ui.GetSlotBorderRarityColor(info.rarity, false, Color.white);
            if (info.rarity == Rarity.Common)
                color = Color.white;
            Border.color = color;
            ItemIcon.sprite = info.icon;
            Vector2 offset = info.iconOffset;
            offset.x += 0.625f;
            offset.y -= 0.625f;
            ItemIcon.transform.localPosition = offset;

            if (objData.amount > 1)
            {
                string amount = objData.amount.ToString();
                AmountShadow.Render(amount, false, true);
                Amount.Render(amount, false, true);
            }
        }
        public override ContainedObjectsBuffer GetContainedObject() => objectBuffer;

        public override TextAndFormatFields GetHoverTitle()
        {
            var nameInfo = PlayerController.GetObjectName(objectBuffer, false);
            nameInfo.color = Manager.text.GetRarityColor(GetObjectRarity(objectData));
            return nameInfo;
        }
        public override HoverTitleIconType GetHoverTitleIconType()
        {
            ObjectInfo objectInfo = GetObjectInfo(objectData.objectID);
            if (objectInfo != null)
            {
                if (objectInfo.objectType == ObjectType.Eatable)
                {
                    return HoverTitleIconType.Edible;
                }

                if (objectInfo.objectType == ObjectType.Valuable)
                {
                    return HoverTitleIconType.Valuable;
                }

                if (objectInfo.objectType == ObjectType.KeyItem)
                {
                    return HoverTitleIconType.Key;
                }
            }

            return base.GetHoverTitleIconType();
        }

        public override bool GetDurabilityOrFullnessOrXp(out int durability, out int maxDurability, out AmountType amountType)
        {
            durability = 0;
            maxDurability = 0;
            amountType = AmountType.Durability;

            var slotObject = objectBuffer;
            if (slotObject.objectID == ObjectID.None)
                return false;

            if (TryGetComponent(objectData, out DurabilityCD dura))
            {
                maxDurability = dura.maxDurability;
                durability = maxDurability;
                return true;
            }

            if (TryGetComponent(objectData, out FullnessCD fullness))
            {
                amountType = AmountType.Fullness;
                maxDurability = fullness.maxFullness;
                return true;
            }

            return false;
        }

        public override List<TextAndFormatFields> GetHoverDescription()
        {
            var slotObject = objectBuffer;
            var id = slotObject.objectID;
            if (id == ObjectID.None)
                return null;
            ObjectID displayId = PlayerController.GetAnyObjectIDReplaceForNameAndDesc(id);
            if (!API.Authoring.ObjectProperties.TryGetPropertyString(displayId, "name", out var itemName))
            {
                itemName = displayId.ToString();
            }

            string nameOverride = Manager.ui.itemOverridesTable.GetNameTermOverride(slotObject.objectData);
            if (nameOverride != null)
                itemName = nameOverride;
            return new List<TextAndFormatFields>
            {
                new() { text = $"Items/{itemName}Desc" },
                new() { text = id + $"({(int)id})", dontLocalize = true, color = Color.gray },
            };
        }

        public override List<MaterialInfo> GetRequiredMaterials(bool isRepairing, bool isReinforcing)
        {
            var player = Manager.main.player;
            if (player == null)
                return null;
            ObjectID currency = Currency == ObjectID.None ? PointShop.Coin : Currency;
            return new List<MaterialInfo> { new(currency, Price, player.playerInventoryHandler.GetExistingAmountOfObject(currency), Entity.Null, null) };
        }
        public override bool ShowRequiredMaterialsAmountNumberColor() => true;
        public override List<TextAndFormatFields> GetHoverStats(bool previewReinforced)
        {
            var containedObject = objectBuffer;
            var objectData = this.objectData;

            var result = new List<TextAndFormatFields>();

            if (TryGetComponent(objectData, out ConsumesManaCD mana))
            {
                result.Add(new TextAndFormatFields
                {
                    text = MANA_COST,
                    formatFields = new[] { mana.manaCost.ToString() },
                    color = Manager.ui.manaTextColor
                });
            }
            if (TryGetComponent(objectData, out HasWeaponDamageCD hasWeaponDamage))
            {
                var weaponDamage = EntityUtility.GetComponentData<WeaponDamageCD>(
                    EntityUtility.GetLevelEntity(objectData), world);

                int damage = weaponDamage.damage;
                int variation = (int)(damage * DAMAGE_VARIATION);
                string damageRange = PugText.ProcessText(
                    hasWeaponDamage.isMagic ? MAGIC_WEAPON_DAMAGE : PHYSICAL_WEAPON_DAMAGE,
                    new[] { (damage - variation).ToString(), (damage + variation).ToString() },
                    true, false);

                result.Add(new TextAndFormatFields
                {
                    text = hasWeaponDamage.isRange ? WEAPON_RANGE_DAMAGE : WEAPON_MELEE_DAMAGE,
                    formatFields = new[] { damageRange },
                    color = Color.white,
                    additionalTextColor = Manager.ui.previewReinforcedColor
                });

                if (TryGetComponent(objectData, out RangeWeaponCD rangeWeaponCD))
                {
                    float cooldown = TryGetComponent(objectData, out CooldownCD cooldownCD) ? cooldownCD.cooldown : 0.6f;
                    result.Add(new TextAndFormatFields
                    {
                        text = WEAPON_SPEED,
                        formatFields = new[] { (1f / cooldown).ToString("F1", CultureInfo.InvariantCulture) }
                    });

                    var projectileId = rangeWeaponCD.projectileID;

                    if (TryGetComponent<PiercingProjectileCD>(projectileId, out var piercing) && piercing.piercesEnemiesAmount > 0)
                    {
                        result.Add(new TextAndFormatFields { text = PIERCES, formatFields = Array.Empty<string>() });
                    }

                    if (TryGetComponent<BouncingProjectileCD>(projectileId, out var bouncing) &&
                        bouncing.maxBounceCount > 0 && !HasComponent<GroundBouncableProjectileCD>(projectileId))
                    {
                        result.Add(new TextAndFormatFields { text = BOUNCES, formatFields = Array.Empty<string>() });
                    }
                }
            }
            if (StatsUIUtility.HasExplosiveWeapon(objectData, out _, world) && TryGetComponent<RangeWeaponCD>(objectData, out _)
                && StatsUIUtility.HasExplosiveWeapon(objectData, out var explosive, world))
            {
                int damage = explosive.damage;

                int variation = (int)(damage * DAMAGE_VARIATION);
                result.Add(new TextAndFormatFields
                {
                    text = EXPLOSIVE_DAMAGE,
                    formatFields = new[] { (damage - variation).ToString(), (damage + variation).ToString() },
                    color = Color.white,
                    additionalTextColor = Manager.ui.previewReinforcedColor
                });

                result.Add(new TextAndFormatFields
                {
                    text = MINING_DAMAGE,
                    formatFields = new[] { explosive.tileDamage.ToString() },
                    color = Color.white,
                    additionalTextColor = Manager.ui.previewReinforcedColor
                });
            }

            if (TryGetComponent(objectData, out AttackContinuouslyCD attack))
            {
                int variation = (int)(attack.damage * DAMAGE_VARIATION);
                result.Add(new TextAndFormatFields
                {
                    text = DAMAGE,
                    formatFields = new[] { (attack.damage - variation).ToString(), (attack.damage + variation).ToString() }
                });
            }

            if (TryGetComponent(objectData, out RangeAttackStateCD rangeAttack))
            {
                int variation = (int)(rangeAttack.rangeDamage * DAMAGE_VARIATION);
                result.Add(new TextAndFormatFields
                {
                    text = WEAPON_RANGE_DAMAGE,
                    formatFields = new[] { (rangeAttack.rangeDamage - variation).ToString(), (rangeAttack.rangeDamage + variation).ToString() }
                });
            }

            if (TryGetComponent(objectData, out PugAutomationMinerConfigCD miner))
            {
                result.Add(new TextAndFormatFields
                {
                    text = MINING_DAMAGE,
                    formatFields = new[] { miner.damage.ToString() }
                });
            }

            if (EntityUtility.TryGetComponentData(EntityUtility.GetLevelEntity(objectData), world, out ExtraInventoryCD extra))
            {
                result.Add(new TextAndFormatFields
                {
                    text = extra.isPouch ? ADDITIONAL_SLOTS_POUCH : ADDITIONAL_SLOTS,
                    formatFields = new[] { extra.size.ToString() }
                });
            }
            var equipConditions = ConditionUIExtensions.GetConditionsOnEquip(objectData);
            result = AddConditionTexts(containedObject, result, equipConditions);

            var normalConditions = ConditionUIExtensions.GetConditions(objectData);
            var merged = MergeConditionsWithSameDescriptions(normalConditions);
            result = AddConditionTexts(containedObject, result, merged);
            if (TryGetComponent(objectData, out OffHandCD offhand))
            {
                if (offhand.mechanic != OffHandMechanic.None && offhand.mechanic != OffHandMechanic.Bait)
                {
                    string value = (offhand.mechanicValue * 100f).ToString("F0", CultureInfo.InvariantCulture);
                    string mechanicText = PugText.ProcessText(OFFHAND_MECHANIC_PREFIX + offhand.mechanic, new[] { value }, true, false);
                    result.Add(new TextAndFormatFields
                    {
                        text = OFFHAND_USE,
                        formatFields = new[] { mechanicText },
                        color = Color.yellow
                    });
                }
            }
            if (!HasComponent<HasWeaponDamageCD>(objectData) && TryGetComponent(objectData, out CooldownCD cooldownCD1))
            {
                float cd = cooldownCD1.cooldown;
                if (cd >= 1f)
                {
                    var format = ConditionUI.GetDurationStrings(cd, out string sec, out string min, true);
                    string textKey = COOLDOWN_SEC;
                    string[] fields;
                    switch (format)
                    {
                        case ConditionUI.DurationFormat.MIN:
                            textKey = COOLDOWN_MIN;
                            fields = new[] { min };
                            break;
                        case ConditionUI.DurationFormat.MIN_AND_SEC:
                            textKey = COOLDOWN_MIN_SEC;
                            fields = new[] { min, sec };
                            break;
                        default:
                            fields = new[] { sec };
                            break;
                    }
                    result.Add(new TextAndFormatFields { text = textKey, formatFields = fields });
                }
            }
            if (HasComponent<CommandMinionWeaponCD>(objectData))
            {
                result.Add(new TextAndFormatFields
                {
                    text = "useItemTerm",
                    formatFields = new string[1] { PugText.ProcessText("commandMinion", null, shouldLocalize: true, shouldLocalizeFormatFields: false) },
                    color = Color.yellow
                });
            }
            if (TryGetComponent(objectData, out SecondaryUseCD secondary))
            {
                if (secondary.mechanic == SecondaryUseMechanic.WindUp)
                {
                    result.Add(new TextAndFormatFields
                    {
                        text = WEAPON_SECONDARY,
                        formatFields = new[] { PugText.ProcessText(WEAPON_SECONDARY_CATEGORY + secondary.useTerm, null, true, false) },
                        color = Color.yellow
                    });
                }
                else if (secondary.mechanic == SecondaryUseMechanic.SpawnMinionWithWindup)
                {
                    foreach (var stat in MinionExtensions.GetSummonMinionStatText(secondary.minionToSpawn, objectData, WEAPON_SECONDARY, WEAPON_SECONDARY_CATEGORY, false))
                    {
                        result.Add(stat);
                    }
                }
            }
            return result.Count > 0 ? result : null;
        }

        private Rarity GetObjectRarity(ObjectDataCD objectData) =>
            GetObjectInfo(objectData.objectID, objectData.variation)?.rarity ?? Rarity.Common;

        private List<ConditionData> MergeConditionsWithSameDescriptions(List<ConditionData> conditions)
        {
            var result = new List<ConditionData>();
            foreach (var condition in conditions)
            {
                var info = Manager.ui.conditionsIconsTable.GetConditionInfo(condition.conditionID);
                int idx = result.FindIndex(c =>
                {
                    var otherInfo = Manager.ui.conditionsIconsTable.GetConditionInfo(c.conditionID);
                    return (otherInfo.useSameDescAsId != ConditionID.None || info.useSameDescAsId != ConditionID.None) &&
                           (c.conditionID == info.useSameDescAsId || condition.conditionID == otherInfo.useSameDescAsId ||
                            otherInfo.useSameDescAsId == info.useSameDescAsId);
                });

                if (idx != -1)
                {
                    var value = result[idx];
                    value.value += condition.value;
                    result[idx] = value;
                }
                else
                {
                    result.Add(condition);
                }
            }
            return result;
        }

        private List<TextAndFormatFields> AddConditionTexts(ContainedObjectsBuffer containedObject, List<TextAndFormatFields> result, IEnumerable<ConditionData> conditions)
        {
            foreach (var condition in conditions)
            {
                if (!Manager.ui.conditionsIconsTable.GetConditionInfo(condition.conditionID).skipShowingStatText)
                {
                    result.Add(ConditionUI.GetConditionTextAndFormatFields(containedObject, condition, false, false, false));
                }
            }
            return result;
        }
        private static bool TryGetComponent<T>(ObjectID objectID, out T result) where T : unmanaged, IComponentData
            => PugDatabase.TryGetComponent(objectID, out result);
        private static bool TryGetComponent<T>(ObjectDataCD objDataCD, out T result) where T : unmanaged, IComponentData
            => PugDatabase.TryGetComponent(objDataCD, out result);
    }
}