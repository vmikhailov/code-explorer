CREATE TABLE customers (
    id INT PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    created_at TIMESTAMP NOT NULL
);

CREATE TABLE orders (
    id INT PRIMARY KEY,
    customer_id INT NOT NULL REFERENCES customers(id),
    total DECIMAL(10, 2) NOT NULL,
    status VARCHAR(32) NOT NULL
);

CREATE TABLE order_items (
    id INT PRIMARY KEY,
    order_id INT NOT NULL,
    sku VARCHAR(64) NOT NULL,
    qty INT NOT NULL,
    CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) REFERENCES orders(id)
);

CREATE TABLE audit_log (
    id INT PRIMARY KEY,
    entity_name VARCHAR(64) NOT NULL,
    entity_id INT NOT NULL,
    action VARCHAR(32) NOT NULL
);

ALTER TABLE orders ADD COLUMN shipped_at TIMESTAMP;

CREATE VIEW active_customer_orders AS
SELECT o.id, o.total, c.email
FROM orders o
JOIN customers c ON c.id = o.customer_id
WHERE o.status = 'active';

CREATE MATERIALIZED VIEW mv_daily_revenue AS
SELECT o.customer_id, o.total
FROM orders o
JOIN order_items oi ON oi.order_id = o.id;
