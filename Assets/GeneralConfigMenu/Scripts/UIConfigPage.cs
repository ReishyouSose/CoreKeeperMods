using CoreLib.Data.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.GeneralConfigMenu.Scripts
{
    public class UIConfigPage : MonoBehaviour
    {
        public Transform ContentContainer;

        [HideInInspector]
        public bool IsCombinePage;

        [HideInInspector]
        public ConfigFile ConfigFile;

        [HideInInspector]
        public PermissionLevel WaitForCheckLevel;

        private readonly Dictionary<UIConfigEntry, string> serverChanges = new();
        private readonly Dictionary<UIConfigEntry, string> clientChanges = new();
        private void Update()
        {
            if (WaitForCheckLevel != PermissionLevel.None)
            {
                CheckPermission(WaitForCheckLevel);
                WaitForCheckLevel = PermissionLevel.None;
            }
        }
        public void OnPageOpen()
        {
            bool isInGame = Manager.networking.isConnected;
            foreach (var entry in GetAllEntries())
            {
                entry.ClientBox.SetRecord();
                if (!entry.ServerBox)
                    continue;
                entry.ServerBox.SetRecord();
                entry.ServerBox.gameObject.SetActive(isInGame);
            }
        }

        public void OnPageExit()
        {
            if (clientChanges.Count > 0)
            {
                foreach (var (ue, _) in clientChanges)
                {
                    ue.ClientBox.ClearRecord();
                }
                clientChanges.Clear();
            }
            if (serverChanges.Count > 0)
            {
                foreach (var (ue, _) in serverChanges)
                {
                    ue.ServerBox.ClearRecord();
                }
                serverChanges.Clear();
            }
            HandleChanges();
        }

        /// <summary>
        /// 获取当前页所有配置条目
        /// </summary>
        private IEnumerable<UIConfigEntry> GetAllEntries()
        {
            foreach (Transform child in ContentContainer)
            {
                if (child.TryGetComponent<UIConfigEntry>(out var entry))
                    yield return entry;
            }
        }

        public void ApplyAllChanges()
        {
            if (clientChanges.Count > 0)
            {
                Dictionary<ConfigEntryBase, string> revert = new();
                foreach (var (ue, value) in clientChanges)
                {
                    var entry = ue.Entry;
                    if (ue.Scope.ShouldSync)
                        revert.Add(entry, entry.GetSerializedValue());
                    entry.SetSerializedValue(value);
                    ue.ClientBox.SetRecord();
                }
                ConfigFile.Save();
                foreach (var (entry, value) in revert)
                {
                    entry.SetSerializedValue(value);
                }
                clientChanges.Clear();
            }
            if (serverChanges.Count > 0)
            {
                var list = GeneralConfigMenuMod.Sync.SendConfigChange();
                var menu = ModConfigMenu.Instance;
                var mapper = menu.Mapper;
                bool listenHost = Manager.ecs != null && Manager.ecs.ServerWorld != null;
                foreach (var (ue, value) in serverChanges)
                {
                    ue.ServerBox.SetRecord();
                    if (listenHost)
                        ue.Entry.SetSerializedValue(value);
                    list.Add((mapper[ue.Entry].id, value));
                }
                serverChanges.Clear();
                menu.SetWaitState(true);
            }
            HandleChanges();
        }

        public void RevertAllChanges()
        {
            if (clientChanges.Count > 0)
            {
                foreach (var (ue, _) in clientChanges)
                {
                    ue.ClientBox.ClearRecord();
                    ue.ClientBox.SetRecord();
                }
                clientChanges.Clear();
            }
            if (serverChanges.Count > 0)
            {
                foreach (var (ue, _) in serverChanges)
                {
                    ue.ServerBox.ClearRecord();
                    ue.ServerBox.SetRecord();
                }
                serverChanges.Clear();
            }
            HandleChanges();
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetAll(bool server)
        {
            foreach (var entry in GetAllEntries())
            {
                entry.ResetToDefault(server);
            }
        }

        /// <summary>
        /// 将服务器状态保存到本地
        /// </summary>
        public void TransferAllValues(bool server)
        {
            string source = "Server", target = "Client";
            if (server)
                (source, target) = (target, source);
            Debug.Log($"Try Transfer from {source} to {target}");
            foreach (var entry in GetAllEntries())
            {
                entry.TransferValue(server);
            }
        }

        /// <summary>
        /// 设置更改权限
        /// </summary>
        public void CheckPermission(PermissionLevel level)
        {
            foreach (var entry in GetAllEntries())
            {
                entry.OnPermissionChange(level);
            }
        }

        public void AddChange(bool server, UIConfigEntry ue, string value)
        {
            Debug.Log($"Apply {(server ? "Server" : "Client")} change {ue.Entry.GetFullName()}, {value}");
            (server ? serverChanges : clientChanges)[ue] = value;
            HandleChanges();
        }

        public void RemoveChange(bool server, UIConfigEntry ue)
        {
            if ((server ? serverChanges : clientChanges).Remove(ue))
            {
                Debug.Log($"Remove {(server ? "Server" : "Client")} change {ue.Entry.GetFullName()}");
            }
            HandleChanges();
        }
        private void HandleChanges()
        {
            ModConfigMenu.Instance.SetWaitForChangeCount(clientChanges.Count + serverChanges.Count);
        }
    }
}