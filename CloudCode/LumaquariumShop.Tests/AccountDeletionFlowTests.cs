using Lumaquarium.Account;
using NUnit.Framework;

namespace LumaquariumShop.Tests;

public sealed class AccountDeletionFlowTests
{
    private readonly List<string> calls = new();
    private AccountDeletionStage stage;
    private string? fail;

    private Task Step(string name)
    {
        calls.Add(name);
        if (fail == name) throw new InvalidOperationException(name);
        return Task.CompletedTask;
    }

    private Task Run() => AccountDeletionFlow.Run(stage,
        () => Step("prepare"), () => Step("cloud"), () => Step("identity"), () => Step("unity"),
        value => { stage = value; calls.Add("checkpoint:" + value); }, () => { calls.Add("local"); stage = AccountDeletionStage.None; });

    [SetUp]
    public void Reset() { calls.Clear(); stage = AccountDeletionStage.None; fail = null; }

    [Test]
    public async Task SuccessDeletesCloudAndBothIdentitiesBeforeLocalProgress()
    {
        await Run();
        Assert.That(calls, Is.EqualTo(new[] { "prepare", "checkpoint:Confirmed", "cloud", "checkpoint:CloudDeleted",
            "checkpoint:IdentityDeletionStarted", "identity", "checkpoint:IdentityDeleted", "checkpoint:AccountDeletionStarted", "unity", "checkpoint:AccountDeleted", "local" }));
        Assert.That(stage, Is.EqualTo(AccountDeletionStage.None));
    }

    [TestCase("prepare", AccountDeletionStage.None)]
    [TestCase("cloud", AccountDeletionStage.Confirmed)]
    [TestCase("identity", AccountDeletionStage.IdentityDeletionStarted)]
    [TestCase("unity", AccountDeletionStage.AccountDeletionStarted)]
    public async Task FailurePreservesCheckpointAndRetryCompletes(string failure, AccountDeletionStage expected)
    {
        fail = failure;
        Assert.ThrowsAsync<InvalidOperationException>(Run);
        Assert.That(stage, Is.EqualTo(expected));
        Assert.That(calls, Does.Not.Contain("local"));
        calls.Clear();
        fail = null;
        await Run();
        Assert.That(calls.Last(), Is.EqualTo("local"));
        if (expected >= AccountDeletionStage.IdentityDeleted)
        {
            Assert.That(calls, Does.Not.Contain("prepare"));
            Assert.That(calls, Does.Not.Contain("identity"));
        }
        if (expected >= AccountDeletionStage.CloudDeleted) Assert.That(calls, Does.Not.Contain("cloud"));
    }

    [Test]
    public void CancelingReauthenticationDoesNotStartDeletion()
    {
        Assert.ThrowsAsync<OperationCanceledException>(() => AccountDeletionFlow.Run(stage,
            () => throw new OperationCanceledException(), () => Step("cloud"), () => Step("identity"), () => Step("unity"),
            value => stage = value, () => calls.Add("local")));
        Assert.That(stage, Is.EqualTo(AccountDeletionStage.None));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task RestartAfterUnityDeletionOnlyClearsDeviceProgress()
    {
        stage = AccountDeletionStage.AccountDeleted;
        await Run();
        Assert.That(calls, Is.EqualTo(new[] { "local" }));
    }
}
