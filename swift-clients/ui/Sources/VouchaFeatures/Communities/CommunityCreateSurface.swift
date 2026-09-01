import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

struct CommunityCreateSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    var viewModel: CommunityCreateViewModel
    let client: APIClient?
    let turnstileSiteKey: String?
    @State
    private var showingTurnstile = false

    init(viewModel: CommunityCreateViewModel, client: APIClient? = nil, turnstileSiteKey: String?) {
        _viewModel = State(initialValue: viewModel)
        self.client = client
        self.turnstileSiteKey = turnstileSiteKey
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            TextField(UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale), text: $viewModel.name)
                .textFieldStyle(.roundedBorder)
            TextField(UiMessages.string(.nativeSwiftCommunitiesSlug, locale: nativeUiLocale), text: $viewModel.slug)
                .textFieldStyle(.roundedBorder)
            NativeMarkdownEditor(client: client, markdown: $viewModel.markdown, minHeight: 140)

            HStack {
                Button(
                    UiMessages.string(
                        viewModel.turnstileToken == nil ? .nativeSwiftCommonVerify : .nativeAuthVerified,
                        locale: nativeUiLocale
                    ),
                    systemImage: "checkmark.shield"
                ) {
                    showingTurnstile = true
                }
                .buttonStyle(.bordered)

                Button(UiMessages.string(.nativeSwiftCommunitiesCreateCommunity, locale: nativeUiLocale)) {
                    Task { await viewModel.create() }
                }
                .buttonStyle(.borderedProminent)
                .disabled(!viewModel.canCreate)
            }

            stateText(viewModel.state, locale: nativeUiLocale)
        }
        .sheet(isPresented: $showingTurnstile) {
            NativeTurnstileChallengeView(
                siteKey: turnstileSiteKey ?? AppConfig.shared.requiredTurnstileSiteKey
            ) { token in
                viewModel.turnstileToken = token
                showingTurnstile = false
            }
        }
    }
}
