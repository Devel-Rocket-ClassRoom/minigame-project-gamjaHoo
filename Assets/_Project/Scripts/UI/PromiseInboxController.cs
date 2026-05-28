// PromiseInboxController.cs
// V1.0 N.5 — 약속 진행 현황 화면.
// state.activePromises 에서 유저 클럽 선수 약속만 표시.

using System.Collections.Generic;
using FMLite.Application;
using FMLite.Core;
using FMLite.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMLite.UI
{
    public class PromiseInboxController : MonoBehaviour
    {
        private const string DashboardScene = "DashboardScene";

        [Header("목록")]
        [SerializeField]
        private Transform listParent;

        [SerializeField]
        private GameObject itemPrefab;

        [SerializeField]
        private TMP_Text emptyLabel;

        private GameState _state;

        private void Start()
        {
            _state = GameManager.Instance?.State;
            Refresh();
        }

        public void OnBackClicked() => SceneManager.LoadScene(DashboardScene);

        private void Refresh()
        {
            if (listParent != null)
                foreach (Transform child in listParent)
                    Destroy(child.gameObject);

            if (_state == null)
                return;

            var userClub = _state.GetClub(_state.userClubId);
            var playerIds = userClub?.seniorSquadIds ?? new List<int>();

            var relevant = new List<Promise>();
            if (_state.activePromises != null)
                foreach (var p in _state.activePromises)
                    if (
                        p != null
                        && p.status == PromiseStatus.Active
                        && playerIds.Contains(p.playerId)
                    )
                        relevant.Add(p);

            if (emptyLabel != null)
                emptyLabel.gameObject.SetActive(relevant.Count == 0);

            foreach (var promise in relevant)
                SpawnItem(promise);
        }

        private void SpawnItem(Promise promise)
        {
            if (itemPrefab == null || listParent == null)
                return;

            var go = Instantiate(itemPrefab, listParent);
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = BuildLine(promise);
        }

        private string BuildLine(Promise promise)
        {
            var player = _state.GetPlayer(promise.playerId);
            string playerName =
                player?.info != null
                    ? $"{player.info.firstName} {player.info.lastName}"
                    : $"id={promise.playerId}";

            string typeLabel = Localization.Get(TypeKey(promise.type));
            string deadline = promise.deadline.ToString("yyyy/MM/dd");

            string progress = "";
            if (promise.type == PromiseType.PlaytimeAgreement)
            {
                int appearances = player?.state?.seasonAppearances ?? 0;
                int required = promise.targets.TryGetValue("appearances", out int r) ? r : 0;
                progress = Localization.Get("promise_progress_fmt", appearances, required);
            }

            return Localization.Get("promise_item_fmt", playerName, typeLabel, deadline, progress);
        }

        private static string TypeKey(PromiseType type) =>
            type switch
            {
                PromiseType.PlaytimeAgreement => "promise_type_playtime",
                PromiseType.TransferIn => "promise_type_transfer_in",
                PromiseType.Renewal => "promise_type_renewal",
                PromiseType.TransferOut => "promise_type_transfer_out",
                _ => "promise_type_playtime",
            };
    }
}
