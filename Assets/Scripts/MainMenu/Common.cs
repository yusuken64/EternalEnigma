using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Common : PersistedSingletonMonoBehaviour<Common>
{
	public GameSaveData GameSaveData = new();

	public ItemManager ItemManager;
	public GameObject SceneTransferObjects;
	protected override void Initialize()
	{
	}
}

public class LoadingSceneIntegration
{
#if UNITY_EDITOR
	public static int otherScene = -2;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void InitLoadingScene()
	{
		int sceneIndex = SceneManager.GetActiveScene().buildIndex;
		if (sceneIndex == 0) return;

		otherScene = sceneIndex;
		//make sure your _preload scene is the first in scene build list
		AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(0);
		asyncOperation.completed += AsyncOperation_completed;
	}

	private static void AsyncOperation_completed(AsyncOperation obj)
	{
		SceneManager.LoadScene(otherScene);
	}
#endif
}