import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeTopicManagementAboutFields: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: NativeTopicManagementViewModel
    let client: APIClient?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            TextField(UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale), text: $viewModel.name)
                .textFieldStyle(.roundedBorder)
            TextField(UiMessages.string(.nativeSwiftCommunitiesSlug, locale: nativeUiLocale), text: $viewModel.slug)
                .textFieldStyle(.roundedBorder)
            NativeMarkdownEditor(client: client, markdown: $viewModel.markdown, minHeight: 140)
            TextField(
                UiMessages.string(.nativeSwiftTopicManagementFieldsHostname, locale: nativeUiLocale),
                text: $viewModel.hostname
            )
            .textFieldStyle(.roundedBorder)
            NativeTopicImageField(
                title: .nativeSwiftTopicManagementFieldsLogoImage,
                previewWidth: 48,
                imageId: $viewModel.logoImageId,
                client: client
            )
            NativeTopicImageField(
                title: .nativeSwiftTopicManagementFieldsHeroImage,
                previewWidth: 120,
                imageId: $viewModel.heroImageId,
                client: client
            )
        }
    }
}

struct NativeTopicManagementBehaviorFields: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: NativeTopicManagementViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            TextField(
                UiMessages.string(.nativeSwiftTopicManagementFieldsTopicType, locale: nativeUiLocale),
                text: $viewModel.topicType
            )
            .textFieldStyle(.roundedBorder)
            Toggle(
                UiMessages.string(.nativeSwiftTopicManagementFieldsNoIndex, locale: nativeUiLocale),
                isOn: $viewModel.noindex
            )
            Toggle(
                UiMessages.string(.nativeSwiftTopicManagementFieldsAllowReviews, locale: nativeUiLocale),
                isOn: $viewModel.allowReviews
            )
        }
    }
}

struct NativeTopicManagementDomainFields: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: NativeTopicManagementViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            TextField(
                UiMessages.string(.nativeSwiftTopicManagementFieldsPrimaryHostname, locale: nativeUiLocale),
                text: $viewModel.hostname
            )
            .textFieldStyle(.roundedBorder)
            HStack(spacing: Spacing.sm) {
                TextField(
                    UiMessages.string(.nativeSwiftTopicManagementFieldsAdditionalHostname, locale: nativeUiLocale),
                    text: $viewModel.additionalHostname
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftTopicManagementFieldsAddHostname, locale: nativeUiLocale)) {
                    Task { await viewModel.addAdditionalHostname() }
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.additionalHostname.trimmed.isEmpty || viewModel.isLoading)
            }
            if viewModel.additionalHostnames.isEmpty {
                EmptyStateView(
                    icon: "globe",
                    title: .message(.nativeSwiftEmptyStateNoAdditionalHostnames),
                    message: .message(.nativeSwiftEmptyStateAddHostnamesMessage)
                )
            } else {
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.additionalHostnames) { hostname in
                        HStack {
                            Text(hostname.hostname)
                                .font(Typography.body)
                            Spacer(minLength: 0)
                            Button {
                                Task { await viewModel.removeAdditionalHostname(hostname.id) }
                            } label: {
                                Image(systemName: "trash")
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    HybridPaginationControl(
                        hasMore: viewModel.additionalHostnamesPagination.hasMore,
                        isLoading: viewModel.additionalHostnamesPagination.isLoading,
                        hasError: viewModel.additionalHostnamesPagination.lastError != nil,
                        accessibilityIdentifier: "additional-hostnames-pagination"
                    ) {
                        await viewModel.loadMoreAdditionalHostnames()
                    }
                }
            }
        }
    }
}

struct NativeTopicManagementSourceFields: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: NativeTopicManagementViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if let source = viewModel.sourceDetail {
                Text(UiMessages.string(
                    source.title.map(UiVerbatimText.verbatim)
                        ?? .message(.nativeSwiftTopicManagementFieldsRssFeed),
                    locale: nativeUiLocale
                ))
                .font(Typography.headline)
                Text(source.rssFeedUrl.url)
                    .font(Typography.subheadline.monospaced())
                    .foregroundStyle(Colors.secondaryLabel)
                    .fixedSize(horizontal: false, vertical: true)
                HStack(spacing: Spacing.sm) {
                    Button(UiMessages.string(
                        source.isEnabled
                            ? .nativeSwiftCommunityActionsDisable
                            : .nativeSwiftCommunityActionsEnable,
                        locale: nativeUiLocale
                    )) {
                        Task { await viewModel.toggleSourceEnabled() }
                    }
                    .buttonStyle(.bordered)
                    Button(UiMessages.string(
                        source.isDiscoverable
                            ? .nativeSwiftTopicManagementFieldsHideFromDiscovery
                            : .nativeSwiftTopicManagementFieldsMakeDiscoverable,
                        locale: nativeUiLocale
                    )) {
                        Task { await viewModel.toggleSourceDiscoverable() }
                    }
                    .buttonStyle(.bordered)
                }
                if let homePageUrl = source.homePageUrl?.url {
                    Text(homePageUrl)
                        .font(Typography.caption.monospaced())
                        .foregroundStyle(Colors.secondaryLabel)
                }
            } else {
                EmptyStateView(
                    icon: "newspaper",
                    title: .message(.nativeSwiftEmptyStateNoSource),
                    message: .message(.nativeSwiftEmptyStateNoSourceMessage)
                )
            }
        }
    }
}
