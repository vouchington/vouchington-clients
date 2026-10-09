import SwiftUI
import UniformTypeIdentifiers
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

public struct ProfileView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    public var viewModel: ProfileViewModel
    @State
    private var showingEditBio = false
    @State
    private var showingAvatarImporter = false

    public init(viewModel: ProfileViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        Group {
            switch viewModel.state {
            case .loading, .idle:
                if viewModel.identity != nil {
                    profileContent
                } else {
                    LoadingView()
                }
            case let .error(error):
                VStack {
                    ErrorStateView(error: error) {
                        await viewModel.reload()
                    }
                    signOutSection
                }
            case .loaded:
                profileContent
            }
        }
        .navigationTitle(UiMessages.string(.nativeSwiftProfileProfile, locale: nativeUiLocale))
        .task { await viewModel.load() }
        .refreshable { await viewModel.reload() }
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                Button(UiMessages.string(.nativeSwiftCommonEdit, locale: nativeUiLocale)) { showingEditBio = true }
                    .disabled(!viewModel.isLoaded)
            }
        }
        .fileImporter(
            isPresented: $showingAvatarImporter,
            allowedContentTypes: [.image],
            allowsMultipleSelection: false
        ) { result in
            if case let .success(urls) = result, let url = urls.first {
                Task { await viewModel.uploadAvatar(from: url) }
            }
        }
        .sheet(isPresented: $showingEditBio) {
            EditBioView(client: viewModel.client, currentBio: viewModel.bio ?? "") { markdown in
                try await viewModel.updateBio(markdown)
            }
        }
        .onDisappear { viewModel.clearAvatarPreviewForNavigation() }
    }

    private var profileContent: some View {
        ScrollView {
            VStack(spacing: Spacing.lg) {
                profileHeader
                if let bio = viewModel.bio {
                    bioSection(bio: bio, html: viewModel.bioHtml)
                }
                signOutSection
            }
            .padding(Spacing.md)
        }
    }

    private var profileHeader: some View {
        VStack(spacing: Spacing.sm) {
            if let avatarPreviewData = viewModel.avatarPreviewData {
                LocalImagePreview(data: avatarPreviewData, uploadComplete: !viewModel.isUploadingAvatar)
                    .frame(width: 72, height: 72)
                    .clipShape(Circle())
                    .accessibilityLabel(profileUsername)
            } else if viewModel.avatarPreviewDecodeFailed, viewModel.isUploadingAvatar {
                LocalImagePreview(data: nil, uploadComplete: false)
                    .frame(width: 72, height: 72)
                    .clipShape(Circle())
            } else {
                Avatar(
                    imageURL: viewModel.avatarURL,
                    username: profileUsername,
                    size: 72
                )
            }
            avatarActions
            VStack(spacing: Spacing.xs) {
                Text(profileUsername)
                    .font(Typography.headline)
                    .fontWeight(.bold)
                    .foregroundStyle(.primary)
                if let email = viewModel.identity?.emailAddress {
                    Text(email)
                        .font(Typography.body)
                        .foregroundStyle(.secondary)
                }
                if let plan = viewModel.identity?.membershipPlan {
                    Text(verbatim: UiMessages.string(membershipPlanText(plan), locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
            }
        }
        .frame(maxWidth: .infinity)
    }

    private var profileUsername: String {
        viewModel.identity?.username ?? viewModel.identity?.id ?? ""
    }

    private func bioSection(bio: String, html: String?) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftProfileBio, locale: nativeUiLocale))
                .font(Typography.subheadline)
                .fontWeight(.semibold)
                .foregroundStyle(.primary)
            NativeHtmlContent(
                html: html,
                fallback: bio,
                font: Typography.body,
                foregroundStyle: .secondary
            )
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.5))
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }

}

private extension ProfileView {
    var avatarActions: some View {
        VStack(spacing: Spacing.xs) {
            HStack(spacing: Spacing.sm) {
                Button {
                    showingAvatarImporter = true
                } label: {
                    Label(
                        UiMessages.string(
                            viewModel.identity?.profileImageId == nil
                                ? .nativeSwiftProfileUploadPhoto
                                : .nativeSwiftProfileReplacePhoto,
                            locale: nativeUiLocale
                        ),
                        systemImage: "photo.on.rectangle.angled"
                    )
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.isUploadingAvatar)

                if viewModel.identity?.profileImageId != nil {
                    Button(role: .destructive) {
                        Task { await viewModel.removeAvatar() }
                    } label: {
                        Label(
                            UiMessages.string(.nativeSwiftProfileRemovePhoto, locale: nativeUiLocale),
                            systemImage: "trash"
                        )
                    }
                    .buttonStyle(.bordered)
                    .disabled(viewModel.isUploadingAvatar)
                }
            }

            if viewModel.isUploadingAvatar {
                ProgressView()
            }

            if let error = viewModel.avatarUploadErrorMessage {
                Text(verbatim: UiMessages.string(error, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }
}
