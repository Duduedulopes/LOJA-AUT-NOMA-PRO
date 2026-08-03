-- Script para vincular RFIDs aos produtos
-- Chip 1: 2A-B5-64-E1 → Energético Baly de Cereja
-- Chip 2: FA-B4-10-35 → Água

-- Primeiro, vamos verificar quais são os IDs dos produtos
SELECT Id, Nome, TagRfid FROM Produtos WHERE Nome LIKE '%Energético%' OR Nome LIKE '%Água%';

-- Depois vamos vincular os RFIDs (substitua os IDs corretos)
UPDATE Produtos SET TagRfid = '2A-B5-64-E1' WHERE Nome LIKE '%Energético%';
UPDATE Produtos SET TagRfid = 'FA-B4-10-35' WHERE Nome LIKE '%Água%';

-- Ajustar estoque para 1 unidade cada
UPDATE Produtos SET QuantidadeEstoque = 1 WHERE Nome LIKE '%Energético%' OR Nome LIKE '%Água%';

-- Verificar o resultado
SELECT Id, Nome, TagRfid, QuantidadeEstoque FROM Produtos WHERE Nome LIKE '%Energético%' OR Nome LIKE '%Água%';