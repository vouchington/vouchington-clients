import Foundation

func androidTurnstileHTML(_ key: String) -> String {
    let escaped = key.replacingOccurrences(of: "&", with: "&amp;")
        .replacingOccurrences(of: "\"", with: "&quot;")
        .replacingOccurrences(of: "<", with: "&lt;")
    return """
    <!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1">
    <script src="https://challenges.cloudflare.com/turnstile/v0/api.js" onerror="location.href='voucha://turnstile?error'" async defer></script></head>
    <body><div class="cf-turnstile" data-sitekey="\(escaped)" data-callback="tokenReady"
    data-expired-callback="tokenExpired" data-error-callback="tokenError"></div>
    <script>
    function tokenReady(token){location.href='voucha://turnstile?token='+encodeURIComponent(token);}
    function tokenExpired(){location.href='voucha://turnstile?expired';}
    function tokenError(){location.href='voucha://turnstile?error';}
    </script></body></html>
    """
}
