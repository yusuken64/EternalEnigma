using NUnit.Framework;
using UnityEngine;

public class PresentationActionTests
{
    [Test]
    public void UnseenPresentationActionsDoNotWaitOrInstantiateEffects()
    {
        // Null scene dependencies ensure skipped effects do not access audio, prefabs, or targets.
        GameAction[] actions = { new RangedAttackAction(), new ThrowItemAction(),
            new LevelUpAction(), new UseInventoryItemAction(), new SkillAction(), new ExplosionAction() };
        for (int i = 0; i < actions.Length; i++)
            Assert.That(actions[i].ExecuteRoutine(null, true).MoveNext(), Is.False, actions[i].GetType().Name);
    }
}
