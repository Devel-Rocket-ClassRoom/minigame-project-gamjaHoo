// FormationPitchLayout.cs
// Stage H.2 — 포메이션 슬롯의 피치 도식 좌표를 런타임 계산 (v1.0-tasks H.2, 사용자 결정 A안).
// FormationSO 에 좌표 필드가 없으므로 displayName("4-2-3-1") 라인 파싱 + 좌우 정렬로
// 정규화 좌표(0~1)를 산출. x: 0=좌, 1=우 / y: 0=자기 골문(GK), 1=상대 골문(최전방).
// 결과 배열은 slotPositions / tactic.slots 인덱스와 1:1 정렬 (result[i] = 슬롯 i 좌표).

using System.Collections.Generic;
using FMLite.Application;
using FMLite.Domain;
using UnityEngine;

namespace FMLite.UI
{
    public static class FormationPitchLayout
    {
        // 포메이션 슬롯 정규화 좌표 산출. 슬롯이 없으면 빈 배열.
        public static Vector2[] Compute(FormationSO formation)
        {
            var positions = formation?.slotPositions;
            if (positions == null || positions.Length == 0)
                return System.Array.Empty<Vector2>();

            var result = new Vector2[positions.Length];

            // 1) 라인 행 분해 — GK(슬롯0) 한 행 + displayName 파싱 행들. (실패 시 라인 밴드 폴백)
            var rows = ResolveRows(formation);

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                float y = (r + 0.5f) / rows.Count; // 뒤(GK)→앞 균등
                var ordered = OrderRowLaterally(row, positions); // 좌→우 정렬
                for (int k = 0; k < ordered.Count; k++)
                {
                    float x = (k + 0.5f) / ordered.Count; // 행 내 가로 균등 (양끝 대칭)
                    result[ordered[k]] = new Vector2(x, y);
                }
            }
            return result;
        }

        // displayName("4-4-2") 파싱 → [GK행, 4행, 4행, 2행] 슬롯 인덱스 묶음. 실패 시 라인 밴드 폴백.
        private static List<List<int>> ResolveRows(FormationSO formation)
        {
            var positions = formation.slotPositions;
            int n = positions.Length;
            var counts = ParseOutfieldCounts(formation.displayName); // 합 = n-1 (GK 제외) 기대

            if (counts != null)
            {
                var rows = new List<List<int>> { new List<int> { 0 } }; // GK 행 = 슬롯 0
                int idx = 1;
                bool ok = true;
                foreach (int c in counts)
                {
                    var row = new List<int>();
                    for (int j = 0; j < c && idx < n; j++)
                        row.Add(idx++);
                    rows.Add(row);
                    if (row.Count != c)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok && idx == n)
                    return rows;
            }
            return BandRows(positions); // 폴백
        }

        // "4-2-3-1" → [4,2,3,1]. 아웃필드 합이 (slot-1) 와 안 맞거나 파싱 실패 시 null.
        private static int[] ParseOutfieldCounts(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return null;
            var parts = displayName.Split('-');
            if (parts.Length < 2)
                return null;
            var counts = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i].Trim(), out counts[i]) || counts[i] <= 0)
                    return null;
            return counts;
        }

        // 폴백 — GK / DF / MF / AT 4 밴드 그룹 (slotPositions 순서 유지).
        private static List<List<int>> BandRows(Position[] positions)
        {
            var rows = new List<List<int>>();
            foreach (var band in new[] { Line.GK, Line.DF, Line.MF, Line.AT })
            {
                var row = new List<int>();
                for (int i = 0; i < positions.Length; i++)
                    if (StartingSquadGacha.LineOf(positions[i]) == band)
                        row.Add(i);
                if (row.Count > 0)
                    rows.Add(row);
            }
            return rows;
        }

        // 행 내 슬롯을 좌→우로 정렬. L/R 포지션은 양쪽, WB 한 쌍은 첫=좌·둘째=우(양끝).
        private static List<int> OrderRowLaterally(List<int> row, Position[] positions)
        {
            int wbSeen = 0;
            var keyed = new List<(int slot, int key, int order)>(row.Count);
            for (int i = 0; i < row.Count; i++)
            {
                int slot = row[i];
                keyed.Add((slot, LateralKey(positions[slot], ref wbSeen), i));
            }
            keyed.Sort(
                (a, b) => a.key != b.key ? a.key.CompareTo(b.key) : a.order.CompareTo(b.order)
            );
            var ordered = new List<int>(row.Count);
            foreach (var e in keyed)
                ordered.Add(e.slot);
            return ordered;
        }

        // 좌우 정렬 키 (음수=좌, 0=중앙, 양수=우). 절대값은 정렬 순서만 결정(간격은 균등 배분).
        private static int LateralKey(Position pos, ref int wbSeen)
        {
            switch (pos)
            {
                case Position.LB:
                case Position.LM:
                case Position.LW:
                    return -2;
                case Position.RB:
                case Position.RM:
                case Position.RW:
                    return 2;
                case Position.WB:
                    return (wbSeen++ % 2 == 0) ? -3 : 3; // 첫 WB=좌, 둘째=우 (양끝)
                default:
                    return 0; // 중앙 — 원본 순서로 tie-break
            }
        }
    }
}
