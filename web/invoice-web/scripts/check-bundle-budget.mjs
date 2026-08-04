import { readdir, stat } from 'node:fs/promises'
import { join } from 'node:path'

const assets = join(process.cwd(), 'dist', 'assets')
const files = await readdir(assets)
const javascript = await Promise.all(
  files.filter(file => file.endsWith('.js')).map(async file => ({
    file,
    bytes: (await stat(join(assets, file))).size,
  })),
)
const total = javascript.reduce((sum, asset) => sum + asset.bytes, 0)
const largest = javascript.reduce((max, asset) => asset.bytes > max.bytes ? asset : max, { file: 'none', bytes: 0 })
const totalBudget = 320 * 1024
const chunkBudget = 220 * 1024

console.log(`JavaScript: ${javascript.length} lazy chunks, ${total} bytes total; largest ${largest.file} at ${largest.bytes} bytes.`)
if (total > totalBudget || largest.bytes > chunkBudget) {
  throw new Error(`Bundle budget exceeded (total ${totalBudget}, chunk ${chunkBudget} bytes).`)
}
