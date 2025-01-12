public interface ISaveSystem
{
    void SaveData(string key, object data);
    T LoadData<T>(string key, T defaultValue);
}