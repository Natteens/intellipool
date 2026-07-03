namespace IntelliPool
{
    public interface IPoolable
    {
        void OnSpawnedFromPool();
        void OnReturnedToPool();
    }
}
