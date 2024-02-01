using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Common : PersistedSingletonMonoBehaviour<Common>
{
	public GameSaveData GameSaveData = new();

	public AudioManager AudioManager;
	public ItemManager ItemManager;
	public SkillManager SkillManager;
	public GameObject SceneTransferObjects;
	protected override void Initialize()
	{
#if !UNITY_EDITOR
		SceneManager.LoadScene(1);
#endif
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
		if (sceneIndex == 0)
		{
			otherScene = 1;
		};

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