import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { SWIFT_ANDROID_MATERIALIZER_PATH } from './swift-repair-common.mjs'

export async function writeSwiftAndroidRepair({ outputRoot = process.cwd(), prepare, ...input }) {
  const result = await prepare({ ...input })
  const materializerPath = resolve(outputRoot, SWIFT_ANDROID_MATERIALIZER_PATH)
  const provenancePath = resolve(outputRoot, 'dependabot-swift-android-provenance.json')
  await mkdir(dirname(materializerPath), { recursive: true })
  await writeFile(materializerPath, result.materializer)
  await writeFile(provenancePath, `${JSON.stringify(result.provenance, null, 2)}\n`)
  return { ...result, materializerPath, provenancePath }
}
