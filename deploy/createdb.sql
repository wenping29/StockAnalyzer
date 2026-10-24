-- ============================================================
-- FundAnalysis 建库脚本（在目标数据库的默认库/超级用户下执行）
-- 应用启动时会在此库中自动创建全部表结构，本脚本只需建库。
-- ============================================================

-- PostgreSQL（psql 连接后执行，或 pgAdmin / 宝塔 pg 管理页执行）:
--   psql -h 服务器地址 -p 端口 -U postgres -f createdb.sql
CREATE DATABASE fund_analysis
    WITH OWNER = postgres
    ENCODING = 'UTF8'
    CONNECTION LIMIT = -1;

-- 为应用创建受限用户（推荐，替代直接使用 postgres 超级用户）:
-- CREATE USER fund WITH PASSWORD '请设置强密码';
-- GRANT ALL PRIVILEGES ON DATABASE fund_analysis TO fund;
-- \c fund_analysis
-- GRANT ALL ON SCHEMA public TO fund;

-- ------------------------------------------------------------
-- 其他数据库（按需取消注释执行）:
--
-- MySQL（含 utf8mb4，排序规则推荐 utf8mb4_unicode_ci）:
--   CREATE DATABASE fund_analysis CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
--   CREATE USER 'fund'@'%' IDENTIFIED BY '请设置强密码';
--   GRANT ALL PRIVILEGES ON fund_analysis.* TO 'fund'@'%';
--   FLUSH PRIVILEGES;
--
-- SQL Server:
--   CREATE DATABASE fund_analysis;
--
-- Oracle（19c+，需 DBA 执行）:
--   CREATE TABLESPACE fund_ts DATAFILE 'fund_ts.dbf' SIZE 500M AUTOEXTEND ON;
--   CREATE USER fund IDENTIFIED BY "请设置强密码" DEFAULT TABLESPACE fund_ts QUOTA UNLIMITED ON fund_ts;
--   GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE VIEW TO fund;
--
-- SQLite：无需建库（首次运行自动生成 db 文件）
