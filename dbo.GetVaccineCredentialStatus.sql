/****** Object:  StoredProcedure [dbo].[GetVaccineCredentialStatus]    Script Date: 9/22/2022 2:24:19 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   PROC [dbo].[GetVaccineCredentialStatus] @FirstName [NVARCHAR](160),@LastName [NVARCHAR](160),@DateOfBirth [NVARCHAR](50),@PhoneNumber [NVARCHAR](50),@EmailAddress [NVARCHAR](255) AS
BEGIN
	SET NOCOUNT ON;
	BEGIN TRY
	
		DECLARE @msg VARCHAR(256) = N'Failed on case -1';
		DECLARE @sql NVARCHAR(MAX);
		DECLARE @crlf CHAR(2) = CHAR(13)+CHAR(10);
		DECLARE @comma CHAR(1) = CHAR(44)
		DECLARE @debug INT = 0
		DECLARE @logStart DATETIME = ((GETDATE() AT TIME ZONE N'UTC') AT TIME ZONE 'Pacific Standard Time');
		DECLARE @logEnd DATETIME;
		DECLARE @logMatchKey INTEGER = -1;
        DECLARE @paramString NVARCHAR(255)
		DECLARE @procName SYSNAME = N'GetVaccineCredentialStatus'
		DECLARE @UserID INTEGER = Null;
		
		SET @PhoneNumber = REPLACE(@PhoneNumber,N'-',N'');
        SET @paramString = '"' + ISNULL(@FirstName,'') + '"|"'+ ISNULL(@LastName,'') + '"|"'+ ISNULL(@DateOfBirth,'') + '"|"' + ISNULL(@PhoneNumber,'') + '"|"'+ ISNULL(@EmailAddress,'') + '"'

		; WITH rslt
          AS (
        SELECT cv.[ASIIS_PAT_ID]
          FROM [dbo].[CovidVaccines] cv
		  
		 WHERE @FirstName = REPLACE(cv.[PAT_FIRST_NAME],CHAR(160),'')
		   AND @LastName = REPLACE(cv.[PAT_LAST_NAME],CHAR(160),'')
		   AND 0 = DATEDIFF(DAY,@DateOfBirth,cv.[PAT_BIRTH_DATE])
		   AND ( CHARINDEX(@PhoneNumber,cv.[PHONE_LIST]) > 0
		      OR @EmailAddress = cv.[ADDRESS_EMAIL]
			   ) 
             )

		SELECT @UserID = [ASIIS_PAT_ID]
			 , @msg = N'Matched - strict'
		  FROM rslt;

        SELECT [UserID] = @UserID
             , [msg] = @msg;
		     
        IF @debug = 1
		BEGIN
           SET @logEnd = ((GETDATE() AT TIME ZONE N'UTC') AT TIME ZONE 'Pacific Standard Time');
          EXEC [dbo].[WriteVaxLog]
               @procName
             , @logStart 
             , @logEnd
             , @UserID
             , @paramString
             , @msg
             ;
        END

	END TRY

	BEGIN CATCH
        SET @logEnd = ((GETDATE() AT TIME ZONE N'UTC') AT TIME ZONE 'Pacific Standard Time');
		SET @msg = N'ERROR [' + CAST(ERROR_NUMBER() AS VARCHAR(10)) + '] : ' + error_message();
        IF @debug = 1
            EXEC [dbo].[WriteVaxLog]
				 @procName
			   , @logStart 
			   , @logEnd
			   , @UserID
			   , @paramString
			   , @msg
			   ;

		RAISERROR(@msg,16,1);
	END CATCH
END
