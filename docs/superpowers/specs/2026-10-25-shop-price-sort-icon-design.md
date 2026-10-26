# Shop Price Sort Icon Design

**Intent:** Make the compact shop sort control immediately read as price sorting.

**Approved design:** Keep the existing 116 × 116 secondary button and search-row layout. Show the system coin icon on the left and a direction indicator on the right: both arrowheads for default order, up for low-to-high price, and down for high-to-low price. Active price sorting keeps the existing green accent.

**Constraints:** Reuse current UI sprites and builder conventions, add no dependency, preserve the three-state click cycle and accessible sort labels, and do not alter the rest of the shop layout.

**Approaches considered:** Reusing the paired back arrows was rejected because it reads as navigation. A bars-plus-arrow glyph was previewed but did not communicate that sorting is specifically by coins. The approved coin-plus-direction design uses the game's real currency visual and keeps the control compact.

