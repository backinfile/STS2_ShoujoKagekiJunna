const fs = require('node:fs');
const assert = require('node:assert/strict');

async function main() {
  const [port, resultPath] = process.argv.slice(2);
  assert(port && resultPath, 'Usage: node verify_arrow_preview.cjs <port> <result.json>');
  async function command(command) {
    const response = await fetch(`http://127.0.0.1:${port}/api/v1/singleplayer`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ action: 'run_command', command })
    });
    const result = await response.json();
    assert.equal(result.status, 'ok', JSON.stringify(result));
    return result;
  }
  await command('junna_hover_density prepare 1');
  await new Promise(resolve => setTimeout(resolve, 800));
  await command('card JUNNA_STRIKE Hand');
  await new Promise(resolve => setTimeout(resolve, 500));
  const previous = fs.existsSync(resultPath) ? fs.statSync(resultPath).mtimeMs : 0;
  await command('junna_preview_placement check');
  const deadline = Date.now() + 15000;
  while (!fs.existsSync(resultPath) || fs.statSync(resultPath).mtimeMs <= previous) {
    assert(Date.now() < deadline, 'Timed out waiting for the in-engine arrow preview check');
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  const result = JSON.parse(fs.readFileSync(resultPath, 'utf8'));
  console.log(JSON.stringify(result, null, 2));
  assert.equal(result.pass, true, 'Arrow previews must stay at their enemy and stay inactive during targeting');
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
