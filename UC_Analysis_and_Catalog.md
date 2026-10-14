# AgriSustain Use Case Analysis and Consolidated Catalog

## 1. Baseline used for consolidation

The 14-page requirement specification is treated as the primary scope because it distinguishes Consumer, Buyer B2B, Farmer, Administrator, pre-order, and offtake contracts. The 7-page use-case list is used only for supporting functions that do not conflict with that scope.

## 2. Analysis of the proposed use cases

| Source / UC group | Decision | Reason and required adjustment |
|---|---|---|
| Authentication and profile management | Keep, revise | Keep registration, login, logout, profile management, change password, and reset password. Separate Farmer/Consumer registration from B2B registration because B2B requires legal information and KYC. |
| View Marketplace Home, Search & Filter, View Product Details | Keep, consolidate | These are valid public marketplace interactions. They are retained as **Browse and Search Marketplace** and **View Product Detail** to avoid unnecessary fragmentation. |
| View Seller Storefront / Supplier Profile | Keep, rename by audience | Consumer sees public farm/seller information; Buyer B2B sees a supplier capacity profile. The same data is shown at different levels of detail. |
| Manage Product Listing, Update Inventory, Submit Product for Approval, Toggle Visibility | Keep, merge | These are subflows of **Manage Product Listing**. Approval is an Admin decision; visibility must be allowed only after approval. |
| Link Traceability Batch, Generate QR, Record Harvest, Attach Biochemical Test | Keep, merge | The Farmer's goal is **Manage Product Batch and Traceability**. QR generation is a system response, not a standalone goal. Harvest and test evidence belong to season, batch, or certificate records. |
| Manage Farm Profile, Crop Seasons, Farming Logs, Certificates | Keep | These are core Farmer functions and support QR traceability. |
| Cart, Checkout, Payment, Order Tracking | Keep, revise actors | Consumer performs retail checkout. Payment Gateway is a secondary external actor: it creates/returns QR payment data and sends the payment webhook. Buyer B2B uses a separate wholesale order flow. |
| Cancel Order and Confirm Delivery Receipt | Conditional | They are reasonable post-order functions in the 7-page list but are not explicitly stated in the 14-page specification. Add only after defining allowed order statuses, cancellation window, and delivery confirmation rule. |
| Pre-order and Offtake Contract | Keep | These are explicit core B2B capabilities in the primary specification. Contract proposal, signature, and progress tracking remain separate because each has a distinct business outcome. |
| Manage Product Reviews | Split | **Submit Product Review** is a Consumer function. **Moderate Product Reviews** is an Admin function. Seller response is not in the primary specification, so exclude it until confirmed. |
| Submit & Resolve Complaint | Split | **Submit Complaint** belongs to Consumer. **Resolve Complaint** belongs to Admin. They cannot be one UC because they have different goals, permissions, and outcomes. |
| View Users and Search Users | Merge | Searching/filtering is part of **Manage Users**, not a separate user goal. |
| Review Seller Application and Approve/Reject Seller | Merge and widen | Replace with **Approve or Reject KYC Accounts** for Farmer and Buyer B2B. The primary specification requires both, not only Seller/Farmer. |
| Manage Quality Standards | Exclude for now | The source mentions certificates, but does not define a master-data function for standards. Add it only if Admin must maintain the standard list (VietGAP, Organic, GlobalGAP, etc.). |
| Monitor Platform Orders and View Platform Analytics | Consolidate | Both are included in **View Platform Dashboard and Pending Work**: KPI, orders requiring coordination, and pending approval queues. |
| View Supply-Demand Reports | Exclude for now | It appears only in the older list. It is not defined as a deliverable in the primary UC scope. It would add market-data/AI scope without functional requirements. |
| Credit, insurance, bank-partner, and logistics-partner functions | Exclude from this catalog | The old database has these entities, but the supplied UC specifications do not define actor goals, flows, or acceptance criteria for them. Treat them as a later phase unless confirmed. |

## 3. Consolidated Use Case Catalog

This table follows the Report 3 SRS format: **ID | Use Case | Feature | Use Case Description**. The primary actor is stated at the start of the description.

| ID | Use Case | Feature | Use Case Description |
|---|---|---|---|
| UC-AUTH-01 | Register Consumer or Farmer Account | Authentication and Profile Management | **Primary actor: Guest.** Creates a Consumer or Farmer account using phone number and password, then records the initial account status. |
| UC-AUTH-02 | Register Buyer B2B Account | Authentication and Profile Management | **Primary actor: Guest.** Creates a Buyer B2B account and submits company, tax code, and legal information for KYC review. |
| UC-AUTH-03 | Complete and Manage Profile | Authentication and Profile Management | **Primary actor: Consumer, Farmer, Buyer B2B.** Creates or updates role-specific profile information, including delivery address, farm details, or business information. |
| UC-AUTH-04 | Log In | Authentication and Profile Management | **Primary actor: Consumer, Farmer, Buyer B2B, Administrator.** Authenticates credentials, verifies account status, and routes the user to the appropriate workspace. |
| UC-AUTH-05 | Log Out | Authentication and Profile Management | **Primary actor: Authenticated user.** Ends the active session and invalidates the authentication token/session. |
| UC-AUTH-06 | Change Password | Authentication and Profile Management | **Primary actor: Authenticated user.** Changes the password after validating the current password and password policy. |
| UC-AUTH-07 | Reset Password | Authentication and Profile Management | **Primary actor: Guest or user.** Starts a verified password-reset process for a forgotten password. |
| UC-MKT-01 | Browse and Search Marketplace | Marketplace | **Primary actor: Guest, Consumer, Buyer B2B.** Browses public products and searches/filters by product name, category, supplier, region, season, and certification where applicable. |
| UC-MKT-02 | View Product Detail | Marketplace | **Primary actor: Guest, Consumer, Buyer B2B.** Views product description, price, stock/batch availability, seller/farm information, certificates, and eligible reviews. |
| UC-TRC-01 | View Product Traceability | Traceability | **Primary actor: Consumer.** Enters a batch code or scans a QR code to view farm, crop season, farming logs, harvest date, and quality certificates. Guest access may be added only if public QR lookup is approved. |
| UC-CON-01 | Manage Shopping Cart | Consumer Ordering | **Primary actor: Consumer.** Adds products, changes quantities, and removes products before checkout. |
| UC-CON-02 | Checkout and Place Retail Order | Consumer Ordering | **Primary actor: Consumer.** Selects delivery address, applies reward points when eligible, validates stock and price, and creates an order in Pending Payment status. |
| UC-CON-03 | Pay Retail Order by VietQR or PayOS | Consumer Payment | **Primary actor: Consumer; secondary actor: Payment Gateway.** Requests a dynamic QR payment, completes payment, and receives the result only after the gateway webhook is validated. |
| UC-CON-04 | Track Retail Order | Consumer Ordering | **Primary actor: Consumer.** Views order history, packing/delivery progress, payment status, and order details. |
| UC-CON-05 | Submit Product Review | Consumer Engagement | **Primary actor: Consumer.** Rates and comments on a product only after an eligible completed order. |
| UC-CON-06 | Submit Order Complaint | Consumer Engagement | **Primary actor: Consumer.** Creates a complaint with reason and evidence for an eligible order. |
| UC-B2B-01 | Search Suppliers and Production Regions | B2B Procurement | **Primary actor: Buyer B2B.** Searches Farmers/farms by region, supply capacity, crop season, and certification. |
| UC-B2B-02 | View Supplier Capacity Profile | B2B Procurement | **Primary actor: Buyer B2B.** Reviews farm capacity, cultivated area, crop calendar, available products, and reputation information. |
| UC-B2B-03 | Create Wholesale Order | B2B Procurement | **Primary actor: Buyer B2B.** Creates a bulk order with quantity, packaging requirements, and requested delivery schedule. |
| UC-B2B-04 | Create Pre-Order Commitment | B2B Procurement | **Primary actor: Buyer B2B.** Reserves upcoming harvest output under agreed quantity, price, and deposit/commitment terms. |
| UC-B2B-05 | Create Offtake Contract Proposal | B2B Contract Management | **Primary actor: Buyer B2B.** Drafts a long-term offtake contract including quantity, price framework, delivery terms, and validity period. |
| UC-B2B-06 | Sign Offtake Contract | B2B Contract Management | **Primary actor: Buyer B2B, Farmer.** Signs or electronically confirms a contract after both parties accept its terms. |
| UC-B2B-07 | Track B2B Order and Contract Progress | B2B Contract Management | **Primary actor: Buyer B2B.** Views wholesale delivery milestones, contract fulfillment progress, invoices, and related records. |
| UC-B2B-08 | Manage Buyer Subscription | B2B Membership | **Primary actor: Buyer B2B.** Views, selects, renews, or cancels an eligible subscription tier. |
| UC-FAR-01 | Manage Farm and Crop Seasons | Farm Management | **Primary actor: Farmer.** Maintains farm profile, crop plan, expected harvest date, estimated yield, actual yield, and crop-season status. |
| UC-FAR-02 | Record E-Farming Logs | Farm Management | **Primary actor: Farmer.** Records cultivation activities such as irrigation, fertilization, pest management, and supporting evidence. |
| UC-FAR-03 | Manage Product Listing | Product Management | **Primary actor: Farmer.** Creates, updates, archives, and submits product listings with name, category, unit, price, images, and publishing data. |
| UC-FAR-04 | Manage Product Batch and Traceability | Product Traceability | **Primary actor: Farmer.** Creates batches linked to crop seasons, records harvest/expiry/quantity, and requests system generation of a QR traceability identifier. |
| UC-FAR-05 | Manage Pre-Order Campaign | Farmer Sales Management | **Primary actor: Farmer.** Opens, updates, or closes a pre-order campaign for an upcoming crop season according to target quantity and dates. |
| UC-FAR-06 | Process Order | Farmer Sales Management | **Primary actor: Farmer.** Confirms or rejects an order, packs it, and records handover/delivery status with a reason where necessary. |
| UC-FAR-07 | Submit Certificate Dossier | Certification | **Primary actor: Farmer.** Uploads VietGAP, GlobalGAP, Organic, or other approved certificate evidence for verification. |
| UC-FAR-08 | Manage Offtake Contract | B2B Contract Management | **Primary actor: Farmer.** Reviews, accepts, signs, or requests termination of an offtake contract proposal. |
| UC-FAR-09 | View Revenue and Cash-Flow Dashboard | Farmer Analytics | **Primary actor: Farmer.** Views retail/B2B sales, revenue, and cash-flow information by crop season and payment stage. |
| UC-ADM-01 | View Platform Dashboard and Pending Work | Administration | **Primary actor: Administrator.** Views platform KPIs, GMV/revenue, user growth, orders requiring coordination, and pending approval queues. |
| UC-ADM-02 | Approve or Reject KYC Account | Administration | **Primary actor: Administrator.** Reviews and decides KYC applications submitted by Farmers and Buyer B2B accounts. |
| UC-ADM-03 | Manage User Account Status | Administration | **Primary actor: Administrator.** Searches user records and suspends, unlocks, activates, or deactivates accounts according to policy. |
| UC-ADM-04 | Moderate Product Listing | Administration | **Primary actor: Administrator.** Reviews product images, descriptions, origin information, and approval status before publication or removal. |
| UC-ADM-05 | Manage Product Categories | Administration | **Primary actor: Administrator.** Creates, updates, reorders, activates, or deactivates the product-category hierarchy. |
| UC-ADM-06 | Resolve Complaint | Administration | **Primary actor: Administrator.** Reviews complaint evidence, records a resolution, and updates the complaint and related order status. |
| UC-ADM-07 | Manage Promotions and Vouchers | Administration | **Primary actor: Administrator.** Configures voucher code, discount rule, minimum order value, usage limit, validity period, and activation status. |
| UC-ADM-08 | Moderate Product Review | Administration | **Primary actor: Administrator.** Reviews reported/violating product reviews and removes or retains them in accordance with policy. |
| UC-ADM-09 | Manage Subscription Tiers | Administration | **Primary actor: Administrator.** Configures membership price, feature entitlement, transaction limits, and tier availability. |

## 4. Scope decisions required before final SRS approval

1. Decide whether Guest can scan a QR code and view traceability. The current primary specification assigns it to Consumer, while public traceability would also support Guest.
2. Decide whether Consumer can cancel an order and confirm delivery receipt; define exact status transitions and deadlines if included.
3. Confirm whether credit, crop insurance, logistics-partner operations, AI supply-demand reports, and quality-standard master data are Phase 1 requirements. They should not appear in the SRS UC catalog without actor goals, acceptance criteria, and a defined integration boundary.
