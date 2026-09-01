import SwiftUI
import UniformTypeIdentifiers
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativePostComposeSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    private var composeViewModel: NativePostComposeViewModel
    @State
    private var showingTurnstile = false
    @State
    var showingImageImporter = false
    private let client: APIClient?
    private let turnstileSiteKey: String?

    init(
        client: APIClient?,
        communityIdOrSlug: String? = nil,
        initialPostType: PostType = .discussion,
        isAdministrator: Bool = false,
        turnstileSiteKey: String? = nil
    ) {
        self.client = client
        self.turnstileSiteKey = turnstileSiteKey
        _composeViewModel = State(
            initialValue: NativePostComposeViewModel(
                client: client,
                communityIdOrSlug: communityIdOrSlug,
                initialPostType: initialPostType,
                isAdministrator: isAdministrator
            )
        )
    }

    var body: some View {
        @Bindable
        var viewModel = composeViewModel

        VStack(alignment: .leading, spacing: Spacing.md) {
            Picker(
                UiMessages.string(.nativeSwiftCrmContactsType, locale: nativeUiLocale),
                selection: $viewModel.postType
            ) {
                ForEach(postTypes, id: \.self) { type in
                    Text(UiMessages.string(type.titleKey, locale: nativeUiLocale))
                        .tag(type)
                }
            }
            .pickerStyle(.segmented)

            TextField(UiMessages.string(.nativeSwiftLandingPagesTitle, locale: nativeUiLocale), text: $viewModel.title)
                .textFieldStyle(.roundedBorder)

            NativeMarkdownEditor(client: client, markdown: $viewModel.bodyText)

            if viewModel.postType == .link {
                TextField(
                    UiMessages.string(.nativeSwiftLandingPagesLinkUrl, locale: nativeUiLocale),
                    text: $viewModel.linkURL
                )
                .textFieldStyle(.roundedBorder)
            }

            if viewModel.postType == .discussion {
                discussionCategorySection(viewModel: viewModel)
            }

            imageSection(viewModel: viewModel)
            NativePostTypeSpecificFields(viewModel: viewModel)

            turnstileVerification(viewModel: viewModel)

            composeActions(viewModel: viewModel)
            composeStatus(viewModel: viewModel)
            drafts(viewModel: viewModel)
        }
        .sheet(isPresented: $showingTurnstile) {
            NativeTurnstileChallengeView(
                siteKey: turnstileSiteKey ?? AppConfig.shared.requiredTurnstileSiteKey
            ) { token in
                viewModel.turnstileToken = token
                showingTurnstile = false
            }
        }
        .fileImporter(
            isPresented: $showingImageImporter,
            allowedContentTypes: [.image],
            allowsMultipleSelection: true
        ) { result in
            if case let .success(urls) = result {
                Task { await viewModel.uploadImages(from: urls) }
            }
        }
        .emailVerificationRecovery(
            client: client,
            gate: viewModel.emailVerificationGate
        )
    }

    private func turnstileVerification(viewModel: NativePostComposeViewModel) -> some View {
        Button {
            showingTurnstile = true
        } label: {
            Label(
                UiMessages.string(
                    viewModel.turnstileToken == nil ? .nativeSwiftCommonVerify : .nativeAuthVerified,
                    locale: nativeUiLocale
                ),
                systemImage: viewModel.turnstileToken == nil ? "checkmark.shield" : "checkmark.shield.fill"
            )
        }
        .buttonStyle(.bordered)
    }

    private func composeActions(viewModel: NativePostComposeViewModel) -> some View {
        HStack(spacing: Spacing.sm) {
            Button(UiMessages.string(.nativeSwiftPostComposeSaveDraft, locale: nativeUiLocale)) {
                viewModel.saveDraft()
            }
            .buttonStyle(.bordered)
            .disabled(!viewModel.canSaveDraft)

            Button(UiMessages.string(.nativeSwiftPostComposePublish, locale: nativeUiLocale)) {
                Task { await viewModel.publish() }
            }
            .buttonStyle(.borderedProminent)
            .disabled(!viewModel.canPublish)
        }
    }

    @ViewBuilder
    private func composeStatus(viewModel: NativePostComposeViewModel) -> some View {
        if case let .error(.api(_, code)) = viewModel.state, let key = NativeContributionAdmissionPresentation.messageKey(code) {
            Text(UiMessages.string(key, locale: nativeUiLocale))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
                .accessibilityAddTraits(.isStaticText)
        }

        if case .required = viewModel.state {
            Text(UiMessages.string(.nativeSwiftPostComposeVerificationTokenRequired, locale: nativeUiLocale))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
        }

        if let publishedPostId = viewModel.publishedPostId {
            Text(UiMessages.string(
                .nativeSwiftPostComposePublishedPost,
                parameters: ["id": UiMessages.string(.verbatim(publishedPostId), locale: nativeUiLocale)],
                locale: nativeUiLocale
            ))
            .font(Typography.subheadline)
            .foregroundStyle(Colors.secondaryLabel)
        }
    }

}

enum NativeContributionAdmissionPresentation {
    static func messageKey(_ code: String?) -> UiMessageKey? {
        switch code {
        case "CONTRIBUTION_ADMISSION_IN_PROGRESS": .nativeTaxonomyContributionAdmissionInProgress
        case "CONTRIBUTION_QUOTA_EXCEEDED": .nativeTaxonomyContributionAdmissionCapacityUnavailable
        case "IDEMPOTENCY_KEY_REUSED": .nativeTaxonomyContributionAdmissionIdempotencyMismatch
        default: nil
        }
    }
}

extension NativePostComposeSurface {
    @ViewBuilder
    private func drafts(viewModel: NativePostComposeViewModel) -> some View {
        if !viewModel.drafts.isEmpty {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                Text(UiMessages.string(.nativeSwiftPostComposeDrafts, locale: nativeUiLocale))
                    .font(Typography.headline)

                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.drafts) { row in
                        NativeSurfaceRow(row: row)
                    }
                }
            }
        }
    }

    private var postTypes: [PostType] {
        composeViewModel.availablePostTypes
    }
}
