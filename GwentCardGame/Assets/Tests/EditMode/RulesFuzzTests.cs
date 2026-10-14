using NUnit.Framework;

namespace Gwent.Rules.Tests
{
    // A few hundred random battles through the rules engine (RulesFuzzer): every invariant must hold all along
    public class RulesFuzzTests
    {
        [Test]
        public void RandomAndAbsurdBattlesKeepEveryInvariant()
        {
            RulesFuzzer.Report report = RulesFuzzer.Run(400, 1);
            Assert.AreEqual(0, report.Failures.Count, report.ToString());
            Assert.Greater(report.Stats["rounds"], 400, "battles went through their rounds");
        }
    }
}
