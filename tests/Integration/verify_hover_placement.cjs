const fs = require('node:fs');
const assert = require('node:assert/strict');

async function main() {
  const [port, resultPath] = process.argv.slice(2);
  assert(port && resultPath, 'Usage: node verify_hover_placement.cjs <port> <result.json>');
  const previous = fs.existsSync(resultPath) ? fs.statSync(resultPath).mtimeMs : 0;
  const response = await fetch(`http://127.0.0.1:${port}/api/v1/singleplayer`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ action: 'run_command', command: 'junna_hover_density placement' })
  });
  const command = await response.json();
  assert.equal(command.status, 'ok', JSON.stringify(command));
  const deadline = Date.now() + 5000;
  while (!fs.existsSync(resultPath) || fs.statSync(resultPath).mtimeMs <= previous) {
    assert(Date.now() < deadline, 'Timed out waiting for the in-engine placement check');
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  const result = JSON.parse(fs.readFileSync(resultPath, 'utf8'));
  console.log(JSON.stringify(result, null, 2));
  assert.equal(result.pass, true, 'Enemy arrow previews must sit above the head and leave native tips visible');
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
