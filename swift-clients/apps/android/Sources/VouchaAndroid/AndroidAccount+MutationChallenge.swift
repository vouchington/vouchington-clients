import Foundation
import SwiftUI

extension AndroidAccount {
    func installMutationChallengeHandler() async {
        await apiClient.setMutationChallengeHandler { @MainActor [weak self] in
            guard let self else { return nil }
            let account = session.currentUserId
            let token = await mutationChallengeBroker.request()
            guard !Task.isCancelled, session.currentUserId == account else { return nil }
            return token
        }
    }
}

extension View {
    func androidMutationChallengeSheet(account: AndroidAccount) -> some View {
        sheet(isPresented: Binding(
            get: { account.mutationChallengeBroker.pendingID != nil },
            set: { if !$0 { account.mutationChallengeBroker.cancel() } }
        )) {
            if let id = account.mutationChallengeBroker.pendingID {
                AndroidTurnstileChallenge(
                    siteKey: account.config.requiredTurnstileSiteKey,
                    actionTag: "bot-challenge.mutation",
                    baseURL: account.config.baseURL
                ) { token in
                    account.mutationChallengeBroker.finish(id: id, token: token)
                }
            }
        }
    }
}
