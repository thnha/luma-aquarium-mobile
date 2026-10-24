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
        public GameObject coin, statusBadge, activeCheck, tankPreview;
        public RectTransform tankShape;

        public void Show(Sprite picture, string name, string priceText, bool showCoin, string statusText, Sprite background,
            bool active = false, AquariumTank.Spec tank = null)
        {
            portrait.sprite = picture;
            portrait.gameObject.SetActive(picture != null);
            title.text = name;
            price.text = priceText;
            coin.SetActive(showCoin);
            status.text = statusText;
            statusBadge.SetActive(!string.IsNullOrEmpty(statusText));
            surface.sprite = background;
            if (activeCheck != null) activeCheck.SetActive(active);
            if (tankPreview != null) tankPreview.SetActive(tank != null);
            if (tankShape != null && tank != null)
                tankShape.sizeDelta = new Vector2(tank.id == "tank_wide" ? 350 : tank.id == "tank_deep" ? 230 : 290,
                    tank.id == "tank_deep" ? 240 : 190);
        }
    }
}
