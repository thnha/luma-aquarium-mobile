using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace LumaquariumShop;

// A one-time purchase as Google Play reports it (the fields of ProductPurchase the shop reads).
public sealed class GooglePlayPurchase
{
    public int purchaseState { get; set; }       // 0 purchased, 1 cancelled (refunded), 2 pending
    public int consumptionState { get; set; }    // 0 yet to be consumed, 1 consumed
    public int? quantity { get; set; }
    public string? orderId { get; set; }
    public int? purchaseType { get; set; }       // 0 test (license tester), 1 promo, 2 rewarded; absent = paid
}

// Thrown by IGooglePlay when Google cannot be asked now: the purchase is neither paid out nor consumed, so it comes again.
public sealed class GooglePlayUnavailableException : Exception
{
    public GooglePlayUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

// Where the shop asks whether a purchase is real: Google in the module, a fake in the tests.
public interface IGooglePlay
{
    // Null when Google knows no such purchase of this product in this app.
    Task<GooglePlayPurchase?> GetPurchase(string productId, string purchaseToken);
}

// Google Play Developer API "purchases.products.get", with an OAuth token for the service account kept in the
// Secret Manager (GOOGLE_PLAY_SERVICE_ACCOUNT = its JSON key file). The account is invited in Play Console with
// "View financial data" and "Manage orders" for this app.
public sealed class GooglePlayApi : IGooglePlay
{
    public const string PackageName = "com.thnha.bettafishaquarium";
    private const string Scope = "https://www.googleapis.com/auth/androidpublisher";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    // One token per module instance, renewed a minute before Google's hour runs out.
    private static string? cachedToken;
    private static DateTimeOffset cachedUntil;
    private readonly IExecutionContext context;
    private readonly IGameApiClient api;
    private readonly ILogger? logger;

    public GooglePlayApi(IExecutionContext context, IGameApiClient api, ILogger? logger = null)
    {
        this.context = context;
        this.api = api;
        this.logger = logger;
    }

    public async Task<GooglePlayPurchase?> GetPurchase(string productId, string purchaseToken)
    {
        try { return await Ask(productId, purchaseToken); }
        catch (GooglePlayUnavailableException e)
        {
            logger?.LogError(e, "Google Play purchase of {ProductId} not checked: {Reason}", productId, e.Message);
            throw;
        }
    }

    private async Task<GooglePlayPurchase?> Ask(string productId, string purchaseToken)
    {
        string token = await AccessToken();
        string url = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{PackageName}/purchases/products/"
            + $"{Uri.EscapeDataString(productId)}/tokens/{Uri.EscapeDataString(purchaseToken)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", token);
        HttpResponseMessage response;
        try { response = await Http.SendAsync(request); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new GooglePlayUnavailableException(e.Message, e);
        }
        using (response)
        {
            string body = await response.Content.ReadAsStringAsync();
            // 400 is a token Google cannot read, 404/410 one it does not know (or no longer keeps) for this product.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                logger?.LogWarning("Google Play: purchase of {ProductId} unknown ({Status}): {Body}", productId, (int)response.StatusCode, body);
                return null;
            }
            // 401/403 is our service account not (yet) allowed for this app: not the player's fault, asked again later.
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized) cachedToken = null;
                throw new GooglePlayUnavailableException($"HTTP {(int)response.StatusCode} {body}");
            }
            return JsonSerializer.Deserialize<GooglePlayPurchase>(body);
        }
    }

    private async Task<string> AccessToken()
    {
        if (cachedToken != null && DateTimeOffset.UtcNow < cachedUntil) return cachedToken;
        string email, privateKey, tokenUri;
        try
        {
            string json = (await api.SecretManager.GetSecret(context, "GOOGLE_PLAY_SERVICE_ACCOUNT")).Value;
            using var key = JsonDocument.Parse(json);
            email = key.RootElement.GetProperty("client_email").GetString() ?? "";
            privateKey = key.RootElement.GetProperty("private_key").GetString() ?? "";
            tokenUri = key.RootElement.TryGetProperty("token_uri", out var uri) ? uri.GetString() ?? "" : "";
        }
        catch (Exception e)
        {
            throw new GooglePlayUnavailableException("GOOGLE_PLAY_SERVICE_ACCOUNT missing or not the service account's JSON key: " + e.Message, e);
        }
        if (string.IsNullOrEmpty(tokenUri)) tokenUri = "https://oauth2.googleapis.com/token";

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = email, scope = Scope, aud = tokenUri, iat = now, exp = now + 3600 }));
        using var rsa = RSA.Create();
        try { rsa.ImportFromPem(privateKey); }
        catch (Exception e) when (e is ArgumentException or CryptographicException)
        {
            throw new GooglePlayUnavailableException("GOOGLE_PLAY_SERVICE_ACCOUNT private_key is not a PEM key: " + e.Message, e);
        }
        byte[] signature = rsa.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string assertion = header + "." + payload + "." + Base64Url(signature);

        HttpResponseMessage response;
        try
        {
            response = await Http.PostAsync(tokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new GooglePlayUnavailableException("token: " + e.Message, e);
        }
        using (response)
        {
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new GooglePlayUnavailableException($"token: HTTP {(int)response.StatusCode} {body}");
            using var reply = JsonDocument.Parse(body);
            cachedToken = reply.RootElement.GetProperty("access_token").GetString();
            int seconds = reply.RootElement.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 3600;
            cachedUntil = DateTimeOffset.UtcNow.AddSeconds(seconds - 60);
            return cachedToken ?? throw new GooglePlayUnavailableException("token: no access_token");
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
