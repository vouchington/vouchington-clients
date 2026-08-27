import AVFoundation
import Foundation
import Observation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaModels

@MainActor
final class PersistenceOperationTracker {
    private var operationIDs: Set<UUID> = []
    private var drainContinuations: [CheckedContinuation<Void, Never>] = []

    func register() -> UUID {
        let operationID = UUID()
        operationIDs.insert(operationID)
        return operationID
    }

    func complete(_ operationID: UUID) {
        operationIDs.remove(operationID)
        guard operationIDs.isEmpty else { return }
        let continuations = drainContinuations
        drainContinuations = []
        continuations.forEach { $0.resume() }
    }

    func drain() async {
        guard !operationIDs.isEmpty else { return }
        await withCheckedContinuation { continuation in
            drainContinuations.append(continuation)
        }
    }
}

@Observable
@MainActor
public final class PodcastPlaybackController {
    public internal(set) var currentItem: RssFeedItem?
    public internal(set) var currentTimeSeconds: Double = 0
    public internal(set) var durationSeconds: Double = 0
    public internal(set) var playbackRate: Double = 1
    public internal(set) var isPlaying = false
    public internal(set) var isLoading = false
    public internal(set) var errorMessage: String?
    public internal(set) var chapters: [PodcastChapter] = []

    let player = AVPlayer()
    public let apiBaseURL: URL

    let client: APIClient
    let sessionManager: SessionManager
    var playbackStartToken: UInt64 = 0
    var playbackPositionWriteGeneration: UInt64 = 0
    var playbackPositionWriteTask: Task<Void, Never>?
    let persistenceOperations = PersistenceOperationTracker()
    var progressTask: Task<Void, Never>?
    var chapterLoadToken: UInt64 = 0
    var chapterLoadTask: Task<Void, Never>?
    var periodicTimeObserver: Any?
    var playbackFinishedObserver: NSObjectProtocol?
    var currentItemStartedPlayback = false

    public let availablePlaybackRates: [Double] = [0.75, 1, 1.25, 1.5, 2]

    public init(client: APIClient, sessionManager: SessionManager) {
        self.client = client
        self.sessionManager = sessionManager
        apiBaseURL = client.baseURL
        installObservers()
    }

    isolated deinit {
        if let periodicTimeObserver {
            player.removeTimeObserver(periodicTimeObserver)
        }
        if let playbackFinishedObserver {
            NotificationCenter.default.removeObserver(playbackFinishedObserver)
        }
        chapterLoadTask?.cancel()
    }

    public var currentTitle: String? {
        currentItem?.title ?? currentItem?.data?.title
    }

    public var currentSubtitle: String? {
        currentItem?.sourceTitle ?? currentItem?.creator ?? currentItem?.link
    }

    public var currentArtworkURL: String? {
        VouchaURLResolver.absoluteString(
            for: currentItem?.displayThumbnailURLString,
            relativeTo: apiBaseURL
        )
    }

    public var currentPlaybackKind: RssFeedItemPlaybackKind? {
        currentItem?.playbackKind
    }

    public var currentExternalURL: URL? {
        switch currentPlaybackKind {
        case let .externalAudio(urlString):
            guard let urlString else { return nil }
            return URL(string: urlString)
        case .audio, .directVideo, .embedOnlyVideo, .unavailable, nil:
            return nil
        }
    }

    public var showsVideoPlayer: Bool {
        if case .directVideo = currentPlaybackKind {
            return true
        }
        return false
    }

    public var showsSpeedControls: Bool {
        if case .audio = currentPlaybackKind {
            return true
        }
        return false
    }

    public func isCurrentItem(_ item: RssFeedItem) -> Bool {
        currentItem?.id == item.id
    }

    func advancePlaybackStartToken() -> UInt64 {
        playbackStartToken &+= 1
        return playbackStartToken
    }

    public func togglePlayback(for item: RssFeedItem) async {
        if isCurrentItem(item) {
            guard !isLoading else { return }
            if isPlaying {
                pause()
            } else {
                await resumeCurrentItem()
            }
            return
        }
        await start(item)
    }

    public func pause() {
        guard !isLoading else { return }
        let item = currentItem
        player.pause()
        let pausedPositionSeconds = currentTimeSeconds
        isPlaying = false
        cancelProgressTask()
        guard let item else { return }
        persistProgressInOperation(for: item, completed: false, positionSeconds: pausedPositionSeconds)
    }

    public func seek(to seconds: Double) {
        guard let item = currentItem else { return }
        let clamped = max(0, seconds)
        currentTimeSeconds = clamped
        player.seek(to: CMTime(seconds: clamped, preferredTimescale: 600))
        guard currentItemStartedPlayback else { return }
        persistProgressInOperation(for: item, completed: false, positionSeconds: clamped)
    }

    public func setPlaybackRate(_ rate: Double) {
        playbackRate = rate
        if isPlaying {
            player.rate = Float(rate)
        }
    }

    func drainPersistenceOperations() async {
        await persistenceOperations.drain()
    }

    func persistProgressInOperation(
        for item: RssFeedItem,
        completed: Bool,
        positionSeconds: Double
    ) {
        let operationID = persistenceOperations.register()
        Task {
            defer { persistenceOperations.complete(operationID) }
            await persistProgress(
                for: item,
                completed: completed,
                positionSeconds: positionSeconds
            )
        }
    }

}
