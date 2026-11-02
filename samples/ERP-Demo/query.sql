-- Chỉ chạy sau khi chọn đúng SQL Server / database trong Connection.
-- Ví dụ này không thay đổi dữ liệu.
SELECT N'Xin chào từ LS Code' AS message,
       DB_NAME() AS database_name,
       SYSDATETIME() AS server_time;
GO
SELECT 1 AS id, N'Khách hàng mẫu' AS customer_name;
