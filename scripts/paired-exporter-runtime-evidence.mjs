export const pairedExporterImage = {
  reference:
    'node:26.12.0-bookworm@sha256:662de7348097c97cc0a43dd71649bf2f5b721da6587eb390c4ee4ad78213bcf1',
  manifestDigest: 'sha256:662de7348097c97cc0a43dd71649bf2f5b721da6587eb390c4ee4ad78213bcf1',
  indexDigest: 'sha256:a856ae5e25d498bbe5ec85fde88f00f5ed99908fda803ba39d7735ab8574f58c',
  platform: 'linux/amd64',
  nodeVersion: 'v26.12.0',
  gitVersion: 'git version 2.39.5',
  sourceRevision: 'f27125230b3c367dcf63043ea3fe286fb3d0914c',
  created: '2026-10-14T18:29:00Z',
  baseImage: 'buildpack-deps:bookworm',
  baseDigest: 'sha256:6664ea6221140b52bddee69d4b3410d0f2f4eda8ed2bbf80dc91ea055cd94a43',
}

export const pairedExporterPackages = [
  {
    name: '@nodable/entities',
    version: '3.1.0',
    integrity:
      'sha512-LsS/DjHr+uDM647Gru/cA8+J3a3HfhttwCKLyuoyN7yXTFCxKBtSgMQBvrW9yNPa2/zDRUNuGPaH5QUFoa4arQ==',
  },
  {
    name: '@vouchington/localization',
    version: '0.2.1',
    integrity:
      'sha512-9ijdOAcTK6/6rorkcyMHeEYgwQGFLBAgothhyaPb/yP/LR3fzPF19Lv+mHpExCCWiqtd4uUisG42+JWnt2wW6g==',
  },
  {
    name: '@vouchington/utils',
    version: '0.7.4',
    integrity:
      'sha512-EANChQVGZlLE9CPTSNVT8y0yIC0lnH1WnT3w/R+MzEGD2bI7cA+8hQZtZMXnX+9lyQuKoUH/ce3JyDN3vHvp3A==',
  },
  {
    name: 'anynum',
    version: '1.0.1',
    integrity:
      'sha512-N6//FLET/tXYNM/F6ABca1oH6fWB+KlTt909Le28WMDBk8oaT4vY17DCrwg2MvmuqUKt3Ni4N5dGJ/EoBgcO6A==',
  },
  {
    name: 'fast-xml-builder',
    version: '1.3.1',
    integrity:
      'sha512-pIM/1n3ntFXKYrUZwW7QCK0gAW7XY+wzj1YMIV3tLDvPj/V+zTGJK5e3/4WJfwj0qWw2ElNXiTixda/R+3YSug==',
  },
  {
    name: 'fast-xml-parser',
    version: '5.11.2',
    integrity:
      'sha512-R9iDuNrQYeQut46cn2r2wHKn4HYzVDvDm5J1wW+koZewykv0yuO2HChTYeZtrULyHIDM9cj9TUXloCsueCQUog==',
  },
  {
    name: 'is-unsafe',
    version: '2.0.2',
    integrity:
      'sha512-HgbIHPBH0KHHCcjLfGsCvhtPTVxjaAZlXjwdz7/GQC40SjSe4sfQsar8J5VFo8JOSbarkpV0OLG95bbaNd9aAQ==',
  },
  {
    name: 'path-expression-matcher',
    version: '1.6.2',
    integrity:
      'sha512-enSlaiat05iasnzmgNxRj8reFdj3puY2QpNgP1aPIaVfT6nn9ICuPoFlKHk8EN22HcwewshO+mN2DGbkCEOtqQ==',
  },
  {
    name: 'strnum',
    version: '2.4.2',
    integrity:
      'sha512-rDG3Ah4TV0k1hWvLSzkZtMmLN9+eS+h3knq4MP6A42Y3Yh5qGNnOUs1jJkoSr8FG5dsL28c7KgkIBzSEykqtuw==',
  },
  {
    name: 'xml-naming',
    version: '0.3.0',
    integrity:
      'sha512-ghig2TBE/H11aOVgmahA3MhimvkBr6JIYknH/Dhdk10nXwdbIqBJsbfMxpvFPG8bAw77gN29aQWvKpmVoPlvPQ==',
  },
]

export const pairedExporterOutputPaths = [
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageDescriptor.g.cs',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.es.resx',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.fr.resx',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.pt.resx',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.resx',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/en.lproj/Localizable.strings',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/es.lproj/Localizable.strings',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/fr.lproj/Localizable.strings',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/Resources/pt.lproj/Localizable.strings',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageDescriptor.swift',
  'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift',
]

export const pairedExporterImageDigest =
  'sha256:662de7348097c97cc0a43dd71649bf2f5b721da6587eb390c4ee4ad78213bcf1'

function fail(message) {
  throw new Error(`Paired exporter runtime evidence rejected: ${message}`)
}

function exactKeys(value, expected) {
  return (
    value &&
    typeof value === 'object' &&
    !Array.isArray(value) &&
    JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...expected].sort())
  )
}

function packageFile(name, version) {
  return `${name.replaceAll('@', '').replaceAll('/', '_')}-${version}.tgz`
}

export function validatePairedExporterRuntimeEvidence(value, { producerHead, lockBlob }) {
  if (
    !exactKeys(value, [
      'schemaVersion',
      'producerRootHead',
      'producerLockBlob',
      'image',
      'packages',
    ]) ||
    value.schemaVersion !== 1 ||
    value.producerRootHead !== producerHead ||
    value.producerLockBlob !== lockBlob ||
    !exactKeys(value.image, Object.keys(pairedExporterImage)) ||
    Object.keys(pairedExporterImage).some(key => value.image[key] !== pairedExporterImage[key]) ||
    !Array.isArray(value.packages) ||
    value.packages.length !== pairedExporterPackages.length
  )
    fail('runtime identity or dependency manifest does not match the reviewed fixed pins')
  value.packages.forEach((record, index) => {
    const expected = pairedExporterPackages[index]
    if (
      !exactKeys(record, ['name', 'version', 'integrity', 'file', 'size']) ||
      record.name !== expected.name ||
      record.version !== expected.version ||
      record.integrity !== expected.integrity ||
      record.file !== packageFile(expected.name, expected.version) ||
      !Number.isSafeInteger(record.size) ||
      record.size < 1 ||
      record.size > 8 * 1024 * 1024
    )
      fail(`dependency record ${index} does not match the reviewed lock closure`)
  })
  return value
}
