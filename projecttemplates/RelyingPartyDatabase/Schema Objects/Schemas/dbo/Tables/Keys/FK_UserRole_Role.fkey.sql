﻿ALTER TABLE [dbo].[UserRole]
    ADD CONSTRAINT [FK_UserRole_Role] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([RoleId]) ON DELETE CASCADE ON UPDATE CASCADE;
