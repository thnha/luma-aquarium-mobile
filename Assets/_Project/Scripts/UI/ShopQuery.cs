using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Lumaquarium.UI
{
    public enum ShopSort { Default, PriceAscending, PriceDescending }

    // The shop's tab / search / sort, kept free of scene objects so it can be tested.
    public static class ShopQuery
    {
        public static ShopGroup GroupOf(ShopTab tab) => tab switch
        {
            ShopTab.Fish => ShopGroup.Fish,
            ShopTab.Tanks or ShopTab.Walls or ShopTab.Substrates => ShopGroup.Aquarium,
            _ => ShopGroup.Decor
        };

        public static List<ShopEntry> Filter(IReadOnlyList<ShopEntry> entries, ShopGroup group, ShopTab tab, string search, ShopSort sort)
        {
            var inGroup = entries.Where(e => GroupOf(e.tab) == group);
            // Rêu & bèo is a single shop category; their catalog and inventory categories stay separate.
            if (group == ShopGroup.Decor && (tab == ShopTab.Moss || tab == ShopTab.Floating))
                return Filter(inGroup.Where(e => e.tab == ShopTab.Moss || e.tab == ShopTab.Floating).ToList(), ShopTab.All, search, sort);
            return Filter(inGroup.ToList(), tab, search, sort);
        }

        // Lower case without Vietnamese marks, so "lua" finds "Lũa" and "da" finds "Đá".
        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string decomposed = text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var plain = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) plain.Append(c);
            return plain.ToString().Normalize(NormalizationForm.FormC);
        }

        public static List<ShopEntry> Filter(IReadOnlyList<ShopEntry> entries, ShopTab tab, string search, ShopSort sort)
        {
            string query = Normalize(search);
            var shown = entries.Where(e => (tab == ShopTab.All || e.tab == tab) && (query.Length == 0 || e.SearchKey.Contains(query)));
            // OrderBy is stable; ties keep the default order.
            return (sort == ShopSort.PriceAscending ? shown.OrderBy(e => e.price).ThenBy(e => e.order)
                : sort == ShopSort.PriceDescending ? shown.OrderByDescending(e => e.price).ThenBy(e => e.order)
                : shown.OrderBy(e => e.order)).ToList();
        }
    }
}
