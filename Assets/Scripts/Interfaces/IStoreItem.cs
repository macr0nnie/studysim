using UnityEngine;

public interface IStoreItem
{
    string Name { get; }
    float Price { get; }
    bool IsOwned { get; set; }
    void Apply();
}