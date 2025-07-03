using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IntelliPool.Editor
{
    [CustomEditor(typeof(PoolDatabase))]
    public class PoolDatabaseEditor : UnityEditor.Editor
    {
        #region Campos Privados
        private PoolDatabase database;
        private SerializedProperty poolsProperty;
        private bool showValidation = true;
        private bool showSettings;
        
        private GUIStyle statusStyle;
        private bool stylesInitialized;
        #endregion

        #region Unity Lifecycle
        void OnEnable()
        {
            database = (PoolDatabase)target;
            if (database)
            {
                poolsProperty = serializedObject.FindProperty("pools");
            }
        }

        public override void OnInspectorGUI()
        {
            if (!database || poolsProperty == null)
            {
                EditorGUILayout.HelpBox("Erro: Database não carregada corretamente", MessageType.Error);
                return;
            }

            InitializeStyles();
            serializedObject.Update();

            DrawSystemToggle();
            DrawPoolHeader();
            DrawMainSettings();
            DrawPoolsList();
            DrawValidation();

            serializedObject.ApplyModifiedProperties();
        }
        #endregion

        #region Estilos
        void InitializeStyles()
        {
            if (stylesInitialized) return;

            statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            stylesInitialized = true;
        }
        #endregion

        #region Interface Principal
        void DrawSystemToggle()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var isEnabled = database.enablePoolSystem;
                    var statusText = isEnabled ? "🟢 SISTEMA ATIVO" : "🔴 SISTEMA DESLIGADO";
                    var statusColor = isEnabled ? Color.green : Color.red;

                    var originalColor = GUI.color;
                    GUI.color = statusColor;
                    EditorGUILayout.LabelField(statusText, statusStyle);
                    GUI.color = originalColor;
                }

                EditorGUILayout.Space(5);

                var enableSystemProp = serializedObject.FindProperty("enablePoolSystem");
                EditorGUILayout.PropertyField(enableSystemProp, new GUIContent("Habilitar Pool System", "Liga/desliga todo o sistema de pool"));

                if (!database.enablePoolSystem)
                {
                    EditorGUILayout.HelpBox("Sistema desabilitado. Nenhum pool será inicializado.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);
        }

        void DrawPoolHeader()
        {
            EditorGUILayout.LabelField("Pool Database", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!database.enablePoolSystem))
                {
                    if (GUILayout.Button("+ Novo Pool", GUILayout.Height(25)))
                    {
                        AddNewPool();
                    }
                }
                
                GUILayout.FlexibleSpace();
                
                var totalPools = poolsProperty.arraySize;
                EditorGUILayout.LabelField($"Total: {totalPools}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.Space(5);
        }

        void DrawMainSettings()
        {
            showSettings = EditorGUILayout.Foldout(showSettings, "Configurações", true);
            
            if (!showSettings) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("enableDebugMode"),
                    new GUIContent("Debug Mode", "Ativa logs detalhados")
                );

                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("useJobsForBatching"),
                    new GUIContent("Jobs System", "Usa Jobs para operações em lote")
                );

                if (database.useJobsForBatching)
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("jobsThreshold"),
                        new GUIContent("Jobs Threshold", "Limite para ativar Jobs")
                    );
                }
            }

            EditorGUILayout.Space(5);
        }

        void DrawPoolsList()
        {
            EditorGUILayout.LabelField("Pools", EditorStyles.boldLabel);

            if (poolsProperty == null)
            {
                EditorGUILayout.HelpBox("Erro: Propriedade pools não encontrada", MessageType.Error);
                return;
            }

            if (poolsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Nenhum pool configurado.", MessageType.Info);
                return;
            }

            for (int i = 0; i < poolsProperty.arraySize; i++)
            {
                if (i >= database.pools.Length)
                {
                    EditorGUILayout.HelpBox($"Erro: Índice {i} fora dos limites do array ({database.pools.Length})", MessageType.Error);
                    break;
                }

                DrawPool(i);
            }
        }

        void DrawPool(int index)
        {
            if (!IsValidPoolIndex(index))
            {
                EditorGUILayout.HelpBox($"Índice inválido: {index}", MessageType.Error);
                return;
            }

            SerializedProperty poolElement;
            PoolDatabase.PoolConfiguration config;

            try
            {
                poolElement = poolsProperty.GetArrayElementAtIndex(index);
                config = database.pools[index];
            }
            catch (System.IndexOutOfRangeException)
            {
                EditorGUILayout.HelpBox($"Erro ao acessar pool {index}: índice fora dos limites", MessageType.Error);
                return;
            }
            catch (System.ArgumentException)
            {
                EditorGUILayout.HelpBox($"Erro ao acessar pool {index}: argumento inválido", MessageType.Error);
                return;
            }

            if (poolElement == null)
            {
                EditorGUILayout.HelpBox($"Pool element {index} é nulo", MessageType.Error);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var displayName = !string.IsNullOrEmpty(config.poolTag) ? config.poolTag : $"Pool {index + 1}";
                    EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel);
                    
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        RemovePool(index);
                        return;
                    }
                }

                EditorGUILayout.PropertyField(poolElement.FindPropertyRelative("poolTag"), new GUIContent("Tag"));
                EditorGUILayout.PropertyField(poolElement.FindPropertyRelative("prefab"), new GUIContent("Prefab"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(
                        poolElement.FindPropertyRelative("initialSize"),
                        new GUIContent("Inicial"),
                        GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.45f)
                    );

                    EditorGUILayout.PropertyField(
                        poolElement.FindPropertyRelative("maxSize"),
                        new GUIContent("Máximo"),
                        GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.45f)
                    );
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(poolElement.FindPropertyRelative("preWarm"), new GUIContent("Pre-Warm"));
                    EditorGUILayout.PropertyField(poolElement.FindPropertyRelative("clearOnSceneLoad"), new GUIContent("Limpar na Cena"));
                }

                ValidatePoolInline(config, index);
            }

            EditorGUILayout.Space(3);
        }

        void DrawValidation()
        {
            showValidation = EditorGUILayout.Foldout(showValidation, "Validação", true);

            if (!showValidation) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var errors = GetValidationErrors();
                var warnings = GetValidationWarnings();

                if (errors.Count == 0 && warnings.Count == 0)
                {
                    EditorGUILayout.HelpBox("Todas as configurações estão válidas!", MessageType.Info);
                }
                else
                {
                    foreach (var error in errors)
                        EditorGUILayout.HelpBox(error, MessageType.Error);

                    foreach (var warning in warnings)
                        EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }

                EditorGUILayout.Space(5);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Auto-detectar Tags"))
                    {
                        AutoDetectTags();
                    }

                    if (GUILayout.Button("Corrigir Problemas"))
                    {
                        AutoFix();
                    }
                }
            }
        }
        #endregion

        #region Validação
        bool IsValidPoolIndex(int index)
        {
            return index >= 0 && index < poolsProperty.arraySize && index < database.pools.Length;
        }

        void ValidatePoolInline(PoolDatabase.PoolConfiguration config, int index)
        {
            if (string.IsNullOrEmpty(config.poolTag))
                EditorGUILayout.HelpBox("Tag é obrigatória!", MessageType.Error);

            if (!config.prefab)
                EditorGUILayout.HelpBox("Prefab é obrigatório!", MessageType.Error);

            if (config.maxSize <= 0)
                EditorGUILayout.HelpBox("Tamanho máximo deve ser > 0!", MessageType.Error);
            else if (config.initialSize > config.maxSize)
                EditorGUILayout.HelpBox("Tamanho inicial > máximo!", MessageType.Warning);

            try
            {
                var duplicateTags = database.pools
                    .Where((p, i) => i != index && !string.IsNullOrEmpty(p.poolTag) && p.poolTag == config.poolTag)
                    .Any();

                if (duplicateTags)
                    EditorGUILayout.HelpBox("Tag duplicada encontrada!", MessageType.Error);
            }
            catch (System.ArgumentNullException)
            {
                Debug.LogWarning("Erro ao verificar tags duplicadas: array é nulo");
            }
            catch (System.InvalidOperationException)
            {
                Debug.LogWarning("Erro ao verificar tags duplicadas: operação inválida");
            }
        }

        System.Collections.Generic.List<string> GetValidationErrors()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (database?.pools == null) return errors;

            try
            {
                var emptyTags = database.pools.Count(p => string.IsNullOrEmpty(p.poolTag));
                if (emptyTags > 0)
                    errors.Add($"{emptyTags} pool(s) sem tag");

                var nullPrefabs = database.pools.Count(p => !p.prefab);
                if (nullPrefabs > 0)
                    errors.Add($"{nullPrefabs} pool(s) sem prefab");

                var invalidSizes = database.pools.Count(p => p.maxSize <= 0);
                if (invalidSizes > 0)
                    errors.Add($"{invalidSizes} pool(s) com tamanho máximo inválido");

                var duplicates = database.pools
                    .Where(p => !string.IsNullOrEmpty(p.poolTag))
                    .GroupBy(p => p.poolTag)
                    .Count(g => g.Count() > 1);

                if (duplicates > 0)
                    errors.Add($"{duplicates} tag(s) duplicada(s)");
            }
            catch (System.ArgumentNullException)
            {
                errors.Add("Erro na validação: dados nulos");
            }
            catch (System.InvalidOperationException)
            {
                errors.Add("Erro na validação: operação inválida");
            }

            return errors;
        }

        System.Collections.Generic.List<string> GetValidationWarnings()
        {
            var warnings = new System.Collections.Generic.List<string>();

            if (database?.pools == null) return warnings;

            try
            {
                var invalidInitial = database.pools.Count(p => p.initialSize > p.maxSize);
                if (invalidInitial > 0)
                    warnings.Add($"{invalidInitial} pool(s) com tamanho inicial > máximo");

                var largePools = database.pools.Count(p => p.maxSize > 1000);
                if (largePools > 0)
                    warnings.Add($"{largePools} pool(s) muito grande(s) (>1000)");
            }
            catch (System.ArgumentNullException)
            {
                warnings.Add("Erro na validação: dados nulos");
            }
            catch (System.InvalidOperationException)
            {
                warnings.Add("Erro na validação: operação inválida");
            }

            return warnings;
        }
        #endregion

        #region Ações
        void AddNewPool()
        {
            if (poolsProperty == null) return;

            Undo.RecordObject(database, "Adicionar Pool");

            poolsProperty.arraySize++;
            var newPool = poolsProperty.GetArrayElementAtIndex(poolsProperty.arraySize - 1);

            newPool.FindPropertyRelative("poolTag").stringValue = $"NovoPool_{poolsProperty.arraySize}";
            newPool.FindPropertyRelative("prefab").objectReferenceValue = null;
            newPool.FindPropertyRelative("initialSize").intValue = 10;
            newPool.FindPropertyRelative("maxSize").intValue = 50;
            newPool.FindPropertyRelative("preWarm").boolValue = true;
            newPool.FindPropertyRelative("clearOnSceneLoad").boolValue = false;

            EditorUtility.SetDirty(database);
        }

        void RemovePool(int index)
        {
            if (poolsProperty == null || !IsValidPoolIndex(index))
                return;

            if (EditorUtility.DisplayDialog("Confirmar", "Remover este pool?", "Sim", "Não"))
            {
                Undo.RecordObject(database, "Remover Pool");
                poolsProperty.DeleteArrayElementAtIndex(index);
                EditorUtility.SetDirty(database);
            }
        }

        void AutoDetectTags()
        {
            if (database?.pools == null) return;

            Undo.RecordObject(database, "Auto-detectar Tags");
            var existingTags = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < database.pools.Length; i++)
            {
                try
                {
                    if (database.pools[i].prefab && string.IsNullOrEmpty(database.pools[i].poolTag))
                    {
                        var baseName = database.pools[i].prefab.name.Replace("(Clone)", "").Trim();
                        var uniqueName = GetUniqueTag(baseName, existingTags);
                        
                        database.pools[i].poolTag = uniqueName;
                        existingTags.Add(uniqueName);
                    }
                    else if (!string.IsNullOrEmpty(database.pools[i].poolTag))
                    {
                        existingTags.Add(database.pools[i].poolTag);
                    }
                }
                catch (System.IndexOutOfRangeException)
                {
                    Debug.LogError($"Erro ao processar pool {i}: índice fora dos limites");
                    break;
                }
                catch (System.ArgumentException ex)
                {
                    Debug.LogError($"Erro ao processar pool {i}: {ex.Message}");
                }
            }

            EditorUtility.SetDirty(database);
        }

        void AutoFix()
        {
            if (database?.pools == null) return;

            Undo.RecordObject(database, "Corrigir Problemas");
            var existingTags = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < database.pools.Length; i++)
            {
                try
                {
                    if (database.pools[i].maxSize <= 0)
                        database.pools[i].maxSize = 50;

                    if (database.pools[i].initialSize > database.pools[i].maxSize)
                        database.pools[i].initialSize = database.pools[i].maxSize;
                    
                    if (database.pools[i].initialSize < 0)
                        database.pools[i].initialSize = 0;

                    if (string.IsNullOrEmpty(database.pools[i].poolTag))
                    {
                        var baseName = database.pools[i].prefab ? database.pools[i].prefab.name : "Pool";
                        database.pools[i].poolTag = GetUniqueTag(baseName, existingTags);
                    }

                    if (existingTags.Contains(database.pools[i].poolTag))
                        database.pools[i].poolTag = GetUniqueTag(database.pools[i].poolTag, existingTags);

                    existingTags.Add(database.pools[i].poolTag);
                }
                catch (System.IndexOutOfRangeException)
                {
                    Debug.LogError($"Erro ao corrigir pool {i}: índice fora dos limites");
                    break;
                }
                catch (System.ArgumentException ex)
                {
                    Debug.LogError($"Erro ao corrigir pool {i}: {ex.Message}");
                }
            }

            EditorUtility.SetDirty(database);
        }

        string GetUniqueTag(string baseTag, System.Collections.Generic.HashSet<string> existingTags)
        {
            if (!existingTags.Contains(baseTag))
                return baseTag;

            int counter = 1;
            string candidateTag;

            do
            {
                candidateTag = $"{baseTag}_{counter}";
                counter++;
            }
            while (existingTags.Contains(candidateTag));

            return candidateTag;
        }
        #endregion
    }
}