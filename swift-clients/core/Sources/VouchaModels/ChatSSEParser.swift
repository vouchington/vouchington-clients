public struct ChatSSEFrame: Sendable, Equatable {
    public let eventType: String
    public let rawData: String
}

public struct ChatSSEParser: Sendable {
    private var buffer = ""
    private var currentEventType = "message"
    private var currentDataLines: [String] = []

    public init() {}

    public mutating func processChunk(_ chunk: String) -> [ChatSSEFrame] {
        buffer += chunk
        var frames: [ChatSSEFrame] = []

        while let lineBreakIndex = buffer.firstIndex(of: "\n") {
            let line = String(buffer[..<lineBreakIndex])
            buffer.removeSubrange(...lineBreakIndex)
            frames.append(contentsOf: processLine(line))
        }

        return frames
    }

    public mutating func processLine(_ line: String) -> [ChatSSEFrame] {
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
            currentEventType = value.isEmpty ? "message" : value
        } else if field == "data" {
            currentDataLines.append(value)
        }

        return []
    }

    public mutating func flush() -> [ChatSSEFrame] {
        var frames: [ChatSSEFrame] = []
        if !buffer.isEmpty {
            frames.append(contentsOf: processLine(buffer))
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
        }
        return [ChatSSEFrame(eventType: currentEventType, rawData: currentDataLines.joined(separator: "\n"))]
    }
}
