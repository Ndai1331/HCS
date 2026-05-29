using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;

namespace HC.PushNotificationWorker.Services;

/// <summary>
/// Validates Firebase service account JSON and detects OAuth credential failures.
/// </summary>
internal static class FirebaseCredentialHelper
{
    private const string FirebaseMessagingScope = "https://www.googleapis.com/auth/firebase.messaging";

    public static GoogleCredential LoadCredential(string path)
    {
        return GoogleCredential.FromFile(path).CreateScoped(FirebaseMessagingScope);
    }

    public static async Task ValidateAccessTokenAsync(GoogleCredential credential, CancellationToken cancellationToken = default)
    {
        if (credential.UnderlyingCredential is not ITokenAccess tokenAccess)
        {
            throw new InvalidOperationException("Firebase credential does not implement ITokenAccess.");
        }

        await tokenAccess.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public static bool IsCredentialError(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is TokenResponseException tokenEx
                && string.Equals(tokenEx.Error?.Error, "invalid_grant", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string? TryReadCredentialMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            var projectId = root.TryGetProperty("project_id", out var pid) ? pid.GetString() : null;
            var keyId = root.TryGetProperty("private_key_id", out var kid) ? kid.GetString() : null;
            var email = root.TryGetProperty("client_email", out var em) ? em.GetString() : null;

            return $"project_id={projectId}, private_key_id={keyId}, client_email={email}";
        }
        catch
        {
            return null;
        }
    }
}
