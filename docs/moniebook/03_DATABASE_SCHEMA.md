# Moniebook Clone — Database Schema

Inferred from the UI. ✅ = already in Sterlin Glams · 🔴 = new table/column to add.
PK = `id`; FKs and key fields listed. Money = `numeric(18,2)`.

---

## Core (mostly ✅ in Sterlin Glams)

**businesses** (🔴 only if multi-tenant — *skip for SG, single business*)

**branches** ✅ (`Stores`)
- id, name, address, city, state, is_active, opening_hours, **is_warehouse** 🔴 (flag), created_at

**users / staff** ✅ (`AspNetUsers` + roles)
- id, first_name, last_name, email, phone, is_guest, **register_pin** 🔴, last_login_at

**roles / permissions** ✅ (`AspNetRoles`, `RolePermissions`)

**categories** ✅ — id, name, slug, parent_id, image_url, is_active, sort_order

**measurement_units** 🟡 (text today) → 🔴 table: id, name, abbrev (Per Item, KG, Litre, Carton…)

**taxes** 🔴 — id, name, rate_percent, is_inclusive, is_active  (e.g. VAT 7%)

---

## Items & pricing

**products** ✅ — id, name, slug, sku (uniq), barcode (uniq), category_id, unit/unit_id,
price (sell), **cost_price** 🔴, **average_cost** 🔴, low_stock_threshold, product_type
(`standard|variant|bundled`), **pricing_type** 🔴 (`fixed|manual|markup|range`),
**markup_percent** 🔴, **price_min/price_max** 🔴, is_active, is_featured, image…

**product_variants** ✅ — id, product_id, name, sku, barcode, price_adjustment,
**cost_price** 🔴, attribute values, is_active

**product_attributes / values** ✅ (`ProductAttributes`, `ProductAttributeValues`)

**bundled_item_components** 🔴 (composite/BOM)
- id, parent_product_id, component_product_id/variant_id, quantity

**pricing_groups** 🔴 — id, name, is_default
**product_group_prices** 🔴 — id, product_id (or variant_id), pricing_group_id, sell_price

**product_branch** ✅ (effectively via `StoreInventories` + branch price)
- 🔴 add **branch_price** + **sell_in_branch** flag per (product/variant, branch)

---

## Inventory & movements (✅ strong)

**store_inventories** ✅ — id, product_id, **product_variant_id (nullable)**, store_id,
quantity_on_hand, quantity_reserved, updated_at  (partial-unique on pool vs variant rows)

**stock_movements** ✅ (append-only ledger)
- id, product_id, product_variant_id, store_id, quantity_change (signed), balance_after,
  type (`Adjustment|Sale|Purchase|Transfer|Return|Damage|Loss`), reference, note,
  created_by_user_id, created_at

**stock_adjustments** 🔴 (header for a multi-line adjustment, like `BSA#####`)
- id, ref, store_id, reason, note, created_by, created_at
- **stock_adjustment_lines** 🔴: id, adjustment_id, product_id/variant_id, qty_delta, **unit_cost**, **expiry_date**, balance_after
- (today SG writes movements directly; a header table gives the `BSA#####` grouping)

**stock_reservations** ✅ — id, order_id, product_id, product_variant_id, store_id, quantity, created_at

**batches** 🔴 — id, product_id/variant_id, store_id, batch_no, expiry_date, qty_remaining, unit_cost

---

## Transfers (✅)

**stock_transfers** ✅ — id, ref (`TO#####`), source_store_id, dest_store_id, status
(`Draft|Sent|Received|Cancelled`), expected_date, created_by, **received_by** 🟡, received_at, created_at

**stock_transfer_items** ✅ — id, transfer_id, product_id/variant_id, qty_ordered,
**qty_received** 🟡, **qty_damaged** 🔴, **qty_wont_fulfil** 🔴, qty_pending (derived)

---

## Suppliers & purchasing (🔴 = OP-16)

**suppliers** 🔴 — id, name, contact_name, phone, email, address, is_active
**product_suppliers** 🔴 — id, product_id, supplier_id, supplier_price, is_default

**purchase_orders** 🔴 — id, ref (`PO#####`), supplier_id, store_id, status
(`Draft|Sent|PartiallyReceived|Received|Cancelled`), expected_date, subtotal, created_by, created_at
**purchase_order_items** 🔴 — id, po_id, product_id/variant_id, qty_ordered, qty_received, unit_cost, line_total

**goods_receipts** 🔴 (optional) — id, po_id, store_id, received_by, received_at
→ each receipt line raises on-hand (Purchase movement) + updates `average_cost`.

---

## Production / recipes (🔴, optional for SG)

**recipes / bom** 🔴 — id, output_product_id, output_qty
**recipe_components** 🔴 — id, recipe_id, component_product_id, qty
**production_runs** 🔴 — id, recipe_id, store_id, qty_produced, created_by, created_at

---

## Sales / POS (✅)

**orders** ✅ — id, order_number, channel (`Pos|Online`), status, customer/user_id, store_id,
subtotal, discount_amount, tax_amount, total, payment_provider, is_paid, amount_tendered,
change_given, created_at  (+ loyalty fields)
**order_items** ✅ — id, order_id, product_id, product_variant_id, quantity, unit_price, line_total
**refunds / refund_items** ✅
**parked_sales / saved_carts** ✅ (`ParkedSales`); **outstanding/credit sales** 🟡 (status)
**till_sessions / registers** ✅ (`TillSessions`, `Registers`)
**payment_methods** 🟡 (providers) → 🔴 first-class table for custom methods + restrictions

---

## CRM / settings (✅)

**customers** ✅ · **discount_codes / discounts** ✅ · **loyalty_accounts / points_ledger** ✅
**site_settings** ✅ (key/value, grouped) · **audit_logs** ✅ · **newsletter_subscribers** ✅

---

## Key relationships (new work)
```
suppliers 1───∞ product_suppliers ∞───1 products
suppliers 1───∞ purchase_orders 1───∞ purchase_order_items ∞───1 products/variants
purchase_orders ──(receive)──► stock_movements(type=Purchase) + store_inventories + products.average_cost
pricing_groups 1───∞ product_group_prices ∞───1 products/variants
products 1───∞ bundled_item_components(parent) ; component ∞───1 products
products/variants 1───∞ batches ∞───1 branches
```
