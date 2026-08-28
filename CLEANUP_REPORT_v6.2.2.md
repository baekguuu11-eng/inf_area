# INFECTED AREA v6.2.2 Clean Optimized

## 정리 목적

v6.2.1 프로젝트에서 게임 실행에 직접 필요하지 않은 과거 패치 설치기, Payload 복사본, 복구 장면, 중복 문서, 테스트 전용 파일과 사용되지 않는 대형 폰트 자산을 제거한 정리본입니다.

## 유지한 핵심 구성

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/OpeningCutscene.unity`
- `Assets/Scenes/GameScene.unity`
- 현재 사용 중인 플레이어·적·무기·UI·맵 스크립트
- 적 4종 Prefab과 안정화 애니메이션
- 무기 정의, 무기 이미지, 총알·파편·재화 시스템
- Galmuri11, PressStart2P, LiberationSans TMP 자산
- 오디오, URP 2D 설정, Packages, ProjectSettings
- `EnemyEllipse.png`는 기존 GUID를 유지한 채 `Assets/Art/Shared`로 이동

## 제거한 항목

- 이전 버전 패치 설치기와 Editor 도구
- `Payload` 코드 복사본
- `_Recovery`, `_RecoveryBackups`
- 버전별 Documentation 폴더
- 적용이 끝난 v4/v5 패치 패키지 폴더
- SampleScene, WeaponTestScene 및 테스트 전용 Controller
- 사용되지 않는 Malgun 원본 폰트 3종과 관련 TMP 자산
- 사용되지 않는 구형 Enemy/Player Animation 및 Controller
- 더 이상 런타임에서 사용하지 않는 EnemyAnimationSheets 원본 시트
- 사용하지 않는 URP 2D Scene Template
- 고아 `.meta` 파일

## 프로젝트 정리 결과

- 원본 파일 수: 1,359
- 정리본 파일 수: 641
- 원본 비압축 크기: 88.64 MB
- 정리본 비압축 크기: 49.35 MB
- 감소량: 39.30 MB

## 정적 검증

- Build Settings의 3개 장면 존재 확인
- 삭제된 GUID를 참조하는 남은 Asset: 0개
- 고아 `.meta`: 0개
- `.meta`가 없는 Asset/폴더: 0개
- 중복 C# 클래스 이름: 0개
- KODB 로고, 적 4종 Prefab, 무기 Resource 존재 확인
- 필수 폴더 `Assets`, `Packages`, `ProjectSettings` 존재 확인

같은 픽셀 데이터인 적 애니메이션 프레임 일부는 Animation Clip의 프레임 타이밍과 GUID 연결을 보존하기 위해 삭제하지 않았습니다. 남은 중복 데이터는 약 6 KB 미만입니다.

## 처음 여는 방법

1. ZIP을 새 폴더에 압축 해제합니다.
2. Unity Hub에서 `INFECTED_AREA_v6_2_2_CleanOptimized` 폴더를 추가합니다.
3. Unity가 Library를 새로 생성하고 Import/Compile을 마칠 때까지 기다립니다.
4. Console의 빨간 오류를 확인한 다음 `MainMenu` 또는 `GameScene`을 실행합니다.

## 주의

이 환경에서는 Unity Editor Compile과 Play Mode를 직접 실행할 수 없었습니다. 위 검사는 파일 구조, GUID 연결, Build Scene, Resource 존재 여부를 기준으로 한 정적 검사입니다.
