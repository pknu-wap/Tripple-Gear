import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { Presentation, PresentationFile } from '@oai/artifact-tool';

const workspaceDir = 'D:/wap/Tripple-Gear/shotgame';
const buildDir = path.join(workspaceDir, '.codex/metal-slug-ui-build');
const outputDir = path.join(workspaceDir, '.codex/metal-slug-ui-output');
const skillDir = 'C:/Users/efson/.codex/plugins/cache/openai-primary-runtime/presentations/26.915.20218/skills/presentations';
const finalPath = path.join(outputDir, 'MetalSlug2_UI_발표자료.pptx');
const screenshot = new Uint8Array(await fs.readFile(path.join(buildDir, 'reference.png')));
const { resolvePresentationFont } = await import(pathToFileURL(path.join(skillDir, 'container_tools/artifact_tool_utils.mjs')).href);
const fontFamily = resolvePresentationFont({ fontFamily: 'Malgun Gothic' });

await fs.mkdir(outputDir, { recursive: true });
const ppt = Presentation.create({ slideSize: { width: 1280, height: 720 } });

const C = { bg: '#171A1F', panel: '#252A31', white: '#F6F2E9', muted: '#B8B8B2', yellow: '#FFD94A', orange: '#F28B35', teal: '#78D6C4', line: '#4B515A' };
function text(slide, value, x, y, w, h, size = 24, color = C.white, bold = false) {
  const s = slide.shapes.add({ geometry: 'textbox', position: { left: x, top: y, width: w, height: h }, fill: 'none', line: { fill: 'none', width: 0 } });
  s.text = value;
  s.text.style = { typeface: fontFamily, fontSize: size, color, bold, autoFit: 'shrinkText' };
  return s;
}
function rect(slide, x, y, w, h, fill, stroke = 'none') {
  return slide.shapes.add({ geometry: 'rect', position: { left: x, top: y, width: w, height: h }, fill, line: { fill: stroke, width: stroke === 'none' ? 0 : 2 } });
}
function base(title, subtitle = '') {
  const s = ppt.slides.add();
  s.background.fill = C.bg;
  text(s, title, 64, 40, 1152, 62, 38, C.white, true);
  rect(s, 64, 116, 64, 5, C.yellow);
  if (subtitle) text(s, subtitle, 64, 132, 1152, 42, 19, C.muted);
  return s;
}
function note(slide, narration, source = '') {
  slide.speakerNotes.textFrame.setText(source ? `${narration}\n\n참고 자료: ${source}` : narration);
}

// 1. Title
{
  const s = ppt.slides.add();
  s.background.fill = C.bg;
  s.images.add({ blob: screenshot, contentType: 'image/png', alt: 'Metal Slug 2 gameplay with HUD', fit: 'cover', position: { left: 0, top: 0, width: 1280, height: 720 }, crop: { left: 0, top: 0.04, right: 0.245, bottom: 0.14 } });
  rect(s, 0, 0, 1280, 720, '#111318');
  // A dark overlay is intentionally avoided; title sits on a simple opaque band for contrast.
  rect(s, 0, 0, 1280, 720, '#171A1F');
  s.images.add({ blob: screenshot, contentType: 'image/png', alt: 'Metal Slug 2 gameplay HUD', fit: 'cover', position: { left: 570, top: 0, width: 710, height: 720 }, crop: { left: 0.02, top: 0.03, right: 0.245, bottom: 0.13 } });
  rect(s, 0, 0, 650, 720, C.bg);
  text(s, '게임 UI 공부', 72, 180, 500, 62, 22, C.teal, true);
  text(s, '메탈슬러그 2의\nHUD 살펴보기', 72, 260, 590, 190, 48, C.white, true);
  rect(s, 72, 475, 76, 6, C.yellow);
  text(s, '화면 정보는 어떻게 보이고,\n게임 상황에 따라 어떻게 바뀔까?', 72, 510, 490, 90, 24, C.muted);
  note(s, '안녕하세요. 오늘은 메탈슬러그 2 화면을 예시로 게임 UI, 특히 플레이 중 화면에 계속 보이는 HUD를 살펴보겠습니다. HUD가 어떤 정보를 보여 주고 게임 데이터와 어떻게 연결되는지 알아보겠습니다.');
}

// 2. Reading the HUD
{
  const s = base('메탈슬러그 2의 HUD 구성', '전투 중 빠르게 확인해야 하는 정보를 화면 위쪽에 모아 보여줍니다.');
  s.images.add({ blob: screenshot, contentType: 'image/png', alt: '메탈슬러그 2 화면과 HUD', fit: 'cover', position: { left: 64, top: 200, width: 650, height: 430 }, crop: { left: 0, top: 0.02, right: 0.245, bottom: 0.14 } });
  text(s, '화면에서 읽을 수 있는 정보', 760, 205, 450, 34, 23, C.yellow, true);
  const rows = [
    ['점수', '적 처치·아이템 획득 등 플레이 성과'],
    ['1UP', '플레이어의 남은 목숨 관련 표시'],
    ['ARMS 30', '현재 무기의 남은 탄약'],
    ['BOMB 29', '사용 가능한 폭탄 수'],
    ['57', '스테이지 타이머 카운트다운'],
    ['LEVEL / CREDIT', '스테이지와 아케이드 크레딧 정보'],
  ];
  let y = 255;
  for (const [label, desc] of rows) {
    text(s, label, 760, y, 150, 34, 20, C.white, true);
    text(s, desc, 905, y, 310, 45, 17, C.muted);
    y += 57;
  }
  text(s, '오른쪽 웹캠·초록색 시계·자막은 영상 플레이어 요소입니다.', 64, 650, 1150, 28, 15, C.muted);
  note(s, '이 화면에서 게임 UI와 영상 UI를 먼저 구분해야 합니다. 오른쪽의 웹캠과 초록색 시계, 아래 자막과 재생 버튼은 영상 플레이어에 속합니다. 게임 자체의 HUD에는 점수, 1UP 표시, ARMS 탄약, BOMB 폭탄 수, 스테이지 타이머, LEVEL과 CREDIT가 보입니다. 큰 57은 ARMS나 BOMB 옆의 숫자가 아니라 스테이지 타이머입니다. 각 UI는 플레이 중 판단에 필요한 정보를 짧고 눈에 띄게 전달합니다.', 'SNK 공식 Metal Slug 2 자료: https://game.snk-corp.co.jp/press/pdf/130207_01.pdf');
}

// 3. Interaction / state flow
{
  const s = base('게임 상태가 HUD에 반영되는 흐름', 'UI는 숫자를 직접 관리하기보다, 게임 상태를 읽어 화면에 보여주는 역할을 합니다.');
  const steps = [
    ['게임 사건', '총을 발사한다'],
    ['게임 데이터', '현재 탄약이 줄어든다'],
    ['UI 갱신', '표시할 문자열을 만든다'],
    ['화면 표시', '예: 29 / 30'],
  ];
  const xs = [64, 365, 666, 967];
  for (let i = 0; i < steps.length; i++) {
    rect(s, xs[i], 260, 245, 175, C.panel, i === 3 ? C.yellow : C.line);
    text(s, `0${i + 1}`, xs[i] + 18, 278, 55, 38, 20, C.teal, true);
    text(s, steps[i][0], xs[i] + 18, 328, 205, 35, 22, C.white, true);
    text(s, steps[i][1], xs[i] + 18, 374, 210, 46, 17, C.muted);
    if (i < 3) text(s, '→', xs[i] + 255, 322, 42, 48, 30, C.yellow, true);
  }
  text(s, '발사·재장전·탄약 획득처럼 값이 바뀌는 사건을 기준으로 UI를 갱신합니다.', 64, 500, 1110, 42, 22, C.white, true);
  text(s, '확인할 점: 화면 숫자와 실제 게임 데이터가 서로 어긋나지 않아야 합니다.', 64, 557, 1110, 36, 18, C.muted);
  note(s, '상호작용은 버튼을 누르는 경우만 뜻하지 않습니다. 게임 사건으로 데이터가 바뀌고, 그 변화가 화면에 전달되는 것도 UI 상호작용입니다. 총을 한 번 쏘면 현재 탄약 데이터가 감소하고 UI가 그 값을 다시 표시합니다. 장전이 끝나거나 탄약 아이템을 얻을 때도 같은 방식으로 화면을 갱신해야 합니다.');
}

// 4. Apply to this project
{
  const s = base('우리 프로젝트의 총알 UI', '기존 총 스크립트의 실제 탄약 값을 화면 텍스트에 연결합니다.');
  text(s, 'SubmachineGunFire', 90, 240, 390, 48, 28, C.yellow, true);
  text(s, '현재 탄약 수\n최대 탄약 수', 90, 310, 350, 84, 22, C.white);
  text(s, '→', 490, 300, 90, 60, 38, C.teal, true);
  text(s, 'BulletUIController', 600, 240, 440, 48, 28, C.yellow, true);
  text(s, '두 값을 읽어\nTextMeshPro 텍스트 갱신', 600, 310, 400, 84, 22, C.white);
  rect(s, 600, 450, 270, 90, C.panel, C.line);
  text(s, '화면 표시 예시', 620, 462, 220, 25, 15, C.muted);
  text(s, '29 / 30', 620, 489, 220, 42, 31, C.white, true);
  text(s, 'SO를 새로 만들지 않고, 총이 이미 관리하는 탄약 데이터를 사용합니다.', 90, 595, 1080, 36, 19, C.muted);
  note(s, '우리 프로젝트에 적용하면 SubmachineGunFire가 실제 현재 탄약과 최대 탄약을 가지고 있습니다. BulletUIController가 이 값을 읽어서 TextMeshPro 텍스트를 “현재 / 최대” 형식으로 보여줍니다. 예를 들어 29발 남았고 최대 30발이면 29 / 30으로 표시합니다. 이 흐름은 새 SO를 만들어 숫자를 따로 복사하는 방식이 아닙니다. UI는 실제 총이 관리하는 값을 보여줍니다.');
}

// 5. Takeaways
{
  const s = base('이번 UI 공부에서 기억할 점', '화면 요소를 외우기보다, 데이터가 바뀌고 표시되는 과정을 따라가면 됩니다.');
  const items = [
    ['무엇을 보여주나?', '점수·목숨·탄약·폭탄·시간처럼 플레이 판단에 필요한 정보'],
    ['언제 바뀌나?', '발사, 장전, 아이템 획득, 사망 등 게임 데이터가 바뀌는 순간'],
    ['잘 읽히나?', '중요한 정보는 눈에 띄게, 배경과 구분되게 배치'],
  ];
  let y = 220;
  for (let i = 0; i < items.length; i++) {
    text(s, `0${i + 1}`, 82, y, 70, 48, 25, C.teal, true);
    text(s, items[i][0], 170, y, 280, 42, 25, C.white, true);
    text(s, items[i][1], 450, y, 730, 54, 20, C.muted);
    y += 105;
  }
  rect(s, 82, 565, 1090, 3, C.line);
  text(s, '핵심: 게임 상태 변화 → UI 갱신 → 플레이어가 결과를 확인', 82, 595, 1120, 42, 23, C.yellow, true);
  note(s, '정리하면, UI를 공부할 때는 무엇을 보여주는지, 어떤 상황에서 바뀌는지, 화면에서 쉽게 읽히는지 세 가지를 확인하면 됩니다. 메탈슬러그 2의 HUD도 게임 상태를 빠르게 전달하고, 우리 총알 UI도 같은 원리로 실제 탄약 변화를 보여줍니다. 이상으로 발표를 마치겠습니다.');
}

const draft = path.join(buildDir, 'draft.pptx');
await (await PresentationFile.exportPptx(ppt)).save(draft);
for (let i = 0; i < ppt.slides.items.length; i++) {
  const slide = ppt.slides.items[i];
  const png = await ppt.export({ slide, format: 'png', scale: 1 });
  await fs.writeFile(path.join(buildDir, `slide-${i + 1}.png`), new Uint8Array(await png.arrayBuffer()));
}
console.log(JSON.stringify({ draft, finalPath, fontFamily, slides: ppt.slides.items.length }));
