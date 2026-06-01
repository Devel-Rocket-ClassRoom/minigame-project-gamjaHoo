// CurrencyIntegrationTests.cs
// Stage Z.3 (#435) — 통화는 표시 전용. 도메인(GameState) amount 는 항상 GBP base 유지 (직렬화 무관).

using System;
using FMLite.Application;
using FMLite.Domain;
using FMLite.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FMLite.Tests
{
    public class CurrencyIntegrationTests
    {
        [TearDown]
        public void Reset() => OptionsManager.Currency = Currency.GBP;

        [Test]
        public void Currency_IsDisplayOnly_DomainAmountUnchanged()
        {
            var club = new Club { id = 0, name = "Arsenal", finance = new Finance { money = 12_500_000 } };

            OptionsManager.Currency = Currency.KRW;
            string krw = CurrencyFormatter.Format(club.finance.money, OptionsManager.Currency);
            OptionsManager.Currency = Currency.GBP;
            string gbp = CurrencyFormatter.Format(club.finance.money, OptionsManager.Currency);

            Assert.AreNotEqual(krw, gbp, "표시는 통화별로 달라야");
            Assert.AreEqual(12_500_000, club.finance.money, "도메인 amount 는 통화 변경에 영향 0");
        }

        [Test]
        public void GameState_SerializationRoundTrip_KeepsGbpBase()
        {
            var state = new GameState
            {
                currentDate = new DateTime(2026, 8, 15),
                userClubId = 0,
                randomSeed = 42,
                nextPlayerId = 1,
            };
            state.AddClub(
                new Club
                {
                    id = 0,
                    name = "Arsenal",
                    finance = new Finance { money = 12_500_000, transferBudget = 8_000_000 },
                }
            );

            OptionsManager.Currency = Currency.KRW; // 통화를 바꿔도 직렬화 결과에 영향 없어야

            var json = JsonConvert.SerializeObject(state);
            var loaded = JsonConvert.DeserializeObject<GameState>(json);
            loaded.BuildIndexes();

            Assert.AreEqual(12_500_000, loaded.GetClub(0).finance.money, "money GBP base 유지");
            Assert.AreEqual(8_000_000, loaded.GetClub(0).finance.transferBudget, "transferBudget GBP base 유지");
            // 통화 설정은 PlayerPrefs(OptionsManager) — GameState(도메인) JSON 에 미포함
            StringAssert.DoesNotContain("\"Currency\"", json);
        }
    }
}
