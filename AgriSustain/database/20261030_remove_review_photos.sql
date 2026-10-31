-- Run after deploying the code that no longer uses review photos.
-- Removes review-photo links only; ratings, comments and complaint evidence are retained.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ProductReviewPhoto', N'U') IS NOT NULL
        DROP TABLE dbo.ProductReviewPhoto;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
