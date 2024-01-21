using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
{
    [SerializeField]
    private List<TKey> keys = new List<TKey>();

    [SerializeField]
    private List<TValue> values = new List<TValue>();

    private Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();

        foreach (var kvp in dictionary)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        dictionary.Clear();

        if (keys.Count != values.Count)
        {
            Debug.LogError("Number of keys and values in SerializableDictionary do not match!");
            return;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            if (!dictionary.ContainsKey(keys[i]))
            {
                dictionary.Add(keys[i], values[i]);
            }
            else
            {
                Debug.LogWarning($"Duplicate key found in SerializableDictionary: {keys[i]}");
            }
        }
    }

    public void Add(TKey key, TValue value)
    {
        if (!dictionary.ContainsKey(key))
        {
            dictionary.Add(key, value);
        }
        else
        {
            Debug.LogWarning($"Key {key} already exists in SerializableDictionary. Updating value.");
            dictionary[key] = value;
        }
    }

    public bool Remove(TKey key)
    {
        return dictionary.Remove(key);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        return dictionary.TryGetValue(key, out value);
    }

    public bool ContainsKey(TKey key)
    {
        return dictionary.ContainsKey(key);
    }

    public bool ContainsValue(TValue value)
    {
        return dictionary.ContainsValue(value);
    }

    public Dictionary<TKey, TValue> ToDictionary()
    {
        return new Dictionary<TKey, TValue>(dictionary);
    }

    public static SerializableDictionary<TKey, TValue> FromDictionary(Dictionary<TKey, TValue> sourceDictionary)
    {
        SerializableDictionary<TKey, TValue> ret = new();

        foreach (var kvp in sourceDictionary)
        {
            ret.Add(kvp.Key, kvp.Value);
        }

        return ret;
    }
}
