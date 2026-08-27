import AVFoundation
import Foundation

@MainActor
extension PodcastPlaybackController {
    func seekPlayer(to seconds: Double) async {
        let clamped = max(0, seconds)
        currentTimeSeconds = clamped
        let seekTime = CMTime(seconds: clamped, preferredTimescale: 600)
        await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
            let lock = NSLock()
            var didResume = false
            func resumeOnce() {
                lock.lock()
                defer { lock.unlock() }
                guard !didResume else { return }
                didResume = true
                continuation.resume()
            }
            let timeoutTask = Task {
                try? await Task.sleep(nanoseconds: 1_500_000_000)
                resumeOnce()
            }
            player.seek(to: seekTime, toleranceBefore: .zero, toleranceAfter: .zero) { _ in
                timeoutTask.cancel()
                resumeOnce()
            }
        }
    }

    func cancelProgressTask() {
        progressTask?.cancel()
        progressTask = nil
    }
}
