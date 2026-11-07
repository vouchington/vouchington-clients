import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct CommunityApplicationReviewControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void

    @State
    private var rejectionReason = ""

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesApplicationReview, locale: nativeUiLocale))
                .font(Typography.headline)
            Picker(
                UiMessages.string(.nativeSwiftCommunitiesApplicationId, locale: nativeUiLocale),
                selection: $viewModel.selectedApplicationId
            ) {
                Text(UiMessages.string(.nativeSwiftCommunityRowsNone, locale: nativeUiLocale)).tag("")
                ForEach(viewModel.rowPagination.items) { item in
                    Text(verbatim: UiMessages.string(item.row.titleText, locale: nativeUiLocale)).tag(item.id)
                }
            }
            TextField(
                UiMessages.string(.nativeSwiftCommunitiesRejectionReason, locale: nativeUiLocale),
                text: $rejectionReason
            ).textFieldStyle(.roundedBorder)
            HStack {
                Button(
                    UiMessages.string(.nativeSwiftCommonApprove, locale: nativeUiLocale),
                    systemImage: "checkmark.circle"
                ) {
                    guard isSignedIn else {
                        showSignIn()
                        return
                    }
                    Task { await viewModel.approveApplication(applicationId: viewModel.selectedApplicationId) }
                }
                Button(
                    UiMessages.string(.nativeSwiftCommunitiesReject, locale: nativeUiLocale),
                    systemImage: "xmark.circle"
                ) {
                    guard isSignedIn else {
                        showSignIn()
                        return
                    }
                    Task { await viewModel.rejectApplication(
                        applicationId: viewModel.selectedApplicationId,
                        reason: rejectionReason
                    ) }
                }
            }
            .disabled(isSignedIn && !viewModel.rowPagination.items
                .contains { $0.id == viewModel.selectedApplicationId })
        }
    }
}
