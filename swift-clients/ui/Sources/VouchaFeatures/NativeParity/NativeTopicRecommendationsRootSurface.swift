import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

enum NativeTopicRecommendationsTab: String, CaseIterable, Identifiable {
    case recommendations
    case hashtags

    var id: Self {
        self
    }
}

struct NativeTopicRecommendationsRootSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    private var selectedTab: NativeTopicRecommendationsTab = .recommendations
    @State
    private var hashtags: NativeTopHashtagsViewModel
    @Bindable
    var routeViewModel: NativeRouteSurfaceViewModel
    let isSignedIn: Bool
    let isAdministrator: Bool
    let onNavigateToTargetPath: (String) -> Void

    init(
        routeViewModel: NativeRouteSurfaceViewModel,
        isSignedIn: Bool,
        isAdministrator: Bool,
        initialTab: NativeTopicRecommendationsTab = .recommendations,
        onNavigateToTargetPath: @escaping (String) -> Void
    ) {
        self.routeViewModel = routeViewModel
        self.isSignedIn = isSignedIn
        self.isAdministrator = isAdministrator
        self.onNavigateToTargetPath = onNavigateToTargetPath
        _selectedTab = State(initialValue: initialTab)
        _hashtags = State(initialValue: NativeTopHashtagsViewModel(client: routeViewModel.client))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            Picker("", selection: $selectedTab) {
                Text(UiMessages.string(
                    .nativeSwiftRouteMetadataMainTopicRecommendationsRecommendationsTitle,
                    locale: nativeUiLocale
                ))
                .tag(NativeTopicRecommendationsTab.recommendations)
                Text(UiMessages.string(.nativeSwiftTopHashtagsTopHashtags, locale: nativeUiLocale))
                    .tag(NativeTopicRecommendationsTab.hashtags)
            }
            .pickerStyle(.segmented)

            if selectedTab == .recommendations {
                NativeListSurface(viewModel: routeViewModel, onNavigateToTargetPath: onNavigateToTargetPath)
            } else if isSignedIn {
                topHashtags
            } else {
                Button(UiMessages.string(.nativeSwiftTagManagementSignIn, locale: nativeUiLocale)) {
                    onNavigateToTargetPath("/login")
                }
                .buttonStyle(.bordered)
            }
        }
    }

    private var topHashtags: some View {
        @Bindable var model = hashtags
        return VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack(spacing: Spacing.sm) {
                TextField(
                    UiMessages.string(.nativeSwiftTopHashtagsSearchHashtags, locale: nativeUiLocale),
                    text: $model.query
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale)) {
                    Task { await model.reload() }
                }
                .buttonStyle(.bordered)
            }
            Picker("", selection: $model.mapping) {
                Text(UiMessages.string(.nativeSwiftTopHashtagsAll, locale: nativeUiLocale))
                    .tag(TopHashtagMapping.all)
                Text(UiMessages.string(.nativeSwiftTopHashtagsLinked, locale: nativeUiLocale))
                    .tag(TopHashtagMapping.linked)
                Text(UiMessages.string(.nativeSwiftTopHashtagsUnlinked, locale: nativeUiLocale))
                    .tag(TopHashtagMapping.unlinked)
            }
            .pickerStyle(.segmented)
            .onChange(of: model.mapping) { _, _ in Task { await model.reload() } }

            ForEach(model.results) { hashtag in
                NativeTopHashtagRow(
                    hashtag: hashtag,
                    topic: hashtag.topicId.flatMap { model.topics[$0] },
                    isAdministrator: isAdministrator,
                    isLoading: model.isLoading,
                    locale: nativeUiLocale,
                    link: { identifier in Task { await model.link(alias: hashtag, to: identifier) } },
                    unlink: { Task { await model.unlink(alias: hashtag) } },
                    create: { name in Task { await model.createTopic(alias: hashtag, name: name) } }
                )
            }
            if model.pageInfo.hasNextPage {
                Button(UiMessages.string(.nativeSwiftTopHashtagsLoadMore, locale: nativeUiLocale)) {
                    Task { await model.loadMore() }
                }
                .buttonStyle(.bordered)
                .disabled(model.isLoading)
            }
            if let error = model.error {
                Text(error.localizedDescription).foregroundStyle(Colors.negativeVote)
            }
        }
        .task { await model.reload() }
    }
}

struct NativeTopHashtagRow: View {
    let hashtag: TopHashtag
    let topic: Topic?
    let isAdministrator: Bool
    let isLoading: Bool
    let locale: Locale
    let link: (String) -> Void
    let unlink: () -> Void
    let create: (String) -> Void
    @State private var topicIdentifier = ""
    @State private var topicName = ""

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(verbatim: nativeAuthoredHashtagToken(hashtag.hashtag))
                .font(Typography.headline)
            Text(UiMessages.string(
                .nativeSwiftTopHashtagsItemsContributors,
                parameters: ["items": "\(hashtag.itemCount)", "contributors": "\(hashtag.contributorCount)"],
                locale: locale
            ))
            .font(Typography.caption)
            .foregroundStyle(Colors.secondaryLabel)
            if let topic {
                Text(verbatim: topic.name).font(Typography.subheadline)
            }
            if isAdministrator {
                adminActions.disabled(isLoading)
            }
        }
        .padding(Spacing.sm)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: Spacing.md))
    }

    @ViewBuilder
    private var adminActions: some View {
        if let topic {
            if CanonicalHashtagSlug(rawValue: hashtag.hashtag)?.value != topic.slug {
                Button(UiMessages.string(.nativeSwiftTopHashtagsUnlink, locale: locale), action: unlink)
                    .buttonStyle(.bordered)
            }
        } else if hashtag.topicId != nil {
            Button(UiMessages.string(.nativeSwiftTopHashtagsUnlink, locale: locale), action: unlink)
                .buttonStyle(.bordered)
        } else {
            HStack(spacing: Spacing.sm) {
                TextField(
                    UiMessages.string(.nativeSwiftTopicRecommendationTopic, locale: locale),
                    text: $topicIdentifier
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftTopHashtagsLinkTopic, locale: locale)) {
                    link(topicIdentifier)
                }
                .buttonStyle(.bordered)
                .disabled(topicIdentifier.trimmed.isEmpty)
            }
            HStack(spacing: Spacing.sm) {
                TextField(
                    UiMessages.string(.nativeSwiftTopicRecommendationTopicTitle, locale: locale),
                    text: $topicName
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftTopHashtagsCreateTopic, locale: locale)) {
                    create(topicName)
                }
                .buttonStyle(.bordered)
                .disabled(topicName.trimmed.isEmpty)
            }
        }
    }
}

func nativeAuthoredHashtagToken(_ hashtag: String) -> String {
    hashtag.hasPrefix("#") ? hashtag : "#\(hashtag)"
}
