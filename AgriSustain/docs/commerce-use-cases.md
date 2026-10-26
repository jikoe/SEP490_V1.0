# UC-FAR-03, UC-FAR-06, UC-RC-01

Implementation keeps the existing single ASP.NET project and React shell. No test project or test files were added. coding-standards.md is not used.

## Run locally

1. Configure the existing ConnectionStrings:MyCnn and JWT settings using your local configuration or environment variables. Do not commit credentials.
2. Run dotnet run --project AgriSustain --launch-profile http from the repository root.
3. Run npm ci then npm run dev inside frontend. Open http://127.0.0.1:5173.
4. Sign in with an actual active, email-verified account. Farmer actions additionally require an approved farmer profile. The existing role demo selector does not issue a JWT and cannot authorize these APIs.

## Screens and endpoints

| Screen (hash route) | API |
| --- | --- |
| /farmer/products, /farmer/products/new, /farmer/products/{id}/edit | /api/farmer/products (options, list, details, create, update, visibility) |
| /farmer/orders, /farmer/orders/{id} | /api/farmer/orders (list, details, confirm, ready, dispatch, reject, delivery-report, evidence) |
| /cart | /api/cart (items, quantity, selection, remove-items, prepare-checkout) |

Product images: POST /api/files/images. Private evidence: POST /api/files/evidence, GET /api/files/evidence/{ownerId}/{fileName}. Multipart field: file. Files are limited to 5 MB; evidence also permits PDF.

The database mapping uses the existing schema with no migrations. Product gallery navigation now supports multiple images. Optimistic checks plus serializable write transactions prevent stale changes and repeated stock consumption. Each order action requires requestKey (UUID) and version returned by the details endpoint.

The cart preparation endpoint revalidates selected products and returns a review token; it creates no order, payment, or stock reservation. Continue shopping returns to the RetailCustomer home (/retail/dashboard). The standalone retail catalog screen and its API have been removed. The cart payment button opens /checkout directly after successful revalidation. Farmer navigation opens product management by default and exposes products and orders in the sidebar. Completing checkout (UC-RC-02), buyer receipt confirmation, staff dispute decisions, refunds, and payouts belong to the corresponding external use cases. Those APIs were not present in the original checkout implementation and were not fabricated here.

Delivery dispatch consumes valid reserved quantities once. Failed delivery/returns create evidence and a support case without restoring saleable inventory. Paid cancellation creates a durable finance review notification and audit record; it does not record a completed refund.

Optional Quantity:Steps configuration maps units to quantity increments (default 1). Timestamps are serialized as UTC; screen date filters use Vietnam time.

## Verification

Backend and frontend builds pass. Database-dependent flows require the configured SQL Server and existing reference/order data. The configured localhost database was unavailable during implementation, so end-to-end persistence and database check-constraint compatibility have not been verified.

