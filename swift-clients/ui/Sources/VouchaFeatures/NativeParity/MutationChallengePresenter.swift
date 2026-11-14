#if os(iOS)
    import Observation
    import SwiftUI
    import UIKit
    import VouchaAuth

    typealias MutationChallengeContentFactory = @MainActor (String, URL, @escaping (String) -> Void) -> AnyView

    private struct MutationChallengeContentKey: EnvironmentKey {
        static let defaultValue: MutationChallengeContentFactory = { siteKey, baseURL, onToken in
            AnyView(NativeTurnstileChallengeView(
                siteKey: siteKey,
                action: AppAttestActionTag(rawValue: "bot-challenge.mutation"),
                baseURL: baseURL,
                onToken: onToken
            ))
        }
    }

    extension EnvironmentValues {
        var mutationChallengeContent: MutationChallengeContentFactory {
            get { self[MutationChallengeContentKey.self] }
            set { self[MutationChallengeContentKey.self] = newValue }
        }
    }

    /// The active window owns a challenge until completion, even if focus moves elsewhere.
    struct MutationChallengePresenter: UIViewControllerRepresentable {
        let broker: MutationChallengeBroker
        let siteKey: String
        let baseURL: URL
        @Environment(\.mutationChallengeContent)
        private var makeContent

        func makeUIViewController(context _: Context) -> MutationChallengePresentationController {
            MutationChallengePresentationController()
        }

        func updateUIViewController(_ controller: MutationChallengePresentationController, context _: Context) {
            controller.broker = broker
            controller.siteKey = siteKey
            controller.baseURL = baseURL
            controller.makeContent = makeContent
            controller.reconcile()
        }

        static func dismantleUIViewController(
            _ controller: MutationChallengePresentationController,
            coordinator _: ()
        ) {
            controller.invalidate()
        }
    }

    final class MutationChallengePresentationController: UIViewController, UIAdaptivePresentationControllerDelegate {
        var broker: MutationChallengeBroker? {
            didSet {
                if oldValue !== broker {
                    if let presentedID { oldValue?.finishPresentation(id: presentedID, owner: owner, token: nil) }
                    observeBroker()
                }
            }
        }

        var siteKey = ""
        var baseURL: URL?
        var makeContent = MutationChallengeContentKey.defaultValue
        private var presentedID: UUID?
        private var challenge: UIHostingController<AnyView>?
        private var isTransitioning = false
        private weak var sourceWindow: UIWindow?
        private let owner = UUID()
        private var isActive = true

        deinit {
            NotificationCenter.default.removeObserver(self)
        }

        override func viewDidLoad() {
            super.viewDidLoad()
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(presentationEnvironmentChanged(_:)),
                name: UIWindow.didBecomeKeyNotification,
                object: nil
            )
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(presentationEnvironmentChanged(_:)),
                name: UIScene.didActivateNotification,
                object: nil
            )
        }

        @objc
        private func presentationEnvironmentChanged(_ notification: Notification) {
            guard isActive else { return }
            if notification.object as? UIWindow === sourceWindow
                || notification.object as? UIScene === sourceWindow?.windowScene {
                reconcile()
            }
        }

        func invalidate() {
            isActive = false
            broker = nil
            reconcile()
        }

        override func viewDidAppear(_ animated: Bool) {
            super.viewDidAppear(animated)
            reconcile()
        }

        /// Keep observing while a full-screen feature cover detaches the root SwiftUI view.
        private func observeBroker() {
            guard isActive, let broker else { return }
            withObservationTracking {
                _ = broker.pendingID
            } onChange: { [weak self, weak broker] in
                Task { @MainActor [weak self, weak broker] in
                    guard let self, isActive, let broker, self.broker === broker else { return }
                    observeBroker()
                    reconcile()
                }
            }
        }

        func reconcile() {
            if let window = viewIfLoaded?.window { sourceWindow = window }
            let pendingID = broker?.pendingID
            guard !isTransitioning else { return }
            if let challenge {
                guard presentedID != pendingID else { return }
                isTransitioning = true
                challenge.dismiss(animated: true) { [weak self] in
                    guard let self else { return }
                    self.challenge = nil
                    presentedID = nil
                    isTransitioning = false
                    reconcile()
                }
                return
            }
            guard isActive, let id = pendingID, let broker, let baseURL,
                  var presenter = sourceWindow?.rootViewController else { return }
            let windowIsActive = sourceWindow?.isKeyWindow == true
                && sourceWindow?.windowScene?.activationState == .foregroundActive
            guard presentedID == id || windowIsActive,
                  broker.claimPresentation(id: id, owner: owner) else { return }
            presentedID = id
            while let presented = presenter.presentedViewController {
                presenter = presented
            }
            // A request can arrive while the feature's sheet is still animating into place.
            if let transition = presenter.transitionCoordinator {
                isTransitioning = true
                transition.animate(alongsideTransition: nil) { [weak self] _ in
                    Task { @MainActor [weak self] in
                        self?.isTransitioning = false
                        self?.reconcile()
                    }
                }
                return
            }
            let challenge = makeChallenge(id: id, broker: broker, baseURL: baseURL)
            self.challenge = challenge
            presentedID = id
            isTransitioning = true
            presenter.present(challenge, animated: true) { [weak self] in
                self?.isTransitioning = false
                self?.reconcile()
            }
            challenge.presentationController?.delegate = self
        }

        private func makeChallenge(
            id: UUID,
            broker: MutationChallengeBroker,
            baseURL: URL
        ) -> UIHostingController<AnyView> {
            let owner = owner
            return UIHostingController(rootView: makeContent(siteKey, baseURL) { token in
                broker.finishPresentation(id: id, owner: owner, token: token)
            })
        }

        func presentationControllerDidDismiss(_: UIPresentationController) {
            if let presentedID { broker?.finishPresentation(id: presentedID, owner: owner, token: nil) }
            challenge = nil
            presentedID = nil
            reconcile()
        }
    }
#endif
