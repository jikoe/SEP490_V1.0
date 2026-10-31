# Product listing and retail cart alignment

The implementation follows Report-3 sections II, III and V and the corresponding Class/Sequence pages. Existing frontend headers and navigation are preserved. Test cases and expected results are unchanged; only references to removed legacy types/methods were updated.

## Product listings

- `FarmerProductController` retains `/api/farmer/products` endpoints. `FarmerProductService` coordinates validation, ownership/version checks, listing history, review status and visibility. `ProductListingRules` separates content edits from price-only edits.
- Shared add/edit validation covers required text, active categories, positive retail price, paired wholesale fields, precision/length limits, 1–10 distinct uploaded JPG/JPEG/PNG images and exactly one primary image. Used selling units stay locked. The frontend sends a string from its 31 fixed dropdown choices; existing units remain readable.
- New listings and rejected resubmissions return the Vietnamese equivalent of MSG71. Other updates return MSG72. Content changes request renewed review and hide the listing. Price-only updates preserve review/visibility and send no new review notification.
- Each accepted submission atomically queues an in-system notification and a separate email row for each active Content Moderator. `ProductNotificationWorker` sends only `ProductListingReview` emails through `NotificationGateway`; failed email deliveries retry without repeating submission or delivered channels. An eligible Content Moderator account must exist to receive these messages.
- Submission history and previous rejection reasons are retained. Review results, current eligible stock and related batches are read-only. Publication revalidates the listing; hiding preserves existing orders/history.

## Email configuration

Product-review SMTP uses the same `GmailOptions` as the existing Gmail service. The `.env` loader runs before `CreateBuilder`. Keep the sender and application password in `AgriSustain/.env`:

```dotenv
Gmail__SenderAddress=YOUR_GMAIL_ADDRESS
Gmail__AppPassword=YOUR_GMAIL_APP_PASSWORD
```

`EmailSettings__FrontendBaseUrl` may override the default `http://localhost:5173` for review links. Gmail SMTP uses port 587 with TLS. The email worker starts by default; `ProductNotifications__EnableWorker=false` disables delivery for isolated verification. The existing Gmail OTP service is unchanged. `EmailSettings` now contains only the frontend base URL; old SMTP credential keys are no longer used.

The application was verified to authenticate to Gmail over TLS and its actual `NotificationGateway` successfully submitted the test email to the recipient explicitly authorized in this chat. SMTP acceptance does not itself verify inbox placement.

## Retail cart

- `RetailCartController` retains `/api/cart`. `RetailCartService` coordinates changes; `CartAvailabilityService` owns quantity increments, sale eligibility and reconciliation; `CartPricingService` computes current per-line prices and selected-item subtotal.
- Only identical product-and-batch lines merge. Catalogue quick-add picks one eligible actual batch; obsolete multi-batch splitting was removed. Explicit batch selection remains available in product details.
- Invalid quantities preserve the last valid value. Excess quantities are capped to the greatest allowed quantity within stock. Unavailable lines are deselected and remain removable. Accepted changes persist and totals refresh.
- Checkout handover rechecks selected lines and current prices. Adjustments or changed price policies keep the customer in the cart for review and another click. Only validated selection proceeds. No cart operation creates orders, reserves/deducts stock, initiates payments or consumes discounts/points.
- The two checkout price expressions sharing the handover were aligned to the same per-line threshold; other checkout logic was not changed.

## Compatibility and verification

Controllers and dependency injection now use `FarmerProductService` and `RetailCartService` directly. Obsolete forwarding services and the old controller assembly marker were deleted. Existing tests reference the current types and method names; their cases, assertions and expected results are unchanged.

Existing Commerce unit tests: 93 passed. Existing component API integration tests: 51 passed (real routing/auth/binding/services with persistence doubles, no live database mutation). Frontend production build passed. Test cases and frontend header/sidebar layouts were preserved. Isolated browser preview checked product validation/dropdown/price-only save, unavailable cart controls, invalid quantity retention and review-before-checkout. Its screenshots use fixture data, not live database records.

The local SQL Server connection timed out during a read-only inspection. End-to-end verification of the database notification queue must therefore be repeated with the configured SQL Server available. No live database records were changed during these checks.
