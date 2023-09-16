/****** Object:  StoredProcedure [dbo].[GetVaccineCredential]    Script Date: 9/22/2022 2:23:33 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   PROC [dbo].[GetVaccineCredential] @UserID [INTEGER] AS
BEGIN
	SET NOCOUNT ON;
	BEGIN TRY
	
		DECLARE @msg VARCHAR(256);
		DECLARE @sql NVARCHAR(MAX);
		DECLARE @ord INTEGER = 0;
		DECLARE @json NVARCHAR(MAX);
		DECLARE @crlf CHAR(2) = CHAR(13)+CHAR(10);
		DECLARE @debug INT = 0;
		DECLARE @LastName NVARCHAR(160);
		DECLARE @FirstName NVARCHAR(160);
		DECLARE @MiddleName NVARCHAR(160);
        DECLARE @Suffix NVARCHAR(10);
		DECLARE @BirthDate NVARCHAR(20); -- (yyyy-mm-dd) from DATETIME2
		DECLARE	@VaxCount INTEGER; -- WHILE loop limit
		DECLARE @VaxCode NVARCHAR(20); -- from INTEGER
		DECLARE @VaxDate NVARCHAR(20); -- (yyyy-mm-dd) from DATETIME2
        DECLARE @LotNum NVARCHAR(20);  

		DECLARE @logStart DATETIME = ((GETDATE() AT TIME ZONE N'UTC') AT TIME ZONE 'Pacific Standard Time');
		DECLARE @logEnd DATETIME;
		DECLARE @logRslt INTEGER = -1;
		DECLARE @procName SYSNAME = N'GetVaccineCredential';
        DECLARE @paramString NVARCHAR(255) = CAST(@UserID AS VARCHAR(10));
		
		--   --   --   --   --   --   --
		SET @msg = N'Validate Parameters'; RAISERROR(@msg,10,1) WITH NOWAIT;
		--   --   --   --   --   --   --

		IF OBJECT_ID(N'tempdb..#tmp',N'U') IS NOT NULL DROP TABLE #tmp;

		--   --   --   --   --   --   --
		SET @msg = N'Get values for return elements'; RAISERROR(@msg,10,1) WITH NOWAIT;
		--   --   --   --   --   --   --

		; WITH sfx
          AS ( SELECT [sfxName] = N'JR'
         UNION SELECT [sfxName] = N'SR'
         UNION SELECT [sfxName] = N'I'
         UNION SELECT [sfxName] = N'II'
         UNION SELECT [sfxName] = N'III'
         UNION SELECT [sfxName] = N'IV'
         UNION SELECT [sfxName] = N'V'
         UNION SELECT [sfxName] = N'VI'
         UNION SELECT [sfxName] = N'VII'
         UNION SELECT [sfxName] = N'VIII'
         UNION SELECT [sfxName] = N'IX'
         UNION SELECT [sfxName] = N'X'
             )
        
		--   --   --   --   --   --   --

        , vt
		  AS ( SELECT [ASIIS_VACC_CODE] = 2080, [CDC_VACC_CODE] = 207, [VACCINE_NAME] = N'Moderna'
		 UNION SELECT [ASIIS_VACC_CODE] = 2089, [CDC_VACC_CODE] = 208, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 2090, [CDC_VACC_CODE] = 213, [VACCINE_NAME] = N'UNKNOWN'
		 UNION SELECT [ASIIS_VACC_CODE] = 2091, [CDC_VACC_CODE] = 210, [VACCINE_NAME] = N'AstraZeneca'
		 UNION SELECT [ASIIS_VACC_CODE] = 2092, [CDC_VACC_CODE] = 212, [VACCINE_NAME] = N'Janssen'
		 UNION SELECT [ASIIS_VACC_CODE] = 3002, [CDC_VACC_CODE] = 211, [VACCINE_NAME] = N'Novavax'
		 UNION SELECT [ASIIS_VACC_CODE] = 3003, [CDC_VACC_CODE] = 500, [VACCINE_NAME] = N'Novavax'
		 UNION SELECT [ASIIS_VACC_CODE] = 3004, [CDC_VACC_CODE] = 501, [VACCINE_NAME] = NULL
		 UNION SELECT [ASIIS_VACC_CODE] = 3005, [CDC_VACC_CODE] = 502, [VACCINE_NAME] = N'Bharat Biotech International Limited'
		 UNION SELECT [ASIIS_VACC_CODE] = 3006, [CDC_VACC_CODE] = 503, [VACCINE_NAME] = NULL
		 UNION SELECT [ASIIS_VACC_CODE] = 3007, [CDC_VACC_CODE] = 504, [VACCINE_NAME] = NULL
		 UNION SELECT [ASIIS_VACC_CODE] = 3008, [CDC_VACC_CODE] = 505, [VACCINE_NAME] = NULL
		 UNION SELECT [ASIIS_VACC_CODE] = 3009, [CDC_VACC_CODE] = 506, [VACCINE_NAME] = NULL
		 UNION SELECT [ASIIS_VACC_CODE] = 3010, [CDC_VACC_CODE] = 507, [VACCINE_NAME] = NULL
		 UNION SELECT [ASIIS_VACC_CODE] = 3011, [CDC_VACC_CODE] = 508, [VACCINE_NAME] = NULL
		 UNION SELECT [ASIIS_VACC_CODE] = 3012, [CDC_VACC_CODE] = 509, [VACCINE_NAME] = NULL
		 UNION SELECT [ASIIS_VACC_CODE] = 3013, [CDC_VACC_CODE] = 510, [VACCINE_NAME] = N'Sinopharm-Biotech'
		 UNION SELECT [ASIIS_VACC_CODE] = 3014, [CDC_VACC_CODE] = 511, [VACCINE_NAME] = N'Sinovac'
		 UNION SELECT [ASIIS_VACC_CODE] = 3015, [CDC_VACC_CODE] = 217, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 3016, [CDC_VACC_CODE] = 218, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 3017, [CDC_VACC_CODE] = 219, [VACCINE_NAME] = N'Pfizer'
         UNION SELECT [ASIIS_VACC_CODE] = 3034, [CDC_VACC_CODE] = 517, [VACCINE_NAME] = NULL
         UNION SELECT [ASIIS_VACC_CODE] = 3033, [CDC_VACC_CODE] = 516, [VACCINE_NAME] = NULL
         UNION SELECT [ASIIS_VACC_CODE] = 3032, [CDC_VACC_CODE] = 515, [VACCINE_NAME] = NULL
         UNION SELECT [ASIIS_VACC_CODE] = 3031, [CDC_VACC_CODE] = 514, [VACCINE_NAME] = NULL
         UNION SELECT [ASIIS_VACC_CODE] = 3030, [CDC_VACC_CODE] = 513, [VACCINE_NAME] = NULL
         UNION SELECT [ASIIS_VACC_CODE] = 3029, [CDC_VACC_CODE] = 512, [VACCINE_NAME] = N'Medicago'
         UNION SELECT [ASIIS_VACC_CODE] = 3028, [CDC_VACC_CODE] = 228, [VACCINE_NAME] = N'Moderna'
         UNION SELECT [ASIIS_VACC_CODE] = 3027, [CDC_VACC_CODE] = 227, [VACCINE_NAME] = N'Moderna'
         UNION SELECT [ASIIS_VACC_CODE] = 3026, [CDC_VACC_CODE] = 221, [VACCINE_NAME] = N'Moderna'
         UNION SELECT [ASIIS_VACC_CODE] = 3025, [CDC_VACC_CODE] = 225, [VACCINE_NAME] = N'Sanofi Pasteur'
         UNION SELECT [ASIIS_VACC_CODE] = 3024, [CDC_VACC_CODE] = 226, [VACCINE_NAME] = N'Sanofi Pasteur'
		 UNION SELECT [ASIIS_VACC_CODE] = 3035, [CDC_VACC_CODE] = 229, [VACCINE_NAME] = N'Moderna'
		 UNION SELECT [ASIIS_VACC_CODE] = 3036, [CDC_VACC_CODE] = 300, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 3037, [CDC_VACC_CODE] = 301, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 3038, [CDC_VACC_CODE] = 230, [VACCINE_NAME] = N'Moderna'
		 UNION SELECT [ASIIS_VACC_CODE] = 3039, [CDC_VACC_CODE] = 302, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 3050, [CDC_VACC_CODE] = 308, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 3051, [CDC_VACC_CODE] = 309, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 3052, [CDC_VACC_CODE] = 310, [VACCINE_NAME] = N'Pfizer'
		 UNION SELECT [ASIIS_VACC_CODE] = 3053, [CDC_VACC_CODE] = 311, [VACCINE_NAME] = N'Moderna'
		 UNION SELECT [ASIIS_VACC_CODE] = 3054, [CDC_VACC_CODE] = 312, [VACCINE_NAME] = N'Moderna'
		 UNION SELECT [ASIIS_VACC_CODE] = 3055, [CDC_VACC_CODE] = 313, [VACCINE_NAME] = N'Moderna'

			 )

		--   --   --   --   --   --   --

			 , vm  -- Vaccination Master (specific to UserID for COVID)
		  AS ( 
        SELECT vm.[ASIIS_PAT_ID_PTR]
			 , vm.[VACC_DATE]
			 , vm.[ASIIS_FAC_ID]
             , [LotNum] = ISNULL(vm.[LOT_NUM],N'N/A')
			 , [VaxCode] = vt.[CDC_VACC_CODE]
			 , [ord] = ROW_NUMBER() OVER (ORDER BY vm.[VACC_DATE] DESC)
          FROM [dbo].[H33_VACCINATION_MASTER] vm
		  JOIN vt
		    ON vm.[ASIIS_VACC_CODE] = vt.[ASIIS_VACC_CODE]
		   AND vm.[ASIIS_PAT_ID_PTR] = @UserID
		   AND vm.[DELETION_DATE] IS NULL
		     )

		--   --   --   --   --   --   --

		SELECT [UserID] = pm.[ASIIS_PAT_ID]
		     , [LastName] = pm.[PAT_LAST_NAME]
			 , [FirstName] = pm.[PAT_FIRST_NAME]
			 , [MiddleName] = pm.[PAT_MIDDLE_NAME]
             , [Suffix] = ISNULL(sfx.[sfxName],N'')
			 , [BirthDate] = ISNULL(CONVERT(NVARCHAR(20),pm.[PAT_BIRTH_DATE] ,23),N'')
			 , vm.[VaxCode]
             , vm.[LotNum]
			 , [VaxDate] = CONVERT(NVARCHAR(20),vm.[VACC_DATE] ,23)
			 , vm.[ord]
		  INTO #tmp
		  FROM [dbo].[H33_PATIENT_MASTER] pm
		  JOIN vm
		    ON pm.[ASIIS_PAT_ID] = vm.[ASIIS_PAT_ID_PTR]
          LEFT JOIN sfx
            ON sfx.[sfxName] = pm.[PAT_SUFFIX]

		--   --   --   --   --   --   --

		; WITH hdr
		  AS (
		SELECT [VaxCount] = MAX([ord])
             , [LastName]
             , [FirstName]
             , [Suffix]
             , [MiddleName]
			 , [BirthDate]
		  FROM #tmp
		 GROUP BY [LastName]
             , [FirstName]
             , [MiddleName]
             , [Suffix]
			 , [BirthDate]
		     )
		SELECT @VaxCount = [VaxCount]
		     , @LastName = [LastName]
		     , @FirstName = [FirstName]
		     , @MiddleName = [MiddleName]
             , @Suffix = [Suffix]
			 , @BirthDate = [BirthDate]
		  FROM hdr;

		--   --   --   --   --   --   --
		SET @msg = N'Return the json expression of the result set'; RAISERROR(@msg,10,1) WITH NOWAIT;
		--   --   --   --   --   --   --

		SET @json = N'
{
  "vc": {
    "type": [
      "https://smarthealth.cards#health-card",
      "https://smarthealth.cards#immunization",
      "https://smarthealth.cards#covid19"
    ],
    "credentialSubject": {
      "fhirVersion": "4.0.1",
      "fhirBundle": {
        "resourceType": "Bundle",
        "type": "collection",
        "entry": [
          {
            "fullUrl": "resource:0",
            "resource": {
              "resourceType": "Patient",
              "name": [
                {
                  "family": "' + @LastName + N'",
                  "given": [
                    "' + @FirstName + ISNULL(N'","' + @MiddleName + N'"',N'"') + N'
                  ],
                  "suffix": ["' + @Suffix + '"]
                }
              ],
              "birthDate": "' + @BirthDate + N'"
            }
          }'

		WHILE @ord < @VaxCount
		BEGIN
			SET @ord = @ord + 1;

			SELECT @VaxCode = [VaxCode]
			     , @VaxDate = [VaxDate]
                 , @LotNum = [LotNum]
			  FROM #tmp
			 WHERE [ord] = @ord;

			SET @json = @json + N',
          {
            "fullUrl": "resource:' + CAST(@ord AS NVARCHAR(10)) + N'",
            "resource": {
              "resourceType": "Immunization",
              "status": "completed",
              "lotNumber":"' + @LotNum + '",
              "vaccineCode": {
                "coding": [
                  {
                    "system":"http://hl7.org/fhir/sid/cvx",
                    "code": "' + @VaxCode + N'"
                  }
                ]
              },
              "patient": {
                "reference": "resource:0"
              },
              "occurrenceDateTime": "' + @VaxDate + N'"
            }
          }'
        END

        SET @json = @json + N'
        ]
      }
    }
  }
}' ;
		IF @debug = 1 RAISERROR(@json,10,1) WITH NOWAIT;
		SET @json = REPLACE(REPLACE(@json, @crlf, ''),N'  ',N'');
		SELECT [UserVaccineCredential] = @json;

		IF @debug = 1		
		BEGIN
			SET @logRslt = 1;  -- success
			SET @logEnd = ((GETDATE() AT TIME ZONE N'UTC') AT TIME ZONE 'Pacific Standard Time');
			EXEC [dbo].[WriteVaxLog]
				 @procName
			   , @logStart 
			   , @logEnd
			   , @logRslt
			   , @paramString
			   , @json
         END

	END TRY

	BEGIN CATCH
        SET @logEnd = ((GETDATE() AT TIME ZONE N'UTC') AT TIME ZONE 'Pacific Standard Time');
		SET @msg = N'ERROR [' + CAST(ERROR_NUMBER() AS VARCHAR(10)) + '] : ' + error_message();

		IF @debug = 1		
		BEGIN
			EXEC [dbo].[WriteVaxLog]
				 @procName
			   , @logStart 
			   , @logEnd
			   , @logRslt
			   , @paramString
			   , @msg
        END

		RAISERROR(@msg,16,1);
	END CATCH
END
