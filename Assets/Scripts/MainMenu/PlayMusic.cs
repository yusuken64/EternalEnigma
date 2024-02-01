using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayMusic : MonoBehaviour
{
    public AudioClip MainMenuMusic;

    // Start is called before the first frame update
    void Start()
    {
        Common.Instance.AudioManager.PlayMusic(MainMenuMusic);
    }
}
