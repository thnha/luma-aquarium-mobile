using System.Reflection;
using NUnit.Framework;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;

namespace LumaquariumShop.Tests;

public sealed class PlayerDataDeletionTests
{
    public class Proxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handle = (_, _) => null;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handle(method!, args);
    }

    private static T Mock<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var mock = DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)mock).Handle = handler;
        return mock;
    }

    [Test]
    public async Task DeletesEveryPlayerAccessClassUsingCallerAndServiceToken()
    {
        var calls = new List<string>();
        var context = Mock<IExecutionContext>((m, _) => m.Name switch {
            "get_PlayerId" => "caller", "get_ProjectId" => "project", "get_ServiceToken" => "service", _ => null });
        var api = Mock<ICloudSaveDataApi>((m, args) => {
            Assert.That(args![0], Is.SameAs(context));
            Assert.That(args[1], Is.EqualTo("service"));
            Assert.That(args[2], Is.EqualTo("project"));
            Assert.That(args[3], Is.EqualTo("caller"));
            calls.Add(m.Name);
            return Task.FromResult<ApiResponse>(null!);
        });
        await PlayerDataDeletion.Delete(context, api);
        Assert.That(calls, Is.EquivalentTo(new[] { "DeleteItemsAsync", "DeleteProtectedItemsAsync", "DeletePublicItemsAsync", "DeletePrivateItemsAsync" }));
    }

    [Test]
    public void MissingCallerIsRejectedBeforeAnyDataIsDeleted()
    {
        var context = Mock<IExecutionContext>((_, _) => null);
        var api = Mock<ICloudSaveDataApi>((_, _) => throw new AssertionException("Deletion must not run"));
        Assert.ThrowsAsync<UnauthorizedAccessException>(() => PlayerDataDeletion.Delete(context, api));
    }

    [Test]
    public void FailureIsReturnedToClientRatherThanReportingSuccess()
    {
        var context = Mock<IExecutionContext>((_, _) => "caller");
        var api = Mock<ICloudSaveDataApi>((_, _) => Task.FromException<ApiResponse>(new InvalidOperationException("offline")));
        Assert.ThrowsAsync<InvalidOperationException>(() => PlayerDataDeletion.Delete(context, api));
    }
}
