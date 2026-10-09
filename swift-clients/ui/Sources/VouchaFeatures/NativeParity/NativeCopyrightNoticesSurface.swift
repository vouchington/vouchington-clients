import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeCopyrightNoticesSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    let noticeId: String?
    let onNavigateToTargetPath: (String) -> Void
    @State var viewModel: NativeCopyrightNoticesViewModel

    init(
        client: APIClient?,
        noticeId: String?,
        onNavigateToTargetPath: @escaping (String) -> Void
    ) {
        self.noticeId = noticeId
        self.onNavigateToTargetPath = onNavigateToTargetPath
        _viewModel = State(initialValue: NativeCopyrightNoticesViewModel(client: client, noticeId: noticeId))
    }

    var notices: [CopyrightNotice] {
        viewModel.notices
    }

    var notice: CopyrightNotice? {
        viewModel.notice
    }

    var pageInfo: CopyrightNoticesPageInfo? {
        viewModel.pageInfo
    }

    var participantNotice: CopyrightNotice? {
        viewModel.participantNotice
    }

    var euSettlements: [CopyrightEuDisputeSettlement] {
        viewModel.euSettlements
    }

    var euSettlementsPageInfo: CopyrightNoticesPageInfo? {
        viewModel.euSettlementsPageInfo
    }

    var isLoading: Bool {
        viewModel.isLoading
    }

    var errorMessage: UiVerbatimText? {
        viewModel.errorMessage
    }

    var paginationFailed: Bool {
        viewModel.paginationFailed
    }

    var body: some View {
        Group {
            if let noticeId {
                detail(noticeId: noticeId)
            } else {
                index
            }
        }
        .task(id: noticeId) {
            await viewModel.loadInitial()
        }
        .onDisappear {
            viewModel.cancelInitialLoad()
        }
    }

    private var index: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            Text(localized(.nativeCopyrightNoticesTitle))
                .font(Typography.largeTitle)
            Text(localized(.nativeCopyrightNoticesPrivacyNote))
                .foregroundStyle(.secondary)
            if isLoading, notices.isEmpty {
                ProgressView()
            }
            if let errorMessage {
                Text(UiMessages.string(errorMessage, locale: nativeUiLocale)).foregroundStyle(.red)
                Button(localized(.nativeCommonRetry)) {
                    Task { await viewModel.loadInitial() }
                }
                .accessibilityIdentifier("copyright-notices-initial-retry")
            }
            ForEach(notices) { row in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Button {
                        onNavigateToTargetPath("/copyright/notices/\(pathComponent(row.id))")
                    } label: {
                        Text(localized(.nativeCopyrightNoticesCase, parameters: ["id": row.id]))
                            .underline()
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                    .buttonStyle(.plain)
                    Text(localized(.nativeCopyrightNoticesAcceptedDate, parameters: [
                        "date": localizedDate(row.acceptedAt ?? row.receivedAt, style: .date)
                    ]))
                    Text(localized(
                        .nativeCopyrightNoticesAffectedPlacementCount,
                        numberParameters: ["count": Double(row.targetCount)]
                    ))
                    claimant(row.claimant)
                }
                .padding(Spacing.md)
                .background(.quaternary, in: RoundedRectangle(cornerRadius: 8))
                .accessibilityIdentifier("copyright-notice-row-\(row.id)")
            }
            if let pageInfo, pageInfo.hasNextPage, pageInfo.endCursor != nil {
                if paginationFailed {
                    Text(localized(.nativeSwiftRouteSurfaceLoadMoreFailed)).foregroundStyle(.red)
                }
                Button(localized(paginationFailed
                        ? .nativeCommonRetry
                        : isLoading ? .nativeSwiftCommonLoadingMore : .nativeCopyrightNoticesLoadMore)) {
                    Task { await viewModel.loadMore() }
                }
                .disabled(isLoading)
                .accessibilityIdentifier("copyright-notices-load-more")
            }
        }
        .padding(Spacing.md)
    }

}
