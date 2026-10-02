using Assets.GeneralConfigMenu.Scripts.ConfigTags;
using CoreLib.Data.Configuration;
using I2.Loc;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.GeneralConfigMenu.Scripts
{
    [RequireComponent(typeof(BoxCollider))]
    public class UIConfigEntry : ButtonUIElement
    {
        public PugText Name;
        public SpriteRenderer SR;
        public SpriteRenderer Hover;
        public Transform Container;
        public GameObject Active;
        public GameObject Inactive;
        public Transform Offset;
        public int Hierarchy { get; private set; }

        [HideInInspector]
        public UIConfigValueBox ServerBox, ClientBox;

        [HideInInspector]
        public ConfigEntryBase Entry;

        [HideInInspector]
        public List<UIConfigEntry> Additional = new();

        [HideInInspector]
        public UIConfigPage OwnerPage;

        [HideInInspector]
        public UIPermissionButton PermissionButton;

        [HideInInspector]
        public ConfigScope Scope;

        private LinearLayoutUIComponent layout;
        private bool expand;

        protected override void Awake()
        {
            layout = GetComponentInParent<LinearLayoutUIComponent>();
            if (Additional.Count > 0)
            {
                expand = true;
                showHoverTitle = true;
            }
            Inactive.SetActive(false);
            base.Awake();
        }

        public override List<TextAndFormatFields> GetHoverDescription()
        {
            return base.GetHoverDescription();
        }

        public void BindEntry(ConfigEntryBase entry, ConfigTemplate template, int hierarchy = 0)
        {
            Entry = entry;
            var def = entry.Definition;
            var key = def.Key;
            var local = entry.Description.Tags.FirstOrDefault(x => x is LocalizationOverride) is LocalizationOverride lfx ? lfx.Key :
                MiscHelper.GetLocalKey(entry.ConfigFile.ConfigFilePath, def.Section, key);
            name = "Entry " + key;
            Name.SetText(local, key);
            Scope = entry.Scope;
            Hierarchy = hierarchy;
            AdjustByHierarchy(hierarchy);

            PermissionButton = Instantiate(Scope.accessLevel switch
            {
                ConfigAccessLevel.Admin => template.Admin,
                ConfigAccessLevel.Server => template.Server,
                ConfigAccessLevel.Client => template.Client,
                _ => template.ViewOnly
            }, Offset);
            PermissionButton.transform.localPosition = new(0.5f, -1.5f, 0);

            string desc = local + "Desc";
            if (LocalizationManager.TryGetTranslation(desc, out _))
            {
                showHoverDesc = true;
                optionalHoverDesc = new() { mTerm = desc };
            }
            if (Scope.requireReload)
                Instantiate(template.Reload, Offset).localPosition = new(1.5f, -1.5f, 0);

            MatchValueBox(template);
        }

        public void ResetToDefault(bool server)
        {
            UIConfigValueBox box;
            if (server)
            {
                if (!Scope.ShouldSync)
                    return;
                if (!ServerBox.Editable)
                    return;
                box = ServerBox;
            }
            else
                box = ClientBox;
            if (box)
                box.ApplyUserChange(Entry.DefaultValue.ToString());
        }

        public void OnReceivedSync(string value) => ServerBox.OnReceiveSync(value);

        private void AdjustByHierarchy(int hierarchy)
        {
            float originalWidth = 23f;
            float newWidth = originalWidth - hierarchy;
            float x = newWidth / 2f;

            if (TryGetComponent<WrapperUIComponent>(out var wrapper))
            {
                wrapper.renderWidthPixels = (int)(newWidth * 16);
            }

            Vector3 pos = Vector3.zero;
            pos.x = hierarchy;
            Offset.localPosition = pos;

            pos = Container.localPosition;
            pos.x -= hierarchy;
            Container.localPosition = pos;

            if (TryGetComponent<BoxCollider>(out var boxCollider))
            {
                Vector3 size = boxCollider.size;
                size.x = newWidth;
                boxCollider.size = size;

                Vector3 center = boxCollider.center;
                center.x = x;
                boxCollider.center = center;
            }

            void SetSR(SpriteRenderer sr)
            {
                Vector2 size = sr.size;
                size.x = newWidth;
                sr.size = size;

                var trans = sr.transform;
                Vector3 localPos = trans.localPosition;
                localPos.x = x;
                trans.localPosition = localPos;
            }
            SetSR(SR);
            SetSR(Hover);
        }

        private void MatchValueBox(ConfigTemplate template)
        {
            ClientBox = SelectBox(template);
            ClientBox.transform.localPosition = new(0, 0, 0);
            ClientBox.BindEntry(this, false);

            switch (Scope.accessLevel)
            {
                case ConfigAccessLevel.Admin:
                case ConfigAccessLevel.Server:
                    ServerBox = SelectBox(template);
                    ServerBox.BindEntry(this, true);
                    ServerBox.transform.localPosition = new(-8.25f, 0, 0);
                    break;
                case ConfigAccessLevel.ViewOnly:
                    ClientBox.Editable = false;
                    break;
            }
        }
        private UIConfigValueBox SelectBox(ConfigTemplate template)
        {
            if (Entry.SettingType == typeof(bool))
                return Instantiate(template.Bool, Container);
            else if (TryMatchListType(template.List, out var box))
                return box;
            return Instantiate(template.Input, Container);
        }

        private bool TryMatchListType(UIConfigValueList list, out UIConfigValueBox box)
        {
            string[] accepts = null;

            if (Entry.SettingType.IsEnum)
            {
                var enums = Enum.GetValues(Entry.SettingType);
                accepts = new string[enums.Length];
                int index = 0;
                foreach (var value in enums)
                {
                    accepts[index++] = value.ToString();
                }
            }
            else if (MiscHelper.TryExtractAcceptableValues(Entry, out string[] values))
            {
                accepts = values;
            }

            if (accepts == null)
            {
                box = null;
                return false;
            }

            var valueList = Instantiate(list, Container);
            valueList.SetAccepts(accepts);
            box = valueList;
            return true;
        }

        public void SwitchExpandState()
        {
            if (Additional.Count == 0)
                return;
            expand = !expand;
            Active.SetActive(expand);
            Inactive.SetActive(!expand);
            foreach (var entry in Additional)
                entry.gameObject.SetActive(expand);
            layout.RenderUIComponent(true);
            if (expand)
                Manager.menu.AttemptToPlayMenuSfx(SfxID.FIXME_menu_select, 0.6f, 0f, reuse: false);
            else
                AudioManager.SfxUI(SfxID.FIXME_menu_select, 0.4f, false, 1f, 0f, true, true, 0f);
        }

        public void OnPermissionChange(PermissionLevel level)
        {
            var accessLevel = Scope.accessLevel;
            if (!ServerBox)
                return;
            ServerBox.Editable = level switch
            {
                PermissionLevel.LockServer => accessLevel < ConfigAccessLevel.Server,
                PermissionLevel.LockAdmin => accessLevel < ConfigAccessLevel.Admin,
                _ => true,
            };
        }

        public void TransferValue(bool server)
        {
            UIConfigValueBox source, target;
            if (!Scope.ShouldSync)
                return;
            if (server)
            {
                if (!ServerBox.Editable)
                    return;
                source = ClientBox;
                target = ServerBox;
            }
            else
            {
                source = ServerBox;
                target = ClientBox;
            }
            target.ApplyUserChange(source.ValidValue.ToString());
        }
        public void ShowUnEditableWarning()
        {
            if (!ModConfigMenu.Instance.ShowIfIsAdminOnlyWarning(Scope.accessLevel))
                PermissionButton.ShowUnEditableWarning();
        }
    }
}