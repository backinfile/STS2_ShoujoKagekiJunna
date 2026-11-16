'use strict';

const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const output = path.join(root, 'out/tmp/full-card-tests');
const runtimeCatalog = JSON.parse(fs.readFileSync(path.join(root, 'data/card-pool.json'), 'utf8'));
const eventCards = new Set(['JK359', 'JK360', 'JK361', 'JK362', 'JK363', 'JK364', 'JK365', 'JK366', 'JK367']);
const catalog = { ...runtimeCatalog, cards: runtimeCatalog.cards.filter(card => !eventCards.has(card.id)) };
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

function summarize() {
    const directory = path.join(output, 'results');
    const results = fs.existsSync(directory) ? fs.readdirSync(directory)
        .filter(file => file.endsWith('-result.json') && !file.endsWith('-video-result.json'))
        .map(file => JSON.parse(fs.readFileSync(path.join(directory, file), 'utf8'))) : [];
    const failures = results.flatMap(result => result.checks.filter(check => !check.pass)
        .map(check => ({ id: result.id, upgraded: result.upgraded, variant: result.variant, ...check })));
    const summary = {
        revision: catalog.revision,
        cards: catalog.cards.length,
        completedScenarios: results.length,
        assertions: results.reduce((count, result) => count + result.checks.length, 0),
        failures,
    };
    fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(summary, null, 2));
    console.log(JSON.stringify({ ...summary, failures: failures.slice(0, 30), failureCount: failures.length }));
    return summary;
}

function plan() {
    const lines = [
        `# r${catalog.revision} 逐牌测试条目`, '',
        `本次包含 ${catalog.cards.length} 张牌。以下为原逐牌检查清单；通用条目须按实际牌效补齐操作、每步预期和执行证据。`,
        '覆盖更正：旧钩子级检查与真实跨回合验收分别记录。断言数量不等于完整覆盖，见[覆盖审查](card-test-coverage-audit.md)。', '',
        '每个测试结果保存实际值、预期值与通过状态。视频在准备完成后开始录制，通过游戏的悬停、出牌、选牌和回合结束操作展示结算。', '',
    ];
    const rows = [];
    for (const card of catalog.cards) {
        const arrow = card.type === '飞矢';
        const entries = [
            ['显示', '核对卡名、基础与升级费用、类型、稀有度、牌效数值、卡图、关键词、提示和升级预览。'],
            ['基础结算', card.text.replaceAll('\n', ' ')],
            ['升级结算', card.upgradedText.replaceAll('\n', ' ')],
            ['费用边界', card.costsX ? '分别使用0、1、3点能量，核对X值、支付次数和实际生成数。' : `能量恰好足够与不足1点。禁止打出时保持牌堆、能量、目标生命和布置状态。`],
            ['资源边界', '清空抽牌堆与可选对象后结算，核对无效目标、空集合、手牌上限、消耗或弃牌去向。'],
            ['改编配合', '将该牌作为零费、5回合飞矢打出，核对等待期间的伤害和格挡，以及完成后的牌效、能力安装和闪耀扣减。'],
        ];
        if (arrow) entries.push(['飞矢', '核对布置时费用与倒计时、等待阶段无提前结算、自然倒计时、加速完成、重复加速与完成后的唯一去向。与娓娓道来、加速、弦声齐鸣和星群配合。']);
        if (card.damageCapable) entries.push(['伤害配合', '加入3点伤害加成及1次额外伤害，对比基础和升级结算。核对有无格挡、单体或全体目标、附带收益执行次数、目标死亡后的后续结算。']);
        if (card.knowledge) entries.push(['知识配合', '核对直接关联牌与同名实际牌，抽牌堆、手牌、弃牌堆和布置区的对应实例。与关联阅读、融会贯通、理想的国语教科书和知识的力量配合，核对每回合触发与费用恢复。']);
        if (card.type === '能力') entries.push(['持续触发', '核对安装时点、当回合和次回合、同名叠加、合法与不满足条件的触发、每回合限制、战斗结束清除及来源版本快照。']);
        if (/闪耀\d/.test(card.text)) entries.push(['闪耀', '核对基础和升级的初始预算，布置时保留预算、完整结算扣减一次、最后一次结算完成后耗尽、临时复制与永久牌对应、耗尽回调去重。']);
        if (card.id === 'U_CARD_008') entries.push(
            ['胜利直接入牌组', '合法打出能力后牌组不立即变化。再正常击败全部敌人；领取任何掉落前，永久牌组已增加1张基础故事，目的地Deck，闪耀3/3。不创建故事掉落，普通选牌奖励仍正常出现。'],
            ['版本与叠加', '基础与升级分别获胜各发1张基础故事；两次合法安装后获胜发2张独立故事。下一场不安装直接获胜应发0张，不重复发上场收益。'],
            ['塞牌动画', '实际故事先展示再飞入右上角永久牌组，飞行完成并发送计数完成通知后出现普通掉落。基础/升级各1张、双层2张、六层6张散开展示；未安装不展示、不飞牌。'],
            ['原生胜利证据', '入牌组/掉落初轮64项与动画修复后五场100项分别保留。详见[故事动画验收](story-deck-animation.md)及[直接入牌组验收](story-direct-deck-tests.md)。测试不直接调用胜利钩子。'],
        );
        if (card.id === 'U_CARD_009') entries.push(['多标签检索', '一张三标签只抽1张。纯飞矢＋飞矢知识＋纯闪耀抽3张。两张同名双标签＋闪耀抽3张。三张三标签抽3张。只有双标签抽2张。抽牌触发改变待选对象后重新配对，保留其他牌相对顺序，使用同步随机数。']);
        if (card.id === 'U_CARD_010') entries.push(
            ['真实轮换', '基础扣2、升级扣1，安装不立即检索。合法结束回合并经过敌方行动，连续6次开始回合的候选必须依次为知识、飞矢、闪耀、知识、飞矢、闪耀；每次选择原牌入手，保留升级与闪耀余量，检索不触发抽牌且其他牌相对顺序不变。'],
            ['独立进度', '首次知识检索后合法打出第二张能力。随后3回合分别为第一张飞矢/第二张知识、第一张闪耀/第二张飞矢、第一张知识/第二张闪耀。'],
            ['轮换边界', '知识候选缺失仍按空知识→飞矢→闪耀推进；10张保留手牌使知识回合跳过，合法打出6张腾位后，下回合取飞矢、再下回合取闪耀。'],
            ['执行证据', '逐步准备、预期、原生操作与剩余分支见[舞台少女心得验收](stage-girl-notes-real-turn-tests.md)。旧直接调用回合钩子的通过结果单独保留。'],
        );
        if (card.id === 'JK37C') entries.push(
            ['实际零费与随机对象', '安装不立即加速已有飞矢；1费防御不触发，0费戒备只随机使一支实际候选减少1。同回合连续3次各只减1，不保证轮流选择。'],
            ['完成与配合', '上弦配合引弦2→1→0后同一原牌回手；加速与渴望各减1使新引弦立即完成并耗尽；两层渴望各触发1，伤害仅完成一次。'],
            ['降费与跨回合', '天候使掌舵3→0后合法打出也触发；真实次回合分开核对自然倒计时与新0费牌触发。七组118项及录像见[追加录像](desire-arrow-video-tests.md)。'],
        );
        if (card.id === 'JA105') entries.push(['加速触发时点', '安装前已有3回合满弦射击，安装后保持原计数和关键词。之后打出的满弦射击立即推进至2回合。两层加速使2回合引弦立即完成，改编飞矢同样按每次打出触发。']);
        if (card.id === 'JA108') entries.push(['战斗内备注', '显示（打出X张），读取当前抽牌堆的飞矢数量。核对基础与升级、空堆、飞矢入堆与离堆后的更新，以及战斗外隐藏。']);
        if (card.id === 'JK314') entries.push(['耗尽渴望', '直接统计耗尽牌堆中的渴望实际数量，跨获取批次并超过3张。重复回调保持数量，卡牌额外次数不产生伤害强化标记，外部额外次数强化正常叠加。']);
        entries.push(['录像', 'Beta基础版，简短展示牌面与本牌主要结算，保留对应MP4。']);
        lines.push(`## ${card.id} ${card.name}`, '', ...entries.map(([name, text]) => `- ${name}。${text}`), '');
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
        current = await until(current => current.state_type !== 'menu', 30);
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
    const targets = requested.length ? requested : [null];
    for (const id of targets) {
        const completion = path.join(output, 'results', `completed${id ? '-' + id : ''}.json`);
        if (fs.existsSync(completion)) fs.unlinkSync(completion);
        await command('junna_full_audit ' + (combosOnly ? 'combos' : 'check') + (id ? ' ' + id : ''));
        const deadline = Date.now() + 3600000;
        let previous = '';
        let lastProgress = Date.now();
        while (!fs.existsSync(completion)) {
            await delay(1000);
            const files = fs.readdirSync(path.join(output, 'results')).filter(file => file.endsWith('-result.json'));
            const latest = files.map(file => fs.statSync(path.join(output, 'results', file)).mtimeMs).sort((a, b) => b - a)[0];
            const progress = `${files.length}/${latest}`;
            if (progress !== previous) { lastProgress = Date.now(); previous = progress; }
            if (Date.now() > deadline || Date.now() - lastProgress > 90000) {
                summarize();
                throw new Error('Per-card checks stopped making progress');
            }
        }
    }
    const summary = summarize();
    const failures = summary.failures.filter(failure => !requested.length || requested.includes(failure.id));
    if (failures.length) throw new Error(`${failures.length} per-card assertions failed`);
}

async function settle(cardId, seconds = 30) {
    const deadline = Date.now() + seconds * 1000;
    let selected = false;
    let lastKey = '';
    let stable = 0;
    while (Date.now() < deadline) {
        const current = await state();
        const selection = current.hand_select || current.card_select;
        if (selection) {
            const key = current.state_type + selection.prompt + selection.screen_type;
            if (key !== lastKey) { selected = false; lastKey = key; }
            stable = 0;
            await delay(350);
            if ((selected || selection.preview_showing) && selection.can_confirm) {
                await action(current.hand_select ? 'combat_confirm_selection' : 'confirm_selection');
                selected = false;
                continue;
            }
            const cards = selection.cards || [];
            const preference = ['JA104', 'JA119', 'JA144'].includes(cardId) ? 'JUNNA_ARROW_TARGET_ENEMY'
                : cardId === 'JA65' ? 'JUNNA_JA15' : 'JUNNA_STRIKE';
            const candidate = cards.find(card => card.id === preference) || cards[0];
            if (candidate) {
                await action(current.hand_select ? 'combat_select_card' : 'select_card',
                    current.hand_select ? { card_index: candidate.index } : { index: candidate.index });
                selected = true;
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

async function playModel(modelId, cardId, required = true) {
    const current = await state();
    const card = current.player?.hand?.find(card => card.id === modelId && card.can_play);
    if (!card) {
        if (required) throw new Error(`Playable ${modelId} missing`);
        return false;
    }
    const target = card.target_type === 'AnyEnemy' ? current.battle.enemies.find(enemy => enemy.hp > 0)?.entity_id : undefined;
    await action('play_card', { card_index: card.index, ...(target ? { target } : {}) });
    await delay(300);
    await settle(cardId, 45);
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

async function record(primary = false) {
    await ensureCombat();
    await command('junna_probe speed 1');
    const videos = path.join(output, 'videos');
    fs.mkdirSync(videos, { recursive: true });
    const selectedCards = catalog.cards.filter(card => !requested.length || requested.includes(card.id));
    for (const card of selectedCards) {
        const directory = path.join(videos, card.id);
        fs.mkdirSync(directory, { recursive: true });
        const receipt = path.join(directory, 'recording.json');
        if (fs.existsSync(receipt) && JSON.parse(fs.readFileSync(receipt)).status === 'completed') continue;
        if (!(await state()).battle) {
            await command('fight CULTISTS_NORMAL');
            await until(current => current.battle?.is_play_phase);
        }
        const preparation = path.join(output, 'results', `${card.id}-base-normal-prepared.json`);
        const previous = fs.existsSync(preparation) ? fs.statSync(preparation).mtimeMs : 0;
        await command(`junna_full_audit prepare ${card.id}`);
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
            await playModel(card.modelId, card.id);
            if (['JG03', 'JG27', 'JG36', 'JK200B', 'JK131', 'JT02', 'JH19'].includes(card.id)) await playModel('JUNNA_STRIKE', card.id, false);
            if (['JA17', 'JH24'].includes(card.id)) await endTurn(card.id);
            if (card.id === 'JK11') await playModel('JUNNA_JK11B', card.id, false);
            if (card.id === 'JK11B') await playModel('JUNNA_JK11', card.id, false);
            if (['U_CARD_008', 'JK127', 'JS19'].includes(card.id)) {
                await playModel('JUNNA_JK275', card.id);
                const rewardState = await settle(card.id);
                const copyReward = rewardState.rewards?.items?.find(item => /复制|知识/.test(item.description || ''));
                if (copyReward) { await action('claim_reward', { index: copyReward.index }); await settle(card.id); }
            } else if (card.id === 'JA105') {
                await delay(1200);
                await saveImage('/screenshot', path.join(directory, 'installed.png'));
                await playModel('JUNNA_FULL_DRAW', card.id);
                await delay(1200);
                await saveImage('/screenshot', path.join(directory, 'new-arrow.png'));
                if (!primary) await playModel('JUNNA_JA71', card.id);
            } else if (card.type === '能力') {
                const instant = ['JA22', 'JK37C', 'JZ05', 'JA118', 'JK235', 'JK254', 'JK260'].includes(card.id);
                const rounds = primary && instant ? 0 : card.id === 'U_CARD_010' && !primary ? 4 : card.id === 'JA96' ? 2 : 1;
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
                let needsCompletion = card.type === '飞矢' || ['JA84', 'JA87', 'JA88', 'JK84', 'JG37', 'JT02', 'JX01', 'JH22', 'JA104', 'JA119', 'JA108', 'JK37B', 'JK84B', 'JA22', 'JA35', 'JK37C', 'JK67', 'JA96', 'JA118', 'JK32', 'JA144', 'JZ05'].includes(card.id);
                if (primary && card.type === '能力' && card.id !== 'JA22') needsCompletion = false;
                if (needsCompletion) {
                    if (!await playModel('JUNNA_JA71', card.id, false)) {
                        await endTurn(card.id);
                        await playModel('JUNNA_JA71', card.id, false);
                    }
                }
                if (card.id === 'JA75') await playModel('JUNNA_STRIKE', card.id, false);
                if (!primary && ['JA81', 'JA76', 'JA82', 'U_CARD_005'].includes(card.id)) {
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
                const record = { id: card.id, name: card.name, beta: true, presentation: primary ? '主要结算' : '结算与触发演示', ...result, ...(error ? { status: 'failed', action_error: error.message } : {}) };
                fs.writeFileSync(receipt, JSON.stringify(record, null, 2));
                if (!error) await saveImage('/recording/frames?frame_count=8&columns=4&frame_width=320', path.join(directory, 'frames.png'));
            }
        }
        if (error) throw error;
        console.log(`Recorded ${card.id} ${card.name}`);
    }
}

function report() {
    const summary = summarize();
    plan();
    const escape = value => String(value).replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]);
    const testPlan = JSON.parse(fs.readFileSync(path.join(output, 'plan.json')));
    const rows = [];
    const details = [];
    const manifest = [];
    for (const card of catalog.cards) {
        const directory = path.join(output, 'videos', card.id);
        const receipt = path.join(directory, 'recording.json');
        const video = fs.existsSync(receipt) ? JSON.parse(fs.readFileSync(receipt)) : null;
        const results = fs.readdirSync(path.join(output, 'results'))
            .filter(file => file.startsWith(card.id + '-') && file.endsWith('-result.json') && !file.endsWith('-video-result.json'))
            .map(file => ({ file, ...JSON.parse(fs.readFileSync(path.join(output, 'results', file))) }));
        const assertions = results.flatMap(result => result.checks);
        const passed = assertions.filter(check => check.pass).length;
        const tested = results.length === 8 && assertions.every(check => check.pass);
        const complete = video?.status === 'completed';
        const videoPath = complete ? path.relative(output, video.output_path).replaceAll('\\', '/') : '';
        const status = tested ? `${passed}/${assertions.length} 脚本通过` : `${passed}/${assertions.length} 待复核`;
        rows.push(`<tr data-name="${escape(card.id + card.name)}"><td>${escape(card.id)}</td><td><a data-video="${escape(videoPath)}" data-id="${card.id}" href="${escape(videoPath || '#')}">${escape(card.name)}</a></td><td>${status}</td><td>${complete ? '已录制' : '待录制'}</td></tr>`);
        const entries = testPlan.cards.find(entry => entry.id === card.id).entries;
        details.push(`<article id="details-${card.id}" hidden><h3>基础版</h3><p>${escape(card.text)}</p><h3>升级版</h3><p>${escape(card.upgradedText)}</p><details><summary>测试条目</summary><ul>${entries.map(([label, text]) => `<li><strong>${escape(label)}</strong> ${escape(text)}</li>`).join('')}</ul></details><details><summary>测试结果</summary><ul>${results.map(result => `<li><a href="results/${result.file}">${result.upgraded ? '升级' : '基础'} ${result.variant} ${result.checks.filter(check => check.pass).length}/${result.checks.length}</a></li>`).join('')}</ul></details></article>`);
        manifest.push({ id: card.id, name: card.name, scenarios: results.length, passed, assertions: assertions.length, tested, coverage: 'script-checks-only', video: videoPath, recorded: complete, version: 'Beta 基础版' });
    }
    const html = `<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>纯那 Beta 逐牌测试</title><style>
body{margin:0;background:#f4f5f6;color:#172026;font:14px Inter,"Microsoft YaHei",sans-serif}main{max-width:1500px;margin:auto;padding:28px}h1{font-size:24px;margin:0 0 10px}p{line-height:1.65;white-space:pre-line}a{color:#126650}input{box-sizing:border-box;width:100%;padding:10px;border:1px solid #bac4c8;border-radius:4px;font:inherit;background:white}.layout{display:grid;grid-template-columns:minmax(430px,.85fr) minmax(560px,1.5fr);gap:26px}aside{height:82vh;overflow:auto}table{width:100%;border-collapse:collapse;margin-top:12px;background:white}th,td{text-align:left;padding:10px 8px;border-bottom:1px solid #e1e6e8;font-size:13px}th{position:sticky;top:0;background:#e9eeed}td:first-child{color:#647278;font-family:monospace}td:nth-child(3){white-space:nowrap}video{width:100%;max-height:61vh;background:#10191c}h2{font-size:19px;margin:0 0 12px}h3{font-size:14px;margin-bottom:0}details{margin:16px 0}summary{cursor:pointer;font-weight:600}li{margin:8px 0;line-height:1.6}.stats{color:#53646b;margin-bottom:22px}@media(max-width:1050px){.layout{display:flex;flex-direction:column-reverse}aside{height:55vh}main{padding:18px}}
</style><main><h1>纯那 Beta 逐牌测试</h1><p class="stats">工作台 r${catalog.revision} · ${catalog.cards.length}张牌 · ${summary.completedScenarios}个场景 · ${summary.assertions}项断言</p><p>此处统计脚本断言。真实回合及剩余场景见专项验收记录，不能由断言总数推定完整覆盖。</p><div class="layout"><aside><input id="search" aria-label="搜索卡名或编号" placeholder="搜索卡名或编号"><table><thead><tr><th>编号</th><th>卡名</th><th>基础与升级脚本断言</th><th>Beta录像</th></tr></thead><tbody>${rows.join('')}</tbody></table></aside><section><h2 id="current">选择卡牌</h2><video id="player" controls preload="none"></video>${details.join('')}</section></div></main><script>
const player=document.getElementById('player');for(const link of document.querySelectorAll('[data-video]'))link.addEventListener('click',event=>{event.preventDefault();if(!link.dataset.video)return;player.src=link.dataset.video;document.getElementById('current').textContent=link.textContent+' · Beta基础版';for(const article of document.querySelectorAll('article'))article.hidden=article.id!=='details-'+link.dataset.id;player.play();});document.getElementById('search').addEventListener('input',event=>{const query=event.target.value.trim().toLowerCase();for(const row of document.querySelectorAll('tr[data-name]'))row.hidden=!row.dataset.name.toLowerCase().includes(query);});
</script></html>`;
    fs.writeFileSync(path.join(output, 'index.html'), html);
    fs.writeFileSync(path.join(output, 'manifest.json'), JSON.stringify({ revision: catalog.revision, cards: manifest }, null, 2));
    console.log(`Indexed ${manifest.filter(card => card.recorded).length}/${manifest.length} videos`);
}

async function main() {
    if (mode === 'plan') plan();
    else if (mode === 'check') await check();
    else if (mode === 'combos') await check(true);
    else if (mode === 'record') await record();
    else if (mode === 'record-primary') await record(true);
    else if (mode === 'setup') await ensureCombat();
    else if (mode === 'summary') summarize();
    else if (mode === 'report') report();
    else throw new Error(`Unknown mode ${mode}`);
}
main().catch(error => { console.error(error.stack); process.exitCode = 1; });
