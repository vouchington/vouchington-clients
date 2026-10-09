import Observation
import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

@Observable
@MainActor
final class NativeTopicManagementOperationState {
    var isSavePending = false
}

struct NativeTopicManagementSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    private var viewModel: NativeTopicManagementViewModel
    @State
    private var logoUploadState: NativeTopicImageFieldUploadState
    @State
    private var heroUploadState: NativeTopicImageFieldUploadState
    @State
    private var operationState: NativeTopicManagementOperationState
    private let client: APIClient?

    init(
        client: APIClient?,
        routeMatch: NativeRouteMatch?,
        logoUploadState: NativeTopicImageFieldUploadState? = nil,
        heroUploadState: NativeTopicImageFieldUploadState? = nil,
        operationState: NativeTopicManagementOperationState? = nil
    ) {
        self.client = client
        _viewModel = State(initialValue: NativeTopicManagementViewModel(client: client, routeMatch: routeMatch))
        _logoUploadState = State(initialValue: logoUploadState ?? NativeTopicImageFieldUploadState())
        _heroUploadState = State(initialValue: heroUploadState ?? NativeTopicImageFieldUploadState())
        _operationState = State(initialValue: operationState ?? NativeTopicManagementOperationState())
    }

    var body: some View {
        @Bindable
        var bindableModel = viewModel

        VStack(alignment: .leading, spacing: Spacing.md) {
            header(viewModel: bindableModel)
            switch bindableModel.section {
            case .create:
                NativeTopicManagementAboutFields(
                    viewModel: bindableModel, client: client,
                    logoUploadState: logoUploadState, heroUploadState: heroUploadState,
                    isImageEditingDisabled: { operationState.isSavePending || bindableModel.isLoading }
                )
                NativeTopicManagementBehaviorFields(viewModel: bindableModel)
            case .about:
                NativeTopicManagementAboutFields(
                    viewModel: bindableModel, client: client,
                    logoUploadState: logoUploadState, heroUploadState: heroUploadState,
                    isImageEditingDisabled: { operationState.isSavePending || bindableModel.isLoading }
                )
            case .behavior:
                NativeTopicManagementBehaviorFields(viewModel: bindableModel)
            case .domains:
                NativeTopicManagementDomainFields(viewModel: bindableModel)
            case .source:
                NativeTopicManagementSourceFields(viewModel: bindableModel)
            case .aliases:
                NativeTopicManagementAliasFields(viewModel: bindableModel)
            case .merge:
                NativeTopicManagementMergeFields(viewModel: bindableModel)
            }
            if let primaryActionTitle = primaryActionTitle(for: bindableModel.section) {
                Button(UiMessages.string(primaryActionTitle, locale: nativeUiLocale)) {
                    guard !operationState.isSavePending, !bindableModel.isLoading, !isImageUploadPending else { return }
                    operationState.isSavePending = true
                    Task {
                        defer { operationState.isSavePending = false }
                        guard !isImageUploadPending else { return }
                        await bindableModel.save()
                    }
                }
                .buttonStyle(.borderedProminent)
                .disabled(operationState.isSavePending || bindableModel.isLoading || isImageUploadPending)
            }
            status(viewModel: bindableModel)
        }
        .task {
            await viewModel.load()
        }
    }

    private var isImageUploadPending: Bool {
        logoUploadState.isUploading || logoUploadState.uploadTask != nil
            || heroUploadState.isUploading || heroUploadState.uploadTask != nil
    }

    private func header(viewModel: NativeTopicManagementViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(
                viewModel.section == .create
                    ? .nativeSwiftTopicManagementCreateTopic
                    : .nativeSwiftTopicManagementTopicSettings,
                locale: nativeUiLocale
            ))
            .font(Typography.headline)
            Text(UiMessages.string(headerDetail(for: viewModel), locale: nativeUiLocale))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
                .fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    @ViewBuilder
    func status(viewModel: NativeTopicManagementViewModel) -> some View {
        switch viewModel.state {
        case .loading:
            ProgressView()
        case let .error(error):
            Text(error.localizedDescription)
                .foregroundStyle(Colors.negativeVote)
        default:
            if let topic = viewModel.topic {
                Text(topic.id)
                    .font(Typography.caption.monospaced())
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }

    private func primaryActionTitle(for section: NativeTopicManagementSection) -> UiMessageKey? {
        switch section {
        case .create:
            .nativeSwiftTopicManagementCreateTopic
        case .about, .behavior, .domains, .merge:
            .nativeSwiftCommonSave
        case .source, .aliases:
            nil
        }
    }

    private func headerDetail(for viewModel: NativeTopicManagementViewModel) -> UiMessageKey {
        switch viewModel.section {
        case .create:
            .nativeSwiftTopicManagementCreateDetail
        case .about:
            .nativeSwiftTopicManagementAboutDetail
        case .behavior:
            .nativeSwiftTopicManagementBehaviorDetail
        case .domains:
            .nativeSwiftTopicManagementDomainsDetail
        case .source:
            .nativeSwiftTopicManagementSourceDetail
        case .aliases:
            .nativeSwiftTopicManagementAliasesDetail
        case .merge:
            .nativeSwiftTopicManagementMergeDetail
        }
    }
}
