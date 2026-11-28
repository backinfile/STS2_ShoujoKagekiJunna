'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const output = path.resolve(process.env.JUNNA_CARD_AUDIT_OUTPUT || path.join(root, 'out/tmp/full-card-tests'));
const catalog = JSON.parse(fs.readFileSync(path.join(root, 'data/card-pool.json'), 'utf8'));
const variants = ['normal', 'empty', 'combo', 'capacity'];
const [mode = 'summary', port = '4852', ...requested] = process.argv.slice(2);
const base = `http://127.0.0.1:${port}/api/v1`;
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
fs.mkdirSync(output, { recursive: true });

async function request(endpoint, body) {
    const response = await fetch(base + endpoint, {
        method: body === undefined ? 'GET' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(30000),
    });
    const result = await response.json();
    if (!response.ok || result.status === 'error') throw new Error(JSON.stringify(result));
    return result;
}
const state = () => request('/singleplayer?format=json');
const action = (action, params = {}) => request('/singleplayer', { action, ...params });
const command = command => action('run_command', { command });

async function dismissTutorials() {
    for (let step = 0; step < 30; step++) {
        const prompt = await request('/ui/prompt');
        if (!prompt.active) return;
        if (!prompt.is_tutorial) throw new Error(`Blocking prompt ${JSON.stringify(prompt)}`);
        if (prompt.ready) await request('/ui/prompt', { option: 'advance', prompt_id: prompt.prompt_id });
        await delay(200);
    }
    throw new Error('Tutorial did not finish');
}

async function until(predicate, seconds = 30) {
    const deadline = Date.now() + seconds * 1000;
    let current;
    while (Date.now() < deadline) {
        current = await state();
        if (predicate(current)) return current;
        await delay(150);
    }
    fs.writeFileSync(path.join(output, 'timeout-state.json'), JSON.stringify(current, null, 2));
    throw new Error(`Timed out in ${current.state_type}`);
}

const runPointer = path.join(output, 'latest-run.json');
const cardIds = new Set(catalog.cards.map(card => card.id));

function readRun(runId = fs.existsSync(runPointer) ? JSON.parse(fs.readFileSync(runPointer, 'utf8')).runId : null) {
    if (runId === null) return { runId, cards: [], variants: [], results: [] };
    if (typeof runId !== 'string' || !/^[a-f0-9]{32}$/.test(runId)) throw new Error('Invalid audit run ID');
    const directory = path.join(output, 'results', runId);
    const marker = JSON.parse(fs.readFileSync(path.join(directory, 'completed.json'), 'utf8'));
    if (marker.completed !== true || marker.runId !== runId || marker.revision !== catalog.revision ||
        !Array.isArray(marker.cards) || marker.cards.length === 0 ||
        new Set(marker.cards).size !== marker.cards.length || !marker.cards.every(id => cardIds.has(id)) ||
        !Array.isArray(marker.variants) || new Set(marker.variants).size !== marker.variants.length ||
        !(marker.variants.length === variants.length && marker.variants.every(variant => variants.includes(variant)) ||
            marker.variants.length === 1 && marker.variants[0] === 'combo'))
        throw new Error('Audit completion marker does not match this card pool or run');
    const results = fs.readdirSync(directory).filter(file => file.endsWith('-result.json')).map(file => {
        const result = JSON.parse(fs.readFileSync(path.join(directory, file), 'utf8'));
        if (!marker.cards.includes(result.id) || typeof result.upgraded !== 'boolean' ||
            !marker.variants.includes(result.variant) || result.revision !== catalog.revision || result.runId !== runId ||
            file !== scenarioKey(result.id, result.upgraded, result.variant) + '-result.json' ||
            !Array.isArray(result.checks) || result.checks.length === 0 ||
            !result.checks.every(check => check && typeof check.name === 'string' && check.name.length &&
                typeof check.pass === 'boolean'))
            throw new Error(`Invalid, empty or out-of-run audit result: ${file}`);
        return { ...result, file };
    });
    return { runId, cards: marker.cards, variants: marker.variants, results };
}

function summarize(run = readRun()) {
    const { runId, results } = run;
    const failures = results.flatMap(result => result.checks.filter(check => !check.pass)
        .map(check => ({ id: result.id, upgraded: result.upgraded, variant: result.variant, ...check })));
    const keys = new Set(results.map(result => scenarioKey(result.id, result.upgraded, result.variant)));
    const missing = (runId ? run.cards : [...cardIds]).flatMap(id =>
        [false, true].flatMap(upgraded => (runId ? run.variants : variants).map(variant => scenarioKey(id, upgraded, variant))))
        .filter(key => !keys.has(key));
    if (keys.size !== results.length) throw new Error('Duplicate scenario results');
    const summary = {
        revision: catalog.revision,
        runId,
        cards: catalog.cards.length,
        scopeCards: run.cards.length,
        scopeVariants: run.variants,
        completedScenarios: results.length,
        assertions: results.reduce((count, result) => count + result.checks.length, 0),
        missing,
        failures,
    };
    fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(summary, null, 2));
    console.log(JSON.stringify({ ...summary, missing: missing.slice(0, 12), missingCount: missing.length,
        failures: failures.slice(0, 30), failureCount: failures.length }));
    return summary;
}

function scenarioKey(id, upgraded, variant) { return `${id}-${upgraded ? 'upgrade' : 'base'}-${variant}`; }

function plan() {
    const casesFile = path.join(root, 'tests/Integration', `card-effect-cases-r${catalog.revision}.json`);
    const cases = JSON.parse(fs.readFileSync(casesFile, 'utf8'));
    const expected = JSON.parse(fs.readFileSync(path.join(root, 'tests/Integration/card-pool-expectations.json'), 'utf8'));
    const ids = cards => cards.map(card => card.id).sort().join(',');
    if (expected.revision !== catalog.revision || ids(expected.cards) !== ids(catalog.cards) ||
        Object.keys(cases).sort().join(',') !== ids(expected.cards))
        throw new Error('Runtime card pool, effect expectations, and scenario plans must cover the same revision and cards');

    const lines = [
        `# r${catalog.revision} 逐牌效果测试`, '',
        `覆盖 ${expected.cards.length} 张已启用牌，包含初始牌、衍生牌和关联牌。基础版与升级版分别验证。`, '',
        '自动场景分别使用常规资源、空资源、机制组合及满手牌。触发专项用卡牌的实际对象、目标、牌堆、费用与前后状态作为依据。', '',
        '每段主要效果录像展示一次可观察的真实流程。录像涉及自然回合或胜利时，按游戏操作推进；其余分支的执行结果见逐牌断言与专项记录。', '',
    ];
    const rows = [];
    for (const card of expected.cards) {
        const steps = cases[card.id];
        if (!Array.isArray(steps) || steps.length !== 3 || steps.some(step => typeof step !== 'string' || !step.trim()))
            throw new Error(`Incomplete scenario for ${card.id}`);
        const entries = [
            ['准备状态', steps[0]],
            ['操作', steps[1]],
            ['预期', steps[2]],
            ['基础牌面', card.text.replaceAll('\n', ' ')],
            ['升级牌面', card.upgradedText.replaceAll('\n', ' ')],
        ];
        lines.push(`## ${card.id} ${card.name}`, '', ...entries.map(([name, value]) => `- **${name}**　${value}`), '');
        rows.push({ id: card.id, name: card.name, entries });
    }
    fs.writeFileSync(path.join(root, `docs/implementation/card-tests-r${catalog.revision}.md`), lines.join('\n'));
    fs.writeFileSync(path.join(output, 'plan.json'), JSON.stringify({ revision: catalog.revision, cards: rows }, null, 2));
    console.log(`Wrote ${rows.length} per-card test entries`);
}

async function ensureCombat() {
    const settings = await request('/settings');
    if (!settings.silent_test || settings.silent_test_error || !settings.minimized_startup_guard) throw new Error('Silent minimized test settings failed');
    await dismissTutorials();
    let current = await state();
    if (current.state_type === 'menu') {
        if (current.options?.includes('continue')) await action('menu_select', { option: 'continue' });
        else {
            if (current.menu_screen === 'main') await action('menu_select', { option: 'singleplayer' });
            await delay(400);
            await dismissTutorials();
            await action('menu_select', { option: 'JUNNA' });
            await action('menu_select', { option: 'confirm' });
        }
        current = await until(current => current.player && (current.battle?.is_play_phase ||
            ['map', 'event'].includes(current.state_type)), 45);
    }
    if (current.state_type === 'rewards') {
        await action('proceed');
        current = await until(current => current.state_type === 'map');
    }
    if (!current.battle?.enemies?.length) {
        if (current.state_type === 'map' && current.map?.next_options?.some(node => node.type === 'Monster')) {
            const deadline = Date.now() + 30000;
            while (Date.now() < deadline && current.state_type === 'map') {
                const node = current.map?.next_options?.find(node => node.type === 'Monster');
                if (node && !current.map.is_traveling)
                    await action('choose_map_node', { index: node.index });
                await delay(1500);
                current = await state();
            }
            current = await until(current => current.battle?.is_play_phase);
        }
    }
    if (!current.battle?.enemies?.length) {
        const deadline = Date.now() + 30000;
        while (true) {
            try { await command('fight CULTISTS_NORMAL'); break; }
            catch (error) {
                if (!error.message.includes('Could not find local player') || Date.now() >= deadline) throw error;
                await delay(300);
            }
        }
        await until(current => current.battle?.is_play_phase, 30);
    }
    await command('unlock all');
}

async function check(combosOnly = false) {
    await ensureCombat();
    await ensureThreeEnemies();
    const runId = randomUUID().replaceAll('-', '');
    const directory = path.join(output, 'results', runId);
    const completion = path.join(directory, 'completed.json');
    await command(['junna_full_audit', combosOnly ? 'combos' : 'check', ...requested, 'run=' + runId].join(' '));
    const deadline = Date.now() + 3600000;
    let previous = 0;
    let lastProgress = Date.now();
    while (!fs.existsSync(completion)) {
        await delay(1000);
        const count = fs.existsSync(directory)
            ? fs.readdirSync(directory).filter(file => file.endsWith('-result.json')).length : 0;
        if (count > previous) {
            previous = count;
            lastProgress = Date.now();
            console.log(`Audit ${runId}: ${count} scenarios`);
        }
        if (Date.now() > deadline || Date.now() - lastProgress > 90000)
            throw new Error(`Audit ${runId} stopped making progress after ${count} scenarios`);
    }
    const run = readRun(runId);
    const expectedCards = requested.length ? requested : [...cardIds];
    if (run.cards.length !== expectedCards.length || !expectedCards.every(id => run.cards.includes(id)) ||
        run.variants.length !== (combosOnly ? 1 : variants.length))
        throw new Error('Game completed a different scope from the requested audit');
    const pointerTemp = runPointer + '.' + runId + '.tmp';
    fs.writeFileSync(pointerTemp, JSON.stringify({ runId }));
    fs.renameSync(pointerTemp, runPointer);
    const { failures, missing } = summarize(run);
    if (missing.length) throw new Error(`${missing.length} requested scenarios produced no results`);
    if (failures.length) throw new Error(`${failures.length} per-card assertions failed`);
}

async function ensureThreeEnemies() {
    const count = current => current.battle?.enemies?.filter(enemy => enemy.hp > 0).length ?? 0;
    if (count(await state()) >= 3) return;
    await command('fight SLIMES_WEAK 6148');
    await until(current => current.battle?.is_play_phase && count(current) >= 3, 45);
}

async function settle(cardId, seconds = 30, selectionCount = 1) {
    const deadline = Date.now() + seconds * 1000;
    let selected = 0;
    const pickedFromGrid = new Set();
    let lastKey = '';
    let stable = 0;
    while (Date.now() < deadline) {
        const current = await state();
        const selection = current.hand_select || current.card_select;
        if (selection) {
            const key = current.state_type + selection.prompt + selection.screen_type;
            if (key !== lastKey) {
                selected = 0;
                pickedFromGrid.clear();
                lastKey = key;
            }
            stable = 0;
            await delay(350);
            if ((selected >= selectionCount || selection.preview_showing) && selection.can_confirm) {
                await action(current.hand_select ? 'combat_confirm_selection' : 'confirm_selection');
                selected = 0;
                pickedFromGrid.clear();
                continue;
            }
            const cards = (selection.cards || []).filter(card => current.hand_select || !pickedFromGrid.has(card.index));
            const preference = ['JA104', 'JA119', 'JA144'].includes(cardId) ? 'JUNNA_ARROW_TARGET_ENEMY'
                : cardId === 'JA65' ? 'JUNNA_JA15' : 'JUNNA_STRIKE';
            const candidate = cards.find(card => card.id === preference) || cards[0];
            if (candidate) {
                await action(current.hand_select ? 'combat_select_card' : 'select_card',
                    current.hand_select ? { card_index: candidate.index } : { index: candidate.index });
                selected++;
                if (!current.hand_select) pickedFromGrid.add(candidate.index);
            } else if (selection.can_confirm) await action(current.hand_select ? 'combat_confirm_selection' : 'confirm_selection');
        } else if (current.battle?.is_play_phase || ['rewards', 'game_over', 'map', 'event'].includes(current.state_type)) {
            let idle = !current.battle;
            if (current.battle) {
                try {
                    const observed = JSON.parse(fs.readFileSync(path.join(output, 'results/readiness.json')));
                    idle = observed.id === cardId && Date.now() - observed.updated < 5000
                        && observed.playPileCount === 0 && !observed.resolving;
                } catch { idle = false; }
            }
            if (idle && ++stable >= 3) return current;
            if (!idle) stable = 0;
        } else stable = 0;
        await delay(200);
    }
    fs.writeFileSync(path.join(output, 'selection-timeout.json'), JSON.stringify(await state(), null, 2));
    throw new Error(`Selection or resolution timed out for ${cardId}`);
}

async function playModel(modelId, cardId, required = true, { targetIndex = 0, selectionCount = 1 } = {}) {
    const current = await state();
    const card = current.player?.hand?.find(card => card.id === modelId && card.can_play);
    if (!card) {
        if (required) throw new Error(`Playable ${modelId} missing`);
        return false;
    }
    const target = card.target_type === 'AnyEnemy'
        ? current.battle.enemies.filter(enemy => enemy.hp > 0)[targetIndex]?.entity_id : undefined;
    await action('play_card', { card_index: card.index, ...(target ? { target } : {}) });
    await delay(300);
    await settle(cardId, 45, selectionCount);
    return true;
}

async function endTurn(cardId) {
    const before = await state();
    if (!before.battle?.is_play_phase) return before;
    const round = before.battle.round;
    await action('end_turn');
    await delay(300);
    const deadline = Date.now() + 60000;
    while (Date.now() < deadline) {
        const current = await settle(cardId, 45);
        if (!current.battle || current.battle.round > round) return current;
        await delay(200);
    }
    throw new Error(`Turn did not advance for ${cardId}`);
}

async function saveImage(endpoint, file) {
    const response = await fetch(base + endpoint, { signal: AbortSignal.timeout(30000) });
    if (!response.ok) throw new Error(`Image capture ${response.status}`);
    fs.writeFileSync(file, Buffer.from(await response.arrayBuffer()));
}

async function record(primary = false, retakes = false, upgraded = false) {
    await ensureCombat();
    await command('junna_probe speed 1');
    const videos = path.join(output, upgraded ? 'videos-upgrade' : 'videos');
    fs.mkdirSync(videos, { recursive: true });
    const selectedCards = catalog.cards.filter(card => !requested.length || requested.includes(card.id));
    const failures = [];
    for (const card of selectedCards) {
        try {
            await recordCard(card, videos, primary, retakes, upgraded);
        } catch (error) {
            failures.push({ id: card.id, error: error.message });
            fs.writeFileSync(path.join(output, 'recording-failures.json'), JSON.stringify(failures, null, 2));
            console.error(`Recording ${card.id} failed: ${error.message}`);
        }
    }
    if (failures.length) throw new Error(`${failures.length} card recordings failed`);
}

async function recordCard(card, videos, primary, retakes, upgraded) {
    const directory = path.join(videos, card.id);
    fs.mkdirSync(directory, { recursive: true });
    const receipt = path.join(directory, 'recording.json');
    if (!retakes && fs.existsSync(receipt)) {
        const prior = JSON.parse(fs.readFileSync(receipt));
        if (prior.status === 'completed' && prior.revision === catalog.revision &&
            Boolean(prior.upgraded) === upgraded && fs.existsSync(prior.output_path)) return;
    }
    if (!(await state()).battle?.is_play_phase) await ensureCombat();
    const multipleEnemies = card.target === 'AllEnemies' ||
        ['JK37B', 'JK37C', 'U_CARD_013', 'U_CARD_036', 'U_CARD_051', 'U_CARD_052', 'U_CARD_079', 'U_CARD_130'].includes(card.id);
    if (multipleEnemies) await ensureThreeEnemies();
    const preparation = path.join(output, 'results', `${scenarioKey(card.id, upgraded, 'normal')}-prepared.json`);
    const previous = fs.existsSync(preparation) ? fs.statSync(preparation).mtimeMs : 0;
    await command(`junna_full_audit prepare ${card.id}${upgraded ? ' upgrade' : ''}`);
    const deadline = Date.now() + 30000;
    while ((!fs.existsSync(preparation) || fs.statSync(preparation).mtimeMs <= previous) && Date.now() < deadline) await delay(150);
    await until(current => current.battle?.is_play_phase && current.player?.hand?.some(item => item.id === card.modelId));
    await delay(600);
    for (let attempt = 0; ; attempt++) {
        const candidate = (await state()).player.hand.find(item => item.id === card.modelId);
        try {
            await action('hover_card', { card_index: candidate.index });
            await action('unhover_card');
            break;
        } catch (error) {
            if (attempt >= 20 || !error.message.includes('not ready')) throw error;
            await delay(250);
        }
    }
    if (primary && card.id === 'JK314') {
        for (let count = 0; count < 3; count++) await playModel('JUNNA_JK122B', card.id);
        await delay(1200);
    }
    const before = await state();
    fs.writeFileSync(path.join(directory, 'before.json'), JSON.stringify(before, null, 2));
    let started = false;
    let error;
    try {
        const recording = await request('/recording/start', { output_dir: directory, fps: 20, width: 1280, height: 720, audio: true, max_duration_seconds: 180 });
        started = true;
        if (recording.audio_source && recording.audio_source !== 'engine_mix') throw new Error('Unexpected audio source ' + recording.audio_source);
        await delay(primary ? 400 : 700);
        const handCard = (await state()).player.hand.find(item => item.id === card.modelId);
        await action('hover_card', { card_index: handCard.index });
        await delay(primary ? 800 : 1400);
        await saveImage('/screenshot', path.join(directory, 'card.png'));
        await action('unhover_card');
        if (!primary && card.id === 'JK314') {
            for (let count = 0; count < 3; count++) await playModel('JUNNA_JK122B', card.id);
            const source = (await state()).player.hand.find(item => item.id === card.modelId);
            await action('hover_card', { card_index: source.index });
            await delay(1200);
            await saveImage('/screenshot', path.join(directory, 'growth.png'));
            await action('unhover_card');
        }
        const selectionCount = card.id === 'JK84' ? upgraded ? 5 : 3 : card.id === 'JK206B' && upgraded ? 2 : 1;
        await playModel(card.id === 'U_CARD_012' ? 'JUNNA_JA38' : card.modelId, card.id, true, { selectionCount });
        if (['JG03', 'JG27', 'JG36', 'JK200B', 'JK131', 'JT02', 'JH19'].includes(card.id)) await playModel('JUNNA_STRIKE', card.id, false);
        if (['JA17', 'JH24', 'U_CARD_013'].includes(card.id)) await endTurn(card.id);
        if (card.id === 'U_CARD_016') {
            for (const id of ['JK49B', 'JA38', 'JF13']) await playModel('JUNNA_' + id, card.id);
        }
        if (['U_CARD_033', 'U_CARD_047'].includes(card.id)) await playModel('JUNNA_STRIKE', card.id);
        if (card.id === 'JK11') await playModel('JUNNA_JK11B', card.id, false);
        if (card.id === 'JK11B') await playModel('JUNNA_JK11', card.id, false);
        if (['U_CARD_008', 'JK127', 'JS19'].includes(card.id)) {
            await playModel('JUNNA_JK275', card.id);
            const rewardState = await settle(card.id);
            if (card.id === 'JK127' || card.id === 'JS19') {
                const copyReward = rewardState.rewards?.items?.find(item => item.type === 'knowledgecopyreward');
                if (!copyReward) throw new Error('Missing knowledge copy reward');
                await action('claim_reward', { index: copyReward.index });
                await settle(card.id);
            }
        } else if (['JK37C', 'U_CARD_181'].includes(card.id)) {
            await playModel('JUNNA_STRIKE', card.id);
            await playModel('JUNNA_STRIKE', card.id);
            if (card.id === 'JK37C') await endTurn(card.id);
        } else if (card.id === 'JA105') {
            await delay(1200);
            await saveImage('/screenshot', path.join(directory, 'installed.png'));
            await playModel('JUNNA_FULL_DRAW', card.id);
            await delay(1200);
            await saveImage('/screenshot', path.join(directory, 'new-arrow.png'));
            if (!primary) await playModel('JUNNA_JA71', card.id);
        } else if (card.id === 'JA205') {
            await playModel('JUNNA_JA38', card.id);
            await playModel('JUNNA_JA71', card.id);
        } else if (card.type === '能力' && card.id !== 'U_CARD_047' && card.id !== 'U_CARD_016') {
            const instant = ['JA22', 'JK37C', 'JZ05', 'JA118', 'JK235', 'JK254', 'JK260'].includes(card.id);
            const rounds = primary && instant ? 0 : card.id === 'U_CARD_010' ? 3 : ['A', 'JA96', 'JA144'].includes(card.id) ? 2 : 1;
            for (let turn = 0; turn < rounds; turn++) await endTurn(card.id);
            const current = await state();
            const triggerId = card.id === 'A' ? 'JUNNA_B' : card.id === 'JK32' ? 'JUNNA_JK32B'
                : card.id === 'JZ05' ? 'JUNNA_STRIKE' : 'JUNNA_JA38';
            const trigger = current.player?.hand?.find(item => item.can_play && item.id !== 'JUNNA_JA71'
                && (['JK235', 'JK260', 'JK254'].includes(card.id) ? item.description.includes('知识') : item.id === triggerId));
            if (trigger && (!primary || instant)) await playModel(trigger.id, card.id);
            if (!primary && card.id === 'JK254') await playModel('JUNNA_JK11B', card.id, false);
            if (!primary && card.id === 'JA144') await playModel('JUNNA_JA119', card.id, false);
        }
        if ((await state()).battle) {
            let needsCompletion = card.type === '飞矢' || ['JA84', 'JA87', 'JA88', 'JK84', 'JG37', 'JT02', 'JX01', 'JH22', 'JA104', 'JA119', 'JA108', 'JK37B', 'JK84B', 'JA22', 'JA35', 'JK67', 'JA96', 'JA118', 'JK32', 'JA144', 'JZ05', 'U_CARD_023', 'U_CARD_187'].includes(card.id);
            if (upgraded && ['JK84', 'U_CARD_023'].includes(card.id))
                fs.copyFileSync(path.join(output, 'results/readiness.json'), path.join(directory, 'deployed.json'));
            if (primary && card.type === '能力' && card.id !== 'JA22') needsCompletion = false;
            if (upgraded && card.id === 'U_CARD_023') {
                needsCompletion = false;
                for (let turn = 0; turn < 3; turn++) await endTurn(card.id);
            }
            if (needsCompletion) {
                if (!await playModel('JUNNA_JA71', card.id, false)) {
                    await endTurn(card.id);
                    await playModel('JUNNA_JA71', card.id, false);
                }
            }
            if (card.id === 'JA75') await playModel('JUNNA_STRIKE', card.id, false);
            if (['JA81', 'JA76', 'JA82', 'U_CARD_005', 'U_CARD_030'].includes(card.id)) {
                await endTurn(card.id);
                await endTurn(card.id);
                if (card.id === 'U_CARD_005') await endTurn(card.id);
            }
        }
        await delay(primary ? 800 : 1400);
        fs.writeFileSync(path.join(directory, 'after.json'), JSON.stringify(await state(), null, 2));
        fs.copyFileSync(path.join(output, 'results/readiness.json'), path.join(directory, 'readiness.json'));
        await saveImage('/screenshot', path.join(directory, 'settled.png'));
    } catch (failure) { error = failure; }
    finally {
        if (started) {
            await request('/recording/stop', {});
            let result;
            const deadline = Date.now() + 60000;
            do { await delay(250); result = await request('/recording'); } while (['recording', 'stopping'].includes(result.status) && Date.now() < deadline);
            if (result.status !== 'completed') error = new Error(`Recording failed ${JSON.stringify(result)}`);
            const record = { id: card.id, name: card.name, revision: catalog.revision, beta: true, upgraded, presentation: primary ? '主要结算' : '结算与触发演示', ...result, ...(error ? { status: 'failed', action_error: error.message } : {}) };
            fs.writeFileSync(receipt, JSON.stringify(record, null, 2));
            if (!error) await saveImage('/recording/frames?frame_count=8&columns=4&frame_width=320', path.join(directory, 'frames.png'));
        }
    }
    if (error) throw error;
    console.log(`Recorded ${card.id} ${card.name}`);
}

async function recordOpening() {
    await ensureCombat();
    await command('junna_probe speed 1');
    const directory = path.join(output, 'supplementary', 'JK37-opening');
    fs.mkdirSync(directory, { recursive: true });
    const fixtureId = 'U_CARD_008';
    await command('junna_full_audit prepare ' + fixtureId);
    await until(current => current.battle?.is_play_phase && current.player?.hand?.some(card => card.id === 'JUNNA_JK275' && card.can_play));
    await command('card JUNNA_JK37 Deck');
    await playModel('JUNNA_JK275', fixtureId);
    await until(current => current.state_type === 'rewards');
    await action('proceed');
    const map = await until(current => current.state_type === 'map' &&
        current.map?.next_options?.some(node => node.type === 'Monster'));
    const next = map.map.next_options.find(node => node.type === 'Monster');
    const start = await request('/recording/start', { output_dir: directory, fps: 20, width: 1280, height: 720,
        audio: true, max_duration_seconds: 100 });
    let failure;
    try {
        if (start.audio_source && start.audio_source !== 'engine_mix') throw new Error('Opening audio source is unexpected');
        await delay(650);
        await action('choose_map_node', { index: next.index });
        await until(current => current.battle?.is_play_phase, 45);
        const opening = await settle(fixtureId, 60);
        const readinessFile = path.join(output, 'results/readiness.json');
        const deployment = JSON.parse(fs.readFileSync(readinessFile, 'utf8')).deployed.find(arrow => arrow.id === 'JK37');
        const expected = catalog.cards.find(card => card.id === 'JK37').values.find(value => value.name === 'Turns').base;
        if (!opening.battle?.is_play_phase || deployment?.Remaining !== expected)
            throw new Error(`Natural combat opening did not deploy JK37 with ${expected} turns`);
        await saveImage('/screenshot', path.join(directory, 'opening.png'));
        let current = opening;
        for (let turn = 0; turn < expected && current.battle; turn++)
            current = await endTurn(fixtureId);
        const finalArrows = JSON.parse(fs.readFileSync(readinessFile, 'utf8')).deployed;
        if (finalArrows.some(arrow => arrow.id === 'JK37')) throw new Error('JK37 did not finish after natural turns');
        fs.writeFileSync(path.join(directory, 'evidence.json'), JSON.stringify({
            revision: catalog.revision, id: 'JK37', round: opening.battle.round, energy: opening.player.energy,
            initialTurns: deployment.Remaining, finishedByNaturalTurns: true, opening, settled: current,
        }, null, 2));
        await delay(900);
    } catch (error) { failure = error; }
    finally {
        await request('/recording/stop', {});
        let end;
        const deadline = Date.now() + 60000;
        do { await delay(250); end = await request('/recording'); }
        while (['recording', 'stopping'].includes(end.status) && Date.now() < deadline);
        fs.writeFileSync(path.join(directory, 'recording.json'), JSON.stringify({ ...end, id: 'JK37',
            revision: catalog.revision, ...(failure ? { status: 'failed', action_error: failure.message } : {}) }, null, 2));
        if (end.status !== 'completed') throw new Error(`Opening recording failed: ${end.error}`);
    }
    if (failure) throw failure;
    console.log('Recorded natural opening and completion for JK37');
}

function recordedVideo(id, upgraded) {
    const receipt = path.join(output, upgraded ? 'videos-upgrade' : 'videos', id, 'recording.json');
    if (!fs.existsSync(receipt)) return '';
    const video = JSON.parse(fs.readFileSync(receipt, 'utf8'));
    if (video.status !== 'completed' || video.id !== id || video.revision !== catalog.revision ||
        Boolean(video.upgraded) !== upgraded || typeof video.output_path !== 'string') return '';
    const file = path.resolve(output, video.output_path);
    return fs.existsSync(file) ? path.relative(output, file).replaceAll('\\', '/') : '';
}

function report() {
    const run = readRun();
    const summary = summarize(run);
    plan();
    const escape = value => String(value).replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]);
    const testPlan = JSON.parse(fs.readFileSync(path.join(output, 'plan.json')));
    const rows = [];
    const details = [];
    const manifest = [];
    for (const card of catalog.cards.filter(card => !requested.length || requested.includes(card.id))) {
        const results = run.results.filter(result => result.id === card.id);
        const assertions = results.flatMap(result => result.checks);
        const passed = assertions.filter(check => check.pass).length;
        const tested = results.length === 8 && assertions.every(check => check.pass);
        const videoPath = recordedVideo(card.id, false);
        const upgradeVideo = recordedVideo(card.id, true);
        const playerVideo = videoPath || upgradeVideo;
        const version = videoPath ? '基础版' : upgradeVideo ? '升级版' : '';
        const status = !results.length ? '本轮未测' : `${passed}/${assertions.length} ${tested ? '脚本通过' : '待复核'}`;
        rows.push(`<tr data-name="${escape(card.id + card.name)}"><td>${escape(card.id)}</td><td><a data-video="${escape(playerVideo)}" data-version="${version}" data-id="${card.id}" href="${escape(playerVideo || '#')}">${escape(card.name)}</a></td><td>${status}</td><td>${version || '待录制'}</td></tr>`);
        const entries = testPlan.cards.find(entry => entry.id === card.id).entries;
        const opening = 'supplementary/JK37-opening/main.mp4';
        const extra = card.id === 'JK37' && fs.existsSync(path.join(output, opening))
            ? '<p><a href="' + opening + '">开局自动布置与自然完成录像</a></p>' : '';
        const upgradeLink = videoPath && upgradeVideo ? `<p><a href="${escape(upgradeVideo)}">升级版录像</a></p>` : '';
        details.push(`<article id="details-${card.id}" hidden><h3>基础版</h3><p>${escape(card.text)}</p><h3>升级版</h3><p>${escape(card.upgradedText)}</p>${upgradeLink}${extra}<details><summary>测试条目</summary><ul>${entries.map(([label, text]) => `<li><strong>${escape(label)}</strong> ${escape(text)}</li>`).join('')}</ul></details><details><summary>测试结果</summary><ul>${results.map(result => `<li><a href="results/${run.runId}/${result.file}">${result.upgraded ? '升级' : '基础'} ${result.variant} ${result.checks.filter(check => check.pass).length}/${result.checks.length}</a></li>`).join('')}</ul></details></article>`);
        manifest.push({ id: card.id, name: card.name, scenarios: results.length, passed, assertions: assertions.length,
            tested, coverage: 'automated-combat-and-hook-checks', video: playerVideo, upgradeVideo,
            recorded: Boolean(playerVideo), version: version && 'Beta ' + version });
    }
    const html = `<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>纯那 Beta 逐牌测试</title><style>
body{margin:0;background:#f4f5f6;color:#172026;font:14px Inter,"Microsoft YaHei",sans-serif}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:24px;margin:0 0 10px}p{line-height:1.65;white-space:pre-line}a{color:#126650}input{box-sizing:border-box;width:100%;padding:10px;border:1px solid #bac4c8;border-radius:4px;font:inherit;background:white}.layout{display:grid;grid-template-columns:minmax(430px,.85fr) minmax(560px,1.5fr);gap:26px}aside{height:82vh;overflow:auto}table{width:100%;border-collapse:collapse;margin-top:12px;background:white}th,td{text-align:left;padding:10px 8px;border-bottom:1px solid #e1e6e8;font-size:13px}th{position:sticky;top:0;background:#e9eeed}td:first-child{color:#647278;font-family:monospace}td:nth-child(3){white-space:nowrap}video{width:100%;max-height:61vh;background:#10191c}h2{font-size:19px;margin:0 0 12px}h3{font-size:14px;margin-bottom:0}details{margin:16px 0}summary{cursor:pointer;font-weight:600}li{margin:8px 0;line-height:1.6}.stats{color:#53646b;margin-bottom:22px}@media(max-width:1050px){.layout{display:flex;flex-direction:column-reverse}aside{height:55vh}main{padding:18px}}
</style><main><h1>纯那 Beta 逐牌测试</h1><p class="stats">工作台 r${catalog.revision} · 本轮 ${summary.scopeCards} 张牌 · ${summary.completedScenarios} 个场景 · ${summary.assertions} 项断言</p><div class="layout"><aside><input id="search" aria-label="搜索卡名或编号" placeholder="搜索卡名或编号"><table><thead><tr><th>编号</th><th>卡名</th><th>基础与升级检查</th><th>Beta录像</th></tr></thead><tbody>${rows.join('')}</tbody></table></aside><section><h2 id="current">选择卡牌</h2><video id="player" controls preload="none" hidden></video>${details.join('')}</section></div></main><script>
const player=document.getElementById('player');for(const link of document.querySelectorAll('[data-video]'))link.addEventListener('click',event=>{event.preventDefault();const {video,version,id}=link.dataset;document.getElementById('current').textContent=link.textContent+(version?' · Beta'+version:'');for(const article of document.querySelectorAll('article'))article.hidden=article.id!=='details-'+id;player.hidden=!video;if(video){player.src=video;player.play();}else{player.pause();player.removeAttribute('src');}});document.getElementById('search').addEventListener('input',event=>{const query=event.target.value.trim().toLowerCase();for(const row of document.querySelectorAll('tr[data-name]'))row.hidden=!row.dataset.name.toLowerCase().includes(query);});
</script></html>`;
    fs.writeFileSync(path.join(output, 'index.html'), html);
    fs.writeFileSync(path.join(output, 'manifest.json'), JSON.stringify({ revision: catalog.revision, cards: manifest }, null, 2));
    console.log(`Indexed recordings for ${manifest.filter(card => card.recorded).length}/${manifest.length} cards`);
}

async function main() {
    if (requested.some(id => !catalog.cards.some(card => card.id === id)))
        throw new Error('Unknown card ID in requested subset');
    if (mode === 'plan') plan();
    else if (mode === 'check') await check();
    else if (mode === 'combos') await check(true);
    else if (mode === 'record') await record();
    else if (mode === 'record-primary') await record(true);
    else if (mode === 'record-retakes' && requested.length) await record(true, true);
    else if (mode === 'record-upgrade' && requested.length) await record(true, false, true);
    else if (mode === 'record-opening') await recordOpening();
    else if (mode === 'setup') await ensureCombat();
    else if (mode === 'summary') summarize();
    else if (mode === 'report') report();
    else throw new Error(`Unknown mode ${mode}`);
}
main().catch(error => { console.error(error.stack); process.exitCode = 1; });
