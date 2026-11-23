using System.Collections;
using UnityEngine;

public class PlayMusic : MonoBehaviour
{
    public AudioClip MainMenuMusic;
    // Filled by the deferred web build, which ships music separately and clears the clip.
    [HideInInspector] public string MainMenuMusicTrack;

    public AudioClip AlternateMusic;
    [HideInInspector] public string AlternateMusicTrack;

    IEnumerator Start()
    {
        yield return LoadingSceneIntegration.EnsureCommon();
        Common.Instance.AudioManager.PlayScore(MainMenuMusic, MainMenuMusicTrack, AlternateMusic, AlternateMusicTrack);
    }
}
