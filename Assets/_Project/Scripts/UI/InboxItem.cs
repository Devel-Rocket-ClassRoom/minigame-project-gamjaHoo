// InboxItem.cs
// V1.0 G.2 Sub-B — Dashboard 인박스 단일 메시지 행.
// in-memory (V1.0 단순) — V1.x persistent (GameState.inboxItems) 검토.

using TMPro;
using UnityEngine;

namespace FMLite.UI
{
    public class InboxItem : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text messageText;

        public void Setup(string message)
        {
            if (messageText != null)
                messageText.text = message;
        }
    }
}
