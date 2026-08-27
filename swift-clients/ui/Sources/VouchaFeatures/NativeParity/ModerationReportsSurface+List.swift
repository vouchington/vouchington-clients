import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension ModerationReportsSurface {
    @ViewBuilder
    var reportList: some View {
        if reportListIsEmpty {
            EmptyStateView(
                icon: "tray",
                title: .message(.nativeSwiftModerationReportsNoReports),
                message: .message(.nativeSwiftModerationReportsNoReportsMessage)
            )
        } else if viewModel.viewerTier == .member {
            ForEach(viewModel.memberReports) { report in
                MemberModerationReportCard(report: report, onNavigate: onNavigate)
            }
        } else if viewModel.mode == .grouped {
            ForEach(viewModel.duplicateClusters) { duplicateCluster in
                if viewModel.hasActiveReports(in: duplicateCluster) {
                    ModerationReportDuplicateClusterCard(
                        duplicateCluster: duplicateCluster,
                        viewModel: viewModel,
                        onRemove: {
                            viewModel.replaceSelectionWithActiveRemovableReports(in: duplicateCluster)
                            viewModel.pendingConfirmation = .bulkRemove
                        }
                    )
                }
            }
            ForEach(viewModel.clusters) { cluster in
                StaffModerationReportClusterCard(
                    cluster: cluster,
                    viewModel: viewModel,
                    onNavigate: onNavigate,
                    onConfirm: { viewModel.pendingConfirmation = $0 }
                )
            }
        } else {
            ForEach(viewModel.staffReports) { report in
                StaffModerationReportCard(
                    report: report,
                    viewModel: viewModel,
                    onNavigate: onNavigate,
                    onConfirm: { viewModel.pendingConfirmation = $0 }
                )
            }
        }
    }

    private var reportListIsEmpty: Bool {
        if viewModel.viewerTier == .member {
            return viewModel.memberReports.isEmpty
        }
        if viewModel.mode == .grouped {
            return !viewModel.duplicateClusters.contains(where: viewModel.hasActiveReports)
                && !viewModel.clusters.contains { !viewModel.activeReports(in: $0).isEmpty }
        }
        return viewModel.staffReports.isEmpty
    }
}
