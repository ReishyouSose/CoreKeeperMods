using Assets.GeneralConfigMenu.Scripts.DataStruct;
using CoreLib.Data.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.GeneralConfigMenu.Scripts
{
    [RequireComponent(typeof(UIScrollWindow))]
    public class ModConfigMenu : RadicalMenu, IScrollable
    {
        internal static ModConfigMenu Instance { get; private set; }
        public Transform PageContainer;
        public UIConfigPage EmptyPage;
        public ConfigTemplate Template;
        public PugText Title;
        public PugText TitleShadow;
        public GameObject FullBar;
        public UIPermissionButton AdminOnlyButton;
        public SpriteRenderer SaveIcon;
        public SpriteRenderer SaveTip;
        public ButtonUIElement SaveButton;
        public GameObject FunctionContainer;

        [HideInInspector]
        public List<(int, string)> Receive = new();

        [HideInInspector]
        public Dictionary<ConfigEntryBase, (int id, UIConfigEntry ue)> Mapper;

        private UIConfigPage filePage;
        private UIConfigPage current;
        private Transform currentContent;
        private LinearLayoutUIComponent layout;
        private UIScrollWindow scroll;
        private Coroutine currentCoroutine;
        private bool connected;
        private int permission;
        private int waitForChangeCount;
        private float tipTimer;
        private bool guest;
        private bool adminOnly;
        private const string TITLE = "GeneralConfigMenu/ModConfig";

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            scroll = GetComponent<UIScrollWindow>();
            Template.gameObject.SetActive(false);
            EmptyPage.gameObject.SetActive(false);
            AdminOnlyButton.gameObject.SetActive(false);
            SaveTip.color = Color.clear;
            FunctionContainer.SetActive(false);
            filePage = Instantiate(EmptyPage, PageContainer);
            filePage.name = "File Page";
            Mapper = new();
            var content = filePage.transform.GetChild(0);
            var list = ConfigFile.AllConfigFilesReadOnly.ToList();
            foreach (var configFile in list)
            {
                if (configFile.ConfigFilePath.StartsWith("CoreLib"))
                    continue;
                configFile.SaveOnConfigSet = false;
                foreach (var entry in configFile.Entries.Values)
                {
                    if (!entry.Scope.ShouldSync)
                        continue;
                    Mapper.Add(entry, (Mapper.Count, null));
                }
            }
            foreach (var pageInfo in CombindConfigPage.PageList.Values)
            {
                var file = pageInfo.File;
                list.Remove(file);
                var path = MiscHelper.GetLocalKey(file.ConfigFilePath);
                var page = RegisterCombinePage(pageInfo, path);
                page.IsCombinePage = true;
                page.ConfigFile = file;
                RegisterFile(file, path, content, page);
            }
            foreach (var file in list)
            {
                var path = MiscHelper.GetLocalKey(file.ConfigFilePath);
                if (path.StartsWith("CoreLib"))
                    continue;
                var page = RegisterDetails(file, path);
                page.ConfigFile = file;
                RegisterFile(file, path, content, page);
            }
            SetCurrent(filePage);
            SaveIcon.gameObject.SetActive(false);
        }
        private void Start()
        {
            SetPermission(PermissionLevel.AllowAll);
        }
        public override void Activate()
        {
            base.Activate();
            scroll.ResetScroll();
        }

        public float GetCurrentWindowHeight()
        {
            return layout.GetUIComponentRenderHeight();
        }

        public bool IsTopElementSelected()
        {
            int count = currentContent.childCount;
            if (count <= 0)
                return false;
            return currentContent.GetChild(0) == Manager.ui.currentSelectedUIElement;
        }

        public bool IsBottomElementSelected()
        {
            int index = currentContent.childCount - 1;
            if (index < 0)
                return false;
            return currentContent.GetChild(index) == Manager.ui.currentSelectedUIElement;
        }

        public void SwitchToDetail(UIConfigFile uf)
        {
            string key = uf.Key;
            var page = uf.Detail;
            Title.SetText(key, key);
            TitleShadow.SetText(key, key);
            SetCurrent(page);
            page.OnPageOpen();
            Manager.menu.AttemptToPlayMenuSfx(SfxID.FIXME_menu_select, 0.6f, 0f, reuse: false);
            FunctionContainer.SetActive(true);
        }

        public void SwitchToFile()
        {
            Title.localize = true;
            Title.Render(TITLE, false, true);
            TitleShadow.localize = true;
            TitleShadow.Render(TITLE, false, true);
            current.OnPageExit();
            SetCurrent(filePage);
            AudioManager.SfxUI(SfxID.FIXME_menu_select, 0.4f, false, 1f, 0f, true, true, 0f);
            FunctionContainer.SetActive(false);
        }

        private UIConfigFile RegisterFile(ConfigFile configFile, string path, Transform content, UIConfigPage page)
        {
            configFile.SaveOnConfigSet = false;
            var file = Instantiate(Template.File, content);
            file.gameObject.SetActive(true);
            file.GetComponentInChildren<PugText>().SetText(path, path);
            file.Key = path;
            file.Detail = page;
            return file;
        }

        private UIConfigPage RegisterDetails(ConfigFile file, string path)
        {
            var page = Instantiate(EmptyPage, PageContainer);
            page.gameObject.SetActive(false);
            page.name = path;
            var content = page.transform.GetChild(0);
            Dictionary<string, List<ConfigEntryBase>> contents = new();
            foreach (var (def, entry) in file.Entries)
            {
                if (!contents.TryGetValue(def.Section, out var entries))
                    contents[def.Section] = entries = new();
                entries.Add(entry);
            }
            var sectionTemplate = Template.Section;
            foreach (var (section, entries) in contents)
            {
                var newSection = Instantiate(sectionTemplate, content);
                newSection.gameObject.SetActive(true);
                newSection.name = "Section " + section;
                newSection.Name.SetText(MiscHelper.GetLocalKey(path, section), section);
                foreach (var entry in entries)
                {
                    UIConfigEntry newEntry = Instantiate(Template.Entry, content);
                    newEntry.BindEntry(entry, Template, 1);
                    newEntry.OwnerPage = page;
                    if (Mapper.TryGetValue(entry, out var value))
                        Mapper[entry] = (value.id, newEntry);
                    newSection.Entries.Add(newEntry);
                }
            }
            return page;
        }

        private UIConfigPage RegisterCombinePage(CombindConfigPage combind, string path)
        {
            var page = Instantiate(EmptyPage, PageContainer);
            page.gameObject.SetActive(false);
            page.name = path;
            var content = page.transform.GetChild(0);
            Dictionary<string, List<ConfigData>> contents = new();
            foreach (var (entry, data) in combind.Configs)
            {
                var def = entry.Definition;
                if (!contents.TryGetValue(def.Section, out var datas))
                    contents[def.Section] = datas = new();
                datas.Add(data);
            }
            var sectionTemplate = Template.Section;
            var icon = Template.Icon.sprite;
            foreach (var (section, datas) in contents)
            {
                var newSection = Instantiate(sectionTemplate, content);
                newSection.gameObject.SetActive(true);
                newSection.name = "Section " + section;
                newSection.Name.SetText(MiscHelper.GetLocalKey(path, section), section);
                foreach (var data in datas)
                {
                    UIConfigEntry newEntry = Instantiate(Template.Entry, content);
                    var @switch = data.Switch;
                    newEntry.BindEntry(@switch, Template, 1);
                    newEntry.OwnerPage = page;
                    if (Mapper.TryGetValue(@switch, out var value))
                        Mapper[@switch] = (value.id, newEntry);
                    newSection.Entries.Add(newEntry);
                    var values = data.Values;
                    if (values == null)
                        continue;
                    newEntry.Active.GetComponent<SpriteRenderer>().sprite = icon;
                    newEntry.Active.transform.localEulerAngles = new(0, 0, -90);
                    foreach (var entry in data.Values.Values)
                    {
                        UIConfigEntry additional = Instantiate(Template.Entry, content);
                        additional.BindEntry(entry, Template, 2);
                        if (Mapper.TryGetValue(entry, out value))
                            Mapper[entry] = (value.id, additional);
                        additional.OwnerPage = page;
                        newSection.Entries.Add(additional);
                        newEntry.Additional.Add(additional);
                    }
                }
            }
            return page;
        }

        private void Update()
        {
            FullBar.SetActive(!scroll.scrollBar.gameObject.activeInHierarchy);
            while (Receive.Count > 0)
            {
                foreach (var (id, value) in Receive)
                {
                    var entry = GeneralConfigMenuMod.Sync.GetEntryByID(id);
                    Debug.Log($"Received change,id:{id} {entry.GetFullName()}, {value}");
                    if (Mapper.TryGetValue(entry, out var item))
                    {
                        // 通知服务器值输入框接收同步
                        item.ue.OnReceivedSync(value);
                        if (entry == GeneralConfigMenuMod.config.AdminOnly)
                        {
                            adminOnly = bool.Parse(value);
                            AdminOnlyButton.gameObject.SetActive(adminOnly);
                            OnPermissionChange();
                        }
                    }
                }
                Receive.Clear();
            }
            bool nowConnected = Manager.networking.isConnected;
            if (connected != nowConnected)
            {
                connected = nowConnected;
                if (nowConnected)
                {
                    GeneralConfigMenuMod.Sync.RequestSyncAll();
                }
                else
                {
                    SetWaitState(null);
                    SetPermission(PermissionLevel.AllowAll);
                }
            }
            CheckPermissionChange();
            bool change = false;
            if (waitForChangeCount > 0)
            {
                tipTimer = (tipTimer + Time.deltaTime) % 2;
                change = true;
            }
            else
            {
                if (tipTimer > 0)
                {
                    tipTimer = Mathf.Max(tipTimer - Time.deltaTime * 3, 0);
                    change = true;
                }
            }
            if (change)
            {
                float lerp = (Mathf.Sin((tipTimer - 0.5f) * Mathf.PI) + 1f) / 2f;
                float t = Mathf.Min(lerp, 0.8f) / 0.8f;
                SaveTip.color = Color.Lerp(Color.clear, Color.yellow, t);
            }
        }

        private void SetCurrent(UIConfigPage page)
        {
            if (current)
                current.gameObject.SetActive(false);
            page.gameObject.SetActive(true);
            scroll.scrollingContent = (current = page).transform;
            currentContent = page.transform.GetChild(0);
            layout = currentContent.GetComponent<LinearLayoutUIComponent>();
            layout.RenderUIComponent(true);
            scroll.ResetScroll();
        }

        public void UpdateContainingElements(float scroll) { }

        public override bool OnCloseMenuRequest()
        {
            if (Input.GetMouseButtonDown(1))
            {
                var selected = Manager.ui.currentSelectedUIElement;
                if (selected && selected.TryGetComponent(out BlockRadicalMenuRightClickBack _))
                {
                    if (selected.TryGetComponent<ButtonUIElement>(out var ui))
                    {
                        ui.OnRightClicked(Input.GetKeyDown(KeyCode.LeftControl), Input.GetKeyDown(KeyCode.LeftShift));
                    }
                    return false;
                }
            }
            if (current != filePage)
            {
                SwitchToFile();
                return false;
            }
            return true;
        }

        public void TryResetConnect()
        {
            if (connected)
            {
                connected = false;
                SaveIcon.color = Color.clear;
                SaveIcon.gameObject.SetActive(false);
                Debug.Log("DisConnected! Reset sync state");
            }
        }

        public static void PlaySelectedSound()
        {
            Manager.menu.AttemptToPlayMenuSfx(SfxID.FIXME_menu_select, 1f, 0f, true);
        }
        public void ShowContextMenu(UIConfigEntry ue)
        {

        }
        private void CheckPermissionChange()
        {
            var player = Manager.main.player;
            if (!player)
                return;
            bool change = false;
            int nowPermission = player.adminPrivileges;
            if (nowPermission != permission)
            {
                permission = nowPermission;
                change = true;
            }
            bool nowGuest = player.guestMode;
            if (nowGuest != guest)
            {
                guest = nowGuest;
                change = true;
            }
            if (change)
            {
                OnPermissionChange();
            }
        }
        public void OnPermissionChange()
        {
            PermissionLevel level;
            if (permission >= 1)
                level = PermissionLevel.AllowAll;
            else if (adminOnly && !guest)
                level = PermissionLevel.LockAdmin;
            else
                level = PermissionLevel.LockServer;
            SetPermission(level);
            Debug.Log($"Change permission adminOnly: {adminOnly}, guest: {guest}");
        }

        private void SetPermission(PermissionLevel level)
        {
            foreach (Transform trans in PageContainer)
            {
                if (trans.TryGetComponent<UIConfigPage>(out var page))
                {
                    page.WaitForCheckLevel = level;
                }
            }
        }
        public void ResetConfigs(bool server)
        {
            current.ResetAll(server);
        }
        public void SaveChanges()
        {
            current.ApplyAllChanges();
        }
        public void RevertChanges()
        {
            current.RevertAllChanges();
        }

        public void TransferAllValues(bool server)
        {
            current.TransferAllValues(server);
        }
        public bool ShowIfIsAdminOnlyWarning(ConfigAccessLevel level)
        {
            if (level == ConfigAccessLevel.Server && adminOnly)
            {
                AdminOnlyButton.ShowUnEditableWarning();
                return true;
            }
            return false;
        }
        public void SetWaitState(bool? wait)
        {
            if (currentCoroutine != null)
            {
                StopCoroutine(currentCoroutine);
            }
            currentCoroutine = StartCoroutine(wait switch
            {
                true => StartWait(),
                false => ReceivedWait(),
                _ => EndWait(),
            });
        }

        public void SetWaitForChangeCount(int count)
        {
            waitForChangeCount = count;
        }
        private IEnumerator StartWait()
        {
            SaveIcon.gameObject.SetActive(true);
            SaveIcon.color = Color.yellow;
            yield return null;
        }

        private IEnumerator ReceivedWait()
        {
            // 立即设置为绿色
            SaveIcon.gameObject.SetActive(true);
            SaveIcon.color = Color.green;
            // 等待一帧确保立即生效（可选）
            yield return null;

            // 从绿色过渡到白色
            Color startColor = Color.green;
            Color targetColor = Color.clear;
            float duration = 2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                SaveIcon.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }

            SaveIcon.color = targetColor;
            SaveIcon.gameObject.SetActive(false);
            yield return null;
        }
        private IEnumerator EndWait()
        {
            // 立即设置为红色
            SaveIcon.gameObject.SetActive(true);
            SaveIcon.color = Color.red;
            // 等待一帧确保立即生效（可选）
            yield return null;

            // 从绿色过渡到白色
            Color startColor = Color.red;
            Color targetColor = Color.clear;
            float duration = 2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                SaveIcon.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }

            SaveIcon.color = targetColor;
            SaveIcon.gameObject.SetActive(false);
            yield return null;
        }
    }
}