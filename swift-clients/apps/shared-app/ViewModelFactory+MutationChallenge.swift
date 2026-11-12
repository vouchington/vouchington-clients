import Foundation

extension ViewModelFactory {
    func installMutationChallengeHandler() async {
        await apiClient.setMutationChallengeHandler { @MainActor [weak self] in
            guard let self, let account = sessionManager.currentUserId else { return nil }
            let token = await mutationChallengeBroker.request()
            guard !Task.isCancelled, sessionManager.currentUserId == account else { return nil }
            return token
        }
    }
}
