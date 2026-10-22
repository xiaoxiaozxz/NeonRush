using NUnit.Framework;

namespace LoongRacing.Tests
{
    public sealed class RaceTimeRulesTests
    {
        [TestCase(0f, false)]
        [TestCase(-1f, false)]
        [TestCase(float.NaN, false)]
        [TestCase(float.PositiveInfinity, false)]
        [TestCase(float.NegativeInfinity, false)]
        [TestCase(0.001f, true)]
        [TestCase(42f, true)]
        public void OnlyFinitePositiveTimesAreValid(float seconds, bool expected)
        {
            Assert.That(RaceTimeRules.IsValid(seconds), Is.EqualTo(expected));
        }

        [TestCase(40f, 42f, true)]
        [TestCase(42f, 42f, false)]
        [TestCase(43f, 42f, false)]
        [TestCase(42f, 0f, true)]
        [TestCase(42f, -1f, true)]
        [TestCase(42f, float.NaN, true)]
        [TestCase(42f, float.PositiveInfinity, true)]
        [TestCase(float.NaN, 42f, false)]
        [TestCase(float.PositiveInfinity, 42f, false)]
        [TestCase(0f, 42f, false)]
        public void RecordsAcceptOnlyValidFasterTimes(float candidate, float previous, bool expected)
        {
            Assert.That(RaceTimeRules.IsImprovement(candidate, previous), Is.EqualTo(expected));
        }
    }
}
