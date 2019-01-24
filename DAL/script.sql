SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[MessageQueue](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[KnuthHash] [nvarchar](30) NULL,
	[MsgContent] [nvarchar](1000) NULL,
	[CanBeRemoved] [bit] NULL,
	[ExecuteCount] [smallint] NULL,
	[LastExecuteTime] [datetime] NULL,
	[CreatedTime] [datetime] NULL,
 CONSTRAINT [PK_MESSAGEQUEUE] PRIMARY KEY NONCLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[MessageQueue] ADD  DEFAULT (getdate()) FOR [CreatedTime]
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Id' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MessageQueue', @level2type=N'COLUMN',@level2name=N'Id'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'KnuthHash' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MessageQueue', @level2type=N'COLUMN',@level2name=N'KnuthHash'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'MsgContent' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MessageQueue', @level2type=N'COLUMN',@level2name=N'MsgContent'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'CanBeRemoved' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MessageQueue', @level2type=N'COLUMN',@level2name=N'CanBeRemoved'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'ExecuteCount' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MessageQueue', @level2type=N'COLUMN',@level2name=N'ExecuteCount'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'LastExecuteTime' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MessageQueue', @level2type=N'COLUMN',@level2name=N'LastExecuteTime'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'CreatedTime' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MessageQueue', @level2type=N'COLUMN',@level2name=N'CreatedTime'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'LightMessager专用消息落地表' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MessageQueue'
GO


