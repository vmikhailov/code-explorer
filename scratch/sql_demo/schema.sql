CREATE DATABASE DemoDB;
GO

CREATE SCHEMA Inventory;
GO

CREATE TABLE Inventory.Products (
    ProductId INT PRIMARY KEY,
    ProductName VARCHAR(100),
    Stock INT
);

CREATE TABLE Inventory.Orders (
    OrderId INT PRIMARY KEY,
    ProductId INT,
    Quantity INT
);

CREATE PROCEDURE Inventory.NotifyStockLevel
    @ProductId INT
AS
BEGIN
    SELECT * FROM Inventory.Products WHERE ProductId = @ProductId;
END;
GO

CREATE PROCEDURE Inventory.UpdateProductStock
    @ProductId INT,
    @Quantity INT
AS
BEGIN
    UPDATE Inventory.Products
    SET Stock = Stock - @Quantity
    WHERE ProductId = @ProductId;

    INSERT INTO Inventory.Orders (ProductId, Quantity)
    VALUES (@ProductId, @Quantity);

    EXEC Inventory.NotifyStockLevel @ProductId;
END;
GO

-- Loose/Top-Level SQL Queries
SELECT * FROM Inventory.Products;
GO

INSERT INTO Inventory.Orders (OrderId, ProductId, Quantity) VALUES (1, 1, 10);
GO
