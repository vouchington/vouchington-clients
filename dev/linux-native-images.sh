#!/usr/bin/env bash
# Keep these images aligned with the Linux native jobs in .github/workflows.
# shellcheck disable=SC2034
SWIFT_LINUX_IMAGE='swift:6.4.0-noble@sha256:31d14d727f4451f25e29bac9c42563a098c39ac66415e8bad970f48038e38aca'
SWIFTFORMAT_LINUX_IMAGE='ghcr.io/nicklockwood/swiftformat:0.61.1@sha256:10f870fb7ae917d0373acb582ea87d60ffca92d75eee3260cbede710666f0fca'
SWIFTLINT_LINUX_IMAGE='ghcr.io/realm/swiftlint:0.65.0@sha256:a482729f4b58741875af1566f23397f3f6db300372756fc31606d0a4527fab9e'
