using System.Text.Json;
using Voucha.Client.Core;

namespace Voucha.Client.App.Pages;

public partial class TurnstileChallengePage : ContentPage
{
  private const string CallbackScheme = "voucha-turnstile";
  private bool completed;

  public TurnstileChallengePage(AppConfig appConfig)
  {
    InitializeComponent();
    TurnstileWebView.Source = new HtmlWebViewSource
    {
      BaseUrl = (appConfig.WebBaseUrl ?? new Uri(AppConfig.DefaultWebBaseUrl)).ToString(),
      Html = BuildHtml(appConfig.RequiredTurnstileSiteKey),
    };
  }

  public event EventHandler<TurnstileTokenCapturedEventArgs>? TokenCaptured;

  public event EventHandler? CloseRequested;

  private void OnCloseClicked(object? sender, EventArgs e) =>
      CloseRequested?.Invoke(this, EventArgs.Empty);

  private void OnNavigating(object? sender, WebNavigatingEventArgs e)
  {
    if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) ||
        !string.Equals(uri.Scheme, CallbackScheme, StringComparison.OrdinalIgnoreCase))
    {
      return;
    }

    e.Cancel = true;
    if (completed)
    {
      return;
    }

    if (uri.Host is "complete" && TryReadQueryValue(uri.Query, "token") is { Length: > 0 } token)
    {
      completed = true;
      TokenCaptured?.Invoke(this, new TurnstileTokenCapturedEventArgs(token));
      return;
    }

    if (uri.Host is "close" or "error" or "expired")
    {
      CloseRequested?.Invoke(this, EventArgs.Empty);
    }
  }

  private static string BuildHtml(string siteKey)
  {
    var jsonSiteKey = JsonSerializer.Serialize(siteKey);
    return $$"""
            <!doctype html>
            <html>
              <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <style>
                  html, body {
                    margin: 0;
                    min-height: 100%;
                    background: #ffffff;
                    color: #0f172a;
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                  }
                  body {
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    padding: 24px;
                  }
                  #turnstile {
                    min-height: 78px;
                  }
                </style>
                <script src="https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit" async defer></script>
                <script>
                  const siteKey = {{jsonSiteKey}};
                  const callbackScheme = "{{CallbackScheme}}";

                  function complete(token) {
                    window.location.href = callbackScheme + "://complete?token=" + encodeURIComponent(token);
                  }

                  function fail(kind) {
                    window.location.href = callbackScheme + "://" + kind;
                  }

                  function mount() {
                    if (!window.turnstile) {
                      window.setTimeout(mount, 50);
                      return;
                    }

                    window.turnstile.render("#turnstile", {
                      sitekey: siteKey,
                      callback: complete,
                      "expired-callback": () => fail("expired"),
                      "error-callback": () => fail("error"),
                    });
                  }

                  window.addEventListener("load", mount);
                </script>
              </head>
              <body>
                <div id="turnstile"></div>
              </body>
            </html>
            """;
  }

  private static string? TryReadQueryValue(string query, string name)
  {
#pragma warning disable CA1307
    if (string.IsNullOrWhiteSpace(query))
    {
      return null;
    }

    foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
      var index = part.IndexOf('=');
      var key = index >= 0 ? part[..index] : part;
      if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.Ordinal))
      {
        continue;
      }

      var value = index >= 0 ? part[(index + 1)..] : "";
      return Uri.UnescapeDataString(value);
    }

    return null;
#pragma warning restore CA1307
  }
}

public sealed class TurnstileTokenCapturedEventArgs : EventArgs
{
  public TurnstileTokenCapturedEventArgs(string token) => Token = token;

  public string Token { get; }
}
