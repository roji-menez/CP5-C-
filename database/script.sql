/* =====================================================================
   Script de criação do banco ProdutosDB
   Execução:  sqlcmd -S "(localdb)\MSSQLLocalDB" -i database/script.sql
   O script é idempotente: pode ser executado várias vezes sem erro.
   ===================================================================== */

-- 1) Cria o banco, se ainda não existir
IF DB_ID(N'ProdutosDB') IS NULL
BEGIN
    CREATE DATABASE ProdutosDB;
END
GO

USE ProdutosDB;
GO

-- 2) Cria a tabela Produtos, se ainda não existir
IF OBJECT_ID(N'dbo.Produtos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Produtos
    (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Produtos PRIMARY KEY,
        Nome      NVARCHAR(100)     NOT NULL,
        Preco     DECIMAL(10,2)     NOT NULL CONSTRAINT CK_Produtos_Preco   CHECK (Preco >= 0),
        Estoque   INT               NOT NULL CONSTRAINT CK_Produtos_Estoque CHECK (Estoque >= 0),
        Categoria NVARCHAR(50)      NOT NULL,
        Ativo     BIT               NOT NULL CONSTRAINT DF_Produtos_Ativo   DEFAULT (1) -- soft delete
    );
END
GO

-- 3) Insere produtos de exemplo apenas se a tabela estiver vazia
IF NOT EXISTS (SELECT 1 FROM dbo.Produtos)
BEGIN
    INSERT INTO dbo.Produtos (Nome, Preco, Estoque, Categoria) VALUES
        (N'Teclado Mecânico',      349.90,  15, N'Periféricos'),
        (N'Mouse Sem Fio',          89.90,  40, N'Periféricos'),
        (N'Monitor 24 polegadas',  899.00,   8, N'Monitores'),
        (N'SSD 1TB NVMe',          459.99,  25, N'Armazenamento'),
        (N'Headset USB',           199.50,  12, N'Áudio');
END
GO

SELECT Id, Nome, Preco, Estoque, Categoria, Ativo FROM dbo.Produtos;
GO
