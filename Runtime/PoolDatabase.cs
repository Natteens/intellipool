using System;
using UnityEngine;

namespace IntelliPool
{
    [CreateAssetMenu(fileName = "PoolDatabase", menuName = "IntelliPool/Pool Database")]
    public class PoolDatabase : ScriptableObject
    {
        [Serializable]
        public struct PoolConfiguration
        {
            [Tooltip("Identificador único do pool")]
            public string poolTag;
            
            [Tooltip("GameObject base para instanciar")]
            public GameObject prefab;
            
            [Tooltip("Quantidade inicial de objetos")]
            public int initialSize;
            
            [Tooltip("Quantidade máxima de objetos (0 = ilimitado)")]
            public int maxSize;
            
            [Tooltip("Criar objetos na inicialização")]
            public bool preWarm;
            
            [Tooltip("Limpar objetos ao carregar nova cena")]
            public bool clearOnSceneLoad;
        }

        [Header("Sistema de Pool")]
        [Tooltip("Ativa/desativa o sistema de pool completamente")]
        public bool enablePoolSystem = true;

        [Header("Pools")]
        public PoolConfiguration[] pools = Array.Empty<PoolConfiguration>();

        [Header("Configurações")]
        [Tooltip("Ativa logs detalhados")]
        public bool enableDebugMode;
    }
}