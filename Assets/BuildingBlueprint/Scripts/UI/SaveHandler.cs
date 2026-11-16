using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Core;
using I2.Loc;
using Newtonsoft.Json;
using PugMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.UI
{
    public class SaveHandler : UILinearLayoutContainer
    {
        #region ========== 常量 & 静态字段 ==========
        private const string SAVE_HEADER = "BuildingBlueprint";
        private const string DEFAULT = "Default";
        private const string SAVE_EXTENSION = ".json";
        private const string BACKUP_EXTENSION = ".pugbackup";
        private const string FOLDER_ORDER_FILE = "FolderOrder.json";
        private static readonly Encoding UTF8NoBom = new UTF8Encoding(false);
        private static readonly WaitForSeconds waitForSeconds0_1 = new(0.1f);
        private static readonly JsonSerializerSettings jsonSettings = new()
        {
            Converters = new List<JsonConverter>() { new Int2JsonConverter(), new PlaceCommand.Converter(), new BuildingInfo.Converter() }
        };
        private readonly string FolderOrderPath = Path.Combine(SAVE_HEADER, FOLDER_ORDER_FILE);
        #endregion

        #region ========== UI 引用 ==========
        public UISaveSearcher Searcher;
        public GameObject Detail;
        public SpriteRenderer ImportTip;
        public TextInputField Desc;
        public GameObject CutMark;
        public GameObject DetailMark;
        public GameObject DeleteMark;
        public UIFolder FolderTemplate;
        public UIBuildingInfo InfoTemplate;
        #endregion

        #region ========== 数据 ==========
        public BuildingInfo Current { get; private set; }
        private readonly SortedDictionary<string, List<BuildingInfo>> saves = new();
        private readonly List<UIFolder> folders = new();
        private readonly List<UIBuildingInfo> infos = new();
        private Dictionary<string, List<string>> folderOrder = new();
        private BuildingInfo cut;
        private string searchKey = string.Empty;
        private bool isSearching;
        private bool isDeleting;
        #endregion

        #region ========== 路径 & IO 工具 ==========
        private string GetFolderPath(string folderName) => Path.Combine(SAVE_HEADER, folderName);
        private string GetFilePath(string folderName, string fileName) => Path.Combine(GetFolderPath(folderName), fileName + SAVE_EXTENSION);

        private byte[] Serialize<T>(T obj) => UTF8NoBom.GetBytes(JsonConvert.SerializeObject(obj, Formatting.Indented, jsonSettings));
        private T Deserialize<T>(byte[] data) => JsonConvert.DeserializeObject<T>(UTF8NoBom.GetString(data), jsonSettings);
        private T DeserializeFromFile<T>(string path) => Deserialize<T>(API.ConfigFilesystem.Read(path));

        private void EnsureDirectoryExists(string path)
        {
            if (!API.ConfigFilesystem.DirectoryExists(path))
                API.ConfigFilesystem.CreateDirectory(path);
        }

        private void WriteToFile(string path, object obj)
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                EnsureDirectoryExists(folder);
            API.ConfigFilesystem.Write(path, Serialize(obj));
        }

        private void SaveBuildingInfo(BuildingInfo info)
        {
            var path = GetFilePath(info.Header, info.Name);
            WriteToFile(path, info);
        }

        private void DeleteBuildingFile(BuildingInfo info)
        {
            var path = GetFilePath(info.Header, info.Name);
            if (API.ConfigFilesystem.FileExists(path))
                API.ConfigFilesystem.Delete(path);
        }

        private void SaveFolderOrder()
        {
            WriteToFile(FolderOrderPath, folderOrder);
        }
        #endregion

        #region ========== Unity 生命周期 ==========
        public override void Awake()
        {
            CutMark.SetActive(false);
            Detail.SetActive(false);
            ImportTip.gameObject.SetActive(false);
            DetailMark.SetActive(false);
            FolderTemplate.gameObject.SetActive(false);
            InfoTemplate.gameObject.SetActive(false);

            LoadAllData();

            base.Awake();
            RenderSaves();
        }

        private void Update()
        {
            if (Detail.activeInHierarchy)
                CutMark.SetActive(cut == Current);
            if (!Searcher.inputIsActive)
                return;
            var key = Searcher.pugText.GetText();
            if (searchKey == key)
                return;
            searchKey = key;
            isSearching = !string.IsNullOrEmpty(searchKey);
            RenderSaves();
        }
        #endregion

        #region ========== 数据加载 ==========
        private void LoadAllData()
        {
            var sys = API.ConfigFilesystem;

            LoadFolderOrder();

            foreach (var folder in folderOrder.Keys)
            {
                saves[folder] = new List<BuildingInfo>();
            }

            var files = sys.GetFiles(SAVE_HEADER);
            foreach (var file in files)
            {
                if (file.EndsWith(BACKUP_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    sys.Delete(file);
                    continue;
                }
                if (!file.EndsWith(SAVE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    continue;

                var directory = Path.GetDirectoryName(file);
                if (string.IsNullOrEmpty(directory))
                    continue;

                var levels = directory.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (levels.Length != 2)
                    continue;

                var folder = levels[1];

                try
                {
                    var info = DeserializeFromFile<BuildingInfo>(file);
                    if (info == null)
                    {
                        Debug.LogError($"反序列化失败 (null): {file}");
                        continue;
                    }

                    if (!saves.TryGetValue(folder, out var list))
                    {
                        list = saves[folder] = new List<BuildingInfo>();
                        if (!folderOrder.ContainsKey(folder))
                            folderOrder[folder] = new List<string>();
                    }
                    list.Add(info);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"加载文件失败: {file}\n{ex}");
                }
            }

            // 同步 folderOrder 并按顺序排序
            foreach (var (folder, list) in saves)
            {
                if (!folderOrder.TryGetValue(folder, out var order))
                {
                    order = folderOrder[folder] = new List<string>();
                }

                var fileNames = list.Select(x => x.Name).ToHashSet();
                order.RemoveAll(x => !fileNames.Contains(x));
                foreach (var info in list)
                {
                    if (!order.Contains(info.Name))
                        order.Add(info.Name);
                }

                list.Sort((a, b) =>
                {
                    var indexA = order.IndexOf(a.Name);
                    var indexB = order.IndexOf(b.Name);
                    if (indexA < 0)
                        indexA = int.MaxValue;
                    if (indexB < 0)
                        indexB = int.MaxValue;
                    return indexA.CompareTo(indexB);
                });
            }

            SaveFolderOrder();
        }

        private void LoadFolderOrder()
        {
            if (API.ConfigFilesystem.FileExists(FolderOrderPath))
            {
                try
                {
                    folderOrder = DeserializeFromFile<Dictionary<string, List<string>>>(FolderOrderPath) ?? new();
                    var keysToRemove = folderOrder.Keys.Where(k => !API.ConfigFilesystem.DirectoryExists(GetFolderPath(k))).ToList();
                    foreach (var key in keysToRemove)
                    {
                        folderOrder.Remove(key);
                    }
                }
                catch
                {
                    folderOrder = new();
                }
            }
            else
            {
                folderOrder = new();
            }

            if (!folderOrder.ContainsKey(DEFAULT))
            {
                folderOrder[DEFAULT] = new();
                EnsureDirectoryExists(GetFolderPath(DEFAULT));
                SaveFolderOrder();
            }
        }

        #endregion

        #region ========== 公开操作 ==========
        public void Save(BuildingInfo info)
        {
            var list = saves[DEFAULT];
            info.Name = DateTime.Now.ToString("yy-MM-dd HH_mm_ss");
            info.Header = DEFAULT;
            list.Add(info);

            if (folderOrder.TryGetValue(DEFAULT, out var order))
            {
                order.Add(info.Name);
            }

            SaveBuildingInfo(info);
            SaveFolderOrder();
            RenderSaves();
        }

        public void NewFolder()
        {
            if (isSearching)
                return;

            var baseName = LocalizationManager.GetTranslation("BuildingBlueprint/FolderName");
            var name = baseName;
            var path = GetFolderPath(name);
            int i = 0;

            while (API.ConfigFilesystem.DirectoryExists(path))
            {
                name = $"{baseName}({++i})";
                path = GetFolderPath(name);
            }

            API.ConfigFilesystem.CreateDirectory(path);
            folderOrder[name] = new List<string>();
            saves[name] = new List<BuildingInfo>();
            SaveFolderOrder();
            RenderSaves();
        }

        public void DeleteFolder(UIFolder go)
        {
            if (!isDeleting)
                return;
            var folder = go.Header;
            if (folder == DEFAULT)
                return;

            saves.Remove(folder);
            folderOrder.Remove(folder);
            API.ConfigFilesystem.DeleteDirectory(GetFolderPath(folder));
            SaveFolderOrder();
            RenderSaves();
        }

        public void DeleteBuilding(UIBuildingInfo go)
        {
            if (!isDeleting)
                return;
            var info = go.Info;
            var folder = info.Header;
            saves[folder].Remove(info);
            DeleteBuildingFile(info);

            if (folderOrder.TryGetValue(folder, out var order))
            {
                order.Remove(info.Name);
            }

            // 如果删除的是 cut，清空 cut
            if (cut == info)
            {
                cut = null;
            }
            SaveFolderOrder();
            RenderSaves();
        }

        public void Copy()
        {
            if (Current == null)
                return;
            GUIUtility.systemCopyBuffer = JsonConvert.SerializeObject(Current, Formatting.Indented, jsonSettings);
        }

        public void Cut()
        {
            if (cut == Current)
            {
                cut = null;
                return;
            }

            cut = Current;
        }

        public void Import()
        {
            var data = GUIUtility.systemCopyBuffer;
            try
            {
                var info = Deserialize<BuildingInfo>(UTF8NoBom.GetBytes(data)) ?? throw new Exception("反序列化返回 null");
                StartCoroutine(ReportImport(true));
                Save(info);
            }
            catch
            {
                StartCoroutine(ReportImport(false));
            }
        }

        public void ClearSearch()
        {
            Searcher.ResetText();
            searchKey = string.Empty;
            isSearching = false;
            RenderSaves();
        }

        public void SwitchDelete()
        {
            isDeleting = !isDeleting;
            DeleteMark.SetActive(isDeleting);
        }
        #endregion

        #region ========== 编辑操作 ==========
        public void FinishEditDesc()
        {
            if (!Desc.inputIsActive || Current == null)
                return;
            Current.Description = Desc.pugText.GetText();
            SaveBuildingInfo(Current);
        }

        public void FinishEditName(UIBuildingInfo go)
        {
            if (!go.Input.inputIsActive)
                return;

            var info = go.Info;
            var oldName = info.Name;
            var newName = go.Input.pugText.GetText();
            if (oldName == newName)
                return;
            if (string.IsNullOrEmpty(newName))
            {
                go.Input.pugText.Render(oldName, false, true);
                return;
            }

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                go.Input.pugText.Render(oldName, false, true);
                return;
            }

            try
            {
                var newPath = GetFilePath(info.Header, newName);
                if (API.ConfigFilesystem.FileExists(newPath))
                {
                    go.Input.pugText.Render(oldName, false, true);
                    return;
                }

                var folder = info.Header;

                if (folderOrder.TryGetValue(folder, out var order))
                {
                    var index = order.IndexOf(oldName);
                    if (index >= 0)
                    {
                        order[index] = newName;
                    }
                }

                DeleteBuildingFile(info);
                info.Name = newName;
                SaveBuildingInfo(info);
                SaveFolderOrder();
                RenderSaves();
            }
            catch (Exception ex)
            {
                Debug.LogError($"重命名失败: {ex.Message}");
                go.Input.pugText.Render(oldName, false, true);
            }
        }

        public void FinishEditFolder(UIFolder go)
        {
            if (!go.Input.inputIsActive)
                return;

            var newName = go.Input.pugText.GetText();
            var oldName = go.Header;
            if (oldName == newName)
                return;

            if (newName.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                go.Input.pugText.Render(oldName, false, true);
                return;
            }

            if (oldName == DEFAULT)
                newName = DEFAULT;

            if (string.IsNullOrEmpty(newName) ||
                API.ConfigFilesystem.DirectoryExists(GetFolderPath(newName)))
            {
                go.Input.pugText.Render(oldName, false, true);
                return;
            }

            try
            {
                var list = saves[oldName];
                var order = folderOrder[oldName];

                saves.Remove(oldName);
                folderOrder.Remove(oldName);
                saves[newName] = list;
                folderOrder[newName] = order;
                go.Header = newName;

                foreach (var info in list)
                {
                    info.Header = newName;
                }

                var sys = API.ConfigFilesystem;
                sys.CopyDirectory(GetFolderPath(oldName), GetFolderPath(newName));
                sys.DeleteDirectory(GetFolderPath(oldName));

                SaveFolderOrder();
                RenderSaves();
            }
            catch (Exception ex)
            {
                Debug.LogError($"重命名文件夹失败: {ex.Message}");
                go.Input.pugText.Render(oldName, false, true);
            }
        }
        #endregion

        #region ========== 剪切/粘贴 ==========
        /// <summary>
        /// 将剪切的条目粘贴到目标文件夹（添加到末尾）
        /// </summary>
        public void PasteToFolder(UIFolder go)
        {
            if (cut == null)
                return;

            var targetFolder = go.Header;
            var oldFolder = cut.Header;
            var cutName = cut.Name;

            // 如果目标文件夹就是来源文件夹，不做任何操作
            if (targetFolder == oldFolder)
                return;

            saves[oldFolder].Remove(cut);
            DeleteBuildingFile(cut);

            if (folderOrder.TryGetValue(oldFolder, out var oldOrder))
            {
                oldOrder.Remove(cutName);
            }

            cut.Header = targetFolder;
            saves[targetFolder].Add(cut);

            if (folderOrder.TryGetValue(targetFolder, out var newOrder))
            {
                newOrder.Add(cutName);
            }

            SaveBuildingInfo(cut);
            cut = null;
            SaveFolderOrder();
            RenderSaves();
        }
        public void PasteAbove() => PasteAt(0);

        public void PasteBelow() => PasteAt(1);

        private void PasteAt(int offset)
        {
            if (cut == null)
                return;

            var targetInfo = Current;
            var targetFolder = targetInfo.Header;
            var targetList = saves[targetFolder];
            var insertIndex = targetList.IndexOf(targetInfo);

            var oldFolder = cut.Header;
            var cutName = cut.Name;

            if (oldFolder == targetFolder)
            {
                // 同一文件夹内移动
                var cutIndex = targetList.IndexOf(cut);
                if (cutIndex < insertIndex)
                    insertIndex--;
                targetList.RemoveAt(cutIndex);
                targetList.Insert(insertIndex + offset, cut);

                if (folderOrder.TryGetValue(targetFolder, out var order))
                {
                    order.Remove(cutName);
                    order.Insert(insertIndex + offset, cutName);
                }
            }
            else
            {
                // 跨文件夹移动
                saves[oldFolder].Remove(cut);
                DeleteBuildingFile(cut);

                if (folderOrder.TryGetValue(oldFolder, out var oldOrder))
                {
                    oldOrder.Remove(cutName);
                }

                cut.Header = targetFolder;
                targetList.Insert(insertIndex + offset, cut);

                if (folderOrder.TryGetValue(targetFolder, out var newOrder))
                {
                    newOrder.Insert(insertIndex + offset, cutName);
                }
            }

            SaveBuildingInfo(cut);
            cut = null;
            SaveFolderOrder();
            RenderSaves();
        }

        public void ClearCut() => cut = null;
        #endregion

        #region ========== UI 交互 ==========
        public void SwitchDetail(UIBuildingInfo go)
        {
            if (go.Info == Current)
            {
                CloseDetail();
                return;
            }

            Current = go.Info;
            Detail.SetActive(true);
            DetailMark.SetActive(true);
            DetailMark.transform.position = go.transform.position;
            Desc.SetInputText(Current.Description ?? string.Empty);
        }

        private void CloseDetail()
        {
            Detail.SetActive(false);
            DetailMark.SetActive(false);
            Current = null;
        }
        #endregion

        #region ========== 材料匹配 ==========
        private bool MatchesMaterial(BuildingInfo info, string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return false;

            var materials = info.GetMaterails(out var variations);

            bool isNumeric = int.TryParse(keyword, out var keywordId);

            for (int i = 0; i < materials.Count; i++)
            {
                var material = materials[i];

                if (isNumeric && (int)material.objectID == keywordId)
                    return true;

                var name = PlayerController.GetObjectName(new ContainedObjectsBuffer()
                {
                    objectData = new()
                    {
                        objectID = material.objectID,
                        variation = variations[i],
                    }
                }, true);

                if (name?.text?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }
            return false;
        }
        #endregion

        #region ========== 渲染 ==========
        public void RenderSaves()
        {
            // 关闭详情，但保持 cut 状态
            CloseDetail();

            foreach (var f in folders)
            {
                f.transform.SetParent(null);
                f.gameObject.SetActive(false);
            }
            foreach (var info in infos)
            {
                info.transform.SetParent(null);
                info.gameObject.SetActive(false);
            }

            bool isSearching = !string.IsNullOrEmpty(searchKey);
            int folderIndex = 0, infoIndex = 0;

            foreach (var (folder, infos) in saves)
            {
                bool folderMatches = isSearching && folder.Contains(searchKey, StringComparison.OrdinalIgnoreCase);

                var filteredInfos = isSearching
                    ? infos.Where(x =>
                        folderMatches ||
                        x.Name.Contains(searchKey, StringComparison.OrdinalIgnoreCase) ||
                        (x.Description?.Contains(searchKey, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        MatchesMaterial(x, searchKey)
                    ).ToList()
                    : infos;

                if (isSearching && filteredInfos.Count == 0)
                    continue;

                if (folders.Count <= folderIndex)
                {
                    var newFolder = Instantiate(FolderTemplate, Container);
                    newFolder.Input.AllowInput += () => !isDeleting;
                    folders.Add(newFolder);
                }
                var folderUI = folders[folderIndex];
                folderUI.gameObject.SetActive(true);
                folderUI.Header = folder;
                folderUI.Input.SetInputText(folder);
                folderUI.transform.SetParent(Container);
                folderIndex++;

                foreach (var info in filteredInfos)
                {
                    if (this.infos.Count <= infoIndex)
                    {
                        this.infos.Add(Instantiate(InfoTemplate, Container));
                    }
                    var slot = this.infos[infoIndex];
                    slot.gameObject.SetActive(true);
                    info.Header = folder;
                    slot.Info = info;
                    slot.Input.SetInputText(info.Name);
                    slot.transform.SetParent(Container);
                    infoIndex++;
                }
            }

            Layout.RenderUIComponent(true);
        }
        #endregion

        #region ========== 导入反馈 ==========
        private IEnumerator ReportImport(bool success)
        {
            ImportTip.color = success ? Color.green : Color.red;
            var go = ImportTip.gameObject;

            for (int i = 0; i < 3; i++)
            {
                go.SetActive(true);
                yield return waitForSeconds0_1;
                go.SetActive(false);
                yield return waitForSeconds0_1;
            }
        }
        #endregion
    }
}