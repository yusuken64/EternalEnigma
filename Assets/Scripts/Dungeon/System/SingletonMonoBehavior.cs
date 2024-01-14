using UnityEngine;

public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<T>();

                if (instance == null)
                {
                    Debug.LogError($"An instance of {typeof(T)} is needed in the scene, but there is none.");
                }
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
