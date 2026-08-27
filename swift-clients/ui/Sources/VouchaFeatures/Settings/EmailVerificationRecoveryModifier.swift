import SwiftUI
import VouchaAPI

struct EmailVerificationRecoveryModifier: ViewModifier {
    let client: APIClient?
    let gate: EmailVerificationGatedMutation

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
                EmailAddressManager(client: client, recoveryMode: true) {
                    gate.dismissRecovery()
                }
                .padding()
            }
        }
    }
}

extension View {
    func emailVerificationRecovery(client: APIClient?, gate: EmailVerificationGatedMutation) -> some View {
        modifier(EmailVerificationRecoveryModifier(client: client, gate: gate))
    }
}
