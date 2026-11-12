import Foundation

extension ViewModelFactory {
    func installMutationChallengeHandler() async {
        await apiClient.setMutationChallengeHandler { @MainActor [weak self] in
            guard let self else { return nil }
            let account = sessionManager.currentUserId
            let token = await mutationChallengeBroker.request()
            guard !Task.isCancelled, sessionManager.currentUserId == account else { return nil }
            return token
        }
    }
}
