import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

const attr = (text, name) => new RegExp(`\\b${name}="([^"]*)"`, 'u').exec(text)?.[1]

/** Reduce a result class name such as `Module.Class` or `Ns.Class, Assembly` to `Class`. */
export function shortClassName(name) {
  return name.split(',')[0].trim().split('.').pop()
}

function parseJUnit(xml) {
  const rows = []
  for (const match of xml.matchAll(/<testcase\b([^>]*?)(?:\/>|>([\s\S]*?)<\/testcase>)/gu)) {
    const className = attr(match[1], 'classname')
    if (className) rows.push({ className, failed: /<(?:failure|error)\b/u.test(match[2] ?? '') })
  }
  return rows
}

function parseTrx(xml) {
  const classById = new Map()
  for (const match of xml.matchAll(
    /<UnitTest\b[^>]*\bid="([^"]+)"[\s\S]*?<TestMethod\b[^>]*\bclassName="([^"]+)"/gu,
  )) {
    classById.set(match[1], match[2])
  }
  const rows = []
  for (const match of xml.matchAll(/<UnitTestResult\b([^>]*)>/gu)) {
    const className = classById.get(attr(match[1], 'testId'))
    if (className) rows.push({ className, failed: attr(match[1], 'outcome') === 'Failed' })
  }
  return rows
}

/** Collect per-class pass/fail rows from every JUnit XML or TRX file under `dir`. */
export function readResults(dir) {
  const rows = []
  for (const entry of readdirSync(dir, { recursive: true })) {
    const path = join(dir, entry)
    if (path.endsWith('.trx')) rows.push(...parseTrx(readFileSync(path, 'utf8')))
    else if (path.endsWith('.xml')) rows.push(...parseJUnit(readFileSync(path, 'utf8')))
  }
  const classes = new Map()
  for (const { className, failed } of rows) {
    const name = shortClassName(className)
    classes.set(name, { failed: failed || classes.get(name)?.failed === true })
  }
  return classes
}
