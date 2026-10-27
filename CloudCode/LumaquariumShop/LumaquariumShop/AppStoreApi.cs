using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
// (TestFlight and sandbox accounts). The reply comes straight from Apple over TLS on our own key, so the
// signedTransactionInfo payload is read without checking its certificate chain again.
public sealed class AppStoreApi : IAppStore
{
    public const string BundleId = "com.thnha.bettafishaquarium";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly string[] Hosts = { "https://api.storekit.itunes.apple.com", "https://api.storekit-sandbox.itunes.apple.com" };
    private readonly IExecutionContext context;
    private readonly IGameApiClient api;

    public AppStoreApi(IExecutionContext context, IGameApiClient api)
    {
        this.context = context;
        this.api = api;
    }

    public async Task<AppStoreTransaction?> GetTransaction(string transactionId)
    {
        string token = await Token();
        foreach (string host in Hosts)
        {
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{host}/inApps/v1/transactions/{Uri.EscapeDataString(transactionId)}");
                request.Headers.Authorization = new("Bearer", token);
                response = await Http.SendAsync(request);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                throw new AppStoreUnavailableException($"{host}: {e.Message}", e);
            }
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.NotFound) continue;
                // 400 is a malformed transaction id: Apple will never know it.
                if (response.StatusCode == HttpStatusCode.BadRequest) return null;
                if (!response.IsSuccessStatusCode) throw new AppStoreUnavailableException($"{host}: HTTP {(int)response.StatusCode}");
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                string jws = body.RootElement.GetProperty("signedTransactionInfo").GetString() ?? "";
                return Decode(jws);
            }
        }
        return null;
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
            keyId = (await api.SecretManager.GetSecret(context, "APPSTORE_KEY_ID")).Value;
            issuerId = (await api.SecretManager.GetSecret(context, "APPSTORE_ISSUER_ID")).Value;
            privateKey = (await api.SecretManager.GetSecret(context, "APPSTORE_PRIVATE_KEY")).Value;
        }
        catch (Exception e)
        {
            throw new AppStoreUnavailableException("App Store secrets missing: " + e.Message, e);
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid = keyId, typ = "JWT" }));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = issuerId, iat = now, exp = now + 600, aud = "appstoreconnect-v1", bid = BundleId }));
        using var key = ECDsa.Create();
        key.ImportFromPem(privateKey);
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
