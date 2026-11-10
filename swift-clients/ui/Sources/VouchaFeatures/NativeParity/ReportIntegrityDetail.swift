import SwiftUI
import VouchaLocalization
import VouchaModels

struct ReportIntegrityDetail: View {
    let viewModel: ReportIntegrityViewModel
    let id: String
    @Environment(\.locale)
    private var locale
    var body: some View {
        ScrollView {
            if viewModel.isLoadingDetail {
                ProgressView().accessibilityIdentifier("report-integrity-detail-loading")
            } else if viewModel.detailFailed {
                StaffDetailError(error: .message(.nativeSwiftIntegrityLoadFailed)) {
                    Task { await viewModel.loadDetail(id: id) }
                }
            } else if let flag = viewModel.detailFlag, flag.id == id {
                VStack(alignment: .leading) {
                    Text(verbatim: flag.entityLabel)
                    Text(UiMessages.string(.nativeSwiftIntegrityEvidence, locale: locale))
                    ForEach(integrityDetails(flag.details, locale: locale), id: \.self) { value in
                        Text(verbatim: value)
                    }
                }
                .accessibilityIdentifier("report-integrity-detail-evidence")
            }
        }
        .padding()
        .safeAreaInset(edge: .top) { StaffDetailDismissButton() }
        .task { await viewModel.loadDetail(id: id) }
    }

}
