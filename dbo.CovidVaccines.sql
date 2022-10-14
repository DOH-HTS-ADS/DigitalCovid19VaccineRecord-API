﻿CREATE OR ALTER VIEW dbo.CovidVaccines AS
WITH vt AS (
    SELECT            2080 AS ASIIS_VACC_CODE, 207 AS CDC_VACC_CODE, N'Moderna' AS VACCINE_NAME
    UNION
    SELECT        2089 AS ASIIS_VACC_CODE, 208 AS CDC_VACC_CODE, N'Pfizer' AS VACCINE_NAME
    UNION
    SELECT        2090 AS ASIIS_VACC_CODE, 213 AS CDC_VACC_CODE, N'UNKNOWN' AS VACCINE_NAME
    UNION
    SELECT        2091 AS ASIIS_VACC_CODE, 210 AS CDC_VACC_CODE, N'AstraZeneca' AS VACCINE_NAME
    UNION
    SELECT        2092 AS ASIIS_VACC_CODE, 212 AS CDC_VACC_CODE, N'Janssen' AS VACCINE_NAME
    UNION
    SELECT        3002 AS ASIIS_VACC_CODE, 211 AS CDC_VACC_CODE, N'Novavax' AS VACCINE_NAME
    UNION
    SELECT        3003 AS ASIIS_VACC_CODE, 500 AS CDC_VACC_CODE, N'Novavax' AS VACCINE_NAME
    UNION
    SELECT        3004 AS ASIIS_VACC_CODE, 501 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3005 AS ASIIS_VACC_CODE, 502 AS CDC_VACC_CODE, N'Bharat Biotech International Limited' AS VACCINE_NAME
    UNION
    SELECT        3006 AS ASIIS_VACC_CODE, 503 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3007 AS ASIIS_VACC_CODE, 504 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3008 AS ASIIS_VACC_CODE, 505 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3009 AS ASIIS_VACC_CODE, 506 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3010 AS ASIIS_VACC_CODE, 507 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3011 AS ASIIS_VACC_CODE, 508 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3012 AS ASIIS_VACC_CODE, 509 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3013 AS ASIIS_VACC_CODE, 510 AS CDC_VACC_CODE, N'Sinopharm-Biotech' AS VACCINE_NAME
    UNION
    SELECT        3014 AS ASIIS_VACC_CODE, 511 AS CDC_VACC_CODE, N'Sinovac' AS VACCINE_NAME
    UNION
    SELECT        3015 AS ASIIS_VACC_CODE, 217 AS CDC_VACC_CODE, N'Pfizer' AS VACCINE_NAME
    UNION
    SELECT        3016 AS ASIIS_VACC_CODE, 218 AS CDC_VACC_CODE, N'Pfizer' AS VACCINE_NAME
    UNION
    SELECT        3017 AS ASIIS_VACC_CODE, 219 AS CDC_VACC_CODE, N'Pfizer' AS VACCINE_NAME
    UNION
    SELECT        3034 AS ASIIS_VACC_CODE, 517 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3033 AS ASIIS_VACC_CODE, 516 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3032 AS ASIIS_VACC_CODE, 515 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3031 AS ASIIS_VACC_CODE, 514 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3030 AS ASIIS_VACC_CODE, 513 AS CDC_VACC_CODE, NULL AS VACCINE_NAME
    UNION
    SELECT        3029 AS ASIIS_VACC_CODE, 512 AS CDC_VACC_CODE, N'Medicago' AS VACCINE_NAME
    UNION
    SELECT        3028 AS ASIIS_VACC_CODE, 228 AS CDC_VACC_CODE, N'Moderna' AS VACCINE_NAME
    UNION
    SELECT        3027 AS ASIIS_VACC_CODE, 227 AS CDC_VACC_CODE, N'Moderna' AS VACCINE_NAME
    UNION
    SELECT        3026 AS ASIIS_VACC_CODE, 221 AS CDC_VACC_CODE, N'Moderna' AS VACCINE_NAME
    UNION
    SELECT        3025 AS ASIIS_VACC_CODE, 225 AS CDC_VACC_CODE, N'Sanofi Pasteur' AS VACCINE_NAME
    UNION
    SELECT        3024 AS ASIIS_VACC_CODE, 226 AS CDC_VACC_CODE, N'Sanofi Pasteur' AS VACCINE_NAME
    UNION
    SELECT        3035 AS ASIIS_VACC_CODE, 229 AS CDC_VACC_CODE, N'Moderna' AS VACCINE_NAME
    UNION
    SELECT        3036 AS ASIIS_VACC_CODE, 300 AS CDC_VACC_CODE, N'Pfizer' AS VACCINE_NAME
    UNION
    SELECT        3037 AS ASIIS_VACC_CODE, 301 AS CDC_VACC_CODE, N'Pfizer' AS VACCINE_NAME
), 
vm AS (
    SELECT        
        vm.ASIIS_PAT_ID_PTR, 
        vt_1.ASIIS_VACC_CODE, 
        vt_1.CDC_VACC_CODE, 
        vt_1.VACCINE_NAME

     FROM        
        dbo.H33_VACCINATION_MASTER AS vm 
        
        INNER JOIN vt AS vt_1 
        ON vm.ASIIS_VACC_CODE = vt_1.ASIIS_VACC_CODE
      
    WHERE        
    (
        vm.DELETION_DATE IS NULL
    ) 
    AND 
    (
        vm.VACC_DATE > DATEADD(YEAR, - 2, GETDATE())
    )
), 
pr AS (
    SELECT        
        pr.ASIIS_PAT_ID_PTR, 
        pr.PHONE_NUMBER

      FROM
        dbo.H33_PHONE_RESERVE AS pr 
        
       INNER JOIN vm AS vm_3 
       ON pr.ASIIS_PAT_ID_PTR = vm_3.ASIIS_PAT_ID_PTR
      
    WHERE EXISTS (SELECT 1 AS Expr1
                  FROM vm AS vm_2
                  WHERE (
                    pr.ASIIS_PAT_ID_PTR = ASIIS_PAT_ID_PTR
                  )
                 )
    GROUP BY pr.ASIIS_PAT_ID_PTR, pr.PHONE_NUMBER
), 
prList AS (
    SELECT        
        ASIIS_PAT_ID_PTR, 
        STRING_AGG(PHONE_NUMBER, ',') AS PHONE_LIST
      
    FROM
        pr AS pr_1
      
    GROUP BY ASIIS_PAT_ID_PTR
)
SELECT
    pm.ASIIS_PAT_ID, 
    pm.PAT_FIRST_NAME, 
    pm.PAT_LAST_NAME, 
    pm.PAT_BIRTH_DATE, 
    prList_1.PHONE_LIST, 
    pm.ADDRESS_EMAIL, 
    vm_1.ASIIS_VACC_CODE, 
    vm_1.CDC_VACC_CODE, 
    vm_1.VACCINE_NAME
     
FROM
    dbo.H33_PATIENT_MASTER AS pm 
    
    INNER JOIN vm AS vm_1 
    ON vm_1.ASIIS_PAT_ID_PTR = pm.ASIIS_PAT_ID 
    
    LEFT OUTER JOIN prList AS prList_1 
    ON pm.ASIIS_PAT_ID = prList_1.ASIIS_PAT_ID_PTR