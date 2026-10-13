import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaModels

extension FollowerDistributionSendSheet {
    func recipientToggle(_ recipient: PublicUser) -> some View {
        let displayName = recipient.name ?? recipient.username ?? recipient.id
        let imageURL = recipient.profileImagePlacement.flatMap { placement in
            imageConfig.imageURL(
                forPlacementId: placement.placementId,
                revision: placement.placementRevision,
                imageId: placement.imageId,
                width: 96
            )
        }

        return Toggle(
            isOn: Binding(
                get: { viewModel.selectedRecipientIds.contains(recipient.id) },
                set: { _ in viewModel.toggleRecipient(recipient.id) }
            )
        ) {
            HStack(spacing: Spacing.sm) {
                Avatar(imageURL: imageURL, username: displayName, size: 36)
                    .accessibilityHidden(true)
                Text(verbatim: displayName)
            }
        }
    }
}
