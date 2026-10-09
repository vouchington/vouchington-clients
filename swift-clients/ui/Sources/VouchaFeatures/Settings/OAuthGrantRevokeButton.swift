import Observation
import SwiftUI
import VouchaLocalization
import VouchaModels

@MainActor
struct OAuthGrantRevokeButton: View {
    let grant: OAuthGrant
    let locale: Locale
    let isDisabled: Bool
    @State private var interactionState: OAuthGrantRevokeInteractionState
    let revoke: @MainActor () async -> Void

    init(
        grant: OAuthGrant,
        locale: Locale,
        isDisabled: Bool,
        interactionState: OAuthGrantRevokeInteractionState,
        revoke: @escaping @MainActor () async -> Void
    ) {
        self.grant = grant
        self.locale = locale
        self.isDisabled = isDisabled
        _interactionState = State(initialValue: interactionState)
        self.revoke = revoke
    }

    var body: some View {
        Button(UiMessages.string(.nativeSwiftSettingsRevoke, locale: locale), role: .destructive) {
            interactionState.confirming = true
        }
        .disabled(isDisabled)
        .buttonStyle(.bordered)
        .accessibilityIdentifier("oauth-grant-revoke-\(grant.id)")
        .confirmationDialog(
            UiMessages.string(
                .nativeCredentialsConfirmRevokeGrant,
                parameters: ["app": grant.client.clientName],
                locale: locale
            ),
            isPresented: Binding(
                get: { interactionState.confirming },
                set: { interactionState.confirming = $0 }
            ),
            titleVisibility: .visible
        ) {
            Button(UiMessages.string(.nativeSwiftSettingsRevoke, locale: locale), role: .destructive) {
                interactionState.confirming = false
                Task { await revoke() }
            }
            Button(UiMessages.string(.commonCancel, locale: locale), role: .cancel) {
                interactionState.confirming = false
            }
        }
    }
}

@MainActor
@Observable
final class OAuthGrantRevokeInteractionState {
    var confirming = false
}
