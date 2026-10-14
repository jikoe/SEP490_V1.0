# Use Case Catalog

| ID | Use Case | Feature | Use Case Description |
|---|---|---|---|
| UC-AUTH-01 | Register Consumer or Farmer Account | Authentication and Profile Management | **Primary actor: Guest.** Creates a Consumer or Farmer account using a phone number and password, then stores the account with its initial status. |
| UC-AUTH-02 | Register Buyer B2B Account | Authentication and Profile Management | **Primary actor: Guest.** Creates a Buyer B2B account and submits company information, tax code, and legal documents for KYC review. |
| UC-AUTH-03 | Manage Profile | Authentication and Profile Management | **Primary actor: Consumer, Farmer, Buyer B2B.** Completes or updates role-specific profile information, including delivery addresses, farm details, or business information. |
| UC-AUTH-04 | Log In | Authentication and Profile Management | **Primary actor: Consumer, Farmer, Buyer B2B, Administrator.** Authenticates credentials, checks account status, and opens the appropriate role workspace. |
| UC-AUTH-05 | Log Out | Authentication and Profile Management | **Primary actor: Authenticated User.** Ends the active session and invalidates the active token or session. |
| UC-AUTH-06 | Change Password | Authentication and Profile Management | **Primary actor: Authenticated User.** Changes the password after validating the current password and password policy. |
| UC-AUTH-07 | Reset Password | Authentication and Profile Management | **Primary actor: Guest or User.** Starts and completes the verified password-reset process for a forgotten password. |
| UC-MKT-01 | Browse Marketplace | Marketplace | **Primary actor: Guest, Consumer, Buyer B2B.** Browses public products and optionally searches or filters them by name, category, supplier, region, season, or certification. |
| UC-MKT-02 | View Product Detail | Marketplace | **Primary actor: Guest, Consumer, Buyer B2B.** Views a product's description, price, available stock or batches, farm information, certificates, and eligible reviews. |
| UC-TRC-01 | View Product Traceability | Traceability | **Primary actor: Consumer.** Enters a batch code or scans a QR code to view the farm, crop season, farming logs, harvest date, and quality certificates. |
| UC-CON-01 | Manage Shopping Cart | Consumer Ordering | **Primary actor: Consumer.** Adds products to a cart, changes quantities, or removes products before ordering. |
| UC-CON-02 | Place Retail Order | Consumer Ordering | **Primary actor: Consumer.** Selects a delivery address, applies eligible reward points, validates stock and price, and creates an order with Pending Payment status. |
| UC-CON-03 | Pay Retail Order | Consumer Payment | **Primary actor: Consumer; secondary actor: VietQR/PayOS Payment Gateway.** Requests a dynamic QR payment and records payment success only after a valid gateway webhook is received. |
| UC-CON-04 | Track Retail Order | Consumer Ordering | **Primary actor: Consumer.** Views order history, payment status, packing and delivery progress, and order details. |
| UC-CON-05 | Submit Product Review | Consumer Engagement | **Primary actor: Consumer.** Submits a star rating and comment for a product after the related order is completed. |
| UC-CON-06 | Submit Order Complaint | Consumer Engagement | **Primary actor: Consumer.** Creates an order complaint with a reason and supporting evidence within the allowed policy period. |
| UC-B2B-01 | Search Suppliers | B2B Procurement | **Primary actor: Buyer B2B.** Searches Farmers and production regions by location, supply capacity, crop season, and certification. |
| UC-B2B-02 | View Supplier Capacity Profile | B2B Procurement | **Primary actor: Buyer B2B.** Views a selected supplier's cultivated area, production capacity, crop calendar, available products, certificates, and reputation information. |
| UC-B2B-03 | Create Wholesale Order | B2B Procurement | **Primary actor: Buyer B2B.** Creates a bulk order with quantity, packaging requirements, and a requested delivery schedule. |
| UC-B2B-04 | Create Pre-Order Commitment | B2B Procurement | **Primary actor: Buyer B2B.** Reserves output from an upcoming crop season under agreed quantity, price, and commitment or deposit terms. |
| UC-B2B-05 | Create Offtake Contract Proposal | B2B Contract Management | **Primary actor: Buyer B2B.** Drafts a long-term offtake proposal containing quantity, price framework, delivery terms, and validity period. |
| UC-B2B-06 | Sign Offtake Contract | B2B Contract Management | **Primary actor: Buyer B2B and Farmer.** Electronically signs or confirms an offtake contract after both parties agree to its terms. |
| UC-B2B-07 | Track B2B Fulfillment | B2B Contract Management | **Primary actor: Buyer B2B.** Tracks contract fulfillment, wholesale delivery milestones, invoices, and related records. |
| UC-B2B-08 | Manage Buyer Subscription | B2B Membership | **Primary actor: Buyer B2B.** Views, selects, renews, or cancels an eligible subscription tier. |
| UC-FAR-01 | Manage Crop Seasons | Farm Management | **Primary actor: Farmer.** Creates and updates crop-season plans, expected harvest dates, estimated and actual yields, and season status. |
| UC-FAR-02 | Record E-Farming Logs | Farm Management | **Primary actor: Farmer.** Records cultivation activities such as irrigation, fertilisation, pest management, and supporting evidence. |
| UC-FAR-03 | Manage Product Listing | Product Management | **Primary actor: Farmer.** Creates, updates, archives, and submits product listings with product name, category, unit, price, images, and publishing information. |
| UC-FAR-04 | Manage Traceability Batches | Product Traceability | **Primary actor: Farmer.** Creates product batches linked to crop seasons, maintains harvest/expiry/stock data, and requests QR traceability identifier generation. |
| UC-FAR-05 | Manage Pre-Order Campaign | Farmer Sales Management | **Primary actor: Farmer.** Opens, updates, or closes a pre-order campaign for an upcoming crop season using target quantity and campaign dates. |
| UC-FAR-06 | Process Order | Farmer Sales Management | **Primary actor: Farmer.** Confirms or rejects an order, packs it, and records handover or delivery status with a reason when required. |
| UC-FAR-07 | Submit Certificate Dossier | Certification | **Primary actor: Farmer.** Uploads VietGAP, GlobalGAP, Organic, or other approved certificate evidence for verification. |
| UC-FAR-08 | Manage Offtake Contract | B2B Contract Management | **Primary actor: Farmer.** Reviews, accepts, signs, or requests termination of an offtake contract. |
| UC-FAR-09 | View Revenue Dashboard | Farmer Analytics | **Primary actor: Farmer.** Views retail and B2B sales, revenue, and cash-flow information by crop season or payment stage. |
| UC-ADM-01 | Monitor Platform Operations | Administration | **Primary actor: Administrator.** Monitors KPIs, GMV or revenue, user growth, orders requiring coordination, and pending work queues. |
| UC-ADM-02 | Review KYC Application | Administration | **Primary actor: Administrator.** Reviews a Farmer or Buyer B2B KYC application and approves or rejects it with a recorded reason. |
| UC-ADM-03 | Manage User Account Status | Administration | **Primary actor: Administrator.** Searches user accounts and activates, suspends, unlocks, or deactivates them according to policy. |
| UC-ADM-04 | Moderate Product Listing | Administration | **Primary actor: Administrator.** Reviews product images, descriptions, origin information, and publication status before or after listing. |
| UC-ADM-05 | Manage Product Categories | Administration | **Primary actor: Administrator.** Creates, updates, reorders, activates, or deactivates the product-category hierarchy. |
| UC-ADM-06 | Resolve Complaint | Administration | **Primary actor: Administrator.** Reviews evidence, records a resolution, and updates the complaint and related order status. |
| UC-ADM-07 | Manage Promotions | Administration | **Primary actor: Administrator.** Configures voucher codes, discount rules, minimum order value, usage limits, validity periods, and activation status. |
| UC-ADM-08 | Moderate Product Review | Administration | **Primary actor: Administrator.** Reviews reported or policy-violating product reviews and decides whether to retain or remove them. |
| UC-ADM-09 | Manage Subscription Tiers | Administration | **Primary actor: Administrator.** Configures tier price, feature entitlement, transaction limits, and availability status. |
