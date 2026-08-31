public struct ChatSSEFrame: Sendable, Equatable {
    public let eventType: String
    public let rawData: String
}

public enum ChatSSEParserError: Error, Equatable {
    case frameTooLarge
}

public struct ChatSSEParser: Sendable {
    public static let maximumFrameCharacters = 1_024 * 1_024
    private var buffer = ""
    private var currentEventType = "message"
    private var currentDataLines: [String] = []
    private var currentFrameCharacters = 0

    public init() {}

    public mutating func processChunk(_ chunk: String) throws -> [ChatSSEFrame] {
        var frames: [ChatSSEFrame] = []
        var start = chunk.startIndex

        while let lineBreakIndex = chunk[start...].firstIndex(of: "\n") {
            let line = buffer + String(chunk[start ..< lineBreakIndex])
            buffer = ""
            try frames.append(contentsOf: processLine(line))
            start = chunk.index(after: lineBreakIndex)
        }

        buffer += String(chunk[start...])
        guard buffer.count <= Self.maximumFrameCharacters else {
            throw ChatSSEParserError.frameTooLarge
        }

        return frames
    }

    public mutating func processLine(_ line: String) throws -> [ChatSSEFrame] {
        let normalizedLine = line.last == "\r" ? String(line.dropLast()) : line

        if normalizedLine.isEmpty {
            return dispatchPendingEvent()
        }

        if normalizedLine.hasPrefix(":") {
            return []
        }

        let colonIndex = normalizedLine.firstIndex(of: ":")
        let field = colonIndex.map { String(normalizedLine[..<$0]) } ?? normalizedLine
        let rawValue = colonIndex.map { String(normalizedLine[normalizedLine.index(after: $0)...]) } ?? ""
        let value = rawValue.hasPrefix(" ") ? String(rawValue.dropFirst()) : rawValue

        if field == "event" {
            guard value.count <= Self.maximumFrameCharacters else {
                throw ChatSSEParserError.frameTooLarge
            }
            currentEventType = value.isEmpty ? "message" : value
        } else if field == "data" {
            currentFrameCharacters += value.count + 1
            guard currentFrameCharacters <= Self.maximumFrameCharacters else {
                throw ChatSSEParserError.frameTooLarge
            }
            currentDataLines.append(value)
        }

        return []
    }

    public mutating func flush() throws -> [ChatSSEFrame] {
        var frames: [ChatSSEFrame] = []
        if !buffer.isEmpty {
            try frames.append(contentsOf: processLine(buffer))
            buffer = ""
        }
        if !currentDataLines.isEmpty || currentEventType != "message" {
            frames.append(contentsOf: dispatchPendingEvent())
        }
        return frames
    }

    private mutating func dispatchPendingEvent() -> [ChatSSEFrame] {
        defer {
            currentEventType = "message"
            currentDataLines = []
            currentFrameCharacters = 0
        }
        return [ChatSSEFrame(eventType: currentEventType, rawData: currentDataLines.joined(separator: "\n"))]
    }
}
