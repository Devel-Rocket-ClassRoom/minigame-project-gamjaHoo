// InboxTextResolver.cs
// Stage B.1 (V1.0) — 인박스 타이틀 렌더. InboxItem(titleKey + ID titleArgs) → GameState 해석(선수명/약속타입/나이) → 로컬라이즈.
// 사용자 결정 (2026-06-01): ID(#12) 아닌 이름 표시. DashboardController 의 기존 인박스 포맷 패턴 재사용.
//
// 인박스 localization 키는 이름기반 positional ({0}=선수명 등). named-ID 키(deadline/counter_offer/contract_*/youth_intake)는
// SeedV10LocalizationData 에서 이름기반으로 migrate (asset 재생성 필요).
//
// 주의: FMLite.UI.InboxItem(구 row) 충돌 회피 — 도메인은 alias(D) 로 참조.

using System.Collections.Generic;
using FMLite.Application;
using UnityEngine;
using D = FMLite.Domain;

namespace FMLite.UI
{
    public static class InboxTextResolver
    {
        public static string ResolveTitle(D.InboxItem item, D.GameState state)
        {
            if (item == null)
                return string.Empty;
            var a = item.titleArgs ?? new Dictionary<string, string>();
            switch (item.titleKey)
            {
                case "inbox_promise_created_fmt":
                case "inbox_promise_fulfilled_fmt":
                case "inbox_promise_broken_fmt":
                {
                    var (name, typeLabel) = PromiseInfo(state, ArgInt(a, "id"));
                    return Localization.Get(item.titleKey, name, typeLabel);
                }
                case "inbox_promise_deadline_fmt":
                {
                    var (name, typeLabel) = PromiseInfo(state, ArgInt(a, "id"));
                    return Localization.Get(item.titleKey, name, typeLabel, ArgStr(a, "days"));
                }
                case "inbox_transfer_request_fmt":
                case "inbox_contract_renewed_fmt":
                case "inbox_contract_rejected_fmt":
                    return Localization.Get(
                        item.titleKey,
                        PlayerName(state, ArgInt(a, "playerId"))
                    );
                case "inbox_counter_offer_fmt":
                case "inbox_personal_negotiation_fmt":
                case "inbox_offer_accepted_fmt":
                case "inbox_offer_rejected_fmt":
                {
                    var offer = state?.activeOffers?.Find(o =>
                        o != null && o.id == ArgInt(a, "offerId")
                    );
                    return Localization.Get(
                        item.titleKey,
                        offer != null ? PlayerName(state, offer.playerId) : "?"
                    );
                }
                case "inbox_youth_intake_fmt":
                {
                    var club = state?.GetClub(ArgInt(a, "clubId"));
                    return Localization.Get(item.titleKey, club?.name ?? "?");
                }
                case "inbox_youth_promotion_fmt":
                {
                    var p = state?.GetPlayer(ArgInt(a, "playerId"));
                    return Localization.Get(
                        item.titleKey,
                        PlayerNameOf(p),
                        ComputeAge(p, state),
                        p?.currentAbility ?? 0
                    );
                }
                default:
                    return Localization.Get(item.titleKey); // 미정의 키 — 원본 템플릿
            }
        }

        // ── state 해석 헬퍼 (테스트 대상) ────────────────────────────

        public static int ArgInt(Dictionary<string, string> args, string key) =>
            args != null && args.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : -1;

        public static string ArgStr(Dictionary<string, string> args, string key) =>
            args != null && args.TryGetValue(key, out var v) ? v : "?";

        public static string PlayerName(D.GameState state, int playerId) =>
            PlayerNameOf(state?.GetPlayer(playerId));

        public static string PlayerNameOf(D.Player p) =>
            p?.info != null ? $"{p.info.firstName} {p.info.lastName}" : "?";

        public static (string name, string typeLabel) PromiseInfo(D.GameState state, int promiseId)
        {
            var pr = state?.activePromises?.Find(x => x != null && x.id == promiseId);
            if (pr == null)
                return ("?", "?");
            return (PlayerName(state, pr.playerId), Localization.Get(PromiseTypeKey(pr.type)));
        }

        public static int ComputeAge(D.Player p, D.GameState state)
        {
            if (p?.info == null || state == null)
                return 0;
            var t = state.currentDate;
            var b = p.info.birthDate;
            int age = t.Year - b.Year;
            if (t.Month < b.Month || (t.Month == b.Month && t.Day < b.Day))
                age--;
            return age;
        }

        public static string PromiseTypeKey(D.PromiseType type) =>
            type switch
            {
                D.PromiseType.PlaytimeAgreement => "promise_type_playtime",
                D.PromiseType.TransferIn => "promise_type_transfer_in",
                D.PromiseType.Renewal => "promise_type_renewal",
                D.PromiseType.TransferOut => "promise_type_transfer_out",
                _ => "promise_type_playtime",
            };
    }
}
