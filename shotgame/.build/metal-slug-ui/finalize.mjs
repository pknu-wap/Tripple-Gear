import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { PresentationFile } from '@oai/artifact-tool';

const workspaceDir = 'D:/wap/Tripple-Gear/shotgame';
const skillDir = 'C:/Users/efson/.codex/plugins/cache/openai-primary-runtime/presentations/26.915.20218/skills/presentations';
const buildDir = path.join(workspaceDir, '.build/metal-slug-ui');
const candidatePath = path.join(buildDir, 'candidate.pptx');
const finalPath = path.join(workspaceDir, 'output/Metal_Slug_2_UI_초보자_발표.pptx');
const { finalizePresentation } = await import(pathToFileURL(path.join(skillDir, 'container_tools/artifact_tool_utils.mjs')).href);
const stagingDir = path.join(workspaceDir, '.build/metal-slug-ui/finalizer');
await fs.mkdir(stagingDir, { recursive:true });
await fs.mkdir(path.dirname(finalPath), { recursive:true });
const result = await finalizePresentation({
  explicitTotalSlideCount:5,
  workspaceDir,
  candidatePath,
  finalPath,
  pythonExecutable:'C:/Users/efson/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
  integrityValidatorPath:path.join(skillDir,'container_tools/inspect_presentation_package_integrity.py'),
  layoutValidatorPath:path.join(skillDir,'container_tools/inspect_presentation_layout_geometry.py'),
  layoutArgs:['--expected-slide-size-emu','12192000,6858000','--validate-heading-fit'],
  requiredNativeTableOwnerSlides:[],
  fontPolicy:{basis:'design',families:['Malgun Gothic']},
  verifyArtifactToolImport:true,
  receiptPath:path.join(stagingDir,'Metal_Slug_2_UI_초보자_발표.validation.json'),
});
console.log(JSON.stringify(result,null,2));
