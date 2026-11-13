import SwiftUI
import VouchaAPI
import VouchaAuth

private struct FreshConfirmationBrokerKey: EnvironmentKey {
    static let defaultValue: NativeOAuthAuthorizationCoordinator? = nil
}

public extension EnvironmentValues {
    /// Broker used by email recovery so provider-backed accounts can finish fresh confirmation.
    var freshConfirmationBroker: NativeOAuthAuthorizationCoordinator? {
        get { self[FreshConfirmationBrokerKey.self] }
        set { self[FreshConfirmationBrokerKey.self] = newValue }
    }
}

struct EmailVerificationRecoveryModifier: ViewModifier {
    let client: APIClient?
    let gate: EmailVerificationGatedMutation
    @Environment(\.freshConfirmationBroker)
    private var broker

    func body(content: Content) -> some View {
        @Bindable
        var gate = gate
        content.sheet(isPresented: Binding(
            get: { gate.isRecoveryPresented },
            set: { isPresented in
                if !isPresented {
                    gate.dismissRecovery()
                }
            }
        )) {
            if let client {
                EmailVerificationRecoverySheet(client: client, broker: broker) { gate.dismissRecovery() }
            }
        }
    }
}

struct EmailVerificationRecoverySheet: View {
    let client: APIClient
    let broker: NativeOAuthAuthorizationCoordinator?
    let onVerified: () -> Void

    var body: some View {
        EmailAddressManager(client: client, broker: broker, recoveryMode: true, onVerified: onVerified)
            .padding()
    }
}

extension View {
    func emailVerificationRecovery(client: APIClient?, gate: EmailVerificationGatedMutation) -> some View {
        modifier(EmailVerificationRecoveryModifier(client: client, gate: gate))
    }
}
