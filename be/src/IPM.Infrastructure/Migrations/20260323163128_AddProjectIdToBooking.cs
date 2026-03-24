using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectIdToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('123_Bookings', 'ProjectId') IS NULL
BEGIN
    ALTER TABLE [123_Bookings] ADD [ProjectId] uniqueidentifier NULL;
END
");

            migrationBuilder.Sql(@"
DECLARE @DefaultProjectId uniqueidentifier = (
    SELECT TOP 1 [Id]
    FROM [123_Projects]
    ORDER BY [Id]
);

IF @DefaultProjectId IS NULL
BEGIN
    DECLARE @FallbackMemberId uniqueidentifier = (
        SELECT TOP 1 [Id]
        FROM [123_Members]
        ORDER BY [Id]
    );

    IF @FallbackMemberId IS NOT NULL
    BEGIN
        SET @DefaultProjectId = NEWID();

        INSERT INTO [123_Projects]
            ([Id], [Name], [ClientId], [ManagerId], [StartDate], [TargetDate], [TotalBudget], [WalletBalance], [Status])
        VALUES
            (@DefaultProjectId, 'Legacy Project', @FallbackMemberId, @FallbackMemberId, SYSUTCDATETIME(), NULL, 0, 0, 0);
    END
END

UPDATE [123_Bookings]
SET [ProjectId] = @DefaultProjectId
WHERE [ProjectId] IS NULL OR [ProjectId] = '00000000-0000-0000-0000-000000000000';
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [123_Bookings] WHERE [ProjectId] IS NULL)
BEGIN
    RAISERROR ('Unable to backfill ProjectId for existing bookings.', 16, 1);
END
");

            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_123_Bookings_123_Projects_ProjectId'
)
BEGIN
    ALTER TABLE [123_Bookings] DROP CONSTRAINT [FK_123_Bookings_123_Projects_ProjectId];
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_123_Bookings_ProjectId'
      AND [object_id] = OBJECT_ID('[123_Bookings]')
)
BEGIN
    DROP INDEX [IX_123_Bookings_ProjectId] ON [123_Bookings];
END

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID('[123_Bookings]')
      AND [name] = 'ProjectId'
      AND [is_nullable] = 1
)
BEGIN
    ALTER TABLE [123_Bookings] ALTER COLUMN [ProjectId] uniqueidentifier NOT NULL;
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_123_Bookings_ProjectId'
      AND [object_id] = OBJECT_ID('[123_Bookings]')
)
BEGIN
    CREATE INDEX [IX_123_Bookings_ProjectId] ON [123_Bookings] ([ProjectId]);
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_123_Bookings_123_Projects_ProjectId'
)
BEGIN
    ALTER TABLE [123_Bookings] WITH CHECK
    ADD CONSTRAINT [FK_123_Bookings_123_Projects_ProjectId]
    FOREIGN KEY ([ProjectId]) REFERENCES [123_Projects]([Id]) ON DELETE CASCADE;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = 'FK_123_Bookings_123_Projects_ProjectId'
)
BEGIN
    ALTER TABLE [123_Bookings] DROP CONSTRAINT [FK_123_Bookings_123_Projects_ProjectId];
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_123_Bookings_ProjectId'
      AND [object_id] = OBJECT_ID('[123_Bookings]')
)
BEGIN
    DROP INDEX [IX_123_Bookings_ProjectId] ON [123_Bookings];
END

IF COL_LENGTH('123_Bookings', 'ProjectId') IS NOT NULL
BEGIN
    ALTER TABLE [123_Bookings] DROP COLUMN [ProjectId];
END
");
        }
    }
}
