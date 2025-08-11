using UnityEngine;
using UnityEditor;
using System.IO;

namespace IntelliPool.Editor
{
    public class PoolSetupWindow : EditorWindow
    {
        #region Campos Privados
        private static bool hasShownSetup;
        private Vector2 scrollPosition;
        private string databasePath = "Assets/IntelliPool";
        private string databaseName = "PoolDatabase";
        private bool enableDebugMode;
        
        private GUIStyle headerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized;
        #endregion

        #region Inicialização
        [InitializeOnLoadMethod]
        static void CheckFirstTime()
        {
            if (!hasShownSetup && !HasPoolDatabase())
            {
                hasShownSetup = true;
                ShowSetupWindow();
            }
        }

        [MenuItem("Tools/IntelliPool/Initial Setup", priority = 1)]
        public static void ShowSetupWindow()
        {
            var window = GetWindow<PoolSetupWindow>("IntelliPool - Setup");
            window.minSize = new Vector2(500, 500);
            window.maxSize = new Vector2(800, 700);
            
            var main = EditorGUIUtility.GetMainWindowPosition();
            var pos = window.position;
            pos.center = main.center;
            window.position = pos;
            
            window.ShowUtility();
        }

        static bool HasPoolDatabase()
        {
            var path = EditorPrefs.GetString("IntelliPool.DatabasePath", "");
            if (!string.IsNullOrEmpty(path))
            {
                var asset = AssetDatabase.LoadAssetAtPath<PoolDatabase>(path);
                return asset != null;
            }
            
            var databases = Resources.LoadAll<PoolDatabase>("");
            return databases.Length > 0;
        }
        #endregion

        #region Interface
        void OnGUI()
        {
            InitializeStyles();
            
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;
                
                DrawHeader();
                DrawWelcomeMessage();
                DrawSetupOptions();
                DrawActionButtons();
            }
        }

        void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.7f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fixedHeight = 35
            };

            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(15, 15, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };

            stylesInitialized = true;
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(20);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("🎯", GUILayout.Width(40), GUILayout.Height(40));
                EditorGUILayout.LabelField("IntelliPool", headerStyle, GUILayout.Height(40));
                GUILayout.FlexibleSpace();
            }
            
            EditorGUILayout.Space(10);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Advanced Object Pooling System", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }
            
            EditorGUILayout.Space(20);
        }

        void DrawWelcomeMessage()
        {
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                EditorGUILayout.LabelField("🚀 Welcome to IntelliPool!", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField(
                    "This wizard will configure the pooling system in your project.\n" +
                    "You'll be able to create object pools to optimize performance automatically.",
                    EditorStyles.wordWrappedLabel
                );
            }
            
            EditorGUILayout.Space(10);
        }

        void DrawSetupOptions()
        {
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                EditorGUILayout.LabelField("⚙️ Setup Configuration", EditorStyles.boldLabel);
                EditorGUILayout.Space(10);
                
                // Database Path
                EditorGUILayout.LabelField("📁 Database Location:");
                using (new EditorGUILayout.HorizontalScope())
                {
                    databasePath = EditorGUILayout.TextField(databasePath);
                    if (GUILayout.Button("📂", GUILayout.Width(30)))
                    {
                        var selectedPath = EditorUtility.OpenFolderPanel("Select folder", "Assets", "");
                        if (!string.IsNullOrEmpty(selectedPath))
                        {
                            databasePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                        }
                    }
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("📝 Database Name:");
                databaseName = EditorGUILayout.TextField(databaseName);
                
                EditorGUILayout.Space(10);
                
                enableDebugMode = EditorGUILayout.ToggleLeft("🐛 Enable debug mode", enableDebugMode);
            }
            
            EditorGUILayout.Space(10);
        }

        void DrawActionButtons()
        {
            using (new EditorGUILayout.VerticalScope(boxStyle))
            {
                EditorGUILayout.LabelField("🎮 Actions", EditorStyles.boldLabel);
                EditorGUILayout.Space(10);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("✅ Setup IntelliPool", buttonStyle))
                    {
                        PerformSetup();
                    }
                    
                    if (GUILayout.Button("❌ Skip Setup", buttonStyle))
                    {
                        Close();
                    }
                }
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("🔧 Open Pool Manager", buttonStyle))
                {
                    PoolManagerWindow.ShowWindow();
                    Close();
                }
            }
        }
        #endregion

        #region Ações
        void PerformSetup()
        {
            try
            {
                CreateDirectories();
                
                var database = CreateDatabase();
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog(
                    "Setup Complete!",
                    $"IntelliPool has been configured successfully!\n\n" +
                    $"Database created at: {databasePath}/{databaseName}.asset\n\n" +
                    "You can now use the Pool Manager to add pools.",
                    "OK"
                );
                
                PoolManagerWindow.ShowWindow();
                Close();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Setup Error", $"Error during setup:\n{ex.Message}", "OK");
                Debug.LogError($"[IntelliPool Setup] Error: {ex.Message}");
            }
        }

        void CreateDirectories()
        {
            if (!Directory.Exists(databasePath))
            {
                Directory.CreateDirectory(databasePath);
                AssetDatabase.Refresh();
            }
        }

        PoolDatabase CreateDatabase()
        {
            var database = CreateInstance<PoolDatabase>();
            database.enablePoolSystem = true;
            database.enableDebugMode = enableDebugMode;
            database.pools = new PoolDatabase.PoolConfiguration[0]; 
            
            var assetPath = $"{databasePath}/{databaseName}.asset";
            AssetDatabase.CreateAsset(database, assetPath);
            
            Pool.SetDatabasePath(assetPath);
            
            return database;
        }
        #endregion
    }
}