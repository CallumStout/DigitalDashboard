using SpotifyAPI.Web;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

public class SpotifyAuthenticationService
{
    private readonly string _clientId;
    private readonly string _tokenPath;

    public SpotifyAuthenticationService(string clientId)
    {
        _clientId = clientId;

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalDashboard");

        Directory.CreateDirectory(directory);

        _tokenPath = Path.Combine(directory, "spotify-token.json");
    }

    public async Task<PKCETokenResponse> AuthenticateAsync()
    {
        if (File.Exists(_tokenPath))
        {
            var json = await File.ReadAllTextAsync(_tokenPath);

            var savedToken = JsonSerializer.Deserialize<PKCETokenResponse>(json);

            if (savedToken != null)
            {
                return savedToken;
            }
        }

        var redirectUri = new Uri("http://127.0.0.1:8888/callback");

        var (verifier, challenge) = PKCEUtil.GenerateCodes();

        var loginRequest = new LoginRequest(redirectUri, _clientId, LoginRequest.ResponseType.Code)
        {
            CodeChallengeMethod = "S256",
            CodeChallenge = challenge,
            Scope = new[]
            {
                Scopes.UserReadPlaybackState,
                Scopes.UserReadCurrentlyPlaying,
                Scopes.UserModifyPlaybackState
            }
        };

        var loginUri = loginRequest.ToUri();

        using var listener = new HttpListener();

        listener.Prefixes.Add("http://127.0.0.1:8888/");
        listener.Start();

        Process.Start(new ProcessStartInfo
        {
            FileName = loginUri.ToString(),
            UseShellExecute = true
        });

        var context = await listener.GetContextAsync();

        var code = context.Request.QueryString["code"];
        var error = context.Request.QueryString["error"];

        const string response = "Spotify authentication complete. You can close this window.";

        var buffer = System.Text.Encoding.UTF8.GetBytes(response);

        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"Spotify authentication failed: {error}");
        }

        if (string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException("Spotify did not return an authorization code.");
        }

        var token = await new OAuthClient().RequestToken(
            new PKCETokenRequest(_clientId, code, redirectUri, verifier));

        await File.WriteAllTextAsync(
            _tokenPath,
            JsonSerializer.Serialize(token));

        return token;
    }

    public async Task<SpotifyClient> CreateClientAsync()
    {
        var token = await AuthenticateAsync();

        var authenticator = new PKCEAuthenticator(_clientId, token);

        authenticator.TokenRefreshed += (_, refreshedToken) =>
        {
            try
            {
                if (string.IsNullOrEmpty(refreshedToken.RefreshToken))
                {
                    refreshedToken.RefreshToken = token.RefreshToken;
                }

                var json = JsonSerializer.Serialize(refreshedToken);
                var temporaryPath = _tokenPath + ".tmp";

                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, _tokenPath, true);

                token = refreshedToken;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Spotify token save failed: {ex}");
            }
        };

        var config = SpotifyClientConfig.CreateDefault()
            .WithAuthenticator(authenticator);

        return new SpotifyClient(config);
    }
}
