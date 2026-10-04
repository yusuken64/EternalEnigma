using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AudioClipExtension
{
    public static void PlayAsUI(this AudioClip clip) => Common.Instance.AudioManager.PlayUISound(clip);
	public static void PlayAsSound(this AudioClip clip)
	{
		Common.Instance.AudioManager.PlaySoundEffect(clip);
	}
}
