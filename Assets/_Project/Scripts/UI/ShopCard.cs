using TMPro;
using UnityEngine;

namespace Lumaquarium.UI
{
    // One card in the shop grid; ShopScreen fills it from a ShopEntry.
    public sealed class ShopCard : MonoBehaviour
    {
        public UnityEngine.UI.Button button;
        public UnityEngine.UI.Image surface, portrait;
        public TMP_Text title, price, status;
        public GameObject coin, statusBadge;

        public void Show(Sprite picture, string name, string priceText, bool showCoin, string statusText, Sprite background)
        {
            portrait.sprite = picture;
            title.text = name;
            price.text = priceText;
            coin.SetActive(showCoin);
            status.text = statusText;
            statusBadge.SetActive(!string.IsNullOrEmpty(statusText));
            surface.sprite = background;
        }
    }
}
