/*
  UC-AUTH-01 / UC-AUTH-03 — align FarmerProfile.VerificationStatus with the SRS state machine:
      NotSubmitted -> PendingApproval -> Approved | Rejected
  Earlier builds wrote:
      'Pending'             at registration (profile never submitted)  -> NotSubmitted
      'PendingVerification' when the farmer pressed "Submit KYC"       -> PendingApproval
  Safe to run more than once.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE dbo.[FarmerProfile]
SET [VerificationStatus] = 'NotSubmitted'
WHERE [VerificationStatus] = 'Pending' AND [ApprovedAt] IS NULL;

UPDATE dbo.[FarmerProfile]
SET [VerificationStatus] = 'PendingApproval'
WHERE [VerificationStatus] = 'PendingVerification';

-- Placeholder values written by the old registration code count as "not filled in".
UPDATE dbo.[FarmerProfile]
SET [Address] = ''
WHERE [Address] = N'Chưa cập nhật địa chỉ' AND [VerificationStatus] = 'NotSubmitted';

COMMIT TRANSACTION;
