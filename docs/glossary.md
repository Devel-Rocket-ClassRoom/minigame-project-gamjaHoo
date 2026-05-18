# Glossary

프로젝트 전반에서 사용되는 용어 정의. 새 용어 추가 시 여기에 기록.

## Football Manager Terms

| 용어 | 영문 | 설명 |
| --- | --- | --- |
| CA | Current Ability | 현재 능력 총량 (일반적으로 0~200 스케일) |
| PA | Potential Ability | 잠재 능력 총량 (성장의 상한선) |
| 성골 유스 | Homegrown Youth | 자체 유스 시스템 출신 선수 (`Player.youthClubId == clubId`) |
| 가챠 | Gacha | 랜덤 결과를 받고 다시 시도하는 메커닉 |
| 리롤 | Reroll | 랜덤 결과를 다시 굴림 |
| 유스 인스펙션 | Youth Intake | 매 시즌 새로운 유망주 후보가 떠오르는 이벤트 (메인 6월, 보조 1월) |
| 트레잇 | Trait | 선수에게 부여되는 숨겨진 특성 (늦깎이형, 빅매치형 등) |
| 박싱데이 | Boxing Day | 12월 26일. EPL은 이때부터 1월 초까지 살인 일정 |
| 보스만 룰 | Bosman Rule | 계약 만료 시 자유 이적 가능한 룰 |
| FA | Free Agent | 자유계약 신분 |
| 명성 | Reputation | 구단/선수의 위상. 이적 협상력, 유스 풀 품질 등에 영향 |
| 컨디션 | Fitness/Condition | 선수의 피로 상태. 100에서 시작해 경기마다 소모, 휴식으로 회복 |
| 사기 | Morale | 선수의 정신적 만족도. 성적/면담/약속 이행에 따라 변동 |
| 폼 | Form | 최근 경기 평균 폼. 경기력에 직접 영향 |
| 임대 | Loan | 일시적 이적. 의무/옵션 영구 이적 조항 추가 가능 |

## Game-Specific Terms

| 용어 | 설명 |
| --- | --- |
| 리롤 토큰 | Reroll Token. 유스 인스펙션 풀을 다시 굴리는 자원 |
| 스타팅 가챠 | 게임 시작 시 구단 스쿼드를 명성 기반으로 랜덤 생성하는 시스템 |
| 5단계 티어 | Elite / Strong / Average / Weak / Poor — 스쿼드 평가 표시 단위 |
| 시드 | Random Seed. 같은 시드는 같은 초기 상태 생성 |
| 비활성 구단 | `isActiveSimulation == false`인 구단. 경량 시뮬만 받음 |
| 활성 시뮬 | 유저 구단 + 같은 리그 + 주요 컴페티터의 풀 시뮬레이션 |
| 인스펙션 풀 | Youth Intake Pool. 유스 영입 시점에 떠오르는 후보 선수 집합 |

## Technical Terms

| 용어 | 설명 |
| --- | --- |
| SO | ScriptableObject. Unity의 정적 데이터 컨테이너 |
| GameState | 게임의 모든 도메인 인스턴스가 모이는 루트 객체. 세이브 단위 |
| EventBus | 시스템 간 결합도를 낮추는 정적 이벤트 발행/구독 시스템 |
| Stateless System | 자신의 상태 없이 GameState만 조작하는 시스템 |
| Composition | "부모 없이 못 사는" 객체 관계 |
| Aggregation | "부모와 별개로 존재 가능한" 객체 관계 |
| ID Reference | 객체 직접 참조 대신 int ID로 참조 |
| Layered Architecture | Presentation - Application - Domain - Data 4단 구조 |

## Position Codes

| 코드 | 명칭 |
| --- | --- |
| GK | Goalkeeper |
| CB | Center Back |
| LB / RB | Left/Right Back |
| WB | Wing Back |
| DM | Defensive Midfielder |
| CM | Central Midfielder |
| AM | Attacking Midfielder |
| LM / RM | Left/Right Midfielder |
| LW / RW | Left/Right Winger |
| ST | Striker |
| CF | Center Forward |

## EPL-Specific Terms

| 용어 | 설명 |
| --- | --- |
| EPL | English Premier League |
| FA Cup | England Football Association Cup (전 디비전 참가) |
| Carabao Cup / EFL Cup | English Football League Cup (EPL + EFL 팀) |
| Championship | EPL 바로 아래 디비전 (2부 리그) |
| Community Shield | 전 시즌 EPL 우승 vs FA컵 우승 |
| A매치 위크 | 국가대표 경기 일정으로 리그가 쉬는 주 |
