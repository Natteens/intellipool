using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace IntelliPool.Editor
{
    public class PoolManagerWindow : EditorWindow
    {
        #region Campos Privados
        private PoolDatabase database;
        private Vector2 poolsScrollPosition;
        private Vector2 editorScrollPosition;
        private int selectedPoolIndex = -1;
        private bool showSystemInfo = true;
        
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private GUIStyle poolItemStyle;
        private GUIStyle selectedPoolStyle;
        private bool stylesInitialized;
        
        private string searchFilter = "";
        private readonly Vector2 minWindowSize = new Vector2(1000, 650);
        private float leftPanelWidth = 380f;
        private bool isResizing = false;
        private Rect resizeRect;
        #endregion

        #region Inicialização
        [MenuItem("Tools/IntelliPool/Pool Manager", priority = 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<PoolManagerWindow>("Pool Manager");
            window.minSize = window.minWindowSize;
            var main = EditorGUIUtility.GetMainWindowPosition();
            var pos = window.position;
            pos.size = new Vector2(1100, 700);
            pos.center = main.center;
            window.position = pos;
            
            window.Show();
        }

        void OnEnable()
        {
            LoadDatabase();
            selectedPoolIndex = -1;
        }

        void LoadDatabase()
        {
            var savedPath = EditorPrefs.GetString("IntelliPool.DatabasePath", "");
            if (!string.IsNullOrEmpty(savedPath))
            {
                database = AssetDatabase.LoadAssetAtPath<PoolDatabase>(savedPath);
                if (database != null) return;
            }
            
            var databases = Resources.LoadAll<PoolDatabase>("");
            if (databases.Length > 0)
            {
                database = databases[0];
                var path = AssetDatabase.GetAssetPath(database);
                Pool.SetDatabasePath(path);
            }
            else
            {
                database = null;
            }
        }
        #endregion

        #region Interface Principal
        void OnGUI()
        {
            InitializeStyles();
            HandleResize();
            
            DrawHeader();
            
            if (database == null)
            {
                DrawNoDatabaseMessage();
                return;
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftPanel();
                DrawResizeHandle();
                DrawRightPanel();
            }
        }

        void HandleResize()
        {
            resizeRect = new Rect(leftPanelWidth - 5, 0, 10, position.height);
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);
            
            if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
            {
                isResizing = true;
            }
            
            if (isResizing)
            {
                leftPanelWidth = Event.current.mousePosition.x;
                leftPanelWidth = Mathf.Clamp(leftPanelWidth, 300f, position.width - 350f);
                Repaint();
            }
            
            if (Event.current.type == EventType.MouseUp)
            {
                isResizing = false;
            }
        }

        void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.7f, 1f) }
            };

            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fixedHeight = 28
            };

            poolItemStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 8, 8, 8),
                margin = new RectOffset(3, 3, 2, 2)
            };

            selectedPoolStyle = new GUIStyle(poolItemStyle)
            {
                normal = { background = MakeTex(1, 1, new Color(0.3f, 0.6f, 1f, 0.8f)) }
            };

            stylesInitialized = true;
        }

        Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(12);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("🎯 IntelliPool Manager", headerStyle);
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("🔄", GUILayout.Width(35), GUILayout.Height(30)))
                {
                    LoadDatabase();
                    Repaint();
                }
                
                if (GUILayout.Button("⚙️", GUILayout.Width(35), GUILayout.Height(30)))
                {
                    PoolSetupWindow.ShowSetupWindow();
                }
            }
            
            EditorGUILayout.Space(12);
            
            if (database != null)
            {
                DrawSystemInfo();
            }
        }

        void DrawSystemInfo()
        {
            showSystemInfo = EditorGUILayout.Foldout(showSystemInfo, "📊 System Information", true);
            
            if (!showSystemInfo) return;
            
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var systemStatus = database.enablePoolSystem ? "✅ Active" : "❌ Disabled";
                    var debugStatus = database.enableDebugMode ? "🐛 Debug ON" : "🔇 Debug OFF";
                    
                    EditorGUILayout.LabelField($"System: {systemStatus}", GUILayout.Width(140));
                    EditorGUILayout.LabelField($"{debugStatus}", GUILayout.Width(120));
                    
                    GUILayout.FlexibleSpace();
                    
                    EditorGUILayout.LabelField($"Pools: {database.pools?.Length ?? 0}", GUILayout.Width(90));
                }
                
                EditorGUILayout.Space(8);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    var enableSystem = EditorGUILayout.ToggleLeft("Enable System", database.enablePoolSystem, GUILayout.Width(150));
                    if (enableSystem != database.enablePoolSystem)
                    {
                        database.enablePoolSystem = enableSystem;
                        EditorUtility.SetDirty(database);
                    }
                    
                    var enableDebug = EditorGUILayout.ToggleLeft("Debug Mode", database.enableDebugMode, GUILayout.Width(120));
                    if (enableDebug != database.enableDebugMode)
                    {
                        database.enableDebugMode = enableDebug;
                        EditorUtility.SetDirty(database);
                    }
                    
                    GUILayout.FlexibleSpace();
                    
                    if (Application.isPlaying && GUILayout.Button("📈 Stats", buttonStyle, GUILayout.Width(80)))
                    {
                        Pool.LogPoolStats();
                    }
                }
            }
        }

        void DrawNoDatabaseMessage()
        {
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                EditorGUILayout.Space(30);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("⚠️ No Database Found", subHeaderStyle);
                    GUILayout.FlexibleSpace();
                }
                
                EditorGUILayout.Space(15);
                
                EditorGUILayout.LabelField(
                    "To use IntelliPool, you need to create a Database first.\n" +
                    "Click the button below to run the initial setup.",
                    EditorStyles.wordWrappedLabel
                );
                
                EditorGUILayout.Space(20);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("🚀 Run Setup", buttonStyle, GUILayout.Width(180), GUILayout.Height(35)))
                    {
                        PoolSetupWindow.ShowSetupWindow();
                    }
                    GUILayout.FlexibleSpace();
                }
                
                EditorGUILayout.Space(30);
            }
        }

        void DrawLeftPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftPanelWidth)))
            {
                DrawPoolsList();
                DrawPoolActions();
            }
        }

        void DrawResizeHandle()
        {
            GUILayout.Box("", GUILayout.Width(3), GUILayout.ExpandHeight(true));
        }

        void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (selectedPoolIndex >= 0 && selectedPoolIndex < database.pools.Length)
                {
                    DrawPoolEditor();
                }
                else
                {
                    DrawNoSelectionMessage();
                }
            }
        }

        void DrawPoolsList()
        {
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("🎯 Pool List", subHeaderStyle);
                    
                    if (GUILayout.Button("➕", buttonStyle, GUILayout.Width(35)))
                    {
                        AddNewPool();
                    }
                }
                
                EditorGUILayout.Space(8);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("🔍", GUILayout.Width(25));
                    searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
                    
                    if (GUILayout.Button("✖", EditorStyles.miniButton, GUILayout.Width(25)))
                    {
                        searchFilter = "";
                        GUI.FocusControl(null);
                    }
                }
                
                EditorGUILayout.Space(8);
                
                if (database.pools == null || database.pools.Length == 0)
                {
                    EditorGUILayout.HelpBox("No pools configured. Click '+' to add.", MessageType.Info);
                    return;
                }
                
                using (var poolsScroll = new EditorGUILayout.ScrollViewScope(poolsScrollPosition, GUILayout.MinHeight(300)))
                {
                    poolsScrollPosition = poolsScroll.scrollPosition;
                    
                    var filteredPools = GetFilteredPools();
                    
                    for (int i = 0; i < filteredPools.Count; i++)
                    {
                        var poolIndex = filteredPools[i];
                        DrawPoolListItem(poolIndex);
                    }
                }
            }
        }

        List<int> GetFilteredPools()
        {
            var filtered = new List<int>();
            
            for (int i = 0; i < database.pools.Length; i++)
            {
                var pool = database.pools[i];
                if (string.IsNullOrEmpty(searchFilter) || 
                    pool.poolTag.ToLower().Contains(searchFilter.ToLower()) ||
                    (pool.prefab && pool.prefab.name.ToLower().Contains(searchFilter.ToLower())))
                {
                    filtered.Add(i);
                }
            }
            
            return filtered;
        }

        void DrawPoolListItem(int index)
        {
            var pool = database.pools[index];
            var isSelected = selectedPoolIndex == index;
            var style = isSelected ? selectedPoolStyle : poolItemStyle;
            
            using (new EditorGUILayout.VerticalScope(style))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var itemRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
                    
                    var clickableRect = new Rect(itemRect.x, itemRect.y, itemRect.width - 45, itemRect.height);
                    if (Event.current.type == EventType.MouseDown && clickableRect.Contains(Event.current.mousePosition))
                    {
                        selectedPoolIndex = index;
                        Event.current.Use();
                        Repaint();
                    }
                    
                    var statusIcon = pool.prefab ? "✅" : "⚠️";
                    var iconRect = new Rect(itemRect.x + 12, itemRect.y + 10, 20, 20);
                    GUI.Label(iconRect, statusIcon);
                    
                    var displayName = string.IsNullOrEmpty(pool.poolTag) ? $"Pool {index + 1}" : pool.poolTag;
                    var nameRect = new Rect(itemRect.x + 40, itemRect.y + 10, itemRect.width - 85, 20);
                    GUI.Label(nameRect, displayName);
                    
                    var deleteRect = new Rect(itemRect.xMax - 35, itemRect.y + 8, 30, 24);
                    if (GUI.Button(deleteRect, "🗑️", EditorStyles.miniButton))
                    {
                        RemovePool(index);
                        Event.current.Use();
                        return;
                    }
                }
            }
        }

        void DrawPoolActions()
        {
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                EditorGUILayout.LabelField("🛠️ General Actions", subHeaderStyle);
                EditorGUILayout.Space(8);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("🔧 Auto-Fix", buttonStyle))
                    {
                        AutoFixPools();
                    }
                    
                    if (GUILayout.Button("🏷️ Auto-Tags", buttonStyle))
                    {
                        AutoDetectTags();
                    }
                }
                
                EditorGUILayout.Space(5);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("💾 Save", buttonStyle))
                    {
                        AssetDatabase.SaveAssets();
                    }
                    
                    if (GUILayout.Button("🔄 Reload", buttonStyle))
                    {
                        LoadDatabase();
                        selectedPoolIndex = -1;
                    }
                }
            }
        }

        void DrawNoSelectionMessage()
        {
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                EditorGUILayout.Space(80);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Select a pool from the list to edit", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                }
                
                EditorGUILayout.Space(30);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Or create a new pool by clicking the + button", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                }
                
                EditorGUILayout.Space(80);
            }
        }

        void DrawPoolEditor()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(editorScrollPosition))
            {
                editorScrollPosition = scroll.scrollPosition;
                
                using (new EditorGUILayout.VerticalScope(boxStyle))
                {
                    var pool = database.pools[selectedPoolIndex];
                    
                    EditorGUILayout.LabelField($"✏️ Editing Pool", subHeaderStyle);
                    EditorGUILayout.Space(12);
                    
                    // Pool fields
                    var newTag = EditorGUILayout.TextField("🏷️ Pool Tag", pool.poolTag);
                    EditorGUILayout.Space(5);
                    
                    var newPrefab = (GameObject)EditorGUILayout.ObjectField("🎯 Prefab", pool.prefab, typeof(GameObject), false);
                    EditorGUILayout.Space(8);
                    
                    var newInitialSize = EditorGUILayout.IntField("Initial Size", pool.initialSize);
                    EditorGUILayout.Space(5);
                    
                    var newMaxSize = EditorGUILayout.IntField("Max Size (0 = unlimited)", pool.maxSize);
                    EditorGUILayout.Space(8);
                    
                    var newPreWarm = EditorGUILayout.ToggleLeft("🔥 Pre-Warm on Start", pool.preWarm);
                    EditorGUILayout.Space(3);
                    
                    var newClearOnScene = EditorGUILayout.ToggleLeft("🧹 Clear on Scene Load", pool.clearOnSceneLoad);
                    
                    // Apply changes
                    pool.poolTag = newTag;
                    pool.prefab = newPrefab;
                    pool.initialSize = Mathf.Max(0, newInitialSize);
                    pool.maxSize = Mathf.Max(0, newMaxSize);
                    pool.preWarm = newPreWarm;
                    pool.clearOnSceneLoad = newClearOnScene;
                    
                    database.pools[selectedPoolIndex] = pool;
                    EditorUtility.SetDirty(database);
                    
                    // Validation
                    EditorGUILayout.Space(15);
                    ValidatePool(pool);
                    
                    // Pool actions
                    EditorGUILayout.Space(15);
                    DrawSelectedPoolActions(pool);
                }
            }
        }

        void ValidatePool(PoolDatabase.PoolConfiguration pool)
        {
            if (string.IsNullOrEmpty(pool.poolTag))
                EditorGUILayout.HelpBox("⚠️ Pool tag is required!", MessageType.Error);
            
            if (!pool.prefab)
                EditorGUILayout.HelpBox("⚠️ Prefab is required!", MessageType.Error);
            
            if (pool.maxSize > 0 && pool.initialSize > pool.maxSize)
                EditorGUILayout.HelpBox("⚠️ Initial size is greater than max size!", MessageType.Warning);
            
            // Check for duplicates
            if (!string.IsNullOrEmpty(pool.poolTag))
            {
                var duplicates = database.pools.Count(p => p.poolTag == pool.poolTag);
                if (duplicates > 1)
                    EditorGUILayout.HelpBox("⚠️ Duplicate tag found!", MessageType.Error);
            }
        }

        void DrawSelectedPoolActions(PoolDatabase.PoolConfiguration pool)
        {
            EditorGUILayout.LabelField("🎮 Pool Actions", subHeaderStyle);
            EditorGUILayout.Space(8);
            
            if (GUILayout.Button("🔗 Auto-Tag from Prefab", buttonStyle, GUILayout.Height(30)))
            {
                if (pool.prefab)
                {
                    var newPool = pool;
                    newPool.poolTag = pool.prefab.name;
                    database.pools[selectedPoolIndex] = newPool;
                    EditorUtility.SetDirty(database);
                }
            }
            
            EditorGUILayout.Space(5);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (Application.isPlaying && Pool.IsInitialized && GUILayout.Button("🔥 PreWarm Now", buttonStyle))
                {
                    Pool.PreWarm(pool.poolTag, pool.initialSize);
                }
                
                if (Application.isPlaying && Pool.IsInitialized && GUILayout.Button("🧹 Clear Pool", buttonStyle))
                {
                    Pool.DespawnAll(pool.poolTag);
                }
            }
            
            // Runtime info
            if (Application.isPlaying && Pool.IsInitialized)
            {
                EditorGUILayout.Space(15);
                EditorGUILayout.LabelField("📊 Runtime Info", subHeaderStyle);
                EditorGUILayout.Space(5);
                
                var activeCount = Pool.GetActiveCount(pool.poolTag);
                EditorGUILayout.LabelField($"Active Objects: {activeCount}");
                
                if (Pool.GetPoolInfo(pool.poolTag, out var info))
                {
                    EditorGUILayout.LabelField($"Total Objects: {info.totalCount}");
                    EditorGUILayout.LabelField($"Available Objects: {info.totalCount - info.activeCount}");
                }
            }
        }
        #endregion

        #region Ações
        void AddNewPool()
        {
            var poolsList = database.pools?.ToList() ?? new List<PoolDatabase.PoolConfiguration>();
            
            poolsList.Add(new PoolDatabase.PoolConfiguration
            {
                poolTag = $"NewPool_{poolsList.Count + 1}",
                prefab = null,
                initialSize = 10,
                maxSize = 50,
                preWarm = true,
                clearOnSceneLoad = false
            });
            
            database.pools = poolsList.ToArray();
            selectedPoolIndex = poolsList.Count - 1;
            EditorUtility.SetDirty(database);
        }

        void RemovePool(int index)
        {
            var poolsList = database.pools.ToList();
            poolsList.RemoveAt(index);
            database.pools = poolsList.ToArray();
            
            // Ajusta o índice selecionado
            if (selectedPoolIndex == index)
                selectedPoolIndex = -1;
            else if (selectedPoolIndex > index)
                selectedPoolIndex--;
            
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            
            // Force repaint
            Repaint();
        }

        void AutoDetectTags()
        {
            bool hasChanges = false;
            
            for (int i = 0; i < database.pools.Length; i++)
            {
                var pool = database.pools[i];
                if (pool.prefab && string.IsNullOrEmpty(pool.poolTag))
                {
                    pool.poolTag = pool.prefab.name;
                    database.pools[i] = pool;
                    hasChanges = true;
                }
            }
            
            if (hasChanges)
            {
                EditorUtility.SetDirty(database);
            }
        }

        void AutoFixPools()
        {
            bool hasChanges = false;
            
            for (int i = 0; i < database.pools.Length; i++)
            {
                var pool = database.pools[i];
                
                if (string.IsNullOrEmpty(pool.poolTag))
                {
                    pool.poolTag = pool.prefab ? pool.prefab.name : $"Pool_{i}";
                    hasChanges = true;
                }
                
                if (pool.maxSize < 0)
                {
                    pool.maxSize = 50;
                    hasChanges = true;
                }
                
                if (pool.initialSize < 0)
                {
                    pool.initialSize = 0;
                    hasChanges = true;
                }
                
                if (pool.maxSize > 0 && pool.initialSize > pool.maxSize)
                {
                    pool.initialSize = pool.maxSize;
                    hasChanges = true;
                }
                
                database.pools[i] = pool;
            }
            
            if (hasChanges)
            {
                EditorUtility.SetDirty(database);
            }
        }
        #endregion
    }
}