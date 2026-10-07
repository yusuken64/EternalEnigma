#if UNITY_EDITOR
using NUnit.Framework.Interfaces;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(TestInputCleanup))]

// UnityTest can abandon an outer iterator when a yielded helper fails. Its using
// and finally blocks then never run, even though NUnit continues with other tests.
public sealed class TestInputCleanup : ITestRunCallback
{
    public void RunStarted(ITest testsToRun) { }
    public void TestStarted(ITest test) { }
    public void RunFinished(ITestResult result) => TestInputScope.RestoreAll();
    public void TestFinished(ITestResult result)
    {
        if (!result.Test.IsSuite) TestInputScope.RestoreAll();
    }
}
#endif
