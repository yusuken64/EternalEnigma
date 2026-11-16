using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayMusic : MonoBehaviour
{
    public AudioClip MainMenuMusic;
    // Filled by the deferred web build, which ships music separately and clears the clip.
    [HideInInspector] public string MainMenuMusicTrack;

    // Start is called before the first frame update
    IEnumerator Start()
    {
        yield return LoadingSceneIntegration.EnsureCommon();
        Common.Instance.AudioManager.PlayMusic(MainMenuMusic, MainMenuMusicTrack);
    }
}
