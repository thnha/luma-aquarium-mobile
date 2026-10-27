# Shop with a separate wallet

Approved through the interactive preview and the user's instruction to apply it on 2026-10-27.

- Keep the existing Cá / Tiểu cảnh / Hồ catalog, search, sorting, inventory and real prices.
- Show the coin balance independently from a prominent gold Nạp xu button with a wallet icon.
- Nạp xu opens a bottom sheet with Mua xu / Nhận miễn phí tabs. Move real IAP packs and rewarded-ad controls here, removing their rows from the catalog.
- Preserve selection, search and scroll when opening/closing the wallet. Insufficient coins lead to the wallet, returning to the selected item.
- Item details open on card taps in a dismissible bottom sheet; the catalog gets the space previously reserved for persistent details.
- Keep existing purchase confirmation, buy/equip/place, server validation, pending reward/purchase retry, privacy and coin effects.
- Unsupported stores/ads show honest availability messages. No simulated balances, pack prices or rewards.
- Use the existing uGUI builder, sprites and fonts. No dependencies, environment changes or commits.

Alternatives: an extra Xu & thưởng catalog tab was previewed; the user chose the separate wallet sheet. Final preview separates the balance from the top-up button.

## Header correction approved 2026-10-27

The cream background must fill the complete screen, including notch and home-indicator areas. Interactive controls remain inside SafeArea. Back, title, independent balance and gold top-up button share one compact header row; remove the extra wallet row and move catalog controls upward.
