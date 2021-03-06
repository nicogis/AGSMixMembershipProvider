USE [AGSMixMembershipProvider]
GO
/****** Object:  Table [dbo].[Roles]    Script Date: 09/09/2016 17:08:52 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Roles](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Rolename] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[Users]    Script Date: 09/09/2016 17:08:52 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Users](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Username] [nvarchar](50) NOT NULL,
	[Password] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[UsersRoles]    Script Date: 09/09/2016 17:08:52 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UsersRoles](
	[IdUser] [int] NOT NULL,
	[IdRole] [int] NOT NULL
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[UsersRolesAD]    Script Date: 09/09/2016 17:08:52 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UsersRolesAD](
	[IdRole] [int] NOT NULL,
	[Username] [nvarchar](70) NOT NULL,
 CONSTRAINT [PK_UsersRolesAD] PRIMARY KEY CLUSTERED 
(
	[IdRole] ASC,
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
ALTER TABLE [dbo].[UsersRoles]  WITH CHECK ADD  CONSTRAINT [FK_UsersRoles_Roles] FOREIGN KEY([IdRole])
REFERENCES [dbo].[Roles] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UsersRoles] CHECK CONSTRAINT [FK_UsersRoles_Roles]
GO
ALTER TABLE [dbo].[UsersRoles]  WITH CHECK ADD  CONSTRAINT [FK_UsersRoles_Users] FOREIGN KEY([IdUser])
REFERENCES [dbo].[Users] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UsersRoles] CHECK CONSTRAINT [FK_UsersRoles_Users]
GO
ALTER TABLE [dbo].[UsersRolesAD]  WITH CHECK ADD  CONSTRAINT [FK_UsersRolesAD_Roles] FOREIGN KEY([IdRole])
REFERENCES [dbo].[Roles] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UsersRolesAD] CHECK CONSTRAINT [FK_UsersRolesAD_Roles]
GO
