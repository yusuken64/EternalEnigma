using UnityEngine;

public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
{
    private static T instance;

    // Optional per-frame consumers can check for an awakened scene object without searching for one.
    internal static T RegisteredInstance => instance != null ? instance : null;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<T>();
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"Multiple instances of {typeof(T)} found. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        instance = (T)this;
        //DontDestroyOnLoad(gameObject);
        Initialize();
    }

    protected virtual void Initialize()
    {
        // Override this method to perform initialization logic in derived classes
    }
}
