import SwiftUI
import VouchaAuth
import VouchaCore
import VouchaLocalization
import WebKit

struct NativeTurnstileChallengeView: View {
    let siteKey: String
    let action: AppAttestActionTag
    var baseURL: URL = AppConfig.shared.baseURL
    let onToken: (String) -> Void

    @Environment(\.locale)
    private var locale

    var body: some View {
        if let context = TurnstileChallengeContext(actionTag: action.rawValue, baseURL: baseURL) {
            NativeTurnstileWebView(siteKey: siteKey, challenge: context, onToken: onToken)
                .id(context)
                .frame(minWidth: 320, minHeight: 240)
                .padding()
        } else {
            Text(UiMessages.string(.nativeAuthVerificationTokenRequired, locale: locale))
        }
    }
}

final class NativeTurnstileCoordinator: NSObject, WKScriptMessageHandler {
    let onToken: (String) -> Void
    var isActive = true

    init(onToken: @escaping (String) -> Void) {
        self.onToken = onToken
    }

    func userContentController(
        _: WKUserContentController,
        didReceive message: WKScriptMessage
    ) {
        receiveTokenMessage(name: message.name, body: message.body, isMainFrame: message.frameInfo.isMainFrame)
    }

    func receiveTokenMessage(name: String, body: Any, isMainFrame: Bool) {
        guard isActive, isMainFrame, name == "turnstileToken",
              let token = body as? String, !token.isEmpty else {
            return
        }
        onToken(token)
    }
}

func makeTurnstileWebView(
    siteKey: String,
    challenge: TurnstileChallengeContext,
    coordinator: NativeTurnstileCoordinator
) -> WKWebView {
    let contentController = WKUserContentController()
    contentController.add(coordinator, name: "turnstileToken")
    let configuration = WKWebViewConfiguration()
    configuration.userContentController = contentController
    let webView = WKWebView(frame: .zero, configuration: configuration)
    webView.loadHTMLString(turnstileHTML(siteKey: siteKey, challenge: challenge), baseURL: challenge.origin)
    return webView
}

func turnstileHTML(siteKey: String, challenge: TurnstileChallengeContext) -> String {
    let attributes = challenge.widgetAttributes(siteKey: siteKey)
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
        <div class="cf-turnstile" \(attributes) data-callback="onTurnstileToken"></div>
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
    struct NativeTurnstileWebView: UIViewRepresentable {
        let siteKey: String
        let challenge: TurnstileChallengeContext
        let onToken: (String) -> Void

        func makeCoordinator() -> NativeTurnstileCoordinator {
            NativeTurnstileCoordinator(onToken: onToken)
        }

        func makeUIView(context: Context) -> WKWebView {
            makeTurnstileWebView(siteKey: siteKey, challenge: challenge, coordinator: context.coordinator)
        }

        func updateUIView(_: WKWebView, context _: Context) {}

        static func dismantleUIView(_ uiView: WKWebView, coordinator: NativeTurnstileCoordinator) {
            coordinator.isActive = false
            uiView.stopLoading()
            uiView.configuration.userContentController.removeScriptMessageHandler(forName: "turnstileToken")
        }
    }

#elseif os(macOS)
    struct NativeTurnstileWebView: NSViewRepresentable {
        let siteKey: String
        let challenge: TurnstileChallengeContext
        let onToken: (String) -> Void

        func makeCoordinator() -> NativeTurnstileCoordinator {
            NativeTurnstileCoordinator(onToken: onToken)
        }

        func makeNSView(context: Context) -> WKWebView {
            makeTurnstileWebView(siteKey: siteKey, challenge: challenge, coordinator: context.coordinator)
        }

        func updateNSView(_: WKWebView, context _: Context) {}

        static func dismantleNSView(_ nsView: WKWebView, coordinator: NativeTurnstileCoordinator) {
            coordinator.isActive = false
            nsView.stopLoading()
            nsView.configuration.userContentController.removeScriptMessageHandler(forName: "turnstileToken")
        }
    }
#endif
