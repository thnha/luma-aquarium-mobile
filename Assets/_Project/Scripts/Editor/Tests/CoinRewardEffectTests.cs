using Lumaquarium.UI;
using NUnit.Framework;

public sealed class CoinRewardEffectTests
{
    [Test]
    public void CountStartsAtOldBalanceAndEndsAtNew()
    {
        Assert.AreEqual(120, CoinRewardEffect.CountAt(120, 140, 0));
        Assert.AreEqual(140, CoinRewardEffect.CountAt(120, 140, 1));
    }

    [Test]
    public void CountClampsOutsideTheRun()
    {
        Assert.AreEqual(120, CoinRewardEffect.CountAt(120, 140, -.5f));
        Assert.AreEqual(140, CoinRewardEffect.CountAt(120, 140, 2));
    }

    [Test]
    public void CountNeverGoesBackOrPastTheEnd()
    {
        int previous = 120;
        for (float t = 0; t <= 1; t += .01f)
        {
            int count = CoinRewardEffect.CountAt(120, 140, t);
            Assert.GreaterOrEqual(count, previous);
            Assert.LessOrEqual(count, 140);
            previous = count;
        }
    }

    [Test]
    public void CountEasesOutSoMostOfItShowsEarly() =>
        Assert.Greater(CoinRewardEffect.CountAt(0, 100, .5f), 50);
}
