public class Stairs : Interactable
{
	internal override void DoInteraction()
	{
		Game.Instance.AdvanceFloor();
	}

	internal override string GetInteractionText()
	{
		return "Take Stairs";
	}
}
