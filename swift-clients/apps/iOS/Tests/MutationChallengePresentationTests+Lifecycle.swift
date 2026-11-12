import UIKit

private final class NotificationWaitState: @unchecked Sendable {
    var tokens: [NSObjectProtocol] = []
    var finished = false
}

@MainActor
func awaitKeyAndActive(_ window: UIWindow, scene: UIScene) async {
    while !(window.isKeyWindow && scene.activationState == .foregroundActive) {
        await nextNotification(UIWindow.didBecomeKeyNotification, UIScene.didActivateNotification)
    }
}

/// Suspends until one of the named notifications posts. The observers are registered before
/// suspension on the main actor, so a notification cannot slip between a state check and the wait.
@MainActor
private func nextNotification(_ names: Notification.Name...) async {
    await withCheckedContinuation { (done: CheckedContinuation<Void, Never>) in
        let state = NotificationWaitState()
        state.tokens = names.map { name in
            NotificationCenter.default.addObserver(forName: name, object: nil, queue: .main) { _ in
                MainActor.assumeIsolated {
                    guard !state.finished else { return }
                    state.finished = true
                    state.tokens.forEach(NotificationCenter.default.removeObserver)
                    done.resume()
                }
            }
        }
    }
}
