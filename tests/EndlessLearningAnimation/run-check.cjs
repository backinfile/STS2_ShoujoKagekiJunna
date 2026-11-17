const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const [port, mode = 'deck', output = 'out/tmp/endless-learning-order', label = mode] = process.argv.slice(2);
const base = 'http://127.0.0.1:' + port + '/api/v1';
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function req(route, body) {
    const response = await fetch(base + route, body === undefined ? {} : {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
    });
    const result = await response.json();
    if (!response.ok || result.error || result.status === 'error') throw new Error(JSON.stringify(result));
    return result;
}
(async () => {
    const settings = await req('/settings');
    assert(settings.silent_test && settings.minimized_startup_guard && !settings.silent_test_error);
    async function dismissTutorials() {
        for (let i = 0; i < 20; i++) {
            const prompt = await req('/ui/prompt');
            if (!prompt.active) return;
            assert(prompt.is_tutorial, 'unexpected non-tutorial prompt');
            const option = prompt.options.find(o => o.name === 'advance' && o.enabled)?.name
                ?? prompt.options.find(o => o.name === 'no' && o.enabled)?.name;
            assert(option, 'tutorial button ready');
            await req('/ui/prompt', { option, prompt_id: prompt.prompt_id });
            await sleep(150);
        }
    }
    await dismissTutorials();
    let state = await req('/singleplayer?format=json');
    if (state.state_type === 'menu') {
        if (state.menu_screen === 'main') {
            await req('/singleplayer', { action: 'menu_select', option: state.options?.includes('continue') ? 'continue' : 'singleplayer' });
            await sleep(400); await dismissTutorials();
        }
        state = await req('/singleplayer?format=json');
        if (state.menu_screen === 'character_select') {
            for (const option of ['JUNNA', 'confirm']) {
                await req('/singleplayer', { action: 'menu_select', option });
                await sleep(400); await dismissTutorials();
            }
        }
        for (let i = 0; i < 100; i++) {
            await sleep(100);
            state = await req('/singleplayer?format=json');
            if (state.player) break;
        }
        await req('/singleplayer', { action: 'run_command', command: 'unlock all' });
    }
    fs.rmSync(path.join(output, 'live.json'), { force: true });
    await req('/singleplayer', { action: 'run_command', command: 'junna_endless_animation check ' + mode });
    const deadline = Date.now() + 15000;
    let live;
    do {
        await sleep(100);
        try { live = JSON.parse(fs.readFileSync(path.join(output, 'live.json'), 'utf8')); } catch { continue; }
        if (live.events.some(e => e.kind === 'command-completed') &&
            (mode !== 'deck' || live.events.some(e => e.kind === 'upgrade-finished'))) break;
    } while (Date.now() < deadline);
    assert(live?.events.some(e => e.kind === 'command-completed'), 'upgrade command completes');
    fs.writeFileSync(path.join(output, label + '.json'), JSON.stringify(live, null, 2) + '\n');
    assert.equal(live.level, 1, 'upgraded once');
    assert.equal(live.gains, 1, 'one physical new knowledge per upgrade');
    assert.equal(live.checks.length, 1);
    assert(live.checks.every(c => c.pass), 'new knowledge waits for the native upgrade animation to exit');
    if (mode === 'deck') {
        const kinds = live.events.map(e => e.kind);
        assert(kinds.indexOf('upgrade-start') < kinds.indexOf('upgrade-finished'));
        assert(kinds.indexOf('upgrade-finished') < kinds.indexOf('knowledge-acquired'));
    }
    console.log('PASS ' + mode + ': ' + live.events.map(e => e.kind).join(' -> '));
})().catch(error => { console.error('FAIL: ' + error.message); process.exitCode = 1; });
