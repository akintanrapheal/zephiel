# Moniebook-likeness — Inventory System Roadmap

Phased plan to make the **Sterlin Glams Inventory System** look and behave like Moniebook.
Cross-references: **01** = Feature Audit · **02** = Clone PRD · **03** = Database Schema · **04** = API Spec.

**Scope decisions (yours):**
- ❌ **Suppliers + Purchase Orders** — excluded (you don't want supplier tracking).
- 🔁 **Cost price → Sale price** — we use a storefront **Sale price** (regular struck-through), not a cost/valuation model. So Moniebook's *cost-based* Inventory Valuation / margin is **out of scope** unless cost price is reinstated later.
- ❌ **Batches/Expiry, Production & Recipes** — food-business features, skipped for jewellery.

So "parity" here = **Moniebook Inventory System minus Suppliers/PO, cost-valuation, batches, production.**

---

## Status at a glance

**✅ Already done**
- Per-variant stock model — variants own stock, product rolls up, no pool fallback. *(01 §9, 02 §1.1, 03 store_inventories)*
- Stock grid with scan + bulk set + typed reasons (Receive/Damage/Loss/Count/Correction). *(01 §4, 02 §1.2)*
- Stock-take, Stock transfer workflow, per-product Stock history. *(01 §4, 02 §1.3)*
- Reports: Reorder, Stock value, **Movement ledger**, **Shrinkage**. *(01 §5, 02 §3, 04 /inventory/movements,/shrinkage)*
- Settings groups: Inventory / Orders / POS / Security. *(02 §1.10)*
- **Phase 0** — Moniebook-style grouped sidebar + **Till → POS** rename. *(01 §1)*
- Storefront **Sale price** (slashed price). *(replaces 02 §1.1 cost-price line)*

---

## Remaining phases

### Phase 0 — Nav reshape + Till→POS  ✅ DONE *(uncommitted)*
Grouped sidebar (Overview · Point of sale → POS · Inventory · Reports). *(ref 01 §1)*

### Phase 1 — Reports & dashboard parity  🟢 DONE
- ✅ **Stock movement** report: Stock-in / Stock-out / Net change / Movements KPI cards on top of the ledger, computed over the active filter. *(Opening/Closing balance cards deferred — they need per-row balance snapshots at the range boundary; In/Out/Net are exact.)* *(01 §5)*
- ✅ **Sales by staff** + **Payment method** views as real Reports tabs (date-range + KPI cards; Payment method splits POS vs Online), promoted from "soon" in the sidebar. *(01 §5)*
- ✅ Overview dashboard: Top products (by units, last 30d) / Top staff (POS sales) cards. *(01 §2)*
- ✅ Bonus: fixed a latent `NormalizeRange` UTC-kind bug that crashed any date-range filter (Movements/Shrinkage too).

### Phase 2 — Stock adjustment headers  🟢 DONE
- ✅ Every change grouped under a `BSA#####` header (branch, reason, date, lines, net units) with a filterable **list view** (Date/Reason/Branch) + **detail**. Both creation paths file headers: the inline grid save (Source=Grid) and a dedicated **New-adjustment form** (Source=Form). Each line still raises a ledger movement that references the BSA number.
- ✅ Dedicated form captures **unit cost** + optional **expiry** per line (with product autocomplete + variant pick). *(grid path leaves cost/expiry null — they're form-only.)*
- ✅ Reason→movement-type mapping centralised in `AdjustmentReasons`.

### Phase 3 — Transfer reconciliation  🟢 DONE
- ✅ Per-line **Received / Damaged / Won't-fulfil / Pending** reconciliation on receive (Dispatched = the four). Multi-round receive (InTransit + PartiallyReceived) until pending clears → Completed. Only received units enter destination stock; damaged/won't-fulfil recorded as transit loss/shortage.
- ✅ **Printable receipt** (standalone auto-printing doc) with dispatched/received/damaged/won't/pending totals + signature lines.

### Phase 4 — Measurement Units  🔴 (small)
- A managed **Measurement Units** list (Per Item, KG, Litre, Carton…) + per-item unit dropdown, instead of free text. *(01 §3, 03 measurement_units, 04 /measurement-units)*

### Phase 5 — Bundled / Composite items  🟡 (optional)
- "Bundled item" type = a set sold as one (e.g. necklace + earrings), deducting its components' stock. *(01 §3, 02 §1.1, 03 bundled_item_components, 04 /items/bundled)*

### Phase 6 — Inventory valuation & margin  ⛔ (needs cost price — out of scope now)
- Only possible if a **cost price** is reintroduced; then Total inventory value (cost), potential profit, margin %. *(01 §5 Inventory valuation, 02 §1.9, 03 products.cost_price/average_cost)*

### Phase 7 — Batches/Expiry + Production/Recipes  ⛔ (food-business — skipped)
- *(01 §4, 03 batches / recipes — not relevant to jewellery)*

---

## How many remain?
- **Core to reach practical Moniebook parity (jewellery): 4 phases** → **Phase 1–4**.
- **Optional: Phase 5** (bundled items).
- **Out of scope by your decisions: Phase 6** (needs cost price) and **Phase 7** (food) + Suppliers/PO entirely.

**Recommended order:** Phase 1 → 2 → 3 → 4 (then Phase 5 if you want bundles).

> After Phase 0 (done) + Phases 1–4, the Inventory System matches Moniebook for a jewellery
> retailer on every screen you'll actually use, without the supplier/PO/cost/batch machinery.