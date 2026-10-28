using System.Collections.Generic;
using System.Threading.Tasks;
using Lumaquarium.Account;
using Lumaquarium.Shop;
using Lumaquarium.UI;
using NUnit.Framework;
using UnityEngine;

public sealed class AccountDeletionTests
{
    private const string Key = "Lumaquarium.Tests.AccountDeletion";
    private bool hadStage;
    private int previousStage;

    private sealed class Cloud : IShopCloud
    {
        public int Calls;
        public TaskCompletionSource<ShopReply> Reply = new TaskCompletionSource<ShopReply>();
        public Task<ShopReply> Call(string function, Dictionary<string, object> args) { Calls++; return Reply.Task; }
    }

    [SetUp]
    public void SaveCheckpoint()
    {
        hadStage = PlayerPrefs.HasKey(AccountSignIn.DeletionStageKey);
        previousStage = PlayerPrefs.GetInt(AccountSignIn.DeletionStageKey);
        PlayerPrefs.DeleteKey(AccountSignIn.DeletionStageKey);
    }

    [TearDown]
    public void RestoreCheckpoint()
    {
        if (hadStage) PlayerPrefs.SetInt(AccountSignIn.DeletionStageKey, previousStage);
        else PlayerPrefs.DeleteKey(AccountSignIn.DeletionStageKey);
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.DeleteKey(Key + ".imported");
    }

    [Test]
    public void PendingDeletionRejectsShopCalls()
    {
        PlayerPrefs.SetInt(AccountSignIn.DeletionStageKey, 1);
        var cloud = new Cloud();
        var backend = new ServerShopBackend(cloud, Key);
        Assert.AreEqual(ShopError.Rejected, backend.ApplyGifts().GetAwaiter().GetResult());
        Assert.AreEqual(0, cloud.Calls);
    }

    [Test]
    public void PendingDeletionCannotRestoreInventoryFromALateReply()
    {
        var cloud = new Cloud();
        var backend = new ServerShopBackend(cloud, Key);
        var pending = backend.ApplyGifts();
        Assert.AreEqual(1, cloud.Calls);
        PlayerPrefs.SetInt(AccountSignIn.DeletionStageKey, 1);
        cloud.Reply.SetResult(new ShopReply { State = JsonUtility.ToJson(new ShopSave()) });
        Assert.AreEqual(ShopError.Rejected, pending.GetAwaiter().GetResult());
        Assert.False(PlayerPrefs.HasKey(Key));
    }

    [Test]
    public void PendingDeletionRejectsLocalSaveReplacement()
    {
        PlayerPrefs.SetInt(AccountSignIn.DeletionStageKey, 1);
        Assert.False(new FishShopInventory(Key).ReplaceWith(JsonUtility.ToJson(new ShopSave())));
        Assert.False(PlayerPrefs.HasKey(Key));
    }

    [Test]
    public void ConfirmationPrefabBlocksInputBehindIt()
    {
        var prefab = Resources.Load<GameObject>("UI/DeleteAccountDialog");
        Assert.NotNull(prefab, "Account deletion confirmation must be included in player builds.");
        Assert.NotNull(prefab.GetComponent<UnityEngine.UI.GraphicRaycaster>());
        var backdrop = prefab.transform.Find("Backdrop").GetComponent<UnityEngine.UI.Image>();
        Assert.True(backdrop.raycastTarget);
    }
}
