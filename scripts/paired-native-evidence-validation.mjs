export const fail = message => {
  throw new Error(`Paired native test evidence rejected: ${message}`)
}

export function exactKeys(value, keys, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value))
    fail(`${label} must be an object`)
  const actual = Object.keys(value).sort()
  const expected = [...keys].sort()
  if (JSON.stringify(actual) !== JSON.stringify(expected))
    fail(`${label} fields differ from the fixed schema`)
}
