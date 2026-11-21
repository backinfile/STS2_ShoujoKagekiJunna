const fs=require('fs'),path=require('path');
const file=path.join(__dirname,'art-plan.json');
const plan=JSON.parse(fs.readFileSync(file,'utf8'));
Object.assign(plan.find(x=>x.id==='JK200'),{
 source:'action-scan/bow-011.jpg',crop:[180,0,620,330],
 fit:'纯那一边高声回应对手，一边展开双臂、放出自己的攻势。清楚的发声主体与出手动作共同呈现用呼声回击的行动。文学知识沿用《李尔王》第三幕第二场的暴风雨独白。',
 artWork:'2021剧场版·狩猎的Revue公开片段约01:42，纯那展弓时高声回应对手',
 artUrl:'https://www.bilibili.com/video/BV1u3411v7i7/?t=102',focus:[0.59,0.52]
});
Object.assign(plan.find(x=>x.id==='JZ03'),{
 source:'action-scan/bow-026.jpg',crop:[110,35,510,335],
 fit:'取原曲全力释放的连续原词。原作画面直接呈现纯那放出的物理箭列向前飞行，箭身、尾羽和速度线共同呈现已经放出的攻击，承接0费、1回合的快速单次伤害。',
 artWork:'2021剧场版·狩猎的Revue公开片段约01:57，纯那放出的箭列高速前进',
 artUrl:'https://www.bilibili.com/video/BV1u3411v7i7/?t=117',focus:[0.48,0.5]
});
Object.assign(plan.find(x=>x.id==='U_CARD_018'),{
 source:'action-scan/green-057.jpg',crop:[360,60,720,330],
 fit:'纯那双手握紧刀柄、将刀刃举向下一次挥击，持械手、眼神和刀刃方向共同指明准备出手的动作。单体伤害对应主动挥刃；重新布置后继续推进下一轮出手。',
 artWork:'2021剧场版·狩猎的Revue公开片段约08:06，纯那双手握刀准备挥击',
 artUrl:'https://www.bilibili.com/video/BV1u3411v7i7/?t=486',focus:[0.5,0.5]
});
Object.assign(plan.find(x=>x.id==='U_CARD_021'),{
 source:'action-scan/gold-049.jpg',crop:[335,35,755,350],focus:[0.27,0.5],
 fit:'金色舞台中的纯那迎向对手、向前送出刀刃，持续燃烧的意志由实际进攻主体和明亮的攻势呈现。伤害结算后重新布置，下一轮仍继续出手，与燃烧不息的名字相连。',
 artWork:'2021剧场版·狩猎的Revue公开片段约06:08，纯那向前挥刃迎击',
 artUrl:'https://www.bilibili.com/video/BV1u3411v7i7/?t=368'
});
Object.assign(plan.find(x=>x.id==='JA96'),{
 source:'action-scan/gold-030.jpg',crop:[140,60,540,360],
 fit:'原词指战斗留下的伤痕。配图呈现纯那承受冲击后伏在地面、抬眼继续应战的近景。实际受到未被格挡的攻击伤害，成为继续推进对敌飞矢的时机。',
 artWork:'2021剧场版·狩猎的Revue公开片段约05:49，纯那受伤后伏地抬眼',focus:[0.54,0.55]
});
fs.writeFileSync(file,JSON.stringify(plan,null,2)+'\n');
console.log('Updated final crops.');
