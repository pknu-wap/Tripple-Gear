import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { Presentation, PresentationFile } from '@oai/artifact-tool';

const workspaceDir = 'D:/wap/Tripple-Gear/shotgame';
const skillDir = 'C:/Users/efson/.codex/plugins/cache/openai-primary-runtime/presentations/26.915.20218/skills/presentations';
const buildDir = path.join(workspaceDir, '.build/metal-slug-ui');
const candidatePath = path.join(buildDir, 'candidate.pptx');
const imagePath = 'C:/Users/efson/Downloads/ChatGPT 이미지 2026년 9월 29일 오후 05_45_52.png';
const { resolvePresentationFont } = await import(pathToFileURL(path.join(skillDir, 'container_tools/artifact_tool_utils.mjs')).href);
const family = resolvePresentationFont({ fontFamily: 'Malgun Gothic' });
const ppt = Presentation.create({ slideSize: { width: 1280, height: 720 } });

const C = { bg: '#171A16', paper: '#F1E8D0', muted: '#B9B5A7', gold: '#F5C84C', orange: '#F07832', green: '#84D789', line: '#716C59', dark: '#252820' };
const text = (slide, value, x, y, w, h, size, color=C.paper, bold=false, opts={}) => {
  const sh = slide.shapes.add({ geometry: 'textbox', name: opts.name, position: { left:x, top:y, width:w, height:h }, fill:'none', line:{ fill:'none', width:0 } });
  sh.text = value;
  sh.text.style = { typeface: family, fontSize:size, bold, color, autoFit:'shrinkText', verticalAlignment:'middle', ...(opts.align ? { alignment:opts.align } : {}) };
  return sh;
};
const base = (n, title, kicker='게임 UI 기초') => {
  const slide = ppt.slides.add(); slide.background.fill = C.bg;
  text(slide, kicker.toUpperCase(), 64, 32, 540, 28, 15, C.gold, true);
  text(slide, title, 64, 72, 1152, 62, 38, C.paper, true);
  slide.shapes.add({ geometry:'line', position:{left:64,top:147,width:1152,height:0}, fill:'none', line:{fill:C.line,width:1.5} });
  text(slide, `METAL SLUG 2  ·  ${String(n).padStart(2,'0')} / 05`, 64, 675, 1152, 22, 13, C.muted, false, {align:'right'});
  return slide;
};
const notes = (slide, script) => slide.speakerNotes.textFrame.setText(script);

// 1. Intro: simple cover with clear scope and learning goal.
{
  const s = ppt.slides.add(); s.background.fill = C.bg;
  text(s, '게임 UI 기초 · 발표', 72, 66, 700, 34, 19, C.gold, true);
  text(s, '메탈슬러그 2로 배우는\n게임 HUD와 상호작용', 72, 152, 770, 178, 48, C.paper, true);
  text(s, '화면에 보이는 정보는\n게임 안에서 어떻게 바뀔까?', 76, 374, 600, 86, 28, C.green, true);
  s.shapes.add({geometry:'line',position:{left:76,top:497,width:520,height:0},fill:'none',line:{fill:C.line,width:2}});
  text(s, 'HUD 읽기   →   정보 배치   →   값이 바뀌는 흐름   →   우리 프로젝트', 76, 522, 1050, 46, 20, C.muted, false);
  text(s, '초보자용 · 5장', 76, 636, 500, 30, 16, C.muted);
  notes(s, '안녕하세요. 오늘은 메탈슬러그 2 화면을 예시로 게임 UI를 살펴보겠습니다. 게임 UI는 점수나 탄약처럼 정보를 보여주고, 입력을 받기도 합니다. 플레이 중 계속 보이는 정보 영역을 HUD라고 부릅니다. 오늘은 HUD를 읽고, 정보가 어떻게 바뀌는지 확인한 뒤 우리 프로젝트의 총알 UI와 연결해 보겠습니다.\n\n참고 영상: https://www.youtube.com/watch?v=-dpxG5J2CGg&t=1s');
}

// 2. Use the supplied screenshot once as the primary evidence.
{
  const s = base(2, '메탈슬러그 2의 HUD 구성');
  const img = new Uint8Array(await fs.readFile(imagePath));
  s.images.add({ blob:img, contentType:'image/png', alt:'메탈슬러그 2 플레이 영상 화면과 HUD. 오른쪽은 영상의 웹캠과 오버레이.', fit:'contain', position:{left:64,top:174,width:716,height:402} });
  text(s, '게임 화면의 정보', 822, 178, 392, 32, 20, C.gold, true);
  text(s, '1,106,961  점수\n1UP = 2  남은 목숨\nARMS 30  현재 무기 탄약\nBOMB 29  폭탄 수\n57  스테이지 타이머\nLEVEL-4 · CREDIT 0  진행·크레딧', 822, 222, 400, 238, 21, C.paper, false);
  text(s, '화면 오른쪽의 웹캠·초록 시계와 아래 자막·재생 버튼은 영상 플레이어 요소입니다.', 822, 488, 380, 86, 17, C.muted);
  text(s, '숫자 57은 탄약이 아니라 시간입니다.', 822, 598, 380, 30, 18, C.orange, true);
  notes(s, '이미지 위쪽의 점수는 플레이 성과를 보여줍니다. 1UP 옆의 2는 남은 목숨 정보이고, ARMS 30은 현재 무기의 탄약, BOMB 29는 폭탄 수입니다. 크게 보이는 57은 탄약이 아니라 스테이지 타이머입니다. 아래 LEVEL-4와 CREDIT 0은 스테이지와 아케이드 크레딧 정보입니다. 오른쪽의 웹캠과 초록색 시계, 자막과 재생 버튼은 게임이 아니라 영상 플레이어에 속합니다.\n\n자료: SNK, Metal Slug 2 관련 자료 https://game.snk-corp.co.jp/press/pdf/130207_01.pdf');
}

// 3. Explain information placement and readability as plain content.
{
  const s = base(3, 'HUD는 빠르게 읽히도록 배치됩니다');
  text(s, '먼저 확인하는 정보', 86, 198, 440, 40, 25, C.gold, true);
  text(s, '점수 · 목숨 · 탄약 · 폭탄\n\n전투 중 자주 확인하는 값은\n화면 위쪽에 모여 있습니다.', 86, 264, 470, 188, 24, C.paper);
  s.shapes.add({ geometry:'line', position:{left:622,top:196,width:0,height:340}, fill:'none', line:{fill:C.line,width:1.5} });
  text(s, '쉽게 구분하는 방법', 690, 198, 480, 40, 25, C.gold, true);
  text(s, '짧은 이름 + 숫자\n\n밝은 글자와 테두리로\n배경과 구별합니다.\n\n전투 장면은 가리지 않게 둡니다.', 690, 264, 500, 246, 24, C.paper);
  text(s, 'UI를 볼 때: 무엇을 보여 주나? 어디에 놓였나? 한눈에 읽히나?', 86, 586, 1100, 44, 21, C.green, true);
  notes(s, '전투 중에는 화면을 계속 읽을 시간이 많지 않기 때문에 중요한 정보가 빨리 보여야 합니다. 메탈슬러그 2에서는 점수와 목숨, 탄약처럼 자주 확인하는 항목이 화면 위쪽에 모여 있습니다. 짧은 이름과 숫자를 쓰고, 밝은 글자와 테두리로 복잡한 배경에서도 알아보기 쉽게 합니다. 화면을 읽을 때는 무엇을 보여주는지, 어디에 있는지, 한눈에 구별되는지를 생각하면 됩니다.\n\n참고: Unity Canvas Scaler 문서 https://docs.unity3d.com/cn/2018.3/Manual/script-CanvasScaler.html');
}

// 4. Native editable interaction flow diagram.
{
  const s = base(4, '게임 상태가 바뀌면 HUD도 바뀝니다');
  const steps = [
    {x:70, title:'총 발사', sub:'플레이어 입력', color:C.orange},
    {x:374, title:'탄약 감소', sub:'게임 값 변경', color:C.gold},
    {x:678, title:'UI가 값 읽기', sub:'현재 값 확인', color:C.green},
    {x:982, title:'숫자 갱신', sub:'화면에 표시', color:C.paper},
  ];
  for (const [i, step] of steps.entries()) {
    text(s, step.title, step.x, 262, 226, 46, 26, step.color, true, {align:'center'});
    text(s, step.sub, step.x, 314, 226, 35, 19, C.muted, false, {align:'center'});
    if (i < steps.length-1) text(s, '→', step.x+244, 269, 48, 44, 32, C.gold, true, {align:'center'});
  }
  s.shapes.add({geometry:'line',position:{left:82,top:390,width:1116,height:0},fill:'none',line:{fill:C.line,width:1.5}});
  text(s, '장전 완료 · 탄약 아이템 획득', 82, 432, 500, 42, 23, C.gold, true);
  text(s, '새 게임 값이 만들어지면 HUD가 그 값을 다시 보여 줍니다.', 82, 486, 1100, 44, 23, C.paper);
  text(s, '상호작용은 버튼 클릭뿐 아니라 입력과 게임 상태 변화가 화면에 반영되는 과정도 포함합니다.', 82, 576, 1090, 48, 18, C.muted);
  notes(s, 'UI의 상호작용은 버튼을 누르는 것만 뜻하지 않습니다. 게임에서 어떤 일이 생겨 값이 바뀌고, 그 결과를 화면에 표시하는 과정도 상호작용입니다. 총을 한 발 쏘면 현재 탄약이 줄고, HUD는 바뀐 값을 읽어 숫자를 새로 보여줍니다. 장전이 끝나거나 탄약 아이템을 얻으면 게임 값이 변하고 UI 숫자도 그에 맞춰 바뀝니다.');
}

// 5. Apply the lesson to the team's existing ammo data.
{
  const s = base(5, '우리 프로젝트의 총알 UI');
  text(s, 'SubmachineGunFire', 82, 196, 420, 48, 26, C.gold, true);
  text(s, '현재 탄약과 최대 탄약을 관리', 82, 250, 500, 46, 21, C.paper);
  text(s, 'BulletUIController', 82, 346, 420, 48, 26, C.green, true);
  text(s, '총의 값을 읽어 TextMeshPro 갱신', 82, 400, 510, 46, 21, C.paper);
  s.shapes.add({geometry:'line',position:{left:650,top:204,width:0,height:268},fill:'none',line:{fill:C.line,width:1.5}});
  text(s, '표시 예시', 720, 226, 400, 36, 20, C.muted, true);
  text(s, '29 / 30', 720, 286, 434, 100, 58, C.paper, true);
  text(s, '현재 / 최대', 720, 392, 400, 34, 18, C.muted);
  text(s, '총이 실제로 쓰는 탄약 값을 UI가 그대로 보여 줍니다.', 82, 514, 1100, 44, 22, C.paper, true);
  text(s, '무엇을 보여 주나?  ·  언제 바뀌나?  ·  쉽게 읽히나?', 82, 586, 1090, 38, 20, C.gold, true);
  notes(s, '우리 프로젝트도 같은 원리로 연결할 수 있습니다. SubmachineGunFire가 총의 현재 탄약과 최대 탄약을 관리하고, BulletUIController가 그 값을 읽어 TextMeshPro에 표시합니다. 현재 탄약이 29발이고 최대가 30발이면 화면에 29 / 30이라고 보입니다. 총이 실제로 사용하는 값을 표시하면 UI 숫자와 게임 상태를 따로 관리할 필요가 없습니다. 오늘 내용을 정리하면 무엇을 보여주는지, 어떤 상황에서 바뀌는지, 화면에서 쉽게 읽히는지 세 가지를 확인하면 됩니다. 감사합니다.');
}

await fs.mkdir(buildDir, {recursive:true});
await (await PresentationFile.exportPptx(ppt)).save(candidatePath);
for (let i=0;i<ppt.slides.items.length;i++) {
  const slide=ppt.slides.items[i];
  const preview=await ppt.export({slide,format:'png',scale:1});
  await fs.writeFile(path.join(buildDir,`slide-${i+1}.png`),new Uint8Array(await preview.arrayBuffer()));
}
const montage=await ppt.export({format:'webp',montage:true,scale:0.5});
await fs.writeFile(path.join(buildDir,'montage.webp'),new Uint8Array(await montage.arrayBuffer()));
console.log(`Created ${candidatePath} with ${ppt.slides.items.length} slides using ${family}`);
