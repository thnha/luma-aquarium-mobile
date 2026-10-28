using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace LumaquariumShop;

// A transaction as Apple reports it (the fields of JWSTransactionDecodedPayload the shop reads).
public sealed class AppStoreTransaction
{
    public string? transactionId { get; set; }
    public string? bundleId { get; set; }
    public string? productId { get; set; }
    public string? type { get; set; }
    public int quantity { get; set; } = 1;
    public long? revocationDate { get; set; }
    public string? environment { get; set; }
}

// Thrown by IAppStore when Apple cannot be asked now: the purchase is neither paid out nor finished, so it comes again.
public sealed class AppStoreUnavailableException : Exception
{
    public AppStoreUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

// Where the shop asks whether a purchase is real: Apple in the module, a fake in the tests.
public interface IAppStore
{
    // Null when Apple knows no such transaction for this app.
    Task<AppStoreTransaction?> GetTransaction(string transactionId);
}

// App Store Server API "Get Transaction Info", signed with the In-App Purchase key kept in the Secret Manager
// (APPSTORE_KEY_ID, APPSTORE_ISSUER_ID, APPSTORE_PRIVATE_KEY = the .p8 file). Production first, then sandbox
// (TestFlight and sandbox accounts), also after a 401 from production, which an unreleased app gets. The reply
// comes straight from Apple over TLS on our own key, so the signedTransactionInfo payload is read without checking
// its certificate chain again.
public sealed class AppStoreApi : IAppStore
{
    public const string BundleId = "com.thnha.bettafishaquarium";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly string[] Hosts = { "https://api.storekit.itunes.apple.com", "https://api.storekit-sandbox.itunes.apple.com" };
    private readonly IExecutionContext context;
    private readonly IGameApiClient api;
    private readonly ILogger? logger;
    // What a 401 is checked against, without the secrets themselves: Apple's Key ID is 10 characters, the Issuer ID a UUID.
    private string keyShape = "";

    public AppStoreApi(IExecutionContext context, IGameApiClient api, ILogger? logger = null)
    {
        this.context = context;
        this.api = api;
        this.logger = logger;
    }

    public async Task<AppStoreTransaction?> GetTransaction(string transactionId)
    {
        try { return await Ask(transactionId); }
        catch (AppStoreUnavailableException e)
        {
            logger?.LogError(e, "App Store transaction {TransactionId} not checked: {Reason}", transactionId, e.Message);
            throw;
        }
    }

    private async Task<AppStoreTransaction?> Ask(string transactionId)
    {
        string token = await Token();
        string? refused = null;
        foreach (string host in Hosts)
        {
            HttpResponseMessage response;
            try { response = await Send(host, transactionId, token); }
            catch (HttpRequestException e)
            {
                // A dropped connection is often gone a moment later: ask once more before giving up.
                logger?.LogWarning("App Store {Host}: {Reason}, asking again", host, Reasons(e));
                await Task.Delay(500);
                try { response = await Send(host, transactionId, token); }
                catch (Exception again) when (again is HttpRequestException or TaskCanceledException)
                {
                    throw new AppStoreUnavailableException($"{host}: {Reasons(again)}", again);
                }
            }
            catch (TaskCanceledException e)
            {
                throw new AppStoreUnavailableException($"{host}: {Reasons(e)}", e);
            }
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.NotFound) continue;
                // Production answers 401 for every transaction of an app not yet released on the App Store: the
                // sandbox may still know it. Only when no host does is the 401 the answer (Apple refused our key).
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    refused ??= $"{host}: HTTP 401 ({keyShape})";
                    continue;
                }
                // 400 is a malformed transaction id: Apple will never know it.
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    logger?.LogWarning("App Store {Host}: transaction {TransactionId} is malformed: {Body}", host, transactionId, await response.Content.ReadAsStringAsync());
                    return null;
                }
                if (!response.IsSuccessStatusCode)
                    throw new AppStoreUnavailableException($"{host}: HTTP {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                string jws = body.RootElement.GetProperty("signedTransactionInfo").GetString() ?? "";
                return Decode(jws);
            }
        }
        // A host that refused us may know the transaction: not paid, not finished, asked again later.
        if (refused != null) throw new AppStoreUnavailableException(refused);
        logger?.LogWarning("App Store: transaction {TransactionId} unknown to production and sandbox", transactionId);
        return null;
    }

    private static async Task<HttpResponseMessage> Send(string host, string transactionId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{host}/inApps/v1/transactions/{Uri.EscapeDataString(transactionId)}");
        request.Headers.Authorization = new("Bearer", token);
        return await Http.SendAsync(request);
    }

    // "An error occurred while sending the request" says nothing: the inner exceptions name DNS, TLS or the socket.
    private static string Reasons(Exception e)
    {
        var reasons = new List<string>();
        for (var x = e; x != null; x = x.InnerException) reasons.Add($"{x.GetType().Name}: {x.Message}");
        return string.Join(" <- ", reasons);
    }

    public static AppStoreTransaction? Decode(string jws)
    {
        string[] parts = jws.Split('.');
        return parts.Length == 3 ? JsonSerializer.Deserialize<AppStoreTransaction>(Base64Url(parts[1])) : null;
    }

    private async Task<string> Token()
    {
        string keyId, issuerId, privateKey;
        try
        {
            // Pasted into the dashboard, a value easily keeps a space or line break, which Apple rejects (401).
            keyId = (await api.SecretManager.GetSecret(context, "APPSTORE_KEY_ID")).Value.Trim();
            issuerId = (await api.SecretManager.GetSecret(context, "APPSTORE_ISSUER_ID")).Value.Trim();
            privateKey = (await api.SecretManager.GetSecret(context, "APPSTORE_PRIVATE_KEY")).Value.Trim();
        }
        catch (Exception e)
        {
            throw new AppStoreUnavailableException("App Store secrets missing: " + e.Message, e);
        }
        keyShape = $"key id {keyId.Length} chars, issuer id {(Guid.TryParse(issuerId, out _) ? "a UUID" : "not a UUID")}, private key {privateKey.Length} chars";
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid = keyId, typ = "JWT" }));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = issuerId, iat = now, exp = now + 600, aud = "appstoreconnect-v1", bid = BundleId }));
        using var key = ECDsa.Create();
        try { key.ImportFromPem(privateKey); }
        catch (Exception e) when (e is ArgumentException or CryptographicException)
        {
            throw new AppStoreUnavailableException("APPSTORE_PRIVATE_KEY is not the .p8 file's PEM text: " + e.Message, e);
        }
        // .NET signs in the IEEE P1363 form (r || s) that JWS ES256 expects.
        byte[] signature = key.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256);
        return header + "." + payload + "." + Base64Url(signature);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64Url(string text)
    {
        string s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
