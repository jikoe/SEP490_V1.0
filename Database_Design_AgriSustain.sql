-- AgriSustain SQL Server 2019+ fresh-install schema. Not an upgrade migration.
-- Run in an empty database with a Vietnamese-compatible case-insensitive collation.
-- Timestamps are UTC. State changes and checkout must use application transactions.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE [accounts] (
  [account_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [email] varchar(255) UNIQUE NOT NULL,
  [phone_number] varchar(30) UNIQUE NOT NULL,
  [password_hash] varchar(255) NOT NULL,
  [role] nvarchar(30) NOT NULL,
  [status] nvarchar(30) NOT NULL DEFAULT 'pending',
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [full_name] nvarchar(50),
  [avatar_url] nvarchar(max),
  [email_verified_at] datetime2(3),
  [phone_verified_at] datetime2(3),
  [failed_login_count] int NOT NULL DEFAULT (0),
  [locked_until] datetime2(3),
  [terms_accepted_at] datetime2(3),
  [version] rowversion NOT NULL,
  CONSTRAINT [CK_accounts_1] CHECK (failed_login_count >= 0),
  CONSTRAINT [CK_accounts_2] CHECK (LEN(full_name) BETWEEN 2 AND 50),
  CONSTRAINT [CK_accounts_3] CHECK (role IN (N'Admin', N'Farmer', N'Buyer', N'Consumer', N'Bank', N'Certifier')),
  CONSTRAINT [CK_accounts_4] CHECK (status IN (N'pending', N'approved', N'rejected', N'suspended'))
)
GO

CREATE TABLE [farmers] (
  [farmer_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier UNIQUE NOT NULL,
  [farm_name] nvarchar(100) NOT NULL,
  [representative_name] nvarchar(50) NOT NULL,
  [address] nvarchar(200) NOT NULL,
  [region] nvarchar(150),
  [farm_size_hectares] decimal(12,2) NOT NULL,
  [reputation_score] decimal(3,2) NOT NULL DEFAULT (5),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [latitude] decimal(9,6),
  [longitude] decimal(9,6),
  [farm_type] nvarchar(30) NOT NULL,
  [short_description] nvarchar(300),
  [farm_photo_url] nvarchar(max),
  CONSTRAINT [CK_farmers_1] CHECK (reputation_score BETWEEN 0 AND 5),
  CONSTRAINT [CK_farmers_2] CHECK (farm_size_hectares > 0),
  CONSTRAINT [CK_farmers_3] CHECK (latitude BETWEEN -90 AND 90),
  CONSTRAINT [CK_farmers_4] CHECK (longitude BETWEEN -180 AND 180),
  CONSTRAINT [CK_farmers_5] CHECK (farm_type IN (N'individual', N'cooperative'))
)
GO

CREATE TABLE [buyers] (
  [buyer_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier UNIQUE NOT NULL,
  [company_name] nvarchar(255) NOT NULL,
  [tax_code] varchar(50) UNIQUE NOT NULL,
  [business_license_url] nvarchar(max),
  [address] nvarchar(500),
  [buyer_type] nvarchar(50) NOT NULL,
  [reputation_score] decimal(3,2) NOT NULL DEFAULT (5),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_buyers_1] CHECK (reputation_score BETWEEN 0 AND 5),
  CONSTRAINT [CK_buyers_2] CHECK (buyer_type IN (N'Supermarket', N'ProcessingPlant', N'Exporter', N'Restaurant'))
)
GO

CREATE TABLE [consumers] (
  [consumer_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier UNIQUE NOT NULL,
  [full_name] nvarchar(50) NOT NULL,
  [gender] nvarchar(20),
  [birth_date] date,
  [reward_points_balance] int NOT NULL DEFAULT (0),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_consumers_1] CHECK (reward_points_balance >= 0),
  CONSTRAINT [CK_consumers_2] CHECK (LEN(full_name) BETWEEN 2 AND 50),
  CONSTRAINT [CK_consumers_3] CHECK (gender IN (N'male', N'female', N'other'))
)
GO

CREATE TABLE [bank_partners] (
  [bank_partner_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier UNIQUE NOT NULL,
  [bank_name] nvarchar(255) NOT NULL,
  [license_number] varchar(100) UNIQUE NOT NULL,
  [contact_person] nvarchar(255),
  [max_credit_limit] decimal(18,2),
  [interest_rate_min] decimal(5,2),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_bank_partners_1] CHECK (interest_rate_min BETWEEN 0 AND 100),
  CONSTRAINT [CK_bank_partners_2] CHECK (max_credit_limit >= 0)
)
GO

CREATE TABLE [certification_bodies] (
  [certification_body_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier UNIQUE NOT NULL,
  [organization_name] nvarchar(255) NOT NULL,
  [accreditation_code] varchar(100) UNIQUE NOT NULL,
  [website] nvarchar(max),
  [contact_email] nvarchar(255),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE TABLE [categories] (
  [category_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [parent_category_id] uniqueidentifier,
  [category_name] nvarchar(100) NOT NULL,
  [description] nvarchar(500),
  [icon_url] nvarchar(max),
  [is_active] bit NOT NULL DEFAULT (1),
  [priority] int NOT NULL DEFAULT (0),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [version] rowversion NOT NULL,
  CONSTRAINT [CK_categories_1] CHECK (priority >= 0),
  CONSTRAINT [CK_categories_2] CHECK (LEN(category_name) BETWEEN 2 AND 100),
  CONSTRAINT [CK_categories_3] CHECK (parent_category_id IS NULL OR parent_category_id <> category_id)
)
GO

CREATE TABLE [products] (
  [product_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [farmer_id] uniqueidentifier NOT NULL,
  [category_id] uniqueidentifier NOT NULL,
  [product_name] nvarchar(255) NOT NULL,
  [description] nvarchar(max),
  [unit] nvarchar(30) NOT NULL,
  [retail_price] decimal(18,2) NOT NULL,
  [b2b_price] decimal(18,2),
  [approval_status] nvarchar(30) NOT NULL DEFAULT 'pending',
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [is_active] bit NOT NULL DEFAULT (1),
  [archived_at] datetime2(3),
  [origin] nvarchar(200) NOT NULL,
  [specifications] nvarchar(max),
  [view_count] bigint NOT NULL DEFAULT (0),
  CONSTRAINT [CK_products_1] CHECK (retail_price > 0),
  CONSTRAINT [CK_products_2] CHECK (b2b_price > 0),
  CONSTRAINT [CK_products_3] CHECK (view_count >= 0),
  CONSTRAINT [CK_products_5] CHECK (approval_status IN (N'pending', N'approved', N'rejected'))
)
GO

CREATE TABLE [crop_seasons] (
  [crop_season_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [farmer_id] uniqueidentifier NOT NULL,
  [season_name] nvarchar(255) NOT NULL,
  [crop_type] nvarchar(100) NOT NULL,
  [area_hectares] decimal(12,2),
  [planting_date] date,
  [expected_harvest_date] date,
  [expected_yield_tons] decimal(14,3),
  [status] nvarchar(30) NOT NULL DEFAULT 'planned',
  [actual_yield_tons] decimal(14,3),
  CONSTRAINT [CK_crop_seasons_1] CHECK (area_hectares > 0),
  CONSTRAINT [CK_crop_seasons_2] CHECK (expected_yield_tons >= 0),
  CONSTRAINT [CK_crop_seasons_3] CHECK (actual_yield_tons >= 0),
  CONSTRAINT [CK_crop_seasons_4] CHECK (expected_harvest_date >= planting_date),
  CONSTRAINT [CK_crop_seasons_5] CHECK (status IN (N'planned', N'in_progress', N'harvested', N'completed', N'cancelled'))
)
GO

CREATE TABLE [farming_logs] (
  [farming_log_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [crop_season_id] uniqueidentifier NOT NULL,
  [activity_type] nvarchar(50) NOT NULL,
  [log_date] date NOT NULL,
  [inputs_used] nvarchar(max),
  [quantity_used] decimal(14,3),
  [image_url] nvarchar(max),
  [performed_by] nvarchar(255),
  CONSTRAINT [CK_farming_logs_1] CHECK (quantity_used >= 0),
  CONSTRAINT [CK_farming_logs_2] CHECK (activity_type IN (N'Sowing', N'Fertilizing', N'PesticideApp', N'Irrigation', N'Harvest'))
)
GO

CREATE TABLE [product_batches] (
  [product_batch_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [product_id] uniqueidentifier NOT NULL,
  [crop_season_id] uniqueidentifier NOT NULL,
  [batch_code] varchar(100) UNIQUE NOT NULL,
  [harvest_date] date NOT NULL,
  [quantity_on_hand] decimal(14,3) NOT NULL,
  [quantity_reserved] decimal(14,3) NOT NULL DEFAULT (0),
  [status] nvarchar(30) NOT NULL DEFAULT 'active',
  [expiry_date] date,
  [qr_code_url] nvarchar(max),
  CONSTRAINT [CK_product_batches_1] CHECK (quantity_on_hand >= quantity_reserved),
  CONSTRAINT [CK_product_batches_2] CHECK (quantity_reserved >= 0),
  CONSTRAINT [CK_product_batches_3] CHECK (expiry_date >= harvest_date),
  CONSTRAINT [CK_product_batches_4] CHECK (status IN (N'active', N'exhausted', N'expired', N'inactive'))
)
GO

CREATE TABLE [certifications] (
  [certification_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [farmer_id] uniqueidentifier NOT NULL,
  [certification_body_id] uniqueidentifier,
  [certificate_type] nvarchar(50) NOT NULL,
  [certificate_number] varchar(100) UNIQUE NOT NULL,
  [issued_date] date NOT NULL,
  [expiry_date] date NOT NULL,
  [document_url] nvarchar(max) NOT NULL,
  [verification_status] nvarchar(30) NOT NULL DEFAULT 'pending',
  [reviewed_by_account_id] uniqueidentifier,
  [reviewed_at] datetime2(3),
  [review_reason] nvarchar(1000),
  CONSTRAINT [CK_certifications_1] CHECK (expiry_date >= issued_date),
  CONSTRAINT [CK_certifications_2] CHECK (certificate_type IN (N'VietGAP', N'GlobalGAP', N'Organic', N'OCOP')),
  CONSTRAINT [CK_certifications_3] CHECK (verification_status IN (N'pending', N'verified', N'rejected', N'expired'))
)
GO

CREATE TABLE [crop_insurances] (
  [crop_insurance_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [crop_season_id] uniqueidentifier NOT NULL,
  [policy_number] varchar(100) UNIQUE NOT NULL,
  [insurer_name] nvarchar(255) NOT NULL,
  [coverage_amount] decimal(18,2) NOT NULL,
  [premium_amount] decimal(18,2) NOT NULL,
  [start_date] date NOT NULL,
  [end_date] date NOT NULL,
  [status] nvarchar(30) NOT NULL,
  CONSTRAINT [CK_crop_insurances_1] CHECK (end_date >= start_date),
  CONSTRAINT [CK_crop_insurances_2] CHECK (coverage_amount >= 0),
  CONSTRAINT [CK_crop_insurances_3] CHECK (premium_amount >= 0),
  CONSTRAINT [CK_crop_insurances_4] CHECK (status IN (N'active', N'claimed', N'expired', N'cancelled'))
)
GO

CREATE TABLE [offtake_contracts] (
  [offtake_contract_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [buyer_id] uniqueidentifier NOT NULL,
  [farmer_id] uniqueidentifier NOT NULL,
  [crop_season_id] uniqueidentifier NOT NULL,
  [contract_code] varchar(100) UNIQUE NOT NULL,
  [agreed_quantity_tons] decimal(14,3) NOT NULL,
  [agreed_price_per_ton] decimal(18,2) NOT NULL,
  [total_value] decimal(18,2) NOT NULL,
  [deposit_amount] decimal(18,2),
  [status] nvarchar(30) NOT NULL DEFAULT 'draft',
  [signed_date] date,
  [version_number] int NOT NULL DEFAULT (1),
  [terms] nvarchar(max) NOT NULL,
  [starts_at] datetime2(3),
  [ends_at] datetime2(3),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_offtake_contracts_1] CHECK (agreed_quantity_tons > 0),
  CONSTRAINT [CK_offtake_contracts_2] CHECK (agreed_price_per_ton > 0),
  CONSTRAINT [CK_offtake_contracts_3] CHECK (deposit_amount IS NULL OR deposit_amount <= total_value),
  CONSTRAINT [CK_offtake_contracts_4] CHECK (ends_at >= starts_at),
  CONSTRAINT [CK_offtake_contracts_5] CHECK (version_number > 0),
  CONSTRAINT [CK_offtake_contracts_6] CHECK (total_value >= 0),
  CONSTRAINT [CK_offtake_contracts_7] CHECK (status IN (N'draft', N'pending_sign', N'active', N'fulfilled', N'defaulted', N'terminated'))
)
GO

CREATE TABLE [subscription_tiers] (
  [subscription_tier_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [tier_name] nvarchar(100) UNIQUE NOT NULL,
  [monthly_fee] decimal(18,2) NOT NULL,
  [features_json] nvarchar(max),
  [is_active] bit NOT NULL DEFAULT (1),
  CONSTRAINT [CK_subscription_tiers_1] CHECK (features_json IS NULL OR ISJSON(features_json) = 1),
  CONSTRAINT [CK_subscription_tiers_2] CHECK (monthly_fee >= 0)
)
GO

CREATE TABLE [buyer_subscriptions] (
  [subscription_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [buyer_id] uniqueidentifier NOT NULL,
  [subscription_tier_id] uniqueidentifier NOT NULL,
  [start_date] date NOT NULL,
  [end_date] date NOT NULL,
  [auto_renew] bit NOT NULL DEFAULT (0),
  [status] nvarchar(30) NOT NULL,
  CONSTRAINT [CK_buyer_subscriptions_1] CHECK (end_date >= start_date),
  CONSTRAINT [CK_buyer_subscriptions_2] CHECK (status IN (N'active', N'cancelled', N'expired'))
)
GO

CREATE TABLE [orders] (
  [order_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier NOT NULL,
  [order_code] varchar(100) UNIQUE NOT NULL,
  [order_type] nvarchar(30) NOT NULL,
  [subtotal] decimal(18,2) NOT NULL,
  [delivery_fee] decimal(18,2) NOT NULL DEFAULT (0),
  [voucher_discount] decimal(18,2) NOT NULL DEFAULT (0),
  [points_discount] decimal(18,2) NOT NULL DEFAULT (0),
  [redeemed_points] int NOT NULL DEFAULT (0),
  [final_amount] decimal(18,2) NOT NULL,
  [payment_method] nvarchar(30) NOT NULL,
  [order_status] nvarchar(30) NOT NULL DEFAULT 'pending_payment',
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [checkout_key] varchar(100) NOT NULL,
  [recipient_name] nvarchar(100) NOT NULL,
  [recipient_phone] varchar(30) NOT NULL,
  [province] nvarchar(100) NOT NULL,
  [district] nvarchar(100) NOT NULL,
  [ward] nvarchar(100) NOT NULL,
  [address_detail] nvarchar(255) NOT NULL,
  [delivery_method] nvarchar(30) NOT NULL,
  [order_notes] nvarchar(300),
  [payment_expires_at] datetime2(3) NOT NULL,
  [cancellation_reason] nvarchar(1000),
  [version] rowversion NOT NULL,
  CONSTRAINT [CK_orders_1] CHECK (subtotal > 0),
  CONSTRAINT [CK_orders_2] CHECK (redeemed_points >= 0),
  CONSTRAINT [CK_orders_3] CHECK (final_amount = subtotal + delivery_fee - voucher_discount - points_discount),
  CONSTRAINT [CK_orders_4] CHECK (payment_expires_at > created_at),
  CONSTRAINT [CK_orders_5] CHECK (delivery_fee >= 0),
  CONSTRAINT [CK_orders_6] CHECK (voucher_discount >= 0),
  CONSTRAINT [CK_orders_7] CHECK (points_discount >= 0),
  CONSTRAINT [CK_orders_8] CHECK (order_type IN (N'retail', N'preorder', N'b2b_wholesale')),
  CONSTRAINT [CK_orders_9] CHECK (order_status IN (N'pending_payment', N'paid', N'processing', N'shipping', N'partially_completed', N'completed', N'partially_cancelled', N'cancelled')),
  CONSTRAINT [CK_orders_10] CHECK (payment_method IN (N'VietQR', N'PayOS', N'Escrow')),
  CONSTRAINT [CK_orders_11] CHECK (delivery_method IN (N'standard', N'fresh_express'))
)
GO

CREATE TABLE [order_items] (
  [order_item_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [order_id] uniqueidentifier NOT NULL,
  [product_id] uniqueidentifier NOT NULL,
  [product_batch_id] uniqueidentifier,
  [quantity] decimal(14,3) NOT NULL,
  [unit_price] decimal(18,2) NOT NULL,
  [subtotal] decimal(18,2) NOT NULL,
  [fulfillment_id] uniqueidentifier NOT NULL,
  [preorder_commitment_id] uniqueidentifier,
  [product_name_snapshot] nvarchar(255) NOT NULL,
  [unit_snapshot] nvarchar(30) NOT NULL,
  [batch_code_snapshot] varchar(100),
  CONSTRAINT [CK_order_items_1] CHECK (quantity > 0),
  CONSTRAINT [CK_order_items_2] CHECK (unit_price > 0),
  CONSTRAINT [CK_order_items_3] CHECK (subtotal = ROUND(quantity * unit_price, 2))
)
GO

CREATE TABLE [financial_transactions] (
  [financial_transaction_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [order_id] uniqueidentifier,
  [offtake_contract_id] uniqueidentifier,
  [gateway_transaction_ref] varchar(255),
  [gateway_name] nvarchar(50) NOT NULL,
  [amount] decimal(18,2) NOT NULL,
  [currency] char(3) NOT NULL DEFAULT 'VND',
  [transaction_type] nvarchar(30) NOT NULL,
  [status] nvarchar(30) NOT NULL DEFAULT 'pending',
  [transaction_time] datetime2(3) NOT NULL,
  [subscription_id] uniqueidentifier,
  [purchase_id] uniqueidentifier,
  [commitment_id] uniqueidentifier,
  [original_transaction_id] uniqueidentifier,
  [refund_fulfillment_id] uniqueidentifier,
  [idempotency_key] varchar(100) UNIQUE NOT NULL,
  [paid_at] datetime2(3),
  [next_check_at] datetime2(3),
  [expires_at] datetime2(3),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_financial_transactions_1] CHECK (amount > 0),
  CONSTRAINT [CK_financial_transactions_2] CHECK ((CASE WHEN order_id IS NULL THEN 0 ELSE 1 END + CASE WHEN offtake_contract_id IS NULL THEN 0 ELSE 1 END + CASE WHEN subscription_id IS NULL THEN 0 ELSE 1 END + CASE WHEN purchase_id IS NULL THEN 0 ELSE 1 END + CASE WHEN commitment_id IS NULL THEN 0 ELSE 1 END) = 1),
  CONSTRAINT [CK_financial_transactions_3] CHECK ((transaction_type = N'Refund' AND original_transaction_id IS NOT NULL) OR (transaction_type <> N'Refund' AND original_transaction_id IS NULL)),
  CONSTRAINT [CK_financial_transactions_4] CHECK (refund_fulfillment_id IS NULL OR (transaction_type = N'Refund' AND order_id IS NOT NULL)),
  CONSTRAINT [CK_financial_transactions_5] CHECK (gateway_name IN (N'VietQR', N'PayOS', N'SePay')),
  CONSTRAINT [CK_financial_transactions_6] CHECK (transaction_type IN (N'OrderPayment', N'Deposit', N'EscrowRelease', N'Refund', N'SubscriptionPayment', N'ReportPayment')),
  CONSTRAINT [CK_financial_transactions_7] CHECK (status IN (N'pending', N'success', N'failed'))
)
GO

CREATE TABLE [market_reports] (
  [report_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [title] nvarchar(500) NOT NULL,
  [region] nvarchar(150),
  [crop_category] nvarchar(150),
  [price_amount] decimal(18,2) NOT NULL,
  [published_date] date NOT NULL,
  [file_url] nvarchar(max) NOT NULL,
  [is_free_for_subscribers] bit NOT NULL DEFAULT (0),
  CONSTRAINT [CK_market_reports_1] CHECK (price_amount >= 0)
)
GO

CREATE TABLE [report_purchases] (
  [purchase_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [buyer_id] uniqueidentifier NOT NULL,
  [market_report_id] uniqueidentifier NOT NULL,
  [price_paid] decimal(18,2) NOT NULL,
  [purchased_at] datetime2(3) NOT NULL,
  CONSTRAINT [CK_report_purchases_1] CHECK (price_paid >= 0)
)
GO

CREATE TABLE [product_reviews] (
  [product_review_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [order_item_id] uniqueidentifier UNIQUE NOT NULL,
  [account_id] uniqueidentifier NOT NULL,
  [rating_stars] smallint NOT NULL,
  [comment_text] nvarchar(max),
  [is_hidden_by_admin] bit NOT NULL DEFAULT (0),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [moderated_by_account_id] uniqueidentifier,
  [moderation_reason] nvarchar(1000),
  [moderated_at] datetime2(3),
  CONSTRAINT [CK_product_reviews_1] CHECK (rating_stars BETWEEN 1 AND 5)
)
GO

CREATE TABLE [complaints] (
  [complaint_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [order_id] uniqueidentifier NOT NULL,
  [complainant_account_id] uniqueidentifier NOT NULL,
  [reason_category] nvarchar(50) NOT NULL,
  [description_text] nvarchar(max) NOT NULL,
  [evidence_image_url] nvarchar(max),
  [resolution_status] nvarchar(30) NOT NULL DEFAULT 'submitted',
  [admin_note] nvarchar(max),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [fulfillment_id] uniqueidentifier,
  [handled_by_account_id] uniqueidentifier,
  [resolved_at] datetime2(3),
  CONSTRAINT [CK_complaints_1] CHECK (reason_category IN (N'Defective', N'Crushed', N'WrongItem', N'Expired')),
  CONSTRAINT [CK_complaints_2] CHECK (resolution_status IN (N'submitted', N'under_review', N'refunded', N'rejected', N'closed'))
)
GO

CREATE TABLE [notifications] (
  [notification_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier NOT NULL,
  [type] nvarchar(50) NOT NULL,
  [channel] nvarchar(30) NOT NULL,
  [title] nvarchar(255) NOT NULL,
  [content] nvarchar(max) NOT NULL,
  [status] nvarchar(30) NOT NULL,
  [read_at] datetime2(3),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [retry_count] int NOT NULL DEFAULT (0),
  [next_retry_at] datetime2(3),
  [sent_at] datetime2(3),
  [last_error] nvarchar(1000),
  CONSTRAINT [CK_notifications_1] CHECK (retry_count BETWEEN 0 AND 3),
  CONSTRAINT [CK_notifications_2] CHECK (channel IN (N'in_app', N'email', N'system')),
  CONSTRAINT [CK_notifications_3] CHECK (type IN (N'Order', N'Contract', N'System', N'Credit', N'Subscription', N'Certificate', N'KYC')),
  CONSTRAINT [CK_notifications_4] CHECK (status IN (N'pending', N'sent', N'failed', N'permanent_failure'))
)
GO

CREATE TABLE [credit_applications] (
  [credit_application_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [applicant_account_id] uniqueidentifier NOT NULL,
  [bank_partner_id] uniqueidentifier NOT NULL,
  [requested_amount] decimal(18,2) NOT NULL,
  [loan_term_months] int NOT NULL,
  [purpose_description] nvarchar(max),
  [status] nvarchar(30) NOT NULL DEFAULT 'submitted',
  [application_date] date NOT NULL,
  CONSTRAINT [CK_credit_applications_1] CHECK (loan_term_months > 0),
  CONSTRAINT [CK_credit_applications_2] CHECK (requested_amount >= 0),
  CONSTRAINT [CK_credit_applications_3] CHECK (status IN (N'submitted', N'under_review', N'approved', N'disbursed', N'rejected'))
)
GO

CREATE TABLE [verification_challenges] (
  [challenge_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier,
  [target_email] varchar(255) NOT NULL,
  [purpose] nvarchar(30) NOT NULL,
  [code_hash] varchar(255) NOT NULL,
  [expires_at] datetime2(3) NOT NULL,
  [verified_at] datetime2(3),
  [consumed_at] datetime2(3),
  [invalidated_at] datetime2(3),
  [failed_attempts] int NOT NULL DEFAULT (0),
  [resend_available_at] datetime2(3) NOT NULL,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_verification_challenges_1] CHECK (failed_attempts >= 0),
  CONSTRAINT [CK_verification_challenges_2] CHECK (expires_at > created_at),
  CONSTRAINT [CK_verification_challenges_3] CHECK (consumed_at IS NULL OR verified_at IS NOT NULL),
  CONSTRAINT [CK_verification_challenges_4] CHECK (purpose IN (N'registration', N'password_reset', N'email_change', N'phone_change'))
)
GO

CREATE TABLE [auth_sessions] (
  [session_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier NOT NULL,
  [token_hash] varchar(255) UNIQUE NOT NULL,
  [expires_at] datetime2(3) NOT NULL,
  [revoked_at] datetime2(3),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_auth_sessions_1] CHECK (expires_at > created_at)
)
GO

CREATE TABLE [kyc_applications] (
  [application_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier NOT NULL,
  [proposed_profile_json] nvarchar(max) NOT NULL,
  [status] nvarchar(30) NOT NULL DEFAULT 'submitted',
  [reviewer_account_id] uniqueidentifier,
  [reason] nvarchar(1000),
  [reviewed_at] datetime2(3),
  [submitted_at] datetime2(3) NOT NULL,
  [version] rowversion NOT NULL,
  CONSTRAINT [CK_kyc_applications_1] CHECK (ISJSON(proposed_profile_json) = 1),
  CONSTRAINT [CK_kyc_applications_2] CHECK (reviewed_at IS NULL OR reviewer_account_id IS NOT NULL),
  CONSTRAINT [CK_kyc_applications_3] CHECK (status IN (N'submitted', N'under_review', N'approved', N'rejected', N'changes_requested'))
)
GO

CREATE TABLE [kyc_documents] (
  [document_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [application_id] uniqueidentifier NOT NULL,
  [document_type] nvarchar(50) NOT NULL,
  [file_url] nvarchar(max) NOT NULL,
  [uploaded_at] datetime2(3) NOT NULL
)
GO

CREATE TABLE [account_addresses] (
  [address_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier NOT NULL,
  [recipient_name] nvarchar(100) NOT NULL,
  [recipient_phone] varchar(30) NOT NULL,
  [province] nvarchar(100) NOT NULL,
  [district] nvarchar(100) NOT NULL,
  [ward] nvarchar(100) NOT NULL,
  [address_detail] nvarchar(255) NOT NULL,
  [is_default] bit NOT NULL DEFAULT (0),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE TABLE [product_images] (
  [image_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [product_id] uniqueidentifier NOT NULL,
  [image_url] nvarchar(max) NOT NULL,
  [alt_text] nvarchar(255),
  [sort_order] int NOT NULL DEFAULT (0),
  CONSTRAINT [CK_product_images_1] CHECK (sort_order >= 0)
)
GO

CREATE TABLE [product_moderation_history] (
  [moderation_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [product_id] uniqueidentifier NOT NULL,
  [actor_account_id] uniqueidentifier NOT NULL,
  [action] nvarchar(30) NOT NULL,
  [reason] nvarchar(1000),
  [occurred_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_product_moderation_history_1] CHECK (action NOT IN (N'reject', N'delist') OR LEN(LTRIM(RTRIM(reason))) > 0 AND reason IS NOT NULL),
  CONSTRAINT [CK_product_moderation_history_2] CHECK (action IN (N'approve', N'reject', N'delist', N'resubmit'))
)
GO

CREATE TABLE [preorder_campaigns] (
  [campaign_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [product_id] uniqueidentifier NOT NULL,
  [crop_season_id] uniqueidentifier NOT NULL,
  [target_quantity] decimal(14,3) NOT NULL,
  [reserved_quantity] decimal(14,3) NOT NULL DEFAULT (0),
  [price_per_unit] decimal(18,2) NOT NULL,
  [deposit_percent] decimal(5,2) NOT NULL DEFAULT (0),
  [opens_at] datetime2(3) NOT NULL,
  [closes_at] datetime2(3) NOT NULL,
  [status] nvarchar(30) NOT NULL DEFAULT 'scheduled',
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_preorder_campaigns_1] CHECK (target_quantity > 0),
  CONSTRAINT [CK_preorder_campaigns_2] CHECK (reserved_quantity BETWEEN 0 AND target_quantity),
  CONSTRAINT [CK_preorder_campaigns_3] CHECK (price_per_unit > 0),
  CONSTRAINT [CK_preorder_campaigns_4] CHECK (deposit_percent BETWEEN 0 AND 100),
  CONSTRAINT [CK_preorder_campaigns_5] CHECK (closes_at > opens_at),
  CONSTRAINT [CK_preorder_campaigns_6] CHECK (status IN (N'scheduled', N'active', N'closed', N'completed', N'cancelled'))
)
GO

CREATE TABLE [preorder_commitments] (
  [commitment_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [campaign_id] uniqueidentifier NOT NULL,
  [buyer_id] uniqueidentifier NOT NULL,
  [quantity] decimal(14,3) NOT NULL,
  [unit_price] decimal(18,2) NOT NULL,
  [deposit_amount] decimal(18,2) NOT NULL DEFAULT (0),
  [status] nvarchar(30) NOT NULL DEFAULT 'pending_payment',
  [checkout_key] varchar(100) NOT NULL,
  [expires_at] datetime2(3) NOT NULL,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_preorder_commitments_1] CHECK (quantity > 0),
  CONSTRAINT [CK_preorder_commitments_2] CHECK (unit_price > 0),
  CONSTRAINT [CK_preorder_commitments_3] CHECK (expires_at > created_at),
  CONSTRAINT [CK_preorder_commitments_4] CHECK (deposit_amount >= 0),
  CONSTRAINT [CK_preorder_commitments_5] CHECK (status IN (N'pending_payment', N'confirmed', N'fulfilled', N'cancelled', N'expired'))
)
GO

CREATE TABLE [carts] (
  [cart_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [account_id] uniqueidentifier UNIQUE NOT NULL,
  [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE TABLE [cart_items] (
  [cart_item_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [cart_id] uniqueidentifier NOT NULL,
  [product_id] uniqueidentifier NOT NULL,
  [product_batch_id] uniqueidentifier,
  [quantity] decimal(14,3) NOT NULL,
  [price_when_added] decimal(18,2) NOT NULL,
  [added_at] datetime2(3) NOT NULL,
  CONSTRAINT [CK_cart_items_1] CHECK (quantity > 0),
  CONSTRAINT [CK_cart_items_2] CHECK (price_when_added > 0)
)
GO

CREATE TABLE [order_fulfillments] (
  [fulfillment_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [order_id] uniqueidentifier NOT NULL,
  [farmer_id] uniqueidentifier NOT NULL,
  [status] nvarchar(30) NOT NULL DEFAULT 'pending_payment',
  [subtotal] decimal(18,2) NOT NULL,
  [delivery_fee] decimal(18,2) NOT NULL DEFAULT (0),
  [voucher_discount] decimal(18,2) NOT NULL DEFAULT (0),
  [points_discount] decimal(18,2) NOT NULL DEFAULT (0),
  [platform_fee] decimal(18,2) NOT NULL DEFAULT (0),
  [packaging_requirements] nvarchar(500),
  [requested_delivery_at] datetime2(3),
  [cancellation_reason] nvarchar(1000),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [version] rowversion NOT NULL,
  CONSTRAINT [CK_order_fulfillments_1] CHECK (subtotal > 0),
  CONSTRAINT [CK_order_fulfillments_2] CHECK (voucher_discount + points_discount <= subtotal + delivery_fee),
  CONSTRAINT [CK_order_fulfillments_3] CHECK (status <> N'cancelled' OR (cancellation_reason IS NOT NULL AND LEN(LTRIM(RTRIM(cancellation_reason))) > 0)),
  CONSTRAINT [CK_order_fulfillments_4] CHECK (delivery_fee >= 0),
  CONSTRAINT [CK_order_fulfillments_5] CHECK (points_discount >= 0),
  CONSTRAINT [CK_order_fulfillments_6] CHECK (platform_fee >= 0),
  CONSTRAINT [CK_order_fulfillments_7] CHECK (status IN (N'pending_payment', N'confirmed', N'processing', N'awaiting_shipper', N'in_transit', N'delivered', N'cancelled'))
)
GO

CREATE TABLE [shipments] (
  [shipment_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [fulfillment_id] uniqueidentifier UNIQUE NOT NULL,
  [carrier_name] nvarchar(100),
  [tracking_code] varchar(100),
  [cold_chain_required] bit NOT NULL DEFAULT (0),
  [status] nvarchar(30) NOT NULL DEFAULT 'pending',
  [packed_at] datetime2(3),
  [handed_over_at] datetime2(3),
  [estimated_delivery_at] datetime2(3),
  [delivered_at] datetime2(3),
  CONSTRAINT [CK_shipments_1] CHECK (status IN (N'pending', N'packed', N'in_transit', N'delivered', N'cancelled'))
)
GO

CREATE TABLE [order_status_history] (
  [history_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [order_id] uniqueidentifier NOT NULL,
  [fulfillment_id] uniqueidentifier,
  [actor_account_id] uniqueidentifier,
  [old_status] nvarchar(30),
  [new_status] nvarchar(30) NOT NULL,
  [reason] nvarchar(1000),
  [occurred_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE TABLE [inventory_reservations] (
  [reservation_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [order_item_id] uniqueidentifier UNIQUE NOT NULL,
  [product_batch_id] uniqueidentifier NOT NULL,
  [quantity] decimal(14,3) NOT NULL,
  [status] nvarchar(30) NOT NULL DEFAULT 'reserved',
  [expires_at] datetime2(3) NOT NULL,
  [consumed_at] datetime2(3),
  [released_at] datetime2(3),
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_inventory_reservations_1] CHECK (quantity > 0),
  CONSTRAINT [CK_inventory_reservations_2] CHECK (expires_at > created_at),
  CONSTRAINT [CK_inventory_reservations_3] CHECK ((status = N'reserved' AND consumed_at IS NULL AND released_at IS NULL) OR (status = N'consumed' AND consumed_at IS NOT NULL AND released_at IS NULL) OR (status = N'released' AND consumed_at IS NULL AND released_at IS NOT NULL)),
  CONSTRAINT [CK_inventory_reservations_4] CHECK (status IN (N'reserved', N'consumed', N'released'))
)
GO

CREATE TABLE [contract_signatures] (
  [signature_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [offtake_contract_id] uniqueidentifier NOT NULL,
  [signer_account_id] uniqueidentifier NOT NULL,
  [signer_role] nvarchar(20) NOT NULL,
  [contract_version] int NOT NULL,
  [method] nvarchar(30) NOT NULL,
  [document_hash] varchar(128) NOT NULL,
  [signed_at] datetime2(3) NOT NULL,
  CONSTRAINT [CK_contract_signatures_1] CHECK (contract_version > 0),
  CONSTRAINT [CK_contract_signatures_2] CHECK (signer_role IN (N'farmer', N'buyer')),
  CONSTRAINT [CK_contract_signatures_3] CHECK (method IN (N'electronic_confirmation', N'digital_signature'))
)
GO

CREATE TABLE [contract_milestones] (
  [milestone_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [offtake_contract_id] uniqueidentifier NOT NULL,
  [title] nvarchar(200) NOT NULL,
  [obligation] nvarchar(max),
  [due_at] datetime2(3) NOT NULL,
  [amount_due] decimal(18,2),
  [quantity_due_tons] decimal(14,3),
  [status] nvarchar(30) NOT NULL DEFAULT 'pending',
  [completed_at] datetime2(3),
  [invoice_code] varchar(100),
  [acceptance_note] nvarchar(1000),
  CONSTRAINT [CK_contract_milestones_1] CHECK (amount_due >= 0),
  CONSTRAINT [CK_contract_milestones_2] CHECK (quantity_due_tons >= 0),
  CONSTRAINT [CK_contract_milestones_3] CHECK (status IN (N'pending', N'completed', N'cancelled'))
)
GO

CREATE TABLE [vouchers] (
  [voucher_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [code] varchar(50) UNIQUE NOT NULL,
  [name] nvarchar(100) NOT NULL,
  [discount_type] nvarchar(20) NOT NULL,
  [discount_value] decimal(18,2) NOT NULL,
  [max_discount] decimal(18,2),
  [minimum_subtotal] decimal(18,2) NOT NULL DEFAULT (0),
  [usage_limit] int NOT NULL,
  [per_account_limit] int NOT NULL DEFAULT (1),
  [starts_at] datetime2(3) NOT NULL,
  [ends_at] datetime2(3) NOT NULL,
  [is_active] bit NOT NULL DEFAULT (1),
  [restrict_products] bit NOT NULL DEFAULT (0),
  [restrict_accounts] bit NOT NULL DEFAULT (0),
  CONSTRAINT [CK_vouchers_1] CHECK (discount_value > 0),
  CONSTRAINT [CK_vouchers_2] CHECK (minimum_subtotal >= 0),
  CONSTRAINT [CK_vouchers_3] CHECK (usage_limit > 0),
  CONSTRAINT [CK_vouchers_4] CHECK (per_account_limit > 0),
  CONSTRAINT [CK_vouchers_5] CHECK (ends_at > starts_at),
  CONSTRAINT [CK_vouchers_6] CHECK (discount_type <> N'percent' OR discount_value <= 100),
  CONSTRAINT [CK_vouchers_7] CHECK (max_discount >= 0),
  CONSTRAINT [CK_vouchers_8] CHECK (discount_type IN (N'fixed', N'percent'))
)
GO

CREATE TABLE [voucher_products] (
  [voucher_product_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [voucher_id] uniqueidentifier NOT NULL,
  [product_id] uniqueidentifier NOT NULL
)
GO

CREATE TABLE [voucher_accounts] (
  [voucher_account_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [voucher_id] uniqueidentifier NOT NULL,
  [account_id] uniqueidentifier NOT NULL
)
GO

CREATE TABLE [voucher_redemptions] (
  [redemption_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [voucher_id] uniqueidentifier NOT NULL,
  [order_id] uniqueidentifier UNIQUE NOT NULL,
  [account_id] uniqueidentifier NOT NULL,
  [code_snapshot] varchar(50) NOT NULL,
  [discount_amount] decimal(18,2) NOT NULL,
  [status] nvarchar(30) NOT NULL DEFAULT 'reserved',
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_voucher_redemptions_1] CHECK (discount_amount >= 0),
  CONSTRAINT [CK_voucher_redemptions_2] CHECK (status IN (N'reserved', N'consumed', N'released'))
)
GO

CREATE TABLE [reward_point_transactions] (
  [point_transaction_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [consumer_id] uniqueidentifier NOT NULL,
  [order_id] uniqueidentifier,
  [delta] int NOT NULL,
  [reason] nvarchar(30) NOT NULL,
  [idempotency_key] varchar(100) UNIQUE NOT NULL,
  [occurred_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT [CK_reward_point_transactions_1] CHECK (delta <> 0),
  CONSTRAINT [CK_reward_point_transactions_2] CHECK (reason IN (N'earn', N'redeem', N'refund', N'adjustment'))
)
GO

CREATE TABLE [payment_events] (
  [event_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [financial_transaction_id] uniqueidentifier NOT NULL,
  [gateway_name] nvarchar(50) NOT NULL,
  [event_key] varchar(255) NOT NULL,
  [source] nvarchar(30) NOT NULL,
  [verified_at] datetime2(3),
  [received_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [processed_at] datetime2(3),
  [last_error] nvarchar(1000),
  CONSTRAINT [CK_payment_events_1] CHECK (source IN (N'webhook', N'reconciliation'))
)
GO

CREATE TABLE [audit_logs] (
  [audit_log_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [actor_account_id] uniqueidentifier,
  [target_id] uniqueidentifier NOT NULL,
  [entity_type] nvarchar(50) NOT NULL,
  [action] nvarchar(50) NOT NULL,
  [old_value] nvarchar(max),
  [new_value] nvarchar(max),
  [reason] nvarchar(1000),
  [occurred_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE TABLE [complaint_evidence] (
  [evidence_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [complaint_id] uniqueidentifier NOT NULL,
  [file_url] nvarchar(max) NOT NULL,
  [uploaded_at] datetime2(3) NOT NULL
)
GO

CREATE TABLE [review_images] (
  [review_image_id] uniqueidentifier PRIMARY KEY DEFAULT (NEWSEQUENTIALID()),
  [product_review_id] uniqueidentifier NOT NULL,
  [image_url] nvarchar(max) NOT NULL
)
GO

CREATE UNIQUE INDEX [categories_index_0] ON [categories] ("parent_category_id", "category_name")
GO

CREATE INDEX [products_index_1] ON [products] ("category_id", "approval_status")
GO

CREATE INDEX [products_index_2] ON [products] ("farmer_id", "created_at")
GO

CREATE INDEX [farming_logs_index_3] ON [farming_logs] ("crop_season_id", "log_date")
GO

CREATE INDEX [product_batches_index_4] ON [product_batches] ("product_id", "expiry_date")
GO

CREATE INDEX [certifications_index_5] ON [certifications] ("expiry_date", "verification_status")
GO

CREATE INDEX [buyer_subscriptions_index_6] ON [buyer_subscriptions] ("end_date", "status")
GO

CREATE INDEX [orders_index_7] ON [orders] ("account_id", "order_status", "created_at")
GO

CREATE UNIQUE INDEX [orders_index_8] ON [orders] ("account_id", "checkout_key")
GO

CREATE INDEX [orders_index_9] ON [orders] ("order_status", "payment_expires_at")
GO

CREATE INDEX [order_items_index_10] ON [order_items] ("order_id")
GO

CREATE INDEX [financial_transactions_index_11] ON [financial_transactions] ("order_id", "status", "transaction_time")
GO

CREATE UNIQUE INDEX [report_purchases_index_12] ON [report_purchases] ("buyer_id", "market_report_id")
GO

CREATE INDEX [complaints_index_13] ON [complaints] ("resolution_status", "created_at")
GO

CREATE INDEX [notifications_index_14] ON [notifications] ("account_id", "read_at")
GO

CREATE INDEX [notifications_index_15] ON [notifications] ("status", "next_retry_at")
GO

CREATE INDEX [verification_challenges_index_16] ON [verification_challenges] ("target_email", "purpose", "created_at")
GO

CREATE INDEX [verification_challenges_index_17] ON [verification_challenges] ("expires_at")
GO

CREATE INDEX [auth_sessions_index_18] ON [auth_sessions] ("account_id", "revoked_at")
GO

CREATE INDEX [kyc_applications_index_19] ON [kyc_applications] ("status", "submitted_at")
GO

CREATE UNIQUE INDEX [product_images_index_20] ON [product_images] ("product_id", "sort_order")
GO

CREATE INDEX [preorder_campaigns_index_21] ON [preorder_campaigns] ("status", "opens_at", "closes_at")
GO

CREATE UNIQUE INDEX [preorder_commitments_index_22] ON [preorder_commitments] ("buyer_id", "checkout_key")
GO

CREATE UNIQUE INDEX [cart_items_index_23] ON [cart_items] ("cart_id", "product_id", "product_batch_id")
GO

CREATE UNIQUE INDEX [order_fulfillments_index_24] ON [order_fulfillments] ("order_id", "farmer_id")
GO

CREATE INDEX [order_fulfillments_index_25] ON [order_fulfillments] ("farmer_id", "status", "created_at")
GO

CREATE UNIQUE INDEX [order_fulfillments_index_26] ON [order_fulfillments] ("fulfillment_id", "order_id")
GO

CREATE INDEX [order_status_history_index_27] ON [order_status_history] ("order_id", "occurred_at")
GO

CREATE INDEX [inventory_reservations_index_28] ON [inventory_reservations] ("status", "expires_at")
GO

CREATE UNIQUE INDEX [contract_signatures_index_29] ON [contract_signatures] ("offtake_contract_id", "contract_version", "signer_role")
GO

CREATE INDEX [contract_milestones_index_30] ON [contract_milestones] ("status", "due_at")
GO

CREATE UNIQUE INDEX [voucher_products_index_31] ON [voucher_products] ("voucher_id", "product_id")
GO

CREATE UNIQUE INDEX [voucher_accounts_index_32] ON [voucher_accounts] ("voucher_id", "account_id")
GO

CREATE INDEX [voucher_redemptions_index_33] ON [voucher_redemptions] ("voucher_id", "account_id", "status")
GO

CREATE INDEX [reward_point_transactions_index_34] ON [reward_point_transactions] ("consumer_id", "occurred_at")
GO

CREATE UNIQUE INDEX [payment_events_index_35] ON [payment_events] ("gateway_name", "event_key")
GO

CREATE INDEX [audit_logs_index_36] ON [audit_logs] ("entity_type", "target_id", "occurred_at")
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Admin, Farmer, Buyer, Consumer, Bank, Certifier',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'accounts',
@level2type = N'Column', @level2name = 'role';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'CHECK: 0.00..5.00',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'farmers',
@level2type = N'Column', @level2name = 'reputation_score';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Supermarket, ProcessingPlant, Exporter, Restaurant',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'buyers',
@level2type = N'Column', @level2name = 'buyer_type';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'CHECK: 0.00..5.00',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'buyers',
@level2type = N'Column', @level2name = 'reputation_score';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Sowing, Fertilizing, PesticideApp, Irrigation, Harvest',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'farming_logs',
@level2type = N'Column', @level2name = 'activity_type';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'QuantityAvailable = quantity_on_hand - quantity_reserved. Exclude expired/inactive stock.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'product_batches';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'VietGAP, GlobalGAP, Organic, OCOP',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'certifications',
@level2type = N'Column', @level2name = 'certificate_type';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Active, Claimed, Expired, Cancelled',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'crop_insurances',
@level2type = N'Column', @level2name = 'status';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Validate with ISJSON() in SQL Server',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'subscription_tiers',
@level2type = N'Column', @level2name = 'features_json';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Active, Cancelled, Expired',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'buyer_subscriptions',
@level2type = N'Column', @level2name = 'status';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'VietQR, PayOS, Escrow',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'orders',
@level2type = N'Column', @level2name = 'payment_method';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'VietQR, PayOS, SePay',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'financial_transactions',
@level2type = N'Column', @level2name = 'gateway_name';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'OrderPayment, Deposit, EscrowRelease, Refund',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'financial_transactions',
@level2type = N'Column', @level2name = 'transaction_type';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'CHECK: 1..5',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'product_reviews',
@level2type = N'Column', @level2name = 'rating_stars';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Defective, Crushed, WrongItem, Expired',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'complaints',
@level2type = N'Column', @level2name = 'reason_category';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'Registration challenge may have no account yet. Store HMAC/hash only; invalidate old challenges on resend. Reset must verify account and consume once.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'verification_challenges';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'Stores initial/re-KYC proposals before approved Farmer/Buyer profile exists. Apply approved proposal and create/update profile atomically.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'kyc_applications';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'One default address per account via filtered SQL unique index. Checkout copies values to orders.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'account_addresses';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'Capacity reservation belongs to campaign, not harvested stock. Deposit payment targets this commitment.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'preorder_commitments';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'One checkout order, independent fulfillment per farmer. Discounts/fees allocated deterministically; reject/refund only the affected fulfillment.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'order_fulfillments';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'Manual/simulated carrier in Report 3 v1; no external shipping API assumed.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'shipments';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'Checkout increments reserved. Paid reservations remain held; unpaid expiry releases once. Farmer acceptance decrements both on_hand and reserved once.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'inventory_reservations';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'Webhook/Reconcile events have deterministic idempotency keys. Persist safe metadata only; never credentials or OTP payloads.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'payment_events';
GO

EXEC sp_addextendedproperty
@name = N'Table_Description',
@value = 'Append-only to application principal. Exclude passwords, session hashes, OTP and confidential documents from snapshots. Archive via privileged maintenance principal.',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'audit_logs';
GO

EXEC sp_addextendedproperty
@name = N'Column_Description',
@value = 'Polymorphic reference; validate target by entity_type in application',
@level0type = N'Schema', @level0name = 'dbo',
@level1type = N'Table',  @level1name = 'audit_logs',
@level2type = N'Column', @level2name = 'target_id';
GO

ALTER TABLE [farmers] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [buyers] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [consumers] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [bank_partners] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [certification_bodies] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [categories] ADD FOREIGN KEY ([parent_category_id]) REFERENCES [categories] ([category_id])
GO

ALTER TABLE [products] ADD FOREIGN KEY ([farmer_id]) REFERENCES [farmers] ([farmer_id])
GO

ALTER TABLE [products] ADD FOREIGN KEY ([category_id]) REFERENCES [categories] ([category_id])
GO

ALTER TABLE [crop_seasons] ADD FOREIGN KEY ([farmer_id]) REFERENCES [farmers] ([farmer_id])
GO

ALTER TABLE [farming_logs] ADD FOREIGN KEY ([crop_season_id]) REFERENCES [crop_seasons] ([crop_season_id])
GO

ALTER TABLE [product_batches] ADD FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id])
GO

ALTER TABLE [product_batches] ADD FOREIGN KEY ([crop_season_id]) REFERENCES [crop_seasons] ([crop_season_id])
GO

ALTER TABLE [certifications] ADD FOREIGN KEY ([farmer_id]) REFERENCES [farmers] ([farmer_id])
GO

ALTER TABLE [certifications] ADD FOREIGN KEY ([certification_body_id]) REFERENCES [certification_bodies] ([certification_body_id])
GO

ALTER TABLE [certifications] ADD FOREIGN KEY ([reviewed_by_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [crop_insurances] ADD FOREIGN KEY ([crop_season_id]) REFERENCES [crop_seasons] ([crop_season_id])
GO

ALTER TABLE [offtake_contracts] ADD FOREIGN KEY ([buyer_id]) REFERENCES [buyers] ([buyer_id])
GO

ALTER TABLE [offtake_contracts] ADD FOREIGN KEY ([farmer_id]) REFERENCES [farmers] ([farmer_id])
GO

ALTER TABLE [offtake_contracts] ADD FOREIGN KEY ([crop_season_id]) REFERENCES [crop_seasons] ([crop_season_id])
GO

ALTER TABLE [buyer_subscriptions] ADD FOREIGN KEY ([buyer_id]) REFERENCES [buyers] ([buyer_id])
GO

ALTER TABLE [buyer_subscriptions] ADD FOREIGN KEY ([subscription_tier_id]) REFERENCES [subscription_tiers] ([subscription_tier_id])
GO

ALTER TABLE [orders] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [order_items] ADD FOREIGN KEY ([order_id]) REFERENCES [orders] ([order_id])
GO

ALTER TABLE [order_items] ADD FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id])
GO

ALTER TABLE [order_items] ADD FOREIGN KEY ([product_batch_id]) REFERENCES [product_batches] ([product_batch_id])
GO

ALTER TABLE [order_items] ADD FOREIGN KEY ([fulfillment_id]) REFERENCES [order_fulfillments] ([fulfillment_id])
GO

ALTER TABLE [order_items] ADD FOREIGN KEY ([preorder_commitment_id]) REFERENCES [preorder_commitments] ([commitment_id])
GO

ALTER TABLE [financial_transactions] ADD FOREIGN KEY ([order_id]) REFERENCES [orders] ([order_id])
GO

ALTER TABLE [financial_transactions] ADD FOREIGN KEY ([offtake_contract_id]) REFERENCES [offtake_contracts] ([offtake_contract_id])
GO

ALTER TABLE [financial_transactions] ADD FOREIGN KEY ([subscription_id]) REFERENCES [buyer_subscriptions] ([subscription_id])
GO

ALTER TABLE [financial_transactions] ADD FOREIGN KEY ([purchase_id]) REFERENCES [report_purchases] ([purchase_id])
GO

ALTER TABLE [financial_transactions] ADD FOREIGN KEY ([commitment_id]) REFERENCES [preorder_commitments] ([commitment_id])
GO

ALTER TABLE [financial_transactions] ADD FOREIGN KEY ([original_transaction_id]) REFERENCES [financial_transactions] ([financial_transaction_id])
GO

ALTER TABLE [financial_transactions] ADD FOREIGN KEY ([refund_fulfillment_id]) REFERENCES [order_fulfillments] ([fulfillment_id])
GO

ALTER TABLE [report_purchases] ADD FOREIGN KEY ([buyer_id]) REFERENCES [buyers] ([buyer_id])
GO

ALTER TABLE [report_purchases] ADD FOREIGN KEY ([market_report_id]) REFERENCES [market_reports] ([report_id])
GO

ALTER TABLE [product_reviews] ADD FOREIGN KEY ([order_item_id]) REFERENCES [order_items] ([order_item_id])
GO

ALTER TABLE [product_reviews] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [product_reviews] ADD FOREIGN KEY ([moderated_by_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [complaints] ADD FOREIGN KEY ([order_id]) REFERENCES [orders] ([order_id])
GO

ALTER TABLE [complaints] ADD FOREIGN KEY ([complainant_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [complaints] ADD FOREIGN KEY ([fulfillment_id]) REFERENCES [order_fulfillments] ([fulfillment_id])
GO

ALTER TABLE [complaints] ADD FOREIGN KEY ([handled_by_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [notifications] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [credit_applications] ADD FOREIGN KEY ([applicant_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [credit_applications] ADD FOREIGN KEY ([bank_partner_id]) REFERENCES [bank_partners] ([bank_partner_id])
GO

ALTER TABLE [verification_challenges] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [auth_sessions] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [kyc_applications] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [kyc_applications] ADD FOREIGN KEY ([reviewer_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [kyc_documents] ADD FOREIGN KEY ([application_id]) REFERENCES [kyc_applications] ([application_id])
GO

ALTER TABLE [account_addresses] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [product_images] ADD FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id])
GO

ALTER TABLE [product_moderation_history] ADD FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id])
GO

ALTER TABLE [product_moderation_history] ADD FOREIGN KEY ([actor_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [preorder_campaigns] ADD FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id])
GO

ALTER TABLE [preorder_campaigns] ADD FOREIGN KEY ([crop_season_id]) REFERENCES [crop_seasons] ([crop_season_id])
GO

ALTER TABLE [preorder_commitments] ADD FOREIGN KEY ([campaign_id]) REFERENCES [preorder_campaigns] ([campaign_id])
GO

ALTER TABLE [preorder_commitments] ADD FOREIGN KEY ([buyer_id]) REFERENCES [buyers] ([buyer_id])
GO

ALTER TABLE [carts] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [cart_items] ADD FOREIGN KEY ([cart_id]) REFERENCES [carts] ([cart_id])
GO

ALTER TABLE [cart_items] ADD FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id])
GO

ALTER TABLE [cart_items] ADD FOREIGN KEY ([product_batch_id]) REFERENCES [product_batches] ([product_batch_id])
GO

ALTER TABLE [order_fulfillments] ADD FOREIGN KEY ([order_id]) REFERENCES [orders] ([order_id])
GO

ALTER TABLE [order_fulfillments] ADD FOREIGN KEY ([farmer_id]) REFERENCES [farmers] ([farmer_id])
GO

ALTER TABLE [shipments] ADD FOREIGN KEY ([fulfillment_id]) REFERENCES [order_fulfillments] ([fulfillment_id])
GO

ALTER TABLE [order_status_history] ADD FOREIGN KEY ([order_id]) REFERENCES [orders] ([order_id])
GO

ALTER TABLE [order_status_history] ADD FOREIGN KEY ([fulfillment_id]) REFERENCES [order_fulfillments] ([fulfillment_id])
GO

ALTER TABLE [order_status_history] ADD FOREIGN KEY ([actor_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [inventory_reservations] ADD FOREIGN KEY ([order_item_id]) REFERENCES [order_items] ([order_item_id])
GO

ALTER TABLE [inventory_reservations] ADD FOREIGN KEY ([product_batch_id]) REFERENCES [product_batches] ([product_batch_id])
GO

ALTER TABLE [contract_signatures] ADD FOREIGN KEY ([offtake_contract_id]) REFERENCES [offtake_contracts] ([offtake_contract_id])
GO

ALTER TABLE [contract_signatures] ADD FOREIGN KEY ([signer_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [contract_milestones] ADD FOREIGN KEY ([offtake_contract_id]) REFERENCES [offtake_contracts] ([offtake_contract_id])
GO

ALTER TABLE [voucher_products] ADD FOREIGN KEY ([voucher_id]) REFERENCES [vouchers] ([voucher_id])
GO

ALTER TABLE [voucher_products] ADD FOREIGN KEY ([product_id]) REFERENCES [products] ([product_id])
GO

ALTER TABLE [voucher_accounts] ADD FOREIGN KEY ([voucher_id]) REFERENCES [vouchers] ([voucher_id])
GO

ALTER TABLE [voucher_accounts] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [voucher_redemptions] ADD FOREIGN KEY ([voucher_id]) REFERENCES [vouchers] ([voucher_id])
GO

ALTER TABLE [voucher_redemptions] ADD FOREIGN KEY ([order_id]) REFERENCES [orders] ([order_id])
GO

ALTER TABLE [voucher_redemptions] ADD FOREIGN KEY ([account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [reward_point_transactions] ADD FOREIGN KEY ([consumer_id]) REFERENCES [consumers] ([consumer_id])
GO

ALTER TABLE [reward_point_transactions] ADD FOREIGN KEY ([order_id]) REFERENCES [orders] ([order_id])
GO

ALTER TABLE [payment_events] ADD FOREIGN KEY ([financial_transaction_id]) REFERENCES [financial_transactions] ([financial_transaction_id])
GO

ALTER TABLE [audit_logs] ADD FOREIGN KEY ([actor_account_id]) REFERENCES [accounts] ([account_id])
GO

ALTER TABLE [complaint_evidence] ADD FOREIGN KEY ([complaint_id]) REFERENCES [complaints] ([complaint_id])
GO

ALTER TABLE [review_images] ADD FOREIGN KEY ([product_review_id]) REFERENCES [product_reviews] ([product_review_id])
GO


-- SQL Server features which DBML cannot express as ordinary indexes.
CREATE UNIQUE INDEX UX_transactions_gateway_ref
ON financial_transactions(gateway_name, gateway_transaction_ref)
WHERE gateway_transaction_ref IS NOT NULL;
GO
CREATE UNIQUE INDEX UX_addresses_default
ON account_addresses(account_id) WHERE is_default = 1;
GO
CREATE UNIQUE INDEX UX_kyc_open
ON kyc_applications(account_id)
WHERE reviewed_at IS NULL;
GO

-- Prevent cross-order assignment of an order item to another fulfillment.
ALTER TABLE order_items ADD CONSTRAINT FK_items_fulfillment_order
FOREIGN KEY (fulfillment_id, order_id)
REFERENCES order_fulfillments(fulfillment_id, order_id);
GO

-- Immutable terminal financial ledger. Refunds are separate transactions.
CREATE TRIGGER TR_transactions_terminal_immutable
ON financial_transactions AFTER UPDATE, DELETE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (SELECT 1 FROM deleted WHERE status IN (N'success', N'failed'))
    THROW 51001, 'Terminal financial transactions cannot be modified or deleted.', 1;
END;
GO

-- Signatures must belong to the actual parties and current document version.
CREATE TRIGGER TR_signatures_validate_party
ON contract_signatures AFTER INSERT, UPDATE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (
    SELECT 1 FROM inserted s
    JOIN offtake_contracts c ON c.offtake_contract_id = s.offtake_contract_id
    JOIN farmers f ON f.farmer_id = c.farmer_id
    JOIN buyers b ON b.buyer_id = c.buyer_id
    WHERE s.contract_version <> c.version_number
      OR NOT ((s.signer_role = N'farmer' AND s.signer_account_id = f.account_id)
           OR (s.signer_role = N'buyer' AND s.signer_account_id = b.account_id))
  ) THROW 51002, 'Signature does not match contract version or party.', 1;
END;
GO
CREATE TRIGGER TR_signatures_immutable
ON contract_signatures AFTER UPDATE, DELETE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (SELECT 1 FROM deleted)
    THROW 51003, 'Signed records are immutable; create a new contract version.', 1;
END;
GO
CREATE TRIGGER TR_contracts_require_two_signatures
ON offtake_contracts AFTER INSERT, UPDATE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (
    SELECT 1 FROM inserted c
    WHERE c.status IN (N'active', N'fulfilled', N'defaulted')
      AND (SELECT COUNT(*) FROM contract_signatures s
           WHERE s.offtake_contract_id = c.offtake_contract_id
             AND s.contract_version = c.version_number) <> 2
  ) THROW 51004, 'Contract activation requires signatures from both parties.', 1;
  IF EXISTS (
    SELECT 1 FROM inserted i JOIN deleted d
      ON i.offtake_contract_id = d.offtake_contract_id
    WHERE EXISTS (SELECT 1 FROM contract_signatures s
                  WHERE s.offtake_contract_id = d.offtake_contract_id
                    AND s.contract_version = d.version_number)
      AND i.version_number <= d.version_number
      AND (i.terms <> d.terms OR i.agreed_quantity_tons <> d.agreed_quantity_tons
        OR i.agreed_price_per_ton <> d.agreed_price_per_ton
        OR i.total_value <> d.total_value OR i.buyer_id <> d.buyer_id
        OR i.farmer_id <> d.farmer_id OR i.crop_season_id <> d.crop_season_id)
  ) THROW 51005, 'Changing signed commercial terms requires a new version and new signatures.', 1;
END;
GO

-- Reject reviews which are not purchases by the reviewing Consumer.
CREATE TRIGGER TR_reviews_validate_purchase
ON product_reviews AFTER INSERT, UPDATE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (
    SELECT 1 FROM inserted r
    JOIN order_items oi ON oi.order_item_id = r.order_item_id
    JOIN orders o ON o.order_id = oi.order_id
    JOIN accounts a ON a.account_id = r.account_id
    JOIN order_fulfillments f ON f.fulfillment_id = oi.fulfillment_id
    WHERE o.account_id <> r.account_id OR a.role <> N'Consumer'
       OR o.order_status NOT IN (N'completed', N'partially_completed')
       OR f.status <> N'delivered'
  ) THROW 51006, 'Reviews require a completed purchase owned by the Consumer.', 1;
END;
GO

-- Batches and preorders must link products and seasons of the same Farmer.
CREATE TRIGGER TR_batches_validate_farmer
ON product_batches AFTER INSERT, UPDATE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (SELECT 1 FROM inserted x
    JOIN products p ON p.product_id = x.product_id
    JOIN crop_seasons s ON s.crop_season_id = x.crop_season_id
    WHERE p.farmer_id <> s.farmer_id)
    THROW 51007, 'Product and crop season must belong to the same Farmer.', 1;
END;
GO
CREATE TRIGGER TR_campaigns_validate_farmer
ON preorder_campaigns AFTER INSERT, UPDATE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (SELECT 1 FROM inserted x
    JOIN products p ON p.product_id = x.product_id
    JOIN crop_seasons s ON s.crop_season_id = x.crop_season_id
    WHERE p.farmer_id <> s.farmer_id)
    THROW 51008, 'Preorder product and crop season must belong to the same Farmer.', 1;
END;
GO

CREATE TRIGGER TR_items_validate_product
ON order_items AFTER INSERT, UPDATE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (SELECT 1 FROM inserted x
    JOIN products p ON p.product_id = x.product_id
    JOIN order_fulfillments f ON f.fulfillment_id = x.fulfillment_id
    LEFT JOIN product_batches b ON b.product_batch_id = x.product_batch_id
    LEFT JOIN preorder_commitments pc ON pc.commitment_id = x.preorder_commitment_id
    LEFT JOIN preorder_campaigns camp ON camp.campaign_id = pc.campaign_id
    JOIN orders o ON o.order_id = x.order_id
    LEFT JOIN buyers buyer ON buyer.buyer_id = pc.buyer_id
    WHERE p.farmer_id <> f.farmer_id
       OR (b.product_batch_id IS NOT NULL AND b.product_id <> x.product_id)
       OR (pc.commitment_id IS NOT NULL
           AND (camp.product_id <> x.product_id OR buyer.account_id <> o.account_id)))
    THROW 51009, 'Order item must match its Farmer, product, batch and preorder owner.', 1;
END;
GO
CREATE TRIGGER TR_reservations_validate_item
ON inventory_reservations AFTER INSERT, UPDATE AS
BEGIN
  SET NOCOUNT ON;
  IF EXISTS (SELECT 1 FROM inserted r
    JOIN order_items oi ON oi.order_item_id = r.order_item_id
    WHERE oi.product_batch_id IS NULL OR oi.product_batch_id <> r.product_batch_id
       OR oi.quantity <> r.quantity)
    THROW 51010, 'Inventory reservation must match its order item batch and quantity.', 1;
END;
GO

-- Application and maintenance principals must have separate permissions.
-- DENY UPDATE, DELETE on audit_logs/order_status_history/reward_point_transactions
-- to the application principal; archive through a privileged maintenance process.
-- No credentials or database/login creation are included in this script.

CREATE INDEX IX_crop_seasons_farmer_id ON [crop_seasons]([farmer_id]);
GO

CREATE INDEX IX_product_batches_crop_season_id ON [product_batches]([crop_season_id]);
GO

CREATE INDEX IX_certifications_farmer_id ON [certifications]([farmer_id]);
GO

CREATE INDEX IX_certifications_certification_body_id ON [certifications]([certification_body_id]);
GO

CREATE INDEX IX_certifications_reviewed_by_account_id ON [certifications]([reviewed_by_account_id]);
GO

CREATE INDEX IX_crop_insurances_crop_season_id ON [crop_insurances]([crop_season_id]);
GO

CREATE INDEX IX_offtake_contracts_buyer_id ON [offtake_contracts]([buyer_id]);
GO

CREATE INDEX IX_offtake_contracts_farmer_id ON [offtake_contracts]([farmer_id]);
GO

CREATE INDEX IX_offtake_contracts_crop_season_id ON [offtake_contracts]([crop_season_id]);
GO

CREATE INDEX IX_buyer_subscriptions_buyer_id ON [buyer_subscriptions]([buyer_id]);
GO

CREATE INDEX IX_buyer_subscriptions_subscription_tier_id ON [buyer_subscriptions]([subscription_tier_id]);
GO

CREATE INDEX IX_order_items_product_id ON [order_items]([product_id]);
GO

CREATE INDEX IX_order_items_product_batch_id ON [order_items]([product_batch_id]);
GO

CREATE INDEX IX_order_items_fulfillment_id ON [order_items]([fulfillment_id]);
GO

CREATE INDEX IX_order_items_preorder_commitment_id ON [order_items]([preorder_commitment_id]);
GO

CREATE INDEX IX_financial_transactions_offtake_contract_id ON [financial_transactions]([offtake_contract_id]);
GO

CREATE INDEX IX_financial_transactions_subscription_id ON [financial_transactions]([subscription_id]);
GO

CREATE INDEX IX_financial_transactions_purchase_id ON [financial_transactions]([purchase_id]);
GO

CREATE INDEX IX_financial_transactions_commitment_id ON [financial_transactions]([commitment_id]);
GO

CREATE INDEX IX_financial_transactions_original_transaction_id ON [financial_transactions]([original_transaction_id]);
GO

CREATE INDEX IX_financial_transactions_refund_fulfillment_id ON [financial_transactions]([refund_fulfillment_id]);
GO

CREATE INDEX IX_report_purchases_market_report_id ON [report_purchases]([market_report_id]);
GO

CREATE INDEX IX_product_reviews_account_id ON [product_reviews]([account_id]);
GO

CREATE INDEX IX_product_reviews_moderated_by_account_id ON [product_reviews]([moderated_by_account_id]);
GO

CREATE INDEX IX_complaints_order_id ON [complaints]([order_id]);
GO

CREATE INDEX IX_complaints_complainant_account_id ON [complaints]([complainant_account_id]);
GO

CREATE INDEX IX_complaints_fulfillment_id ON [complaints]([fulfillment_id]);
GO

CREATE INDEX IX_complaints_handled_by_account_id ON [complaints]([handled_by_account_id]);
GO

CREATE INDEX IX_credit_applications_applicant_account_id ON [credit_applications]([applicant_account_id]);
GO

CREATE INDEX IX_credit_applications_bank_partner_id ON [credit_applications]([bank_partner_id]);
GO

CREATE INDEX IX_verification_challenges_account_id ON [verification_challenges]([account_id]);
GO

CREATE INDEX IX_kyc_applications_account_id ON [kyc_applications]([account_id]);
GO

CREATE INDEX IX_kyc_applications_reviewer_account_id ON [kyc_applications]([reviewer_account_id]);
GO

CREATE INDEX IX_kyc_documents_application_id ON [kyc_documents]([application_id]);
GO

CREATE INDEX IX_account_addresses_account_id ON [account_addresses]([account_id]);
GO

CREATE INDEX IX_product_moderation_history_product_id ON [product_moderation_history]([product_id]);
GO

CREATE INDEX IX_product_moderation_history_actor_account_id ON [product_moderation_history]([actor_account_id]);
GO

CREATE INDEX IX_preorder_campaigns_product_id ON [preorder_campaigns]([product_id]);
GO

CREATE INDEX IX_preorder_campaigns_crop_season_id ON [preorder_campaigns]([crop_season_id]);
GO

CREATE INDEX IX_preorder_commitments_campaign_id ON [preorder_commitments]([campaign_id]);
GO

CREATE INDEX IX_cart_items_product_id ON [cart_items]([product_id]);
GO

CREATE INDEX IX_cart_items_product_batch_id ON [cart_items]([product_batch_id]);
GO

CREATE INDEX IX_order_status_history_fulfillment_id ON [order_status_history]([fulfillment_id]);
GO

CREATE INDEX IX_order_status_history_actor_account_id ON [order_status_history]([actor_account_id]);
GO

CREATE INDEX IX_inventory_reservations_product_batch_id ON [inventory_reservations]([product_batch_id]);
GO

CREATE INDEX IX_contract_signatures_signer_account_id ON [contract_signatures]([signer_account_id]);
GO

CREATE INDEX IX_contract_milestones_offtake_contract_id ON [contract_milestones]([offtake_contract_id]);
GO

CREATE INDEX IX_voucher_products_product_id ON [voucher_products]([product_id]);
GO

CREATE INDEX IX_voucher_accounts_account_id ON [voucher_accounts]([account_id]);
GO

CREATE INDEX IX_voucher_redemptions_account_id ON [voucher_redemptions]([account_id]);
GO

CREATE INDEX IX_reward_point_transactions_order_id ON [reward_point_transactions]([order_id]);
GO

CREATE INDEX IX_payment_events_financial_transaction_id ON [payment_events]([financial_transaction_id]);
GO

CREATE INDEX IX_audit_logs_actor_account_id ON [audit_logs]([actor_account_id]);
GO

CREATE INDEX IX_complaint_evidence_complaint_id ON [complaint_evidence]([complaint_id]);
GO

CREATE INDEX IX_review_images_product_review_id ON [review_images]([product_review_id]);
GO
