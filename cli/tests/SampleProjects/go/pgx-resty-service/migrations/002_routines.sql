CREATE OR REPLACE FUNCTION fn_record_audit()
RETURNS TRIGGER
AS $$
BEGIN
    INSERT INTO audit_log (entity_name, entity_id, action)
    VALUES ('orders', 1, 'updated');
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE sp_complete_order(p_order_id INT)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT o.id, oi.sku
    FROM orders o
    JOIN order_items oi ON oi.order_id = o.id
    WHERE o.id = p_order_id;

    UPDATE orders
    SET status = 'completed'
    WHERE id = p_order_id;

    CALL fn_record_audit();
END;
$$;

CREATE TRIGGER trg_orders_after_insert
AFTER INSERT ON orders
FOR EACH ROW
EXECUTE FUNCTION fn_record_audit();
