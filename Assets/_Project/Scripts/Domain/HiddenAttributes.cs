// HiddenAttributes.cs
// 선수 숨김 능력치 9종 (design-decisions.md #40). 스카우트 시스템에서 부분 공개.

using System;

namespace FMLite.Domain
{
    [Serializable]
    public class HiddenAttributes
    {
        public int loyalty; // 충성도 — 재계약 주급 요구 ↓, 이적 요청 ↓
        public int ambition; // 야망 — 출전 부족 / 빅클럽 오퍼 시 이적 요청 ↑
        public int professionalism; // 훈련 효율 / 사기 안정 (변동폭 ×0.7)
        public int pressureHandling; // 빅매치 평점 가산
        public int temperament; // 카드 / 라커룸 분위기
        public int controversy; // 미디어 사고 확률 (V1.x)
        public int injuryProneness; // 부상 발생률 곱셈
        public int consistency; // 폼 변동폭
        public int versatility; // 2차 포지션 적응 속도
    }
}
