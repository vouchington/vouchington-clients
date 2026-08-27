import AVFoundation
import Foundation
import VouchaAPI
import VouchaModels

@MainActor
extension PodcastPlaybackController {
    func start(_ item: RssFeedItem) async {
        let startToken = advancePlaybackStartToken()
        guard let url = directPlayableURL(for: item) else { return }
        cancelChapterLoad()

        if let previousItem = currentItem {
            player.pause()
            let previousPositionSeconds = currentTimeSeconds
            let previousItemStartedPlayback = currentItemStartedPlayback
            isPlaying = false
            cancelProgressTask()
            if previousItemStartedPlayback {
                await persistProgress(for: previousItem, completed: false, positionSeconds: previousPositionSeconds)
            }
            guard startToken == playbackStartToken else { return }
            guard currentItem?.id == previousItem.id else { return }
        }

        guard startToken == playbackStartToken else { return }
        currentItem = item
        chapters = []
        errorMessage = nil
        isLoading = true
        currentTimeSeconds = 0
        durationSeconds = max(0, Double(item.durationSeconds ?? 0))
        isPlaying = false
        currentItemStartedPlayback = false
        cancelProgressTask()

        player.pause()
        player.replaceCurrentItem(with: AVPlayerItem(url: url))

        let resumeSeconds = await resumeSecondsIfNeeded(for: item)
        guard startToken == playbackStartToken else { return }
        guard currentItem?.id == item.id else { return }
        if resumeSeconds > 0 {
            await seekPlayer(to: resumeSeconds)
        } else {
            currentTimeSeconds = 0
        }

        isLoading = false
        player.playImmediately(atRate: Float(playbackRate))
        isPlaying = true
        currentItemStartedPlayback = true
        startChapterLoad(for: item)
        startProgressTask()
    }

    func resumeCurrentItem() async {
        guard currentItem != nil else { return }
        guard !isLoading else { return }
        if durationSeconds > 0, currentTimeSeconds >= durationSeconds {
            await seekPlayer(to: 0)
        }
        player.playImmediately(atRate: Float(playbackRate))
        isPlaying = true
        currentItemStartedPlayback = true
        startProgressTask()
    }

    func resumeSecondsIfNeeded(for item: RssFeedItem) async -> Double {
        guard sessionManager.isSignedIn else { return 0 }
        guard case .audio = item.playbackKind else { return 0 }

        do {
            let response: PodcastPlaybackPositionResponse = try await client.send(
                .podcastPlaybackPosition(rssFeedItemId: item.id)
            )
            guard let playbackPosition = response.playbackPosition else { return 0 }
            return playbackPosition.isCompleted ? 0 : playbackPosition.positionSeconds
        } catch {
            return 0
        }
    }

    func persistProgress(
        for item: RssFeedItem,
        completed: Bool,
        positionSeconds: Double
    ) async {
        guard sessionManager.isSignedIn else { return }
        guard case .audio = item.playbackKind else { return }
        let completedDurationSeconds = effectiveDurationSeconds()
        let persistedCompleted = completed && completedDurationSeconds > 0
        let playbackPositionSeconds = persistedCompleted
            ? max(positionSeconds, completedDurationSeconds)
            : positionSeconds
        playbackPositionWriteGeneration &+= 1
        let writeGeneration = playbackPositionWriteGeneration
        let previousWriteTask = playbackPositionWriteTask
        let writeTask = Task { [client, sessionManager, previousWriteTask] in
            await previousWriteTask?.value
            guard sessionManager.isSignedIn else { return }
            guard case .audio = item.playbackKind else { return }
            do {
                let _: EmptyResponse = try await client.send(
                    .updatePodcastPlaybackPosition(
                        rssFeedItemId: item.id,
                        positionSeconds: playbackPositionSeconds,
                        completed: persistedCompleted
                    )
                )
            } catch {
                return
            }
        }
        playbackPositionWriteTask = writeTask
        await writeTask.value
        if playbackPositionWriteGeneration == writeGeneration {
            playbackPositionWriteTask = nil
        }
    }

    func installObservers() {
        periodicTimeObserver = player.addPeriodicTimeObserver(
            forInterval: CMTime(seconds: 1, preferredTimescale: 2),
            queue: .main
        ) { [weak self] time in
            Task { @MainActor in
                guard let self else { return }
                self.currentTimeSeconds = max(0, time.seconds)
                if let duration = self.player.currentItem?.duration.seconds, duration.isFinite, duration > 0 {
                    self.durationSeconds = duration
                }
            }
        }

        playbackFinishedObserver = NotificationCenter.default.addObserver(
            forName: .AVPlayerItemDidPlayToEndTime,
            object: nil,
            queue: .main
        ) { [weak self] notification in
            MainActor.assumeIsolated {
                guard let self,
                      let endedItem = notification.object as? AVPlayerItem,
                      endedItem === self.player.currentItem
                else { return }
                self.currentTimeSeconds = self.durationSeconds
                self.isPlaying = false
                self.currentItemStartedPlayback = false
                self.cancelProgressTask()
                if let item = self.currentItem {
                    let completedPositionSeconds = self.currentTimeSeconds
                    self.persistProgressInOperation(
                        for: item,
                        completed: true,
                        positionSeconds: completedPositionSeconds
                    )
                }
            }
        }
    }

    func directPlayableURL(for item: RssFeedItem) -> URL? {
        guard case let .audio(urlString) = item.playbackKind else {
            guard case let .directVideo(urlString) = item.playbackKind else { return nil }
            return URL(string: normalizedPlayableMediaURLString(urlString))
        }
        return URL(string: normalizedPlayableMediaURLString(urlString))
    }

    func effectiveDurationSeconds() -> Double {
        if durationSeconds > 0 {
            return durationSeconds
        }
        guard let playerDurationSeconds = player.currentItem?.duration.seconds,
              playerDurationSeconds.isFinite,
              playerDurationSeconds > 0
        else { return 0 }
        return playerDurationSeconds
    }

    func startProgressTask() {
        progressTask?.cancel()
        guard let item = currentItem else { return }
        progressTask = Task { [weak self] in
            while !Task.isCancelled {
                do {
                    try await Task.sleep(nanoseconds: 12_000_000_000)
                } catch {
                    return
                }
                guard let self else { return }
                let progressSeconds = currentTimeSeconds
                await persistProgress(for: item, completed: false, positionSeconds: progressSeconds)
            }
        }
    }

}
