using UnityEngine;

[CreateAssetMenu(menuName = "Game/Presentation Profile")]
public sealed class GamePresentationProfile : ScriptableObject
{
    public Ally AllyTemplate;
    public Sprite CoinIcon, BagIcon;
    public Sprite[] ItemIcons = new Sprite[11];
    public Color Silhouette = new(.12f,.17f,.21f,.24f);
    public float SunPeriodSeconds = 1200;
    private static GamePresentationProfile current;
    public static GamePresentationProfile Current => current != null ? current : current = Resources.Load<GamePresentationProfile>("UI/GamePresentationProfile");
}
