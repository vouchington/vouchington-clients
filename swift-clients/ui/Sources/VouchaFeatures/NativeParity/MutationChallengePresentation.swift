import SwiftUI
import VouchaAuth

public extension View {
    func mutationChallengeSheet(broker: MutationChallengeBroker, siteKey: String, baseURL: URL) -> some View {
        #if os(iOS)
            background(MutationChallengePresenter(
                broker: broker,
                siteKey: siteKey,
                baseURL: baseURL
            ))
        #else
            sheet(isPresented: Binding(
                get: { broker.pendingID != nil },
                set: { if !$0 { broker.cancel() } }
            )) {
                if let id = broker.pendingID {
                    NativeTurnstileChallengeView(
                        siteKey: siteKey,
                        action: AppAttestActionTag(rawValue: "bot-challenge.mutation"),
                        baseURL: baseURL
                    ) { token in
                        broker.finish(id: id, token: token)
                    }
                }
            }
            .onDisappear { broker.cancel() }
        #endif
    }
}
