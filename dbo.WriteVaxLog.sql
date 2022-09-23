/****** Object:  StoredProcedure [dbo].[WriteVaxLog]    Script Date: 9/22/2022 2:25:53 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER   PROC [dbo].[WriteVaxLog] @procname [SYSNAME],@dtStart [DATETIME],@dtEnd [DATETIME],@rsltKey [INTEGER],@paramString [NVARCHAR](256),@msgString [NVARCHAR](MAX) AS 
		BEGIN
		    BEGIN TRAN
			INSERT [dbo].[VaxLog]
			     ( DateKey
				 , StoredProcName
                 , dtStart
				 , dtEnd
				 , RsltKey
                 , ParamString
                 , MsgString
				 )
			SELECT CAST(CONVERT(CHAR(8),@dtStart,112) AS INTEGER)
				 , @procname
                 , @dtStart
				 , @dtEnd -- [dbo].[fnGetElapsedTime](@dtStart,@dtEnd)
				 , @rsltKey
                 , @paramString
                 , @msgString;
			COMMIT TRAN
			WHILE @@TRANCOUNT > 0 ROLLBACK TRAN;
		END
