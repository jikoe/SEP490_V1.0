# Commerce alignment

Scope: UC-FAR-03, UC-FAR-06 and UC-RC-01. Header and sidebar unchanged.

Bank entities and mappings were refreshed from the existing AgriSustain database (SupportedBank,
UserBankAccount, RefundTransaction, FarmerPayout and User). No database migration is required or supplied.
The default saved account and an active SupportedBank are required before a paid rejection.
Refund destinations are snapshotted on RefundTransaction; there is no SellerOrderId column on that table.
PaymentInvoiceAllocation links a refund to its seller order. Legacy single-seller payments can use the stable
farmer-reject request key. Ambiguous unallocated multi-seller payments must not be refunded as a whole order.

PaymentGatewayProxy is an explicit Development-only simulated transfer adapter, as authorized for the demo.
It validates the request and returns a stable DEMO reference before the completed refund is recorded.
This adapter never transfers actual funds. Real deployment requires a provider adapter.

Order notifications are saved atomically with fulfillment changes. OrderNotificationWorker sends email only
after commit, retries Failed/stale Sending records, and leaves successful channels alone. The SMTP transport
uses Gmail__AppPassword. No test email is sent by building or running unit tests.

The cart uses reviewToken to reject stale prices/selection before navigating to checkout. Unit choices are
fixed in frontend and validated again by backend. Existing test source has been retained. Older tests that accept the free-text unit bag or require 15-character rejection reasons need a separate update to match the specification.
