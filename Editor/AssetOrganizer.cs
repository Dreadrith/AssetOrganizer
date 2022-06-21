using System;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.Animations;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Object = UnityEngine.Object;

//This work is licensed under the Creative Commons Attribution-NonCommercial 2.0 License. 
//To view a copy of the license, visit https://creativecommons.org/licenses/by-nc/2.0/legalcode

//Made by Dreadrith#3238
//Discord: https://discord.gg/ZsPfrGn
//Github: https://github.com/Dreadrith/DreadScripts
//Gumroad: https://gumroad.com/dreadrith
//Ko-fi: https://ko-fi.com/dreadrith

namespace DreadScripts.AssetOrganizer
{
    public class AssetOrganizer : EditorWindow
    {
        #region Declarations
        #region Constants

        private const string PrefsKey = "AvatarAssetOrganizerSettings";
        private static readonly OrganizeType[] organizeTypes =
        {
            new OrganizeType(0, "Animation", typeof(AnimationClip), typeof(BlendTree)),
            new OrganizeType(1, "Controller", typeof(AnimatorController), typeof(AnimatorOverrideController)),
            new OrganizeType(2, "Texture", typeof(Texture)),
            new OrganizeType(3, "Material", typeof(Material)),
            new OrganizeType(4, "Model", new string[] {".fbx",".obj", ".dae", ".3ds", ".dxf"}, typeof(Mesh)),
            new OrganizeType(5, "Prefab", new string[] {".prefab"}, typeof(GameObject)),
            new OrganizeType(6, "Audio", typeof(AudioClip)),
            new OrganizeType(7, "Mask", typeof(AvatarMask)),
            new OrganizeType(8, "Scene", typeof(SceneAsset)),
            new OrganizeType(9, "Preset", typeof(Preset)),
            new OrganizeType(10, "VRC", "VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters","VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu"),
            new OrganizeType(11, "Shader", typeof(Shader)),
            new OrganizeType(12, "Script", new string[] {".dll"}, typeof(MonoScript)),
            new OrganizeType(13, "Other", typeof(ScriptableObject))
        };
        private static readonly string[] mainTabs = {"Organizer", "Options"};
        private static readonly string[] optionTabs = {"Folders", "Types"};
        

        private enum OrganizeAction
        {
            Skip,
            Move
            //Removed till an intuitive way for user friendly GUID replacement option is implemented
            //Copy
        }

        private enum SortOptions
        {
            AlphabeticalPath,
            AlphabeticalAsset,
            AssetType
        }
        #endregion
        #region Automated Variables
        private static int mainToolbarIndex;
        private static int optionsToolbarIndex;
        private static DependencyAsset[] assets;
        private static List<string> createdFolders = new List<string>();
        private Vector2 scrollview;
        #endregion
        #region Input
        private static Object mainAsset;
        private static string destinationPath;

        [SerializeField] private List<CustomFolder> specialFolders;
        [SerializeField] private OrganizeAction[] typeActions;
        [SerializeField] private SortOptions sortByOption;
        [SerializeField] private bool deleteVoidFolders = true;
        #endregion
        #endregion

        #region Methods
        #region Main Methods
        private void OnGUI()
        {
            scrollview = EditorGUILayout.BeginScrollView(scrollview);
            mainToolbarIndex = GUILayout.Toolbar(mainToolbarIndex, mainTabs);

            switch (mainToolbarIndex)
            {
                case 0:
                    DrawOrganizerTab();
                    break;
                case 1:
                    DrawOptionsTab();
                    break;
            }

            DrawSeparator();
            Credit();
            EditorGUILayout.EndScrollView();
        }
        private void GetDependencyAssets()
        {
            destinationPath = AssetDatabase.GetAssetPath(mainAsset);
            bool isFolder = AssetDatabase.IsValidFolder(destinationPath);
            string[] assetsPath = isFolder ? GetAssetPathsInFolder(destinationPath).ToArray() : AssetDatabase.GetDependencies(destinationPath);
            assets = assetsPath.Select(p => new DependencyAsset(p)).ToArray();
            
            if (!isFolder) destinationPath = destinationPath.Replace('\\', '/').Substring(0, destinationPath.LastIndexOf('/'));
            
            foreach (var a in assets)
            {
                string[] subFolders = a.path.Split('/');

                bool setByFolder = false;
                foreach (var f in specialFolders)
                {
                    if (!f.active) continue;
                    if (subFolders.All(s => s != f.name)) continue;
                    
                    a.action = f.action;
                    setByFolder = true;
                    break;

                }

                if (setByFolder) continue;
                
                if (!TrySetAction(a))
                    a.associatedType = organizeTypes.Last();
            }

            switch (sortByOption)
            {
                case SortOptions.AlphabeticalPath:
                    assets = assets.OrderBy(a => a.path).ToArray();
                    break;
                case SortOptions.AlphabeticalAsset:
                    assets = assets.OrderBy(a => a.asset.name).ToArray();
                    break;
                case SortOptions.AssetType:
                    assets = assets.OrderBy(a => a.type.Name).ToArray();
                    break;
            }

        }
        private void OrganizeAssets()
        {
            CheckFolders();
            List<string> affectedFolders = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                int count = assets.Length;
                float progressPerAsset = 1f / count;
                for (var i = 0; i < count; i++)
                {
                    EditorUtility.DisplayProgressBar("Organizing", $"Organizing Assets ({i+1}/{count})", (i + 1) * progressPerAsset);
                    var a = assets[i];
                    string newPath = AssetDatabase.GenerateUniqueAssetPath($"{destinationPath}/{a.associatedType.name}/{Path.GetFileName(a.path)}");
                    switch (a.action)
                    {
                        default: case OrganizeAction.Skip: continue;
                        case OrganizeAction.Move:
                            AssetDatabase.MoveAsset(a.path, newPath);
                            affectedFolders.Add(Path.GetDirectoryName(a.path));
                            break;
                        /*case OrganizeAction.Copy:
                            AssetDatabase.CopyAsset(a.path, newPath);
                            break;*/
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                
                foreach (var folderPath in createdFolders.Concat(affectedFolders).Distinct().Where(DirectoryIsEmpty))
                    AssetDatabase.DeleteAsset(folderPath);
            }
            finally { AssetDatabase.StopAssetEditing(); }

            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(destinationPath));

            assets = null;
            destinationPath = null;
        }
        #endregion
        #region GUI Methods

        private void DrawOrganizerTab()
        {
            GUIStyle boxStyle = GUI.skin.GetStyle("box");

            using (new GUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                mainAsset = EditorGUILayout.ObjectField("Main Asset", mainAsset, typeof(Object), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (mainAsset)
                    {
                        destinationPath = AssetDatabase.GetAssetPath(mainAsset);
                        if (!AssetDatabase.IsValidFolder(destinationPath)) destinationPath = Path.GetDirectoryName(destinationPath).Replace('\\', '/');
                    }
                    assets = null;
                }

                using (new EditorGUI.DisabledScope(!mainAsset))
                    if (GUILayout.Button("Get Assets", GUILayout.Width(80)))
                        GetDependencyAssets();
            }

            destinationPath = AssetFolderPath(destinationPath, "Destination Folder");


            if (assets != null)
            {
                DrawSeparator(4, 20);
                
                var h = EditorGUIUtility.singleLineHeight;
                var squareOptions = new GUILayoutOption[] { GUILayout.Width(h), GUILayout.Height(h) };
                foreach (var a in assets)
                {
                    using (new GUILayout.HorizontalScope(boxStyle))
                    {
                        GUILayout.Label(a.icon, squareOptions);
                        if (GUILayout.Button($"| {a.path}", GUI.skin.label))
                            EditorGUIUtility.PingObject(a.asset);

                        a.action = (OrganizeAction)EditorGUILayout.EnumPopup(a.action, GUILayout.Width(60));
                    }

                }

                if (GUILayout.Button("Organize Assets"))
                    OrganizeAssets();

            }
        }

        private void DrawOptionsTab()
        {
            optionsToolbarIndex = GUILayout.Toolbar(optionsToolbarIndex, optionTabs);
            switch (optionsToolbarIndex)
            {
                case 0:
                    DrawFolderOptions();
                    break;
                case 1:
                    DrawTypeOptions();
                    break;
            }

            DrawSeparator();
            using (new GUILayout.HorizontalScope("helpbox"))
            {
                deleteVoidFolders = EditorGUILayout.Toggle(new GUIContent("Delete Empty Folders", "After moving assets, delete source folders if they're empty"), deleteVoidFolders);
                sortByOption = (SortOptions)EditorGUILayout.EnumPopup("Sort Search By", sortByOption);
            }
        }

        private void DrawFolderOptions()
        {
            for (var i = 0; i < specialFolders.Count; i++)
            {
                var f = specialFolders[i];
                using (new GUILayout.HorizontalScope("helpbox"))
                {
                    using (new BGColoredScope(Color.green, Color.grey, f.active))
                        f.active = GUILayout.Toggle(f.active, f.active ? "Enabled" : "Disabled", GUI.skin.button, GUILayout.Width(100), GUILayout.Height(18));
                    using (new EditorGUI.DisabledScope(!f.active))
                    {
                        f.name = GUILayout.TextField(f.name);
                        f.action = (OrganizeAction) EditorGUILayout.EnumPopup(f.action, GUILayout.Width(60));
                        if (GUILayout.Button("X", GUILayout.Width(18), GUILayout.Height(18)))
                            specialFolders.RemoveAt(i);
                    }
                }
            }

            if (GUILayout.Button("Add"))
                specialFolders.Add(new CustomFolder());
        }

        private void DrawTypeOptions()
        {
            using (new GUILayout.HorizontalScope())
            {
                void DrawTypeGUI(OrganizeType t)
                {
                    var icon = GUIContent.none;
                    if (t.associatedTypes.Length > 0)
                        icon = new GUIContent(AssetPreview.GetMiniTypeThumbnail(t.associatedTypes[0]));

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label(icon, GUILayout.Height(18), GUILayout.Width(18));
                        GUILayout.Label($"| {t.name}");
                        if (TryGetTypeAction(t, out _))
                            typeActions[t.actionIndex] = (OrganizeAction)EditorGUILayout.EnumPopup(typeActions[t.actionIndex], GUILayout.Width(60));
                    }
                }
                
                using (new GUILayout.VerticalScope("helpbox"))
                {
                    for (int i = 0; i < organizeTypes.Length; i+=2)
                        DrawTypeGUI(organizeTypes[i]);
                }
                using (new GUILayout.VerticalScope("helpbox"))
                {
                    for (int i = 1; i < organizeTypes.Length; i += 2)
                        DrawTypeGUI(organizeTypes[i]);
                }
            }
        }

        private static string AssetFolderPath(string variable, string title)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField(title, variable);

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var dummyPath = EditorUtility.OpenFolderPanel(title, AssetDatabase.IsValidFolder(variable) ? variable : "Assets", string.Empty);
                    if (string.IsNullOrEmpty(dummyPath))
                        return variable;
                    string newPath = FileUtil.GetProjectRelativePath(dummyPath);

                    if (!newPath.StartsWith("Assets"))
                    {
                        Debug.LogWarning("New Path must be a folder within Assets!");
                        return variable;
                    }

                    variable = newPath;
                }
            }

            return variable;
        }

        private static void DrawSeparator(int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(thickness + padding));
            r.height = thickness;
            r.y += padding / 2f;
            r.x -= 2;
            r.width += 6;
            ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585", out Color lineColor);
            EditorGUI.DrawRect(r, lineColor);
        }
        #endregion

        #region Sub-Main Methods

        [MenuItem("DreadTools/Asset Organizer", false, 36)]
        private static void ShowWindow() => GetWindow<AssetOrganizer>(false, "Asset Organizer", true);
        private bool TrySetAction(DependencyAsset a)
        {
            for (int i = 0; i < organizeTypes.Length; i++)
            {
                if (!organizeTypes[i].IsAppliedTo(a)) continue;

                if (TryGetTypeAction(organizeTypes[i], out var action))
                {
                    a.action = action;
                    a.associatedType = organizeTypes[i];
                    return true;
                }

            }

            return false;
        }

        private bool TryGetTypeAction(OrganizeType type, out OrganizeAction action)
        {
            bool hasDoubleTried = false;
            TryAgain:
            try
            {
                action = typeActions[type.actionIndex];
                return true;
            }
            catch (Exception)
            {
                if (hasDoubleTried) throw;

                OrganizeAction[] newArray = new OrganizeAction[organizeTypes.Length];
                for (int j = 0; j < newArray.Length; j++)
                {
                    try { newArray[j] = typeActions[j]; }
                    catch { newArray[j] = OrganizeAction.Skip; }
                }

                Debug.LogWarning("Type Actions re-initialized due to a loading/serialization.");
                typeActions = newArray;
                hasDoubleTried = true;
                goto TryAgain;
            }
        }

        private static void CheckFolders()
        {
            if (!destinationPath.StartsWith("Assets/"))
                destinationPath = "Assets/" + destinationPath;
            ReadyPath(destinationPath);
            
            createdFolders.Clear();

            void CheckFolder(string name)
            {
                string path = $"{destinationPath}/{name}";
                if (ReadyPath(path)) createdFolders.Add(path);
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < organizeTypes.Length; i++)
                    CheckFolder(organizeTypes[i].name);
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }
        public static void DeleteIfEmptyFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
                folderPath = Path.GetDirectoryName(folderPath);
            while (DirectoryIsEmpty(folderPath) && folderPath != "Assets")
            {
                var parentDirectory = Path.GetDirectoryName(folderPath);
                FileUtil.DeleteFileOrDirectory(folderPath);
                FileUtil.DeleteFileOrDirectory(folderPath + ".meta");
                folderPath = parentDirectory;
            }
        }
        public static bool DirectoryIsEmpty(string path) => !Directory.EnumerateFileSystemEntries(path).Any();
        #endregion
        #region Automated Methods
        private void OnEnable()
        {
            string data = EditorPrefs.GetString(PrefsKey, JsonUtility.ToJson(this, false));
            JsonUtility.FromJsonOverwrite(data, this);
            if (!EditorPrefs.HasKey(PrefsKey))
            {
                //Default Folder based actions. Based on usual VRC assets.
                specialFolders = new List<CustomFolder>
                {
                    new CustomFolder("VRCSDK"),
                    new CustomFolder("_PoiyomiShaders"),
                    new CustomFolder("VRLabs"),
                    new CustomFolder("PumkinsAvatarTools"),
                    new CustomFolder("DreadScripts"),
                    new CustomFolder("Packages"),
                    new CustomFolder("Plugins"),
                    new CustomFolder("Editor")
                };

                //Default Type based Actions
                typeActions = new OrganizeAction[]
                {
                    OrganizeAction.Move,
                    OrganizeAction.Move,
                    OrganizeAction.Move,
                    OrganizeAction.Move,
                    OrganizeAction.Move,
                    OrganizeAction.Move,
                    OrganizeAction.Move,
                    OrganizeAction.Move,
                    OrganizeAction.Move,
                    OrganizeAction.Skip,
                    OrganizeAction.Move,
                    OrganizeAction.Skip,
                    OrganizeAction.Skip,
                    OrganizeAction.Skip,
                };
            }

            createdFolders = new List<string>();
        }

        private void OnDisable()
        {
            string data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString(PrefsKey, data);
        }
        #endregion
        #region Helper Methods
        private static void Credit()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Made By Dreadrith#3238", "boldlabel"))
                    Application.OpenURL("https://linktr.ee/Dreadrith");
            }
        }

        private static bool ReadyPath(string folderPath)
        {
            if (Directory.Exists(folderPath)) return false;

            Directory.CreateDirectory(folderPath);
            AssetDatabase.ImportAsset(folderPath);
            return true;
        }
        public static List<string> GetAssetPathsInFolder(string path, bool deep = true)
        {
            string[] fileEntries = Directory.GetFiles(path);
            string[] subDirectories = deep ? AssetDatabase.GetSubFolders(path) : null;

            List<string> list = 
                (from fileName in fileEntries 
                    where !fileName.EndsWith(".meta")
                    select fileName.Replace('\\', '/')).ToList();


            if (deep) 
                foreach (var sd in subDirectories)
                    list.AddRange(GetAssetPathsInFolder(sd));


            return list;
        }
        #endregion
        #endregion

        #region Classes & Structs

        [System.Serializable]
        private class CustomFolder
        {
            public string name;
            public bool active = true;
            public OrganizeAction action;
            public CustomFolder(){}
            public CustomFolder(string newName, OrganizeAction action = OrganizeAction.Skip)
            {
                name = newName;
                this.action = action;
            }
        }


        private class DependencyAsset
        {
            public readonly Object asset;
            public readonly string path;
            public readonly Type type;
            public readonly GUIContent icon;
            public OrganizeAction action;
            public OrganizeType associatedType;

            public DependencyAsset(string path)
            {
                this.path = path;
                asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                action = OrganizeAction.Skip;
                type = asset.GetType();
                icon = new GUIContent(AssetPreview.GetMiniTypeThumbnail(type), type.Name);
            }
        }
        private readonly struct OrganizeType
        {
            public readonly int actionIndex;
            public readonly string name;
            public readonly Type[] associatedTypes;
            private readonly string[] associatedExtensions;

            public OrganizeType(int actionIndex, string name)
            {
                this.actionIndex = actionIndex;
                this.name = name;
                this.associatedTypes = Array.Empty<Type>();
                this.associatedExtensions = Array.Empty<string>();
            }
            public OrganizeType(int actionIndex, string name, params string[] associatedTypes)
            {
                this.actionIndex = actionIndex;
                this.name = name;
                this.associatedTypes = new Type[associatedTypes.Length];
                for (int i = 0; i < associatedTypes.Length; i++)
                    this.associatedTypes[i] = System.Type.GetType(associatedTypes[i]);
                
                this.associatedExtensions = Array.Empty<string>();
            }

            public OrganizeType(int actionIndex, string name, params Type[] associatedTypes)
            {
                this.actionIndex = actionIndex;
                this.name = name;

                this.associatedTypes = associatedTypes;
                this.associatedExtensions = Array.Empty<string>();
            }

            public OrganizeType(int actionIndex, string name, string[] associatedExtensions, params string[] associatedTypes)
            {
                this.actionIndex = actionIndex;
                this.name = name;
                
                this.associatedTypes = new Type[associatedTypes.Length];
                for (int i = 0; i < associatedTypes.Length; i++)
                    this.associatedTypes[i] = System.Type.GetType(associatedTypes[i]);
                
                this.associatedExtensions = associatedExtensions;
            }

            public OrganizeType(int actionIndex, string name, string[] associatedExtensions, params Type[] associatedTypes)
            {
                this.actionIndex = actionIndex;
                this.name = name;

                int count = associatedTypes.Length;
                this.associatedTypes = associatedTypes;
                this.associatedExtensions = associatedExtensions;
            }

            public bool IsAppliedTo(DependencyAsset a)
            {
                bool applies = a.type != null &&
                    (associatedTypes.Any(t => t != null && (a.type == t || a.type.IsSubclassOf(t)))
                     || associatedExtensions.Any(e => !string.IsNullOrWhiteSpace(e) && a.path.EndsWith(e)));

                return applies;
            }
            
        }

        private class BGColoredScope : System.IDisposable
        {
            private readonly Color ogColor;
            public BGColoredScope(Color setColor)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = setColor;
            }
            public BGColoredScope(Color setColor, bool isActive)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? setColor : ogColor;
            }
            public BGColoredScope(Color active, Color inactive, bool isActive)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? active : inactive;
            }

            public BGColoredScope(int selectedIndex, params Color[] colors)
            {
                ogColor = GUI.backgroundColor;
                GUI.backgroundColor = colors[selectedIndex];
            }
            public void Dispose()
            {
                GUI.backgroundColor = ogColor;
            }
        }
        #endregion
    }
}