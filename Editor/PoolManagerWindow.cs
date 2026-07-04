using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace IntelliPool.Editor
{
    public sealed class PoolManagerWindow : EditorWindow
    {
        enum StatusKind { Muted, Success, Warning, Error }
        enum RuntimeState { Found, Missing, SelectedNotLoadable }

        const string DefaultPackagePath = "Packages/com.natteens.intellipool";
        const string RuntimeDefaultFolder = "Assets/Resources/IntelliPool";
        const string RuntimeDefaultAssetPath = "Assets/Resources/IntelliPool/PoolDatabase.asset";
        const int EntryRowHeight = 58;

        PoolDatabase database;
        SerializedObject serializedDatabase;
        string searchFilter = string.Empty;
        readonly List<string> problems = new List<string>();
        readonly List<int> filteredIndices = new List<int>();

        ObjectField databaseField;
        VisualElement databaseFieldContainer;
        Label headerStatusBadge;
        Button createButton;
        Button findButton;
        Button saveButton;
        Button validateButton;
        Button refreshButton;
        Label databaseStatusLabel;

        Label runtimePathLabel;
        Label runtimeBadge;
        Button createRuntimeDefaultButton;
        Button useSelectedButton;
        Button pingRuntimeButton;
        VisualElement runtimeWarningBox;

        VisualElement emptyState;
        Button createDatabaseButton;
        Button findDatabaseButton;
        Button createRuntimeDefaultEmptyButton;

        VisualElement workspace;
        Button addButtonHeader;
        TextField searchField;
        ListView entriesList;
        VisualElement entriesEmpty;
        Button addEntryEmptyButton;
        Button addButtonFooter;
        Button duplicateButton;
        Button removeButton;

        VisualElement detailsEmpty;
        VisualElement detailsContent;
        VisualElement identityFields;
        Button autoIdButton;
        Button pingButton;
        VisualElement capacityFields;
        VisualElement behaviorFields;
        Label entryStatusLabel;
        Button validateEntryButton;

        Label statsIdle;
        VisualElement statsTable;
        VisualElement statsBody;

        [MenuItem("Tools/IntelliPool/Pool Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<PoolManagerWindow>("Pool Manager");
            window.minSize = new Vector2(760, 520);
        }

        void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.minHeight = 0;

            VisualTreeAsset uxml = null;
            StyleSheet uss = null;
            try
            {
                var basePath = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PoolManagerWindow).Assembly)?.assetPath ?? DefaultPackagePath;
                uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{basePath}/Editor/PoolManagerWindow.uxml");
                uss = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{basePath}/Editor/PoolManagerWindow.uss");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IntelliPool] Failed to load Pool Manager layout assets: {e.Message}");
            }

            if (uxml == null)
            {
                Debug.LogWarning("[IntelliPool] PoolManagerWindow.uxml not found. Using fallback UI.");
                BuildFallbackUI("missing UXML");
                return;
            }

            uxml.CloneTree(rootVisualElement);
            if (uss != null) rootVisualElement.styleSheets.Add(uss);

            if (!TryBindElements())
            {
                Debug.LogWarning("[IntelliPool] Pool Manager UXML is missing required elements. Using fallback UI.");
                BuildFallbackUI("layout structure mismatch");
                return;
            }

            WireCallbacks();
            WireTooltips();
            rootVisualElement.schedule.Execute(UpdateStats).Every(500);
            SetDatabase(FindDatabase());
        }

        bool TryBindElements()
        {
            databaseFieldContainer = rootVisualElement.Q<VisualElement>("database-field-container");
            headerStatusBadge = rootVisualElement.Q<Label>("header-status-badge");
            createButton = rootVisualElement.Q<Button>("create-button");
            findButton = rootVisualElement.Q<Button>("find-button");
            saveButton = rootVisualElement.Q<Button>("save-button");
            validateButton = rootVisualElement.Q<Button>("validate-button");
            refreshButton = rootVisualElement.Q<Button>("refresh-button");
            databaseStatusLabel = rootVisualElement.Q<Label>("database-status-label");

            runtimePathLabel = rootVisualElement.Q<Label>("runtime-path-label");
            runtimeBadge = rootVisualElement.Q<Label>("runtime-badge");
            createRuntimeDefaultButton = rootVisualElement.Q<Button>("create-runtime-default-button");
            useSelectedButton = rootVisualElement.Q<Button>("use-selected-button");
            pingRuntimeButton = rootVisualElement.Q<Button>("ping-runtime-button");
            runtimeWarningBox = rootVisualElement.Q<VisualElement>("runtime-warning-box");

            emptyState = rootVisualElement.Q<VisualElement>("empty-state");
            createDatabaseButton = rootVisualElement.Q<Button>("create-database-button");
            findDatabaseButton = rootVisualElement.Q<Button>("find-database-button");
            createRuntimeDefaultEmptyButton = rootVisualElement.Q<Button>("create-runtime-default-empty-button");

            workspace = rootVisualElement.Q<VisualElement>("workspace");
            addButtonHeader = rootVisualElement.Q<Button>("add-button-header");
            searchField = rootVisualElement.Q<TextField>("search-field");
            entriesList = rootVisualElement.Q<ListView>("entries-list");
            entriesEmpty = rootVisualElement.Q<VisualElement>("entries-empty");
            addEntryEmptyButton = rootVisualElement.Q<Button>("add-entry-empty-button");
            addButtonFooter = rootVisualElement.Q<Button>("add-button-footer");
            duplicateButton = rootVisualElement.Q<Button>("duplicate-button");
            removeButton = rootVisualElement.Q<Button>("remove-button");

            detailsEmpty = rootVisualElement.Q<VisualElement>("details-empty");
            detailsContent = rootVisualElement.Q<VisualElement>("details-content");
            identityFields = rootVisualElement.Q<VisualElement>("identity-fields");
            autoIdButton = rootVisualElement.Q<Button>("auto-id-button");
            pingButton = rootVisualElement.Q<Button>("ping-button");
            capacityFields = rootVisualElement.Q<VisualElement>("capacity-fields");
            behaviorFields = rootVisualElement.Q<VisualElement>("behavior-fields");
            entryStatusLabel = rootVisualElement.Q<Label>("entry-status-label");
            validateEntryButton = rootVisualElement.Q<Button>("validate-entry-button");

            statsIdle = rootVisualElement.Q<Label>("stats-idle");
            statsTable = rootVisualElement.Q<VisualElement>("stats-table");
            statsBody = rootVisualElement.Q<VisualElement>("stats-body");

            return databaseFieldContainer != null && headerStatusBadge != null && createButton != null && findButton != null &&
                   saveButton != null && validateButton != null && refreshButton != null && databaseStatusLabel != null &&
                   runtimePathLabel != null && runtimeBadge != null && createRuntimeDefaultButton != null && useSelectedButton != null &&
                   pingRuntimeButton != null && runtimeWarningBox != null &&
                   emptyState != null && createDatabaseButton != null && findDatabaseButton != null && createRuntimeDefaultEmptyButton != null &&
                   workspace != null && addButtonHeader != null && searchField != null && entriesList != null && entriesEmpty != null &&
                   addEntryEmptyButton != null && addButtonFooter != null && duplicateButton != null && removeButton != null &&
                   detailsEmpty != null && detailsContent != null && identityFields != null && autoIdButton != null && pingButton != null &&
                   capacityFields != null && behaviorFields != null && entryStatusLabel != null && validateEntryButton != null &&
                   statsIdle != null && statsTable != null && statsBody != null;
        }

        void WireCallbacks()
        {
            databaseField = new ObjectField { objectType = typeof(PoolDatabase) };
            databaseField.style.flexGrow = 1;
            databaseField.RegisterValueChangedCallback(evt => SetDatabase(evt.newValue as PoolDatabase));
            databaseFieldContainer.Add(databaseField);

            createButton.clicked += CreateDatabase;
            findButton.clicked += () => SetDatabase(FindDatabase());
            saveButton.clicked += SaveDatabase;
            validateButton.clicked += RunValidation;
            refreshButton.clicked += () => SetDatabase(database);

            createDatabaseButton.clicked += CreateDatabase;
            findDatabaseButton.clicked += () => SetDatabase(FindDatabase());
            createRuntimeDefaultEmptyButton.clicked += CreateRuntimeDefault;

            createRuntimeDefaultButton.clicked += CreateRuntimeDefault;
            useSelectedButton.clicked += UseSelectedAsRuntimeDefault;
            pingRuntimeButton.clicked += PingRuntimeDefault;

            addButtonHeader.clicked += AddEntry;
            addButtonFooter.clicked += AddEntry;
            addEntryEmptyButton.clicked += AddEntry;
            removeButton.clicked += RemoveSelectedEntry;
            duplicateButton.clicked += DuplicateSelectedEntry;
            autoIdButton.clicked += AutoIdFromPrefab;
            pingButton.clicked += PingSelectedPrefab;
            validateEntryButton.clicked += ValidateSelectedEntry;

            searchField.RegisterValueChangedCallback(evt =>
            {
                searchFilter = evt.newValue;
                entriesList.reorderable = string.IsNullOrEmpty(searchFilter);
                RebuildFilteredIndices();
            });

            entriesList.makeItem = MakeEntryRow;
            entriesList.bindItem = BindEntryRow;
            entriesList.fixedItemHeight = EntryRowHeight;
            entriesList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            entriesList.selectionType = SelectionType.Single;
            entriesList.showBorder = true;
            entriesList.showBoundCollectionSize = false;
            entriesList.showAddRemoveFooter = false;
            entriesList.reorderable = true;
            ConfigureEntriesListLayout();
            entriesList.selectionChanged += _ =>
            {
                ShowSelectedEntry();
                entriesList.RefreshItems();
            };
            entriesList.itemIndexChanged += (oldIndex, newIndex) =>
            {
                if (database == null || !string.IsNullOrEmpty(searchFilter)) return;
                if (oldIndex < 0 || oldIndex >= database.entries.Count || newIndex < 0 || newIndex >= database.entries.Count) return;

                var moved = database.entries[oldIndex];
                database.entries.RemoveAt(oldIndex);
                database.entries.Insert(newIndex, moved);
                MarkDatabaseDirty();
                RebuildFilteredIndices(newIndex);
            };
        }

        void ConfigureEntriesListLayout()
        {
            PinToAvailableHeight(entriesList);
            entriesList.RegisterCallback<GeometryChangedEvent>(_ => PinEntriesListScrollView());
            entriesList.schedule.Execute(PinEntriesListScrollView);
        }

        void PinEntriesListScrollView()
        {
            var scrollView = entriesList.Q<ScrollView>();
            if (scrollView == null) return;

            PinToAvailableHeight(scrollView);
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            var contentAndVerticalScroll = scrollView.Q<VisualElement>("unity-content-and-vertical-scroll-container");
            if (contentAndVerticalScroll != null) PinToAvailableHeight(contentAndVerticalScroll);

            var viewport = scrollView.Q<VisualElement>("unity-content-viewport");
            if (viewport != null) PinToAvailableHeight(viewport);
        }

        static void PinToAvailableHeight(VisualElement element)
        {
            element.style.flexGrow = 1;
            element.style.flexShrink = 1;
            element.style.flexBasis = 0;
            element.style.minHeight = 0;
            element.style.minWidth = 0;
            element.style.overflow = Overflow.Hidden;
        }

        void WireTooltips()
        {
            databaseField.tooltip = "PoolDatabase currently being edited in this window.";
            createButton.tooltip = "Creates a new PoolDatabase asset for editing, saved wherever you choose.";
            findButton.tooltip = "Finds an existing PoolDatabase asset in the project.";
            saveButton.tooltip = "Marks the database dirty and saves modified assets.";
            validateButton.tooltip = "Checks for missing prefabs, empty ids, duplicate ids, and runtime setup issues.";
            refreshButton.tooltip = "Reloads the current database.";

            runtimePathLabel.tooltip = "Path Pool.Get(id) uses to auto-load the runtime database.";
            createRuntimeDefaultButton.tooltip = "Creates the database used automatically by Pool.Get(id) at runtime.";
            useSelectedButton.tooltip = "Moves the selected database to the runtime default path so Pool.Get(id) can auto-load it.";
            pingRuntimeButton.tooltip = "Highlights the runtime default database asset in the Project window.";

            searchField.tooltip = "Filters entries by id or prefab name.";
            addButtonHeader.tooltip = "Adds a new empty pool entry.";
            addButtonFooter.tooltip = "Adds a new empty pool entry.";
            addEntryEmptyButton.tooltip = "Adds a new empty pool entry.";
            removeButton.tooltip = "Removes the selected pool entry.";
            duplicateButton.tooltip = "Duplicates the selected pool entry.";

            autoIdButton.tooltip = "Sets the entry id to the assigned prefab's name.";
            pingButton.tooltip = "Highlights the assigned prefab in the Project window.";
            validateEntryButton.tooltip = "Checks the selected entry for missing prefab, empty id, or duplicate id.";

            var statsTitle = rootVisualElement.Q<Label>("stats-title");
            if (statsTitle != null) statsTitle.tooltip = "Live active/inactive/total counts per pool while in Play Mode.";
        }

        void BuildFallbackUI(string reason)
        {
            rootVisualElement.Clear();
            rootVisualElement.Add(new Label($"Pool Manager UI failed to load ({reason}). See console for details. Basic fallback below.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6 }
            });

            var dbField = new ObjectField("Database") { objectType = typeof(PoolDatabase) };
            dbField.SetValueWithoutNotify(database);
            dbField.RegisterValueChangedCallback(evt => database = evt.newValue as PoolDatabase);
            rootVisualElement.Add(dbField);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            buttons.Add(new Button(CreateDatabase) { text = "Create" });
            buttons.Add(new Button(() => { database = FindDatabase(); dbField.SetValueWithoutNotify(database); }) { text = "Find" });
            rootVisualElement.Add(buttons);

            var listLabel = new Label { style = { marginTop = 6, whiteSpace = WhiteSpace.Normal } };
            rootVisualElement.Add(listLabel);
            rootVisualElement.schedule.Execute(() =>
            {
                if (database == null) { listLabel.text = "No database selected."; return; }
                var text = string.Empty;
                for (int i = 0; i < database.entries.Count; i++)
                {
                    var e = database.entries[i];
                    text += $"{(string.IsNullOrEmpty(e.id) ? "(no id)" : e.id)} -> {(e.prefab != null ? e.prefab.name : "(missing prefab)")}\n";
                }
                listLabel.text = text.Length > 0 ? text : "No entries.";
            }).Every(1000);
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
            serializedDatabase = db != null ? new SerializedObject(db) : null;
            databaseField.SetValueWithoutNotify(db);

            searchFilter = string.Empty;
            searchField.SetValueWithoutNotify(string.Empty);
            entriesList.reorderable = true;

            bool hasDb = db != null;
            emptyState.style.display = hasDb ? DisplayStyle.None : DisplayStyle.Flex;
            workspace.style.display = hasDb ? DisplayStyle.Flex : DisplayStyle.None;

            saveButton.SetEnabled(hasDb);
            validateButton.SetEnabled(hasDb);
            addButtonHeader.SetEnabled(hasDb);
            addButtonFooter.SetEnabled(hasDb);
            removeButton.SetEnabled(hasDb);
            duplicateButton.SetEnabled(hasDb);

            RebuildFilteredIndices(-1);

            SetDatabaseStatus(hasDb ? $"{db.entries.Count} entr{(db.entries.Count == 1 ? "y" : "ies")}." : "No database selected.", StatusKind.Muted);
            UpdateRuntimeStatus();
        }

        int GetSelectedRealIndex()
        {
            int index = entriesList.selectedIndex;
            return index >= 0 && index < filteredIndices.Count ? filteredIndices[index] : -1;
        }

        void RebuildFilteredIndices(int? forceSelectRealIndex = null)
        {
            int targetRealIndex = forceSelectRealIndex ?? GetSelectedRealIndex();

            filteredIndices.Clear();
            if (database != null)
            {
                for (int i = 0; i < database.entries.Count; i++)
                    if (MatchesFilter(database.entries[i])) filteredIndices.Add(i);
            }

            entriesList.itemsSource = filteredIndices;
            entriesList.Rebuild();
            PinEntriesListScrollView();
            UpdateEntriesEmptyState();

            int filteredPos = targetRealIndex >= 0 ? filteredIndices.IndexOf(targetRealIndex) : -1;
            if (filteredPos >= 0)
            {
                entriesList.SetSelection(filteredPos);
                entriesList.ScrollToItem(filteredPos);
            }
            else
            {
                entriesList.ClearSelection();
                ShowSelectedEntry();
            }
        }

        void UpdateEntriesEmptyState()
        {
            bool hasEntries = filteredIndices.Count > 0;
            entriesList.style.display = hasEntries ? DisplayStyle.Flex : DisplayStyle.None;
            entriesEmpty.style.display = hasEntries ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void CreateDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Pool Database", "PoolDatabase", "asset",
                "Choose where to save the new PoolDatabase asset.", "Assets");
            if (string.IsNullOrEmpty(path)) return;

            var db = CreateInstance<PoolDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(db);
            SetDatabase(db);
        }

        void SaveDatabase()
        {
            if (database == null) return;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            SetDatabaseStatus("Saved.", StatusKind.Success);
        }

        void RunValidation()
        {
            if (database == null)
            {
                SetDatabaseStatus("No database selected.", StatusKind.Muted);
                return;
            }

            problems.Clear();
            bool valid = database.Validate(problems);
            entriesList.RefreshItems();
            SetDatabaseStatus(valid ? "Database valid." : $"{problems.Count} problem{(problems.Count == 1 ? string.Empty : "s")} found: {string.Join(" | ", problems)}",
                valid ? StatusKind.Success : StatusKind.Warning);
        }

        void EnsureRuntimeFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(RuntimeDefaultFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "IntelliPool");
        }

        static bool IsRuntimeLoadable(PoolDatabase db)
        {
            var path = AssetDatabase.GetAssetPath(db);
            return !string.IsNullOrEmpty(path) && path.Contains("/Resources/");
        }

        RuntimeState ComputeRuntimeState()
        {
            var canonical = AssetDatabase.LoadAssetAtPath<PoolDatabase>(RuntimeDefaultAssetPath);
            if (canonical != null) return RuntimeState.Found;
            if (database != null && !IsRuntimeLoadable(database)) return RuntimeState.SelectedNotLoadable;
            return RuntimeState.Missing;
        }

        void UpdateRuntimeStatus()
        {
            var state = ComputeRuntimeState();
            bool selectedIsCanonical = database != null && AssetDatabase.GetAssetPath(database) == RuntimeDefaultAssetPath;

            switch (state)
            {
                case RuntimeState.Found:
                    SetBadge(headerStatusBadge, "Runtime Ready", StatusKind.Success);
                    SetBadge(runtimeBadge, "Found", StatusKind.Success);
                    runtimeWarningBox.style.display = DisplayStyle.None;
                    break;
                case RuntimeState.SelectedNotLoadable:
                    SetBadge(headerStatusBadge, "Selected Database Not Runtime-Loadable", StatusKind.Warning);
                    SetBadge(runtimeBadge, "Selected DB will not auto-load", StatusKind.Warning);
                    runtimeWarningBox.style.display = DisplayStyle.Flex;
                    break;
                default:
                    SetBadge(headerStatusBadge, "No Runtime Database", StatusKind.Warning);
                    SetBadge(runtimeBadge, "Missing", StatusKind.Warning);
                    runtimeWarningBox.style.display = DisplayStyle.None;
                    break;
            }

            useSelectedButton.SetEnabled(database != null && !selectedIsCanonical);
        }

        void CreateRuntimeDefault()
        {
            EnsureRuntimeFolder();
            var existing = AssetDatabase.LoadAssetAtPath<PoolDatabase>(RuntimeDefaultAssetPath);
            if (existing == null)
            {
                existing = CreateInstance<PoolDatabase>();
                AssetDatabase.CreateAsset(existing, RuntimeDefaultAssetPath);
                AssetDatabase.SaveAssets();
            }
            EditorGUIUtility.PingObject(existing);
            SetDatabase(existing);
        }

        void UseSelectedAsRuntimeDefault()
        {
            if (database == null) return;
            var currentPath = AssetDatabase.GetAssetPath(database);
            if (currentPath == RuntimeDefaultAssetPath) return;

            EnsureRuntimeFolder();
            var error = AssetDatabase.MoveAsset(currentPath, RuntimeDefaultAssetPath);
            if (!string.IsNullOrEmpty(error))
            {
                SetBadge(runtimeBadge, "Move failed", StatusKind.Error);
                Debug.LogWarning($"[IntelliPool] Could not move database to runtime default path: {error}");
                return;
            }
            AssetDatabase.SaveAssets();
            SetDatabase(database);
        }

        void PingRuntimeDefault()
        {
            var db = AssetDatabase.LoadAssetAtPath<PoolDatabase>(RuntimeDefaultAssetPath);
            if (db != null) EditorGUIUtility.PingObject(db);
        }

        VisualElement MakeEntryRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ip-entry-row");
            var titleLabel = new Label { name = "row-title" };
            titleLabel.AddToClassList("ip-entry-title");
            var subtitleLabel = new Label { name = "row-subtitle" };
            subtitleLabel.AddToClassList("ip-entry-subtitle");
            var metaLabel = new Label { name = "row-meta" };
            metaLabel.AddToClassList("ip-entry-meta");
            row.Add(titleLabel);
            row.Add(subtitleLabel);
            row.Add(metaLabel);
            return row;
        }

        void BindEntryRow(VisualElement element, int index)
        {
            int realIndex = filteredIndices[index];
            var entry = database.entries[realIndex];
            var (invalid, _) = EvaluateEntry(entry, realIndex);

            var titleLabel = element.Q<Label>("row-title");
            var subtitleLabel = element.Q<Label>("row-subtitle");
            var metaLabel = element.Q<Label>("row-meta");

            titleLabel.text = (string.IsNullOrEmpty(entry.id) ? "(no id)" : entry.id) + (invalid ? "  ⚠" : string.Empty);
            subtitleLabel.text = entry.prefab != null ? entry.prefab.name : "missing prefab";
            metaLabel.text = $"prewarm {entry.prewarmCount} • max {entry.maxSize}";

            element.EnableInClassList("ip-entry-row-invalid", invalid);
            element.EnableInClassList("ip-entry-row-selected", entriesList.selectedIndex == index);
        }

        bool MatchesFilter(PoolEntry entry)
        {
            if (string.IsNullOrEmpty(searchFilter)) return true;
            if (!string.IsNullOrEmpty(entry.id) && entry.id.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (entry.prefab != null && entry.prefab.name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        bool HasDuplicateId(PoolEntry entry, int index)
        {
            if (string.IsNullOrEmpty(entry.id)) return false;
            for (int i = 0; i < database.entries.Count; i++)
            {
                if (i == index) continue;
                if (database.entries[i].id == entry.id) return true;
            }
            return false;
        }

        (bool invalid, string message) EvaluateEntry(PoolEntry entry, int index)
        {
            if (entry.prefab == null) return (true, "Selected entry is missing a prefab.");
            if (string.IsNullOrEmpty(entry.id)) return (true, "Selected entry has an empty id.");
            if (HasDuplicateId(entry, index)) return (true, $"Selected entry id '{entry.id}' is duplicated.");
            return (false, $"Entry '{entry.id}' is valid.");
        }

        void ShowSelectedEntry()
        {
            identityFields.Clear();
            capacityFields.Clear();
            behaviorFields.Clear();

            int index = GetSelectedRealIndex();
            bool hasSelection = database != null && index >= 0;

            detailsEmpty.style.display = hasSelection ? DisplayStyle.None : DisplayStyle.Flex;
            detailsContent.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasSelection)
            {
                entryStatusLabel.text = string.Empty;
                return;
            }

            serializedDatabase.Update();
            var entryProp = serializedDatabase.FindProperty("entries").GetArrayElementAtIndex(index);

            AddPropertyField(identityFields, entryProp, "id");
            AddPropertyField(identityFields, entryProp, "prefab");

            AddCompactField(capacityFields, entryProp, "prewarmCount", "Prewarm");
            AddCompactField(capacityFields, entryProp, "defaultCapacity", "Capacity");
            AddCompactField(capacityFields, entryProp, "maxSize", "Max Size", isLast: true);

            AddPropertyField(behaviorFields, entryProp, "collectionCheck");
            AddPropertyField(behaviorFields, entryProp, "clearOnSceneLoad");
            AddPropertyField(behaviorFields, entryProp, "containerName");

            detailsContent.Bind(serializedDatabase);

            var (invalid, message) = EvaluateEntry(database.entries[index], index);
            SetEntryStatus(message, invalid ? StatusKind.Warning : StatusKind.Success);
        }

        void AddPropertyField(VisualElement container, SerializedProperty entryProp, string relativeName)
        {
            var field = new PropertyField(entryProp.FindPropertyRelative(relativeName));
            field.AddToClassList("ip-field-row");
            container.Add(field);
        }

        void AddCompactField(VisualElement container, SerializedProperty entryProp, string relativeName, string labelText, bool isLast = false)
        {
            var column = new VisualElement();
            column.AddToClassList("ip-field-row-compact-item");
            if (isLast) column.AddToClassList("ip-field-row-compact-item-last");

            var label = new Label(labelText);
            label.AddToClassList("ip-field-label");
            label.tooltip = entryProp.FindPropertyRelative(relativeName).tooltip;

            var field = new IntegerField { label = string.Empty };
            field.AddToClassList("ip-field-control");
            field.tooltip = label.tooltip;
            field.BindProperty(entryProp.FindPropertyRelative(relativeName));

            column.Add(label);
            column.Add(field);
            container.Add(column);
        }

        void AddEntry()
        {
            if (database == null) return;
            Undo.RecordObject(database, "Add Pool Entry");
            database.entries.Add(new PoolEntry());
            MarkDatabaseDirty();
            RefreshEntries(database.entries.Count - 1);
        }

        void RemoveSelectedEntry()
        {
            int index = GetSelectedRealIndex();
            if (database == null || index < 0) return;
            Undo.RecordObject(database, "Remove Pool Entry");
            database.entries.RemoveAt(index);
            MarkDatabaseDirty();
            RefreshEntries(-1);
        }

        void DuplicateSelectedEntry()
        {
            int index = GetSelectedRealIndex();
            if (database == null || index < 0) return;
            var source = database.entries[index];
            var copy = new PoolEntry
            {
                id = source.id + "_Copy",
                prefab = source.prefab,
                prewarmCount = source.prewarmCount,
                defaultCapacity = source.defaultCapacity,
                maxSize = source.maxSize,
                collectionCheck = source.collectionCheck,
                clearOnSceneLoad = source.clearOnSceneLoad,
                containerName = source.containerName
            };
            Undo.RecordObject(database, "Duplicate Pool Entry");
            database.entries.Insert(index + 1, copy);
            MarkDatabaseDirty();
            RefreshEntries(index + 1);
        }

        void AutoIdFromPrefab()
        {
            int index = GetSelectedRealIndex();
            if (database == null || index < 0) return;
            var entry = database.entries[index];
            if (entry.prefab == null)
            {
                SetEntryStatus("Selected entry has no prefab assigned.", StatusKind.Warning);
                return;
            }
            Undo.RecordObject(database, "Auto Id From Prefab");
            entry.id = entry.prefab.name;
            MarkDatabaseDirty();
            RefreshEntries(index);
        }

        void PingSelectedPrefab()
        {
            int index = GetSelectedRealIndex();
            if (database == null || index < 0) return;
            var prefab = database.entries[index].prefab;
            if (prefab != null) EditorGUIUtility.PingObject(prefab);
            else SetEntryStatus("Selected entry has no prefab assigned.", StatusKind.Warning);
        }

        void ValidateSelectedEntry()
        {
            int index = GetSelectedRealIndex();
            if (database == null || index < 0) return;
            var (invalid, message) = EvaluateEntry(database.entries[index], index);
            SetEntryStatus(message, invalid ? StatusKind.Warning : StatusKind.Success);
        }

        void MarkDatabaseDirty()
        {
            if (database != null) EditorUtility.SetDirty(database);
        }

        void RefreshEntries(int selectRealIndex)
        {
            serializedDatabase.Update();
            RebuildFilteredIndices(selectRealIndex);
            SetDatabaseStatus($"{database.entries.Count} entr{(database.entries.Count == 1 ? "y" : "ies")}.", StatusKind.Muted);
        }

        void SetDatabaseStatus(string text, StatusKind kind) => SetStatusLabel(databaseStatusLabel, text, kind);
        void SetEntryStatus(string text, StatusKind kind) => SetStatusLabel(entryStatusLabel, text, kind);

        static void SetStatusLabel(Label label, string text, StatusKind kind)
        {
            label.text = text;
            label.EnableInClassList("ip-muted", kind == StatusKind.Muted);
            label.EnableInClassList("ip-success-box", kind == StatusKind.Success);
            label.EnableInClassList("ip-warning-box", kind == StatusKind.Warning);
            label.EnableInClassList("ip-error-box", kind == StatusKind.Error);
        }

        static void SetBadge(Label badge, string text, StatusKind kind)
        {
            badge.text = text;
            badge.EnableInClassList("ip-status-success", kind == StatusKind.Success);
            badge.EnableInClassList("ip-status-warning", kind == StatusKind.Warning);
            badge.EnableInClassList("ip-status-error", kind == StatusKind.Error);
        }

        void UpdateStats()
        {
            if (statsBody == null) return;

            var service = Application.isPlaying ? Pool.Current : null;
            bool hasStats = service != null && database != null;

            statsIdle.style.display = hasStats ? DisplayStyle.None : DisplayStyle.Flex;
            statsTable.style.display = hasStats ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasStats) return;

            statsBody.Clear();
            for (int i = 0; i < database.entries.Count; i++)
            {
                var entry = database.entries[i];
                if (!service.TryGetStats(entry.id, out var stats)) continue;

                var row = new VisualElement();
                row.AddToClassList("ip-stats-row");
                var idLabel = new Label(entry.id);
                idLabel.AddToClassList("ip-stats-col");
                idLabel.AddToClassList("ip-stats-col-id");
                row.Add(idLabel);
                AddStatsColumn(row, stats.CountActive.ToString());
                AddStatsColumn(row, stats.CountInactive.ToString());
                AddStatsColumn(row, stats.CountAll.ToString());
                AddStatsColumn(row, stats.MaxSize.ToString());
                statsBody.Add(row);
            }
        }

        static void AddStatsColumn(VisualElement row, string text)
        {
            var label = new Label(text);
            label.AddToClassList("ip-stats-col");
            row.Add(label);
        }
    }
}
