# PoC_HourglassDual 프로젝트 종합 분석 (AI Handoff)

작성 기준: 2026-05-18  
분석 범위: `Assets/01.Scripts`, `Assets/98.Debugger`, `Assets/00.Scenes/CodexScene.unity`, `Assets/07.Data/Combat`, 매니저 프리팹, 입력 액션/빌드 설정

---

## 1) 프로젝트 한 줄 정의

이 프로젝트는 **싱글 씬 기반의 1:1 턴제 전투 PoC**이며, 핵심은 `Hourglass(모래시계)` 자원 시스템으로 턴당 사용/이월 모래를 제어하고, `Break -> Groggy(적 턴 스킵) -> Bonus Turn` 루프로 템포 우위를 만드는 전투다.

---

## 2) 실행 환경/기술 스택

- Unity: `6000.3.9f1`
- Render Pipeline: URP
- 입력: New Input System (`com.unity.inputsystem 1.18.0`)
- UI: UGUI + TextMeshPro
- 카메라: Cinemachine 3.x
- 트윈: DOTween (`DG.Tweening`, 코드상 사용)
- 아키텍처: Singleton + EventBus(pub/sub) + Presenter/View 분리
- 로그: CSV 비동기 로거(`GameCsvLogger`)

---

## 3) 씬/부트스트랩 구조

### Build Settings

- 활성 씬: `Assets/00.Scenes/CodexScene.unity`
- `SampleScene`는 비활성

### 초기화 핵심

- `Bootstrapper`(`DefaultExecutionOrder(-100)`)가 시작점
- 필수 매니저를 `EnsureInstance(...)`로 확보 후 `BootstrapIfNeeded()` 호출
- 실제로 `CodexScene`에는 다음이 존재:
  - 씬 오브젝트/프리팹 인스턴스: `HourglassCombatManager`, `PoolManager`
  - 나머지 매니저는 `Bootstrapper`가 프리팹에서 런타임 생성

### Singleton/DDOL 정책(현재 프리팹 기준)

- DDOL(1): `GameManager`, `GameFlowManager`, `InputReader`, `SceneLoader`, `TimeManager`, `PauseManager`, `SoundManager`
- 비-DDOL(0): `PoolManager`, `HourglassCombatManager`, `UIManager(씬 Canvas 컴포넌트)`

### CodexScene 주요 오버라이드

- `CombatWaveController`
  - `startOnEnable = true`
  - 3웨이브 적 데이터 연결
  - `carryPlayerHpBetweenWaves = false`
- `CombatFeedbackView`
  - `_popupSpawnInterval = 0.5` (코드 기본값 0.05보다 느림)
- `HourglassSandView`
  - `_flipDuration = 0.34`
- `CombatScreenPresenter`
  - 비용: Strike 3 / Pierce 3 / Hex 3 / Guard 2
- `MainMenuPanelPresenter`
  - 가이드 패널 표시 후 `_guideDismissDelay = 0.8` 이후 입력으로 시작
- Debug Runner
  - `GameFlowDebugRunner._autoEnterPlaying = false`
  - `HourglassCombatDebugRunner.autoStartCombat = false`

---

## 4) 상태 머신

## 4.1 전역 `GameState`

`None -> MainMenu -> Playing -> Paused -> GameOver -> Loading` 흐름을 이벤트로 제어.

- `GameManager`가 상태 단일 소유자
- 변경 시 `GameStateChangedEvent` 발행
- `UIManager`가 상태에 맞춰 패널 활성화
- `PauseManager`가 `PauseInputEvent`를 받아 `Playing <-> Paused` 토글
- `TimeManager`는 `Paused/GameOver`에서 `Time.timeScale = 0`

## 4.2 `InGameState` (게임플로우 상태)

- `GameFlowManager` 소유
- 실사용 상태: `None`, `Initializing`, `Completed`, `Failed` 중심
- `GameState.Playing` 진입 시 `InGameState.Initializing`으로 전환, 내부에서 `HourglassCombatManager.StartCombat()` 호출
- `CombatEndedEvent`에서 승패 따라 `Completed/Failed`

참고: `Ready`, `Running`, `Suspended` enum은 현재 실질 사용이 거의 없다.

---

## 5) 입력 시스템

`InputReader`가 `PlayerInput`을 통해 액션맵을 읽고 모두 `EventBus` 이벤트로 브로드캐스트한다.

- 액션맵: `Player`, `UI`, `System`, `Combat`
- 전투 단축키(Keyboard):
  - `Q` Strike
  - `W` Pierce
  - `E` Hex
  - `R` Guard
  - `F` EndTurn
- 일시정지: `Esc`

입력은 직접 매니저 호출이 아니라 이벤트 발행 후, `CombatScreenPresenter`가 버튼 인터랙터블 체크를 통과하면 `HourglassCombatManager` 요청 메서드를 호출한다.

---

## 6) 이벤트 아키텍처

`EventBus`는 `Dictionary<Type, Delegate>` 기반의 경량 pub/sub.

- 구독: `Subscribe<T>(Action<T>)`
- 해제: `Unsubscribe<T>(Action<T>)`
- 발행: `Publish<T>(T)`

장점:
- 시스템 결합도 낮음
- UI/로거/매니저 분리가 쉬움

주의:
- 동기 호출이며 스레드 안전하지 않음
- 구독 해제 누락 시 중복 호출 가능

---

## 7) 실제 플레이 루프 (현재 구현)

## 7.1 유저 입장 루프

1. MainMenu에서 Start
2. 가이드 패널 표시
3. 지연 시간 후 아무 입력(Submit/Cancel/Primary/Secondary)으로 닫힘
4. `GameState.Playing`
5. 전투 시작
6. 플레이어 턴에서 액션 반복(모래 소모)
7. EndTurn(Flip)
8. 적 턴 AI 수행
9. 승리 시 다음 웨이브(총 3웨이브)
10. 플레이어 패배 시 GameOver 패널

## 7.2 내부 루프(전투 단위)

`StartCombat()`
- 런타임 상태 초기화
- 플레이어 턴으로 시작
- `CombatStartedEvent`, `CombatTurnStartedEvent` 발행

플레이어 액션
- `Strike/Pierce/Hex/Guard/EndTurn`
- 유효성(턴/전투종료/전환중/모래부족) 체크
- 성공 시 `CombatActionExecutedEvent` 및 상황별 이벤트 발행

턴 종료 전환(핵심)
- `StartTurnTransition(...) -> RunTurnTransitionSequence(...)`
- MinimumFall 선행 처리 -> 필요 시 이벤트 발행/대기
- Flip 완료 후 다음 턴 확정
- `CombatTurnEndedEvent`, `CombatTurnStartedEvent` 발행

적 턴
- 딜레이 후 의도 결정
- `CombatActionRequestedEvent` 발행
- 딜레이 후 공격/회복 실행
- EndTurn 실행 후 플레이어 턴으로 복귀

종료
- HP 0 판정 시 `CombatEndedEvent`

웨이브
- `CombatWaveController`가 `CombatEndedEvent`를 받아 다음 적 세팅 후 재시작

---

## 8) 전투 메카닉 상세

## 8.1 모래시계 자원

런타임 상태:
- `TotalSand`, `LockedSand`, `UpperSand`, `LowerSand`
- 현재 턴 액터 기준 `Upper=사용 가능`, `Lower=이번 턴 내려간 모래`

턴 중 액션 코스트 지불 시:
- `AvailableSand -= cost`
- `TransferredSand += cost`

즉 코스트를 쓰면 곧바로 아래로 이동한 것으로 취급.

## 8.2 MinimumFall

의도: 아무 행동 없이 Flip하는 경우도 다음 턴 최소 행동량 확보.

공식(턴 종료 직전):
- if `Lower < MinimumFall`:
  - `forced = min(MinimumFall - Lower, Upper)`
  - `Upper -= forced`, `Lower += forced`

중요: 이 프로젝트는 MinimumFall을 **Flip 직전 명시 단계**로 처리하며, 이벤트(`CombatMinimumFallAppliedEvent`)도 별도 발행한다.

## 8.3 Flip 및 턴 전환

1. pre-flip 상태(`CompletedUpper/Lower`) 확정
2. Flip:
   - `flippedUpper = CompletedLower`
   - `flippedLower = CompletedUpper`
3. 다음 턴 액터로 전환
4. `SyncActorSand()`

## 8.4 플레이어 액션 수치(현재 데이터)

- Strike: 비용 3, 피해 6, 브레이크 1
- Pierce: 비용 3, 피해 2, 브레이크 3
- Hex: 비용 3, 피해 2, 브레이크 1, 위협 -1
- Guard: 비용 2, 플레이어 가드 +4

## 8.5 피해 계산

`ApplyDamage(target, rawDamage)`:
- 먼저 `target.GuardValue`로 피해를 흡수
- 남은 피해만 HP 차감
- HP 최소 0

즉 Guard는 HP 앞단의 고정 흡수막.

## 8.6 적 가드/브레이크/그로기

- 적은 `EnemyGuard` 보유
- 플레이어 공격의 `breakPower`로 감소
- 0 이하가 되면 Break
  - `EnemyGuard = 0`
  - `GroggyPending = true`

다음 플레이어 EndTurn에서:
- 적 턴이 스킵되고 플레이어 보너스 턴 획득
- 적 `EnemyGuard`는 최대치로 복원
- 그로기 플래그 해제

## 8.7 위협(Threat)과 적 AI 의도

턴 시작 시 의도 결정:
- 위협 최대(`EnemyThreat >= ThreatCap`)면 무조건 `DesperationStrike`
- 아니면 모래량으로:
  - `<=2`: RecoverGuard
  - `<=4`: WeakAttack
  - `<=6`: HeavyAttack
  - `>=7`: HeavyAttackPlus

턴 종료 시 위협:
- 위협 최대로 시작한 턴이면 0으로 리셋
- 아니면 `+EnemyThreatGainPerTurn`
- Break 시 현재 설정은 위협 즉시 리셋(`resetThreatOnBreak = true`)

## 8.8 적 공격 수치(현재 config)

- Weak: 3
- Heavy: 7
- HeavyPlus: 9
- Desperation: 10
- RecoverGuard: +2
- HighSandRecoverBonus: +1 (HeavyPlus 조건에서)

## 8.9 승패

- 적 HP 0: 승리
- 플레이어 HP 0: 패배
- 웨이브 컨트롤러가 패배 시 `GameState.GameOver`로 전환

---

## 9) 데이터 자산(실제 값)

## 9.1 `HourglassCombatConfig_Test.asset`

- totalSand: 10
- lockedSand: 2 (실사용 가능한 모래 8)
- minimumFall: 3
- defaultPlayerSand: 6
- defaultEnemySand: 4
- breakThreshold: 8
- threatCap: 3
- enemyThreatGainPerTurn: 1
- hexThreatDelta: -1
- breakThreatDelta: -2 (단, 현재 resetThreatOnBreak=true라 체감 적음)
- resetThreatOnBreak: true
- enemyRecoverGuardAmount: 2
- enemyHighSandRecoverGuardBonus: 1
- enemyWeakDamage: 3
- enemyHeavyDamage: 7
- enemyHeavyPlusDamage: 9
- enemyDesperationDamage: 10
- allowThreatMaxDoubleAction: true
- enemyDoubleActionFirstDamage: 3
- enemyDoubleActionSecondDamage: 6

## 9.2 액션 SO

- Strike: actionType=Strike, dmg6, cost3, break1
- Pierce: actionType=Pierce, dmg2, cost3, break3
- Hex: actionType=Hex, dmg2, cost3, break1
- Guard: actionType=Guard, cost2, guard+4
- RecoverGuard: actionType=RecoverGuard
- WeakAttack: actionType=WeakAttack
- DesperationStrike.asset: **actionType 값이 12(HeavyAttack)로 저장됨**

주의: 적 실제 데미지는 현재 `HourglassCombatManager`가 config 수치를 직접 사용하므로, 위 SO의 baseDamage가 전투 데미지에 직접 쓰이지 않는 구간이 있다.

## 9.3 액터 SO(웨이브)

- Player_Test: HP 36, baseGuard 0
- Enemy_Test 1: HP 24, baseGuard 8
- Enemy_Test 2: HP 26, baseGuard 12
- Enemy_Test (3웨이브): HP 32, baseGuard 16

`CombatWaveController` 웨이브 순서:
1. Enemy_Test 1
2. Enemy_Test 2
3. Enemy_Test

---

## 10) UI/연출 책임 분리

- `CombatScreenPresenter`
  - 상태창/버튼/로그/입력 이벤트 연결
  - 액션 요청 라우팅
- `CombatView`
  - 전투 일러스트(SpriteRenderer) 전용
  - 공격 이동/피격 흔들림/사망 페이드
  - 그로기 스프라이트 잠금 처리
- `HourglassSandView`
  - 모래 슬라이더/텍스트/Flip 연출
  - EndTurn 프리뷰 -> 실제 턴 스왑 시 Flip
- `CombatFeedbackView` + `PopupToast`
  - 데미지/브레이크/그로기 팝업
  - 화면 플래시

현재 팝업 지연 체감은 Scene 오버라이드 `_popupSpawnInterval = 0.5` 영향이 매우 큼.

---

## 11) 로깅/디버그

`GameCsvLogger`:
- 광범위 이벤트 구독(게임 상태, 입력, 전투, 카메라, 사운드, 타임스케일)
- 비동기 큐 + 백그라운드 스레드로 CSV 기록
- 경로: 실행 파일 폴더의 `Logs/` (실패 시 `persistentDataPath` fallback)
- 파일명: `framework_log_yyyyMMdd_HHmmss.csv`

전투 스냅샷 필드(`CombatLogSnapshot`)가 풍부해서 사후 분석에 유리.

---

## 12) 현재 구조에서 알아야 할 핵심 리스크/주의점

1. **전투 시작 트리거 중복 가능성**
- `GameFlowManager`도 `StartCombat()` 호출
- `CombatWaveController.startOnEnable=true`도 씬 시작 시 `StartCombat()` 호출
- 의도에 따라 둘 중 하나를 게이트해야 한다.

2. **데이터 필드-로직 불일치 구간**
- `HourglassCombatConfigSO.defaultEnemySand`는 현재 실사용 경로가 없음
- `CombatActorDataSO.initialSand`도 현재 사실상 미사용
- `CombatActionDataSO.threatDelta`도 실질 미사용(위협 감소는 config의 `hexThreatDelta` 사용)

3. **AI 확장 필드 미사용**
- `allowThreatMaxDoubleAction`, `EnemyDoubleAction*` 수치가 있으나 현재 의도 결정에서 DoubleAction 분기 미사용

4. **그로기 상태 플래그**
- 실질적으로 `GroggyPending` 중심으로 동작
- `GroggyActive`는 현재 명시적 true 전환 코드가 없음

5. **데이터 자산 정합성**
- `DesperationStrike.asset`의 `actionType`이 HeavyAttack 값(12)으로 저장되어 있음
- 현재 적 데미지 계산은 config 직결이라 즉시 치명은 아니어도, 툴링/로그/향후 리팩터링 시 혼선 가능

6. **부트스트랩 참조 형태 주의**
- `Bootstrapper`의 `_hourglassCombatManagerPrefab`는 현재 씬 인스턴스 참조(fileID) 형태
- 단일 씬 PoC에서는 동작하지만, 멀티씬/프리팹 교체 워크플로우에서는 자산 참조형으로 정리하는 편이 안전

---

## 13) 스크립트 카탈로그 (역할 요약)

## 13.1 Combat/Core

- `CombatActionResolver.cs`: 플레이어 액션/피해/브레이크/그로기 적용
- `CombatActorRuntime.cs`: 액터 런타임 데이터/모래 지불/수신
- `CombatRuntimeState.cs`: 전투 전체 런타임 컨테이너
- `CombatTurnProcessor.cs`: MinimumFall + Flip + 보너스턴 + 턴전환 계산

## 13.2 Combat/Data

- `CombatActionDataSO.cs`: 액션 정적 데이터
- `CombatActorDataSO.cs`: 액터 능력치 + 일러스트/연출 데이터
- `HourglassCombatConfigSO.cs`: 전투 공통 설정값

## 13.3 Combat/Event

- `CombatEvents.cs`: 전투 이벤트/스냅샷 타입

## 13.4 Combat/Manager

- `HourglassCombatManager.cs`: 전투 상태 소유/요청 처리/AI/턴 시퀀스 중심
- `CombatWaveController.cs`: 웨이브 진행/승리 시 다음 웨이브/패배 시 게임오버

## 13.5 Combat/Runtime

- `CombatActionType.cs`: 액션 enum
- `CombatActorType.cs`: 액터 enum
- `CombatTurnState.cs`: 턴 상태 enum
- `CombatElementType.cs`: 요소 enum

## 13.6 Combat/UI

- `CombatScreenPresenter.cs`: 전투 UI 오케스트레이터
- `CombatView.cs`: SpriteRenderer 전투 일러스트 연출 전담
- `CombatActorPanelView.cs`: HP/Guard/Threat/Groggy 상태패널
- `CombatActionPanelView.cs`: 액션 버튼/텍스트 참조 집합
- `CombatFeedbackView.cs`: 팝업 큐 + 스크린 플래시
- `PopupToast.cs`: 개별 토스트 애니메이션
- `CombatLogView.cs`: 전투 로그 스크롤
- `HourglassSandView.cs`: 모래시계 수치/텍스트/Flip 애니메이션
- `HourglassShapeGraphic.cs`: 모래시계 외곽 커스텀 그래픽
- `HourglassSandChamberGraphic.cs`: 챔버 채움 커스텀 그래픽
- `MainMenuPanelPresenter.cs`: 시작/가이드/종료
- `PausePanelPresenter.cs`: 계속/재시작/종료

## 13.7 Framework/Frame

- `Singleton.cs`: 제네릭 싱글톤 + 부트스트랩 인터페이스
- `EventBus.cs`: 이벤트 허브
- `Bootstrapper.cs`: 매니저 인스턴스 보장 및 부팅

## 13.8 Framework/Manager

- `GameManager.cs`: 전역 GameState
- `GameFlowManager.cs`: InGameState 및 전투 시작 연동
- `InputReader.cs`: New Input System -> EventBus 이벤트 변환
- `SceneLoader.cs`: 씬 로드 관리
- `PauseManager.cs`: 일시정지 토글
- `TimeManager.cs`: pause/hitstop/slow-motion 타임스케일 제어
- `SoundManager.cs`: BGM/SFX 재생
- `PoolManager.cs`: 오브젝트/ UI 풀
- `PoolSetupData.cs`: 풀 세팅 구조체
- `UIManager.cs`: 메인/일시정지/로딩/게임오버 패널 스위칭
- `CameraManager.cs`: 카메라 셰이크 이벤트 처리

## 13.9 Framework/Event, Enum, Interfaces

- `GameEvents.cs`: 공용 이벤트 타입
- `GameState.cs`, `InGameState.cs`, `Enums.cs`: 상태/공용 enum
- `IDamageable.cs`: 일반 데미지 인터페이스(현재 전투 루프와 직접 결합은 낮음)

## 13.10 Utilities

- `DespawnController.cs`: 파티클 종료 시 풀 반환

## 13.11 Debugger

- `GameCsvLogger.cs`: 이벤트 기반 CSV 로그
- `GameLogEventType.cs`: 로그 이벤트 타입 enum
- `LoggableEntity.cs`: 로그용 엔티티 메타
- `DebugRunner/GameFlowDebugRunner.cs`: 강제 Playing 진입 디버그
- `DebugRunner/HourglassCombatDebugRunner.cs`: 전투 요청 디버그 호출

---

## 14) 다른 AI에게 바로 넘길 때 핵심 요약

이 프로젝트를 이어받는 AI는 아래 4가지를 먼저 보면 된다.

1. `HourglassCombatManager.cs`  
전투 흐름/턴전환/AI의 단일 진실 소스.

2. `CombatTurnProcessor.cs`  
MinimumFall, Flip, BonusTurn 규칙의 수학적 핵심.

3. `CombatScreenPresenter.cs` + `CombatView.cs` + `HourglassSandView.cs`  
입력/표현/UI 타이밍 연결.

4. `HourglassCombatConfig_Test.asset` + 웨이브 적 Actor SO들  
현재 밸런스 값 실제 소스.

이 네 축을 잡으면 프로젝트의 플레이 감각과 버그 발생 지점을 빠르게 파악할 수 있다.
