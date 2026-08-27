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
    private var applicationId = ""
    @State
    private var rejectionReason = ""

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesApplicationReview, locale: nativeUiLocale))
                .font(Typography.headline)
            TextField(
                UiMessages.string(.nativeSwiftCommunitiesApplicationId, locale: nativeUiLocale),
                text: $applicationId
            ).textFieldStyle(.roundedBorder)
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
                    Task { await viewModel.approveApplication(applicationId: applicationId) }
                }
                Button(
                    UiMessages.string(.nativeSwiftCommunitiesReject, locale: nativeUiLocale),
                    systemImage: "xmark.circle"
                ) {
                    guard isSignedIn else {
                        showSignIn()
                        return
                    }
                    Task { await viewModel.rejectApplication(applicationId: applicationId, reason: rejectionReason) }
                }
            }
        }
    }
}
