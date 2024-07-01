using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AudioClipExtension
{
	public static void PlayAsSound(this AudioClip clip)
	{
		Common.Instance.AudioManager.PlaySoundEffect(clip);
	}
}
