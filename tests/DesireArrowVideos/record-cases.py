import argparse,json,time,uuid,urllib.request
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--port',required=True,type=int);p.add_argument('--output-dir',required=True,type=Path);p.add_argument('--cases',default='control,random,return,haste,double,discount,turns');a=p.parse_args();root=a.output_dir.resolve();root.mkdir(parents=True,exist_ok=True);base=f'http://127.0.0.1:{a.port}/api/v1'
def req(path,body=None):
    q=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(q,timeout=30) as response:r=json.load(response)
    if r.get('status')=='error' or r.get('error'):raise RuntimeError(r)
    return r
def act(action,**kw):return req('/singleplayer',dict(action=action,**kw))
def state():return req('/singleplayer?format=json')
def live():
    try:return json.loads((root/'live.json').read_text(encoding='utf-8-sig'))
    except (FileNotFoundError,json.JSONDecodeError):return None
def wait(predicate,timeout=55):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        s=state();v=live()
        if predicate(s,v):return s,v
        time.sleep(.12)
    (root/'timeout.json').write_text(json.dumps({'state':s,'live':v},ensure_ascii=False,indent=2),encoding='utf-8');raise TimeoutError('See timeout.json')
s=state()
if s['state_type']=='menu':act('menu_select',option='continue');wait(lambda s,v:bool(s.get('player')) and s['state_type'] not in ['menu','loading'])
s=state()
if not s.get('battle',{}).get('is_play_phase'):
    if s['state_type']=='rewards':act('proceed');wait(lambda s,v:s['state_type']=='map')
    act('run_command',command='fight CULTISTS_NORMAL');wait(lambda s,v:s.get('battle',{}).get('is_play_phase'))
settings=req('/settings');assert settings['silent_test'] and settings['minimized_startup_guard']
for mode in a.cases.split(','):
    rows=[];steps=[];case_id=uuid.uuid4().hex
    def check(name,actual,expected):
        rows.append(dict(name=name,actual=actual,expected=expected,passed=actual==expected))
        if actual!=expected:raise AssertionError(rows[-1])
    def card(v,role):return next(c for c in v['cards'] if c['role']==role)
    def play(role,count,powers=None):
        s=state();v=live();before=v;offset=len(v['events']);index=next(i for i,c in enumerate(v['hand']) if c['role']==role);cost=v['hand'][index]['cost']
        target={'target':next(e['entity_id'] for e in s['battle']['enemies'] if e['hp']>0)} if s['player']['hand'][index]['target_type']=='AnyEnemy' else {}
        act('hover_card',card_index=index,**target);time.sleep(.6);act('unhover_card');act('play_card',card_index=index,**target)
        s,v=wait(lambda s,v:v and v['caseId']==case_id and not any(c['role']==role for c in v['hand']) and len(v['events'])>=offset+count and all(e['done'] for e in v['events'][offset:]) and s.get('battle',{}).get('is_play_phase'))
        time.sleep(.65);v=live();events=v['events'][offset:]
        check(role+' actual payment',before['energy']-v['energy'],cost)
        check(role+' acceleration count',len(events),count)
        if powers is not None:check(role+' source powers',sorted(e['power'] for e in events),sorted(powers))
        for i,e in enumerate(events):
            check(role+f' event {i} amount',e['amount'],1)
            check(role+f' event {i} actual countdown decrease',e['before']-e['after'],min(1,e['before']))
            check(role+f' event {i} actual eligible arrow',e['role'] in e['candidates'],True)
            if e['power']=='JK37C':check(role+f' event {i} actual paid zero',e['paidEnergy'],0)
        steps.append({'action':'play','role':role,'before':before,'after':v,'newEvents':events});return before,v
    act('run_command',command=f'junna_desire_video prepare {mode} {case_id}')
    s,v=wait(lambda s,v:v and v['caseId']==case_id);time.sleep(.8)
    req('/recording/start',dict(output_dir=str(root/mode),fps=15,width=1280,height=720,audio=True,max_duration_seconds=120))
    try:
        time.sleep(.4)
        if mode=='control':
            b,v=play('subject',0,[]);check('installation does not accelerate existing arrow',card(v,'arrow_3')['remaining'],3)
            b,v=play('paid',0,[]);check('one energy leaves countdown unchanged',card(v,'arrow_3')['remaining'],3)
            b,v=play('zero_0',1,['JK37C']);check('zero energy advances one turn',card(v,'arrow_3')['remaining'],2)
        elif mode=='random':
            for i in range(3):
                b,v=play('zero_'+str(i),1,['JK37C']);roles=['arrow_3','arrow_9','arrow_2']
                diffs=[card(b,r)['remaining']-card(v,r)['remaining'] for r in roles]
                check('random trigger '+str(i)+' changes exactly one arrow',sorted(diffs),[0,0,1])
        elif mode=='return':
            b,v=play('zero_0',1,['JK37C']);check('first acceleration to one',card(v,'arrow_2')['remaining'],1)
            hp=sum(e['hp'] for e in v['enemies']);b,v=play('zero_1',1,['JK37C'])
            check('completes once for eighteen damage',hp-sum(e['hp'] for e in v['enemies']),18)
            check('bowstring returns actual arrow to hand',card(v,'arrow_2')['pile'],'Hand')
            check('returned arrow no longer deployed',card(v,'arrow_2')['deployed'],False)
        elif mode=='haste':
            hp=sum(e['hp'] for e in live()['enemies']);b,v=play('arrow_2',2,['JA105','JK37C'])
            check('haste plus desire completes once',hp-sum(e['hp'] for e in v['enemies']),18)
            check('haste arrow ends in exhaust',card(v,'arrow_2')['pile'],'Exhaust')
            b,v=play('zero_0',0,[]);check('no arrow means no further attack',sum(e['hp'] for e in b['enemies'])-sum(e['hp'] for e in v['enemies']),0)
        elif mode=='double':
            hp=sum(e['hp'] for e in live()['enemies']);b,v=play('zero_0',2,['JK37C','JK37C'])
            check('two installations complete arrow exactly once',hp-sum(e['hp'] for e in v['enemies']),18)
            check('completed arrow not deployed',card(v,'arrow_2')['deployed'],False)
        elif mode=='discount':
            check('helm starts at three energy',card(live(),'helm')['cost'],3)
            b,v=play('weather',0,[]);check('weather reduces helm to zero',card(v,'helm')['cost'],0)
            b,v=play('helm',1,['JK37C']);check('discounted power accelerates arrow',card(v,'arrow_3')['remaining'],2)
        elif mode=='turns':
            b,v=play('zero_0',1,['JK37C']);check('first turn nine to eight',card(v,'arrow_9')['remaining'],8)
            before=v;offset=len(v['events']);act('end_turn')
            s,v=wait(lambda s,v:v and v['turn']==before['turn']+1 and card(v,'arrow_9')['remaining']==7 and s.get('battle',{}).get('is_play_phase') and any(c['role']=='next_zero' for c in v['hand']) and all(e['done'] for e in v['events'][offset:]))
            check('one real turn progressed',v['turn']-before['turn'],1);check('native countdown separate from desire',[e['power'] for e in v['events'][offset:]],['native'])
            steps.append({'action':'end_turn','before':before,'after':v});time.sleep(.5)
            b,v=play('next_zero',1,['JK37C']);check('next turn zero fee remains active',card(v,'arrow_9')['remaining'],6)
        time.sleep(.9)
        print(json.dumps({'case':mode,'assertions':len(rows),'failures':0,'events':live()['events']},ensure_ascii=False),flush=True)
    except Exception as error:
        rows.append({'name':'native recording execution','passed':False,'error':str(error)})
        raise
    finally:
        req('/recording/stop',{})
        for _ in range(150):
            recording=req('/recording')
            if recording['status'] in ['completed','failed']:break
            time.sleep(.2)
        (root/(mode+'-recording.json')).write_text(json.dumps(recording,indent=2),encoding='utf-8')
        (root/(mode+'-results.json')).write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
        (root/(mode+'-steps.json')).write_text(json.dumps(steps,ensure_ascii=False,indent=2),encoding='utf-8')
