import SwiftUI
import WebKit

struct NativeTurnstileChallengeView: View {
    let siteKey: String
    let onToken: (String) -> Void

    var body: some View {
        NativeTurnstileWebView(siteKey: siteKey, onToken: onToken)
            .frame(minWidth: 320, minHeight: 240)
            .padding()
    }
}

private final class NativeTurnstileCoordinator: NSObject, WKScriptMessageHandler {
    let onToken: (String) -> Void

    init(onToken: @escaping (String) -> Void) {
        self.onToken = onToken
    }

    func userContentController(
        _: WKUserContentController,
        didReceive message: WKScriptMessage
    ) {
        guard message.name == "turnstileToken", let token = message.body as? String, !token.isEmpty else {
            return
        }
        onToken(token)
    }
}

private func makeTurnstileWebView(siteKey: String, coordinator: NativeTurnstileCoordinator) -> WKWebView {
    let contentController = WKUserContentController()
    contentController.add(coordinator, name: "turnstileToken")
    let configuration = WKWebViewConfiguration()
    configuration.userContentController = contentController
    let webView = WKWebView(frame: .zero, configuration: configuration)
    webView.loadHTMLString(turnstileHTML(siteKey: siteKey), baseURL: URL(string: "https://voucha.ai"))
    return webView
}

private func turnstileHTML(siteKey: String) -> String {
    let escapedSiteKey = siteKey
        .replacingOccurrences(of: "&", with: "&amp;")
        .replacingOccurrences(of: "\"", with: "&quot;")
        .replacingOccurrences(of: "<", with: "&lt;")
    return """
    <!doctype html>
    <html>
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script>
        <style>
          body { align-items: center; display: flex; height: 100vh; justify-content: center; margin: 0; }
        </style>
      </head>
      <body>
        <div class="cf-turnstile" data-sitekey="\(escapedSiteKey)" data-callback="onTurnstileToken"></div>
        <script>
          function onTurnstileToken(token) {
            window.webkit.messageHandlers.turnstileToken.postMessage(token);
          }
        </script>
      </body>
    </html>
    """
}

#if os(iOS)
    private struct NativeTurnstileWebView: UIViewRepresentable {
        let siteKey: String
        let onToken: (String) -> Void

        func makeCoordinator() -> NativeTurnstileCoordinator {
            NativeTurnstileCoordinator(onToken: onToken)
        }

        func makeUIView(context: Context) -> WKWebView {
            makeTurnstileWebView(siteKey: siteKey, coordinator: context.coordinator)
        }

        func updateUIView(_: WKWebView, context _: Context) {}

        static func dismantleUIView(_ uiView: WKWebView, coordinator _: NativeTurnstileCoordinator) {
            uiView.configuration.userContentController.removeScriptMessageHandler(forName: "turnstileToken")
        }
    }

#elseif os(macOS)
    private struct NativeTurnstileWebView: NSViewRepresentable {
        let siteKey: String
        let onToken: (String) -> Void

        func makeCoordinator() -> NativeTurnstileCoordinator {
            NativeTurnstileCoordinator(onToken: onToken)
        }

        func makeNSView(context: Context) -> WKWebView {
            makeTurnstileWebView(siteKey: siteKey, coordinator: context.coordinator)
        }

        func updateNSView(_: WKWebView, context _: Context) {}

        static func dismantleNSView(_ nsView: WKWebView, coordinator _: NativeTurnstileCoordinator) {
            nsView.configuration.userContentController.removeScriptMessageHandler(forName: "turnstileToken")
        }
    }
#endif
