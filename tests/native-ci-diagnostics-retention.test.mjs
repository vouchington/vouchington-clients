import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'

const workflow = readFileSync(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)

describe('native CI failure diagnostics', () => {
  it('keeps failed launch xcresults long enough to investigate', () => {
    for (const name of ['macos', 'ios', 'ipad']) {
      assert.match(
        workflow,
        new RegExp(
          `name: ${name}-launch-diagnostics-[^\\n]*\\n(?:(?!retention-days)[^\\n]*\\n)*\\s+retention-days: 7`,
          'u',
        ),
      )
    }
  })

  it('resolves macOS packages through the retrying action without wiping the clones', () => {
    assert.doesNotMatch(workflow, /rm -rf "\$XCODE_DERIVED_DATA" "\$SWIFT_PACKAGE_CLONES"/u)
    assert.match(
      workflow,
      /uses: \.\/\.github\/actions\/resolve-swift-packages\n\s+with:\n\s+platform: macOS\n\s+source-root: candidate-clients\n\s+clones-dir: \$\{\{ runner\.temp \}\}\/swift-package-clones/u,
    )
  })
})
