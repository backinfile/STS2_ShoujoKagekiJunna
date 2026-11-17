const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const [port, output = 'out/tmp/endless-learning-order', label = 'native-rest'] = process.argv.slice(2);
const root = path.resolve(output), base = 'http://127.0.0.1:' + port + '/api/v1';
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
async function req(route, body) {
    const response = await fetch(base + route, body === undefined ? {} : {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
    });
    const result = await response.json();
    if (!response.ok || result.error || result.status === 'error') throw new Error(JSON.stringify(result));
    return result;
}
const act = (action, values = {}) => req('/singleplayer', { action, ...values });
const live = () => JSON.parse(fs.readFileSync(path.join(root, 'live.json'), 'utf8'));
(async () => {
    const settings = await req('/settings');
    assert(settings.silent_test && settings.minimized_startup_guard && !settings.silent_test_error);
    let state = await req('/singleplayer?format=json');
    if (state.rest_site) {
        await act('choose_rest_option', { index: state.rest_site.options.find(o => o.id === 'SMITH').index });
        await sleep(500);
        state = await req('/singleplayer?format=json');
    }
    const card = state.card_select.cards.filter(c => c.id === 'JUNNA_JK82').at(-1);
    assert(card, 'prepared learning card exists');
    const before = live();
    await req('/recording/start', { output_dir: root, fps: 15, width: 1280, height: 720,
        audio: true, max_duration_seconds: 40 });
    try {
        await sleep(600);
        await act('select_card', { index: card.index });
        await sleep(1000);
        assert((await req('/singleplayer?format=json')).card_select?.can_confirm);
        await act('confirm_selection');
        const deadline = Date.now() + 15000;
        let after;
        do { await sleep(100); after = live(); } while (Date.now() < deadline && after.gains === before.gains);
        assert.equal(after.gains - before.gains, 1, 'exactly one new knowledge');
        assert.equal(after.level - before.level, 1, 'exactly one upgrade');
        assert(after.checks.every(c => c.pass));
        assert.deepEqual(after.events.slice(before.events.length).map(e => e.kind),
            ['upgrade-start', 'upgrade-finished', 'knowledge-acquired']);
        await sleep(3200);
        assert.equal(live().events.filter(e => e.kind === 'upgrade-start').length,
            before.events.filter(e => e.kind === 'upgrade-start').length + 1, 'smith animation plays once');
        fs.writeFileSync(path.join(root, label + '.json'), JSON.stringify(live(), null, 2));
        console.log('PASS native rest: upgrade-start -> upgrade-finished -> knowledge-acquired; animation once');
    } finally {
        await req('/recording/stop', {});
        let status;
        for (let i = 0; i < 100; i++) {
            await sleep(200); status = await req('/recording');
            if (['completed', 'failed'].includes(status.status)) break;
        }
        fs.writeFileSync(path.join(root, label + '-recording.json'), JSON.stringify(status, null, 2));
        assert.equal(status.status, 'completed');
        assert.equal(status.audio_source, 'engine_mix');
        console.log('Video: ' + status.output_path);
        const image = await fetch(base + '/recording/frames?frame_count=12&columns=4&frame_width=320');
        assert(image.ok);
        fs.writeFileSync(path.join(root, label + '-frames.png'), Buffer.from(await image.arrayBuffer()));
    }
})().catch(error => { console.error('FAIL: ' + error.message); process.exitCode = 1; });
