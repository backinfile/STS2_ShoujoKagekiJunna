const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const [port, output = 'out/tmp/endless-learning-order', label = 'gain-flow-fixed'] = process.argv.slice(2);
const base = 'http://127.0.0.1:' + port + '/api/v1';
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
(async () => {
    const settings = await (await fetch(base + '/settings')).json();
    assert(settings.silent_test && settings.minimized_startup_guard && !settings.silent_test_error);
    fs.rmSync(path.join(output, 'gain-flow.completed'), { force: true });
    const response = await (await fetch(base + '/singleplayer', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ action: 'run_command', command: 'junna_gain_flow check' }) })).json();
    assert(!response.error, JSON.stringify(response));
    const deadline = Date.now() + 60000;
    while (!fs.existsSync(path.join(output, 'gain-flow.completed')) && Date.now() < deadline) await sleep(100);
    assert(fs.existsSync(path.join(output, 'gain-flow.completed')), 'check completed');
    const rows = JSON.parse(fs.readFileSync(path.join(output, 'gain-flow.json'), 'utf8'));
    fs.writeFileSync(path.join(output, label + '.json'), JSON.stringify(rows, null, 2) + '\n');
    assert.equal(rows.length, 9);
    assert(rows.every(row => row.pass), JSON.stringify(rows.filter(row => !row.pass)));
    console.log('PASS: 9 nonblocking acquisition checks; counts, versions, Random placement and original order preserved.');
})().catch(error => { console.error(error.message); process.exitCode = 1; });
