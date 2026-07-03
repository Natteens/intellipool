using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace IntelliPool.Editor
{
    public sealed class PoolManagerWindow : EditorWindow
    {
        const string DefaultAssetFolder = "Assets/Resources/IntelliPool";

        PoolDatabase database;
        ObjectField databaseField;
        HelpBox validationBox;
        Foldout statsFoldout;
        ScrollView inspectorScroll;
        readonly List<string> problems = new List<string>();

        [MenuItem("Tools/IntelliPool/Pool Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<PoolManagerWindow>("Pool Manager");
            window.minSize = new Vector2(420, 400);
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 6;

            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            databaseField = new ObjectField("Database") { objectType = typeof(PoolDatabase) };
            databaseField.style.flexGrow = 1;
            databaseField.RegisterValueChangedCallback(evt => SetDatabase(evt.newValue as PoolDatabase));
            toolbar.Add(databaseField);
            toolbar.Add(new Button(CreateDatabase) { text = "Create" });
            toolbar.Add(new Button(RunValidation) { text = "Validate" });
            root.Add(toolbar);

            validationBox = new HelpBox("", HelpBoxMessageType.Info);
            validationBox.style.display = DisplayStyle.None;
            root.Add(validationBox);

            statsFoldout = new Foldout { text = "Runtime Stats (Play Mode)", value = true };
            statsFoldout.style.display = DisplayStyle.None;
            root.Add(statsFoldout);

            inspectorScroll = new ScrollView { style = { flexGrow = 1 } };
            root.Add(inspectorScroll);

            root.schedule.Execute(UpdateStats).Every(500);
            SetDatabase(FindDatabase());
        }

        static PoolDatabase FindDatabase()
        {
            var guids = AssetDatabase.FindAssets("t:PoolDatabase");
            return guids.Length == 0
                ? null
                : AssetDatabase.LoadAssetAtPath<PoolDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        void SetDatabase(PoolDatabase db)
        {
            database = db;
            databaseField.SetValueWithoutNotify(db);
            validationBox.style.display = DisplayStyle.None;
            inspectorScroll.Clear();
            if (db == null)
            {
                inspectorScroll.Add(new HelpBox("No PoolDatabase selected. Create one or assign an existing asset.", HelpBoxMessageType.Info));
                return;
            }
            inspectorScroll.Add(new InspectorElement(db));
        }

        void CreateDatabase()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(DefaultAssetFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "IntelliPool");

            var db = CreateInstance<PoolDatabase>();
            var path = AssetDatabase.GenerateUniqueAssetPath(DefaultAssetFolder + "/PoolDatabase.asset");
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(db);
            SetDatabase(db);
        }

        void RunValidation()
        {
            if (database == null) return;

            problems.Clear();
            if (database.Validate(problems))
            {
                validationBox.text = "Database is valid.";
                validationBox.messageType = HelpBoxMessageType.Info;
            }
            else
            {
                validationBox.text = string.Join("\n", problems);
                validationBox.messageType = HelpBoxMessageType.Warning;
            }
            validationBox.style.display = DisplayStyle.Flex;
        }

        void UpdateStats()
        {
            var service = Application.isPlaying ? Pool.Current : null;
            if (service == null || database == null)
            {
                statsFoldout.style.display = DisplayStyle.None;
                return;
            }

            statsFoldout.style.display = DisplayStyle.Flex;
            statsFoldout.Clear();
            foreach (var entry in database.entries)
            {
                if (service.TryGetStats(entry.id, out var stats))
                    statsFoldout.Add(new Label($"{entry.id}  -  active: {stats.CountActive}, pooled: {stats.CountInactive}, total: {stats.CountAll}, max: {stats.MaxSize}"));
            }
        }
    }
}
