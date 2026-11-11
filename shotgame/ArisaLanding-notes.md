# Arisa landing animation

Created with the built-in imagegen tool; source image preserved.

Reference: Assets/캐릭터애니메이션/아리사/아리사 앉기.png
Output: Assets/캐릭터애니메이션/아리사/ArisaLanding.png
Prefab: Assets/캐릭터애니메이션/아리사/아리사 움직임_2.prefab

Four horizontal cells, 543 x 724 pixels. Transparent RGBA. Sprite PPU 285.16484 maps the standing torso/legs height to the original 1.82 units. Shared pivot keeps soles aligned. Runtime order 1,2,3,0 at 16 fps (0.25 seconds). Upper body Y offsets -0.14027,-0.34366,-0.17884,0 units follow neck height changes. Existing crouch and normal animation take over when landing ends. Jump, crouch, hit reaction, teleport, and component disable cancel the landing. Horizontal movement remains responsive. Gravity and collision sizes are unchanged.

The sprite sequence is played from ArisaHorizontalMovement.LateUpdate, following the existing crouch sprite approach, so no Animator controller state changes are required. ArisaMouseAim incorporates the landing offsets before calculating aim angles.

## Generation prompt

Use case: identity-preserve. Create a production 2D game landing sprite sheet from the supplied Arisa clothing reference. Transparent RGBA background. Exactly 4 equal-width cells in ONE horizontal row, canvas 1536x512. Each cell 384x512. Show ONLY headless armless torso and both legs, matching reference olive green waistcoat/jacket, pale inner shirt, brown shorts, dark olive tall boots, original softly painted detailed game sprite style. No head, no arms, no weapon, no text, no floor or shadows. All four same character facing right in slight three-quarter side view as first reference standing sprite. Fixed camera, same limb lengths, clothing details and scale across frames. Boots soles planted at identical y=460 in every cell, foot positions fixed horizontally relative to each cell, pelvis centered x=192. Standing figure approx 290px high. Frame 1 standing knees nearly straight; frame 2 shallow landing squat knees visibly bent hips lower 25px; frame 3 deeper athletic landing squat knees visibly flexed hips lower 55px with thighs angling forward and shins back, both boot soles remain flat on ground under body, NOT sitting with feet sticking forward; frame 4 shallow recovery squat same as frame 2. Torso remains attached to pelvis and leans forward slightly on compression. Need real anatomically bent knees, not squashed or vertically scaled artwork. Preserve original muted colors, texture, silhouette and outfit. Generous clear gutters. Exact regular 4-cell alignment for Unity sprite slicing.

The generated output is 2172x724; import slicing uses the actual dimensions.

## Jump anticipation

The same four sprites now also play before takeoff in order 0,1,2,3 over 0.14 seconds. W starts a latched grounded preparation; takeoff occurs after the timer, even after a short tap. Holding or pressing W again does not restart the timer. Crouch, hit reaction, leaving a platform, disabling the component, and teleporting cancel preparation. The head and arms follow the matching neck offsets while aiming continues. Jump speed, gravity, collision sizes, and horizontal input are preserved. Prefabs without preparation frames, or with zero preparation duration, retain instant takeoff. Inspector fields: 점프 준비 시간, 점프 준비 스프라이트, 점프 준비 상체 위치 보정.
