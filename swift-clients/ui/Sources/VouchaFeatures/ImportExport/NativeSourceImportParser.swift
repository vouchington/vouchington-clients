import Foundation

enum NativeSourceImportParser {
    static func feedURLs(in text: String, format: SourceImportFileFormat) throws -> [String] {
        switch format {
        case .csv:
            try csvFeedURLs(in: text)
        case .opml:
            opmlFeedURLs(in: text)
        }
    }

    private static func csvFeedURLs(in text: String) throws -> [String] {
        let records = try CSVRecords.parse(text)
        guard let header = records.first else { return [] }
        let normalizedHeader = header.map {
            $0.trimmingCharacters(in: .whitespaces)
                .lowercased()
        }
        let recognizedColumns = ["url", "xmlurl", "rss_feed_url"]
        if let urlColumn = normalizedHeader.firstIndex(where: recognizedColumns.contains) {
            return records.dropFirst().compactMap { record in
                let value = record[urlColumn].trimmingCharacters(in: .whitespacesAndNewlines)
                return value.isEmpty ? nil : value
            }
        }

        let nonemptyLines = text.components(separatedBy: "\n").compactMap { line in
            let value = line.trimmingCharacters(in: .whitespacesAndNewlines)
            return value.isEmpty ? nil : value
        }
        guard let firstLine = nonemptyLines.first, firstLine.contains("\t") else {
            return nonemptyLines
        }
        let headers = firstLine.lowercased().components(separatedBy: "\t")
        guard let urlColumn = headers.firstIndex(where: { $0 == "xmlurl" || $0 == "url" }) else {
            return []
        }
        return nonemptyLines.dropFirst().compactMap { line in
            let fields = line.components(separatedBy: "\t")
            guard urlColumn < fields.count else { return nil }
            let value = fields[urlColumn]
            return value.isEmpty ? nil : value
        }
    }

    private static func opmlFeedURLs(in text: String) -> [String] {
        guard let outlinePattern = try? NSRegularExpression(
            pattern: #"<outline\s[^>]*?xmlUrl\s*=\s*"([^"]*)"[^>]*?/?>"#,
            options: .caseInsensitive
        ), let attributePattern = try? NSRegularExpression(pattern: #"(\w+)\s*=\s*"([^"]*)""#)
        else { return [] }

        let source = text as NSString
        return outlinePattern
            .matches(in: text, range: NSRange(location: 0, length: source.length))
            .compactMap { match in
                let tag = source.substring(with: match.range) as NSString
                var xmlURL: String?
                for attribute in attributePattern.matches(
                    in: tag as String,
                    range: NSRange(location: 0, length: tag.length)
                ) where tag.substring(with: attribute.range(at: 1)).lowercased() == "xmlurl" {
                    xmlURL = tag.substring(with: attribute.range(at: 2))
                }
                return xmlURL?.isEmpty == false ? xmlURL : nil
            }
    }
}

private enum CSVParseError: Error {
    case invalidFormat
}

private enum CSVRecords {
    static func parse(_ text: String) throws -> [[String]] {
        var reader = CSVReader(text)
        return try reader.parse()
    }
}

private struct CSVReader {
    private let csv: String
    private var records: [[String]] = []
    private var record: [String] = []
    private var field = ""
    private var inQuotes = false
    private var afterClosingQuote = false
    private var recordHasContent = false
    private var recordDelimiter: Character?

    init(_ text: String) {
        csv = text.unicodeScalars.first?.value == 0xFEFF
            ? String(text.unicodeScalars.dropFirst())
            : text
    }

    mutating func parse() throws -> [[String]] {
        var index = csv.startIndex
        while index < csv.endIndex {
            let character = csv[index]
            let nextIndex = csv.index(after: index)
            if inQuotes {
                if consumeQuoted(character, nextIndex: nextIndex) {
                    index = csv.index(after: nextIndex)
                    continue
                }
            } else if isRecordDelimiter(character) {
                appendRecord()
                afterClosingQuote = false
            } else if afterClosingQuote {
                try consumeAfterClosingQuote(character)
            } else {
                try consumeUnquoted(character)
            }
            index = nextIndex
        }
        guard !inQuotes else { throw CSVParseError.invalidFormat }
        appendRecord()
        guard !records.isEmpty else { return [] }
        guard let width = records.first?.count, records.allSatisfy({ $0.count == width }) else {
            throw CSVParseError.invalidFormat
        }
        return records
    }

    private mutating func isRecordDelimiter(_ character: Character) -> Bool {
        guard character == "\n" || character == "\r" || character == "\r\n" else {
            return false
        }
        if recordDelimiter == nil {
            recordDelimiter = character
        }
        return character == recordDelimiter
            || (character == "\r\n" && (recordDelimiter == "\r" || recordDelimiter == "\n"))
    }

    private mutating func consumeQuoted(_ character: Character, nextIndex: String.Index) -> Bool {
        guard character == "\"" else {
            field.append(character)
            return false
        }
        if nextIndex < csv.endIndex, csv[nextIndex] == "\"" {
            field.append("\"")
            return true
        }
        inQuotes = false
        afterClosingQuote = true
        return false
    }

    private mutating func consumeAfterClosingQuote(_ character: Character) throws {
        switch character {
        case ",":
            appendField()
            afterClosingQuote = false
        case " ", "\t":
            break
        default:
            throw CSVParseError.invalidFormat
        }
    }

    private mutating func consumeUnquoted(_ character: Character) throws {
        switch character {
        case ",":
            appendField()
        case "\"" where field.trimmingCharacters(in: .whitespaces).isEmpty:
            field = ""
            inQuotes = true
            recordHasContent = true
        case "\"":
            throw CSVParseError.invalidFormat
        default:
            field.append(character)
            if !character.isWhitespace {
                recordHasContent = true
            }
        }
    }

    private mutating func appendField() {
        record.append(field)
        field = ""
        recordHasContent = true
    }

    private mutating func appendRecord() {
        record.append(field)
        field = ""
        if recordHasContent {
            records.append(record)
        }
        record = []
        recordHasContent = false
    }
}
