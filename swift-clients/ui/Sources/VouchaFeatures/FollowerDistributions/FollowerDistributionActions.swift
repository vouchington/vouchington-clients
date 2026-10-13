import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

public struct FollowerDistributionActions: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    private var isSendSheetPresented = false
    @State
    private var isShareFailurePresented = false
    @State
    private var viewModel: FollowerDistributionViewModel
    private let client: APIClient
    private let currentUserId: String?
    private let target: FollowerDistributionTarget

    public init(client: APIClient, currentUserId: String?, target: FollowerDistributionTarget) {
        self.client = client
        self.currentUserId = currentUserId
        self.target = target
        _viewModel = State(initialValue: .init(client: client, currentUserId: currentUserId, target: target))
    }

    public var body: some View {
        Menu {
            Button {
                Task {
                    let didShare = await viewModel.share()
                    if !didShare, viewModel.error != nil {
                        isShareFailurePresented = true
                    }
                }
            } label: {
                Label(message(.shareWithFollowers), systemImage: "person.2")
            }
            .disabled(viewModel.isSharing)

            Button {
                isSendSheetPresented = true
            } label: {
                Label(message(.sendToFollowers), systemImage: "paperplane")
            }
        } label: {
            Image(systemName: "square.and.arrow.up")
        }
        .accessibilityLabel(message(.actions))
        .alert(message(.actions), isPresented: $isShareFailurePresented) {
            Button(UiMessages.string(.nativeSwiftCommonOK, locale: nativeUiLocale), role: .cancel) {
                viewModel.clearError()
            }
        } message: {
            Text(message(.unableToShare))
        }
        .sheet(isPresented: $isSendSheetPresented, onDismiss: viewModel.resetSendSheet) {
            Self.sendSheet(viewModel: viewModel, client: client)
        }
        .onChange(of: currentUserId) { _, nextUserId in
            isShareFailurePresented = false
            viewModel.replaceContext(currentUserId: nextUserId, target: target)
        }
        .onChange(of: target) { _, nextTarget in
            isShareFailurePresented = false
            viewModel.replaceContext(currentUserId: currentUserId, target: nextTarget)
        }
    }

    private func message(_ key: FollowerDistributionCopy) -> String {
        UiMessages.string(key.messageKey, locale: nativeUiLocale)
    }

    static func sendSheet(
        viewModel: FollowerDistributionViewModel,
        client: APIClient
    ) -> FollowerDistributionSendSheet {
        FollowerDistributionSendSheet(
            viewModel: viewModel,
            imageConfig: AppConfig(baseURL: client.baseURL, imageBaseURL: client.imageBaseURL)
        )
    }
}

struct FollowerDistributionSendSheet: View {
    @Environment(\.dismiss)
    private var dismiss
    @Environment(\.locale)
    private var nativeUiLocale
    @Bindable var viewModel: FollowerDistributionViewModel
    let imageConfig: AppConfig

    init(viewModel: FollowerDistributionViewModel, imageConfig: AppConfig = .shared) {
        self.viewModel = viewModel
        self.imageConfig = imageConfig
    }

    var body: some View {
        NavigationStack {
            Form {
                Picker(message(.audience), selection: $viewModel.sendsToAllFollowers) {
                    Text(message(.allFollowers)).tag(true)
                    Text(message(.selectedFollowers)).tag(false)
                }
                .pickerStyle(.segmented)

                if !viewModel.sendsToAllFollowers {
                    if !viewModel.selectedRecipients.isEmpty {
                        Section(message(.selectedFollowers)) {
                            ForEach(viewModel.selectedRecipients) { recipient in
                                recipientToggle(recipient)
                            }
                        }
                    }
                    TextField(
                        message(.searchFollowers),
                        text: Binding(
                            get: { viewModel.query },
                            set: { query in Task { await viewModel.searchRecipients(query: query) } }
                        )
                    )
                    ForEach(
                        viewModel.recipients.filter { !viewModel.selectedRecipientIds.contains($0.id) }
                    ) { recipient in
                        recipientToggle(recipient)
                            .disabled(!viewModel.canToggleRecipient(recipient.id))
                    }
                    if viewModel.canLoadMoreRecipients {
                        Button(message(.loadMoreFollowers)) { Task { await viewModel.loadMoreRecipients() } }
                    }
                }
            }
            .navigationTitle(message(.sendToFollowers))
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(UiMessages.string(.commonCancel, locale: nativeUiLocale)) { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(UiMessages.string(.nativeSwiftCommonSend, locale: nativeUiLocale)) {
                        Task {
                            if await viewModel.send() {
                                dismiss()
                            }
                        }
                    }
                    .disabled(!viewModel.canSend || viewModel.isSending)
                }
            }
        }
        .alert(message(.actions), isPresented: errorIsPresented) {
            Button(UiMessages.string(.nativeSwiftCommonOK, locale: nativeUiLocale), role: .cancel) {
                viewModel.clearError()
            }
        } message: {
            Text(message(.unableToSend))
        }
    }

    private var errorIsPresented: Binding<Bool> {
        Binding(get: { viewModel.error != nil }, set: {
            if !$0 {
                viewModel.clearError()
            }
        })
    }

    private func message(_ key: FollowerDistributionCopy) -> String {
        UiMessages.string(key.messageKey, locale: nativeUiLocale)
    }

}

private enum FollowerDistributionCopy {
    case actions, shareWithFollowers, sendToFollowers, audience, allFollowers
    case selectedFollowers, searchFollowers, loadMoreFollowers, unableToSend, unableToShare

    var messageKey: UiMessageKey {
        switch self {
        case .actions: .nativeSwiftFollowerDistributionActions
        case .shareWithFollowers: .nativeSwiftFollowerDistributionShareWithFollowers
        case .sendToFollowers: .nativeSwiftFollowerDistributionSendToFollowers
        case .audience: .nativeSwiftFollowerDistributionAudience
        case .allFollowers: .nativeSwiftFollowerDistributionAllFollowers
        case .selectedFollowers: .nativeSwiftFollowerDistributionSelectedFollowers
        case .searchFollowers: .nativeSwiftFollowerDistributionSearchFollowers
        case .loadMoreFollowers: .nativeSwiftFollowerDistributionLoadMoreFollowers
        case .unableToSend: .nativeSwiftFollowerDistributionUnableToSend
        case .unableToShare: .nativeSwiftFollowerDistributionUnableToShare
        }
    }
}
