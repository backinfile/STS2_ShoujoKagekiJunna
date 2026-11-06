'use strict';
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const crypto = require('node:crypto');
const { WorkbenchStore } = require('../mcp-store.cjs');
const { workspaceId } = require('../mcp-config.cjs');

function createPublisher({ file = '/var/lib/junna-workbench/state.sqlite', web = '/var/www/junna', templateFile = path.join(__dirname, 'public-template.html') } = {}) {
  const template = fs.readFileSync(templateFile, 'utf8');
  const store = new WorkbenchStore({ file, seed: { data: { cards: [] } }, workspaceId, assetDirectory: path.join(web, 'assets') });
  let publishedRevision = null;

  function publish() {
    const current = store.read({ copy: false });
    if (current.revision === publishedRevision) return false;
    const snapshot = store.state({ assetURLs: true });
    const exportedAt = new Date().toISOString();
    const data = JSON.stringify({ ...snapshot.bundle, versions: snapshot.versions, exportedAt }).replace(/</g, '\\u003c');
    const date = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Shanghai', dateStyle: 'short' }).format(new Date(exportedAt));
    const html = template.replace(/只读 · \d{4}-\d{2}-\d{2}/, '只读 · ' + date);
    const directory = path.join(web, 'releases', 'live-r' + snapshot.revision + '-' + crypto.randomUUID());
    fs.mkdirSync(directory, { recursive: true });
    fs.symlinkSync(path.join(web, 'assets'), path.join(directory, 'assets'), 'junction');
    fs.writeFileSync(path.join(directory, 'index.html'), html);
    fs.writeFileSync(path.join(directory, 'index.html.gz'), zlib.gzipSync(html));
    fs.writeFileSync(path.join(directory, 'snapshot.json'), data);
    fs.writeFileSync(path.join(directory, 'snapshot.json.gz'), zlib.gzipSync(data));
    const manifest = { revision: snapshot.revision, exportedAt, assets: snapshot.assetIds.length,
      counts: Object.fromEntries(Object.entries(current.data).filter(([, value]) => Array.isArray(value)).map(([key, value]) => [key, value.length])),
      htmlBytes: Buffer.byteLength(html), snapshotBytes: Buffer.byteLength(data) };
    fs.writeFileSync(path.join(directory, 'manifest.json'), JSON.stringify(manifest) + '\n');
    const next = path.join(web, 'current.pending');
    if (fs.existsSync(next)) fs.unlinkSync(next);
    fs.symlinkSync(directory, next, 'junction');
    fs.renameSync(next, path.join(web, 'current'));
    publishedRevision = snapshot.revision;
    console.log('Published read-only r' + publishedRevision);
    return true;
  }

  return { publish, close: () => store.storage.close() };
}

function start() {
  const publisher = createPublisher();
  const tick = () => { try { publisher.publish(); } catch (error) { console.error('Read-only publish failed:', error.message); } };
  tick();
  const timer = setInterval(tick, 1000);
  process.on('SIGTERM', () => { clearInterval(timer); publisher.close(); process.exit(0); });
}

if (require.main === module) start();
module.exports = { createPublisher, start };
