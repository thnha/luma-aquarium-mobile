using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lumaquarium.UI
{
    // Coins that burst from a button and fly into the wallet chip, bumping it and counting the balance up,
    // with a "+N" rising under it. Built at runtime from the wallet's own coin sprite; it never takes touches.
    public sealed class CoinRewardEffect : MonoBehaviour
    {
        const int Coins = 10;
        const float Stagger = .05f, Burst = .18f, Flight = .55f, CountTail = .15f, LabelTime = .9f;
        static readonly Color Gold = new Color32(255, 213, 74, 255);

        private RectTransform layer, wallet;
        private TMP_Text balance, gain;
        private Sprite coinSprite;
        private readonly List<RectTransform> coins = new List<RectTransform>();
        private readonly Vector2[] burstAt = new Vector2[Coins];
        private AudioSource voice;
        private Vector2 from, to, gainAt;
        private int fromBalance, toBalance, arrived;
        private float startedAt = -1;
        public bool Playing => startedAt >= 0;

        static float Arrival(int i) => i * Stagger + Burst + Flight;
        static float LastArrival => Arrival(Coins - 1);

        // Balance shown at t in [0, 1] of the count: eased out so most of it shows with the first coins.
        public static int CountAt(int from, int to, float t)
        {
            t = Mathf.Clamp01(t);
            return from + Mathf.RoundToInt((to - from) * (1 - (1 - t) * (1 - t)));
        }

        public void Setup(RectTransform walletChip, TMP_Text balanceLabel, Sprite coin)
        {
            wallet = walletChip;
            balance = balanceLabel;
            coinSprite = coin;
        }

        public void Play(RectTransform source, int oldBalance, int newBalance)
        {
            if (wallet == null || source == null || newBalance <= oldBalance) return;
            Build();
            layer.SetAsLastSibling();
            from = layer.InverseTransformPoint(source.TransformPoint(source.rect.center));
            to = layer.InverseTransformPoint(wallet.TransformPoint(wallet.rect.center + new Vector2(-wallet.rect.width * .3f, 0)));
            // Left of the chip: under it sit other buttons (the ad row's privacy button in the shop).
            gainAt = layer.InverseTransformPoint(wallet.TransformPoint(wallet.rect.center + new Vector2(-wallet.rect.width * .5f - 90, 0)));
            for (int i = 0; i < Coins; i++)
                burstAt[i] = from + Random.insideUnitCircle * 90 + new Vector2(0, 40);
            fromBalance = oldBalance;
            toBalance = newBalance;
            gain.text = "+" + Loc.Money(newBalance - oldBalance);
            arrived = 0;
            startedAt = Time.unscaledTime;
        }

        private void Build()
        {
            if (layer != null) return;
            var canvas = balance.canvas.rootCanvas;
            layer = new GameObject("CoinRewardLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(canvas.transform, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            for (int i = 0; i < Coins; i++)
            {
                var image = new GameObject("Coin", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.sprite = coinSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.rectTransform.SetParent(layer, false);
                image.rectTransform.sizeDelta = new Vector2(72, 72);
                image.gameObject.SetActive(false);
                coins.Add(image.rectTransform);
            }
            gain = new GameObject("Gain", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            gain.rectTransform.SetParent(layer, false);
            gain.rectTransform.sizeDelta = new Vector2(240, 70);
            gain.font = balance.font;
            gain.fontSize = 52;
            gain.fontStyle = FontStyles.Bold;
            gain.alignment = TextAlignmentOptions.Center;
            gain.color = Gold;
            gain.outlineWidth = .2f;
            gain.outlineColor = new Color32(90, 60, 10, 255);
            gain.raycastTarget = false;
            gain.gameObject.SetActive(false);
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
        }

        private void LateUpdate()
        {
            if (!Playing) return;
            float t = Time.unscaledTime - startedAt;
            for (int i = 0; i < Coins; i++) MoveCoin(i, t - i * Stagger);
            while (arrived < Coins && t >= Arrival(arrived)) Land(arrived++);

            float first = Arrival(0), end = LastArrival + CountTail;
            balance.text = Loc.Money(CountAt(fromBalance, toBalance, (t - first) / (end - first)));

            float bump = Mathf.Clamp01(1 - (t - Arrival(Mathf.Max(0, arrived - 1))) / .15f);
            wallet.localScale = Vector3.one * (1 + .1f * (arrived > 0 ? bump : 0));

            float g = (t - first) / LabelTime;
            gain.gameObject.SetActive(g >= 0 && g < 1);
            if (g >= 0 && g < 1)
            {
                gain.rectTransform.anchoredPosition = gainAt + new Vector2(0, 60 * g);
                gain.alpha = g < .6f ? 1 : 1 - (g - .6f) / .4f;
            }
            if (t >= Mathf.Max(end, first + LabelTime)) Finish();
        }

        private void MoveCoin(int i, float t)
        {
            var coin = coins[i];
            bool flying = t >= 0 && t < Burst + Flight;
            coin.gameObject.SetActive(flying);
            if (!flying) return;
            if (t < Burst)
            {
                float p = t / Burst;
                coin.anchoredPosition = Vector2.Lerp(from, burstAt[i], 1 - (1 - p) * (1 - p));
                coin.localScale = Vector3.one * Mathf.Lerp(.4f, 1, p);
                return;
            }
            float f = (t - Burst) / Flight;
            f *= f;   // speeds up into the wallet
            Vector2 start = burstAt[i], bend = new Vector2((start.x + to.x) / 2, Mathf.Max(start.y, to.y) + 160);
            coin.anchoredPosition = (1 - f) * (1 - f) * start + 2 * (1 - f) * f * bend + f * f * to;
            coin.localScale = Vector3.one * Mathf.Lerp(1, .6f, f);
        }

        private void Land(int i)
        {
            var tons = AquariumAudio.Current != null ? AquariumAudio.Current.tons : null;
            if (tons == null || tons.Length == 0) return;
            voice.pitch = 1.5f + .05f * i;
            voice.PlayOneShot(tons[i % tons.Length], .5f);
        }

        // Ends at once, e.g. when the shop closes mid-flight.
        public void Finish()
        {
            if (!Playing) return;
            startedAt = -1;
            foreach (var coin in coins) coin.gameObject.SetActive(false);
            gain.gameObject.SetActive(false);
            wallet.localScale = Vector3.one;
            balance.text = Loc.Money(toBalance);
        }

        private void OnDisable() => Finish();

        private void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
