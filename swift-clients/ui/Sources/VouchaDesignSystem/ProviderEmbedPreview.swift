import SwiftUI
import VouchaLocalization
import VouchaModels
import WebKit

/// The sole native embedded-web exception: server-approved YouTube nocookie and Vimeo players.
public struct ProviderEmbedPreview: View {
    public let embed: UrlEmbed
    @State private var showingPlayer = false
    @Environment(\.locale) private var locale

    public init(embed: UrlEmbed) {
        self.embed = embed
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if let thumbnailURL = embed.thumbnailUrl {
                AsyncImageView(urlString: thumbnailURL, contentMode: .fill)
                    .frame(maxWidth: .infinity)
                    .frame(height: 180)
                    .clipShape(RoundedRectangle(cornerRadius: Spacing.md))
            }
            if let provider = embed.previewProvider {
                Text(verbatim: UiMessages.string(.externalProvider(provider), locale: locale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
            if let title = embed.previewTitle {
                Text(verbatim: UiMessages.string(.userContent(title), locale: locale))
                    .font(Typography.headline)
            }
            if let description = embed.previewDescription {
                Text(verbatim: UiMessages.string(.userContent(description), locale: locale))
                    .font(Typography.body)
                    .foregroundStyle(Colors.secondaryLabel)
                    .lineLimit(3)
            }
            if let player = embed.approvedPlayerWithSource {
                Button(UiMessages.string(.nativeSwiftCommonPlayVideo, locale: locale)) {
                    _ = player
                    showingPlayer = true
                }
                .sheet(isPresented: $showingPlayer) {
                    ProviderEmbedPlayer(url: player.playerURL, sourceURL: player.sourceURL)
                        .frame(minHeight: 200)
                }
            }
            if let sourceURL = embed.validatedSourceURL {
                Link(destination: sourceURL) {
                    Label(
                        UiMessages.string(.nativeSwiftPodcastPlaybackOpenSource, locale: locale),
                        systemImage: "arrow.up.right.square"
                    )
                }
            }
        }
    }

}

private struct ProviderEmbedPlayer: View {
    let url: URL
    let sourceURL: URL
    @Environment(\.locale) private var locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            ProviderEmbedWebView(url: url)
                .frame(minHeight: 200)
                .accessibilityIdentifier("provider-embed-player")
            Link(destination: sourceURL) {
                Label(
                    UiMessages.string(.nativeSwiftPodcastPlaybackOpenSource, locale: locale),
                    systemImage: "arrow.up.right.square"
                )
            }
        }
    }
}

#if os(macOS)
    private struct ProviderEmbedWebView: NSViewRepresentable {
        let url: URL

        func makeCoordinator() -> Coordinator {
            Coordinator()
        }

        func makeNSView(context: Context) -> WKWebView {
            context.coordinator.makeWebView()
        }

        func updateNSView(_ webView: WKWebView, context: Context) {
            context.coordinator.load(url, in: webView)
        }

        static func dismantleNSView(_ webView: WKWebView, coordinator: Coordinator) {
            coordinator.tearDown(webView)
        }
    }
#else
    private struct ProviderEmbedWebView: UIViewRepresentable {
        let url: URL

        func makeCoordinator() -> Coordinator {
            Coordinator()
        }

        func makeUIView(context: Context) -> WKWebView {
            context.coordinator.makeWebView()
        }

        func updateUIView(_ webView: WKWebView, context: Context) {
            context.coordinator.load(url, in: webView)
        }

        static func dismantleUIView(_ webView: WKWebView, coordinator: Coordinator) {
            coordinator.tearDown(webView)
        }
    }
#endif

private final class Coordinator: NSObject, WKNavigationDelegate, WKUIDelegate {
    private var loadedURL: URL?

    func makeWebView() -> WKWebView {
        let configuration = WKWebViewConfiguration()
        configuration.websiteDataStore = .nonPersistent()
        configuration.preferences.javaScriptCanOpenWindowsAutomatically = false
        let webView = WKWebView(frame: .zero, configuration: configuration)
        webView.navigationDelegate = self
        webView.uiDelegate = self
        return webView
    }

    func load(_ url: URL, in webView: WKWebView) {
        guard loadedURL != url, UrlEmbed.isAllowedProviderURL(url) else { return }
        loadedURL = url
        var request = URLRequest(url: url)
        let bundleID = Bundle.main.bundleIdentifier ?? "ai.voucha"
        request.setValue("https://\(bundleID)/", forHTTPHeaderField: "Referer")
        webView.load(request)
    }

    func tearDown(_ webView: WKWebView) {
        webView.stopLoading()
        webView.navigationDelegate = nil
        webView.uiDelegate = nil
        loadedURL = nil
    }

    func webView(
        _: WKWebView,
        decidePolicyFor action: WKNavigationAction,
        decisionHandler: @escaping (WKNavigationActionPolicy) -> Void
    ) {
        guard let url = action.request.url, UrlEmbed.isAllowedProviderURL(url) else {
            decisionHandler(.cancel)
            return
        }
        decisionHandler(.allow)
    }

    func webView(
        _: WKWebView,
        createWebViewWith _: WKWebViewConfiguration,
        for _: WKNavigationAction,
        windowFeatures _: WKWindowFeatures
    ) -> WKWebView? {
        nil
    }
}
