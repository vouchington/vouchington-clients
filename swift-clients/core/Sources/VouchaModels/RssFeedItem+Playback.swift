import Foundation

public enum RssFeedItemPlaybackKind: Sendable, Equatable {
    case audio(url: String)
    case directVideo(url: String)
    case externalAudio(url: String?)
    case embedOnlyVideo(platform: String?, videoID: String?)
    case unavailable
}

public extension RssFeedItem {
    var sourceTitle: String? {
        normalizedNonEmpty(rssFeed?.title)
    }

    var primaryMediaURLString: String? {
        enclosureURL ?? mediaContent?.url
    }

    var externalURLString: String? {
        normalizedNonEmpty(url?.url) ?? normalizedNonEmpty(link)
    }

    var displayThumbnailURLString: String? {
        thumbnailURL ?? rssFeed?.podcastShow?.coverArtUrl
    }

    var normalizedMediaType: String? {
        normalizedNonEmpty(mediaType)?.lowercased()
    }

    var playbackKind: RssFeedItemPlaybackKind {
        let kind = normalizedMediaType
        let directURL = primaryMediaURLString
        let externalVideoID = normalizedNonEmpty(videoID)
        let externalVideoPlatform = normalizedNonEmpty(videoPlatform)
        let enclosureMIMEType = normalizedMIMEType(enclosureType)
        let isAudioMedia = isAudioMediaType(kind) || isAudioMediaType(enclosureMIMEType)
        if isAudioMedia {
            if let directURL {
                return .audio(url: directURL)
            }
            return .externalAudio(url: externalURLString)
        }
        if isVideoMediaType(kind) || externalVideoID != nil || externalVideoPlatform != nil
            || enclosureMIMEType?.hasPrefix("video/") == true {
            if let directURL {
                return .directVideo(url: directURL)
            }
            return .embedOnlyVideo(platform: externalVideoPlatform, videoID: externalVideoID)
        }
        return .unavailable
    }

    var hasPlayableMedia: Bool {
        switch playbackKind {
        case .audio, .directVideo:
            true
        case .externalAudio, .embedOnlyVideo, .unavailable:
            false
        }
    }

    func replacingThumbnailURL(_ newThumbnailURL: String?) -> RssFeedItem {
        RssFeedItem(
            id: id,
            rssFeedId: rssFeedId,
            title: title,
            description: description,
            content: content,
            link: link,
            url: url,
            publishedAt: publishedAt,
            creator: creator,
            categories: categories,
            data: data,
            rssFeed: rssFeed,
            mediaContent: mediaContent,
            thumbnailURL: newThumbnailURL ?? thumbnailURL,
            mediaType: mediaType,
            enclosureURL: enclosureURL,
            enclosureType: enclosureType,
            durationSeconds: durationSeconds,
            videoID: videoID,
            videoPlatform: videoPlatform,
            election: election
        )
    }

    private func normalizedNonEmpty(_ value: String?) -> String? {
        guard let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines), !trimmed.isEmpty else {
            return nil
        }
        return trimmed
    }

    private func normalizedMIMEType(_ value: String?) -> String? {
        normalizedNonEmpty(value)?.lowercased()
    }

    private func isAudioMediaType(_ value: String?) -> Bool {
        guard let normalized = normalizedMIMEType(value) else {
            return false
        }
        return normalized == "audio" || normalized.hasPrefix("audio/")
    }

    private func isVideoMediaType(_ value: String?) -> Bool {
        guard let normalized = normalizedMIMEType(value) else {
            return false
        }
        return normalized == "video" || normalized.hasPrefix("video/")
    }
}
