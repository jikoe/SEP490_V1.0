/*
  UC-FIN-03 Process Refund — record how Finance paid a refund back to the customer.
      RefundMethod: 'BankTransfer' | 'GatewayRefund' (PayOS/VietQR merchant portal)
  The column is NULL for refunds that are still Pending and for refunds settled before this
  change; older rows are not backfilled because their method was never recorded.
  Already included in Update_DB.sql and Create_DB_First_Time.sql. Safe to run more than once.
*/
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.[RefundTransaction]', N'RefundMethod') IS NULL
    ALTER TABLE dbo.[RefundTransaction] ADD [RefundMethod] varchar(50) NULL;
