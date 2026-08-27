import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

public struct LandingPagesView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    private var viewModel: LandingPagesViewModel

    public init(viewModel: LandingPagesViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        List {
            createSection
            pagesSection
            if viewModel.selectedPage != nil {
                analyticsSection
                detailsSection
                contentSection
            }
            if let error = viewModel.errorMessage {
                Section {
                    Text(UiMessages.string(error, locale: nativeUiLocale))
                        .foregroundStyle(.red)
                }
            }
        }
        .navigationTitle(UiMessages.string(.nativeSwiftLandingPagesLandingPages, locale: nativeUiLocale))
        .task { await viewModel.load() }
        .task(id: viewModel.selectedPage?.id) { await viewModel.loadSelectedPageAnalytics() }
        .refreshable { await viewModel.reload() }
    }

    private var createSection: some View {
        Section(header: Text(verbatim: UiMessages.string(.nativeSwiftCommonCreate, locale: nativeUiLocale))) {
            TextField(
                UiMessages.string(.nativeSwiftLandingPagesTitle, locale: nativeUiLocale),
                text: $viewModel.newTitle
            )
            TextField(UiMessages.string(.nativeSwiftCommunitiesSlug, locale: nativeUiLocale), text: $viewModel.newSlug)
            TextField(
                UiMessages.string(.nativeSwiftLandingPagesSubtitle, locale: nativeUiLocale),
                text: $viewModel.newSubtitle,
                axis: .vertical
            )
            Button(UiMessages.string(.nativeSwiftLandingPagesCreateLandingPage, locale: nativeUiLocale)) {
                Task { await viewModel.createPage() }
            }
            .disabled(!viewModel.canCreatePage)
        }
    }

    private var pagesSection: some View {
        Section(header: Text(verbatim: UiMessages.string(.nativeSwiftLandingPagesPages, locale: nativeUiLocale))) {
            ForEach(viewModel.pages) { page in
                Button {
                    Task { await viewModel.selectPage(id: page.id) }
                } label: {
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        Text(page.title)
                        Text(UiMessages.string(page.listSubtitle, locale: nativeUiLocale))
                            .font(Typography.caption)
                            .foregroundStyle(Colors.secondaryLabel)
                    }
                }
                .disabled(viewModel.isLoading)
            }
        }
    }

    private var analyticsSection: some View {
        LandingPageAnalyticsSection(viewModel: viewModel)
    }

    private var detailsSection: some View {
        Section(header: Text(verbatim: UiMessages.string(.nativeSwiftLandingPagesDetails, locale: nativeUiLocale))) {
            TextField(UiMessages.string(.nativeSwiftLandingPagesTitle, locale: nativeUiLocale), text: $viewModel.title)
            TextField(UiMessages.string(.nativeSwiftCommunitiesSlug, locale: nativeUiLocale), text: $viewModel.slug)
            TextField(
                UiMessages.string(.nativeSwiftLandingPagesSubtitle, locale: nativeUiLocale),
                text: $viewModel.subtitle,
                axis: .vertical
            )
            Button(UiMessages.string(.nativeSwiftLandingPagesSavePageDetails, locale: nativeUiLocale)) {
                Task { await viewModel.saveDetails() }
            }
            .disabled(viewModel.isLoading)
            if viewModel.selectedPage?.isDefault == false {
                Button(UiMessages.string(.nativeSwiftLandingPagesMakeDefault, locale: nativeUiLocale)) {
                    Task { await viewModel.setDefault() }
                }
                .disabled(viewModel.isLoading)
            }
            Button(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), role: .destructive) {
                Task { await viewModel.deleteSelectedPage() }
            }
            .disabled(viewModel.isLoading)
        }
        .disabled(viewModel.isLoading)
    }

    private var contentSection: some View {
        LandingPageContentSection(viewModel: viewModel, locale: nativeUiLocale)
    }
}

private extension LandingPage {
    var listSubtitle: UiVerbatimText {
        .joined(
            [
                .verbatim(slug),
                isDefault ? .message(.nativeSwiftLandingPagesDefaultPage) : nil,
                subtitle.map(UiVerbatimText.verbatim)
            ]
            .compactMap { $0 }
        )
    }
}
