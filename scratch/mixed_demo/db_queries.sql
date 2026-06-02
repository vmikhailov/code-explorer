CREATE DATABASE MixedDemoDB;
GO

CREATE SCHEMA AppSchema;
GO

CREATE TABLE AppSchema.Users (
    UserId INT PRIMARY KEY,
    UserName VARCHAR(50)
);

CREATE PROCEDURE AppSchema.CreateUser
    @UserId INT,
    @UserName VARCHAR(50)
AS
BEGIN
    INSERT INTO AppSchema.Users (UserId, UserName)
    VALUES (@UserId, @UserName);
END;
GO
