using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayMusic : MonoBehaviour
{
    public AudioClip MainMenuMusic;

    // Start is called before the first frame update
    IEnumerator Start()
    {
        yield return LoadingSceneIntegration.EnsureCommon();
        Common.Instance.AudioManager.PlayMusic(MainMenuMusic);
    }
}
